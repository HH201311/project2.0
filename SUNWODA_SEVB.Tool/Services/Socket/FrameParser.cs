using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// socket内容按照类型解析
    /// </summary>
    public class FrameParser : IFrameParser
    {
        private readonly ProtocolOptions _options;

        public FrameParser(ProtocolOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public FrameParseResult TryParseFrame(byte[] buffer, int offset, int length)
        {
            return _options.FramingMode switch
            {
                FramingMode.StartEndDelimiter => ParseStartEndDelimiter(buffer, offset, length),
                FramingMode.EndDelimiterOnly => ParseEndDelimiterOnly(buffer, offset, length),
                FramingMode.FixedLength => ParseFixedLength(buffer, offset, length),
                FramingMode.LengthPrefixed => ParseLengthPrefixed(buffer, offset, length),
                FramingMode.None => ParseNone(buffer, offset, length),
                _ => new FrameParseResult { Success = false }
            };
        }

        /// <summary>
        /// 起始符+结束符模式解析
        /// </summary>
        private FrameParseResult ParseStartEndDelimiter(byte[] buffer, int offset, int length)
        {
            if (_options.StartDelimiter == null || _options.EndDelimiter == null)
                return new FrameParseResult { Success = false };

            var startIndex = FindPattern(buffer, offset, length, _options.StartDelimiter);
            if (startIndex < 0)
                return new FrameParseResult { Success = false };

            var searchOffset = startIndex + _options.StartDelimiter.Length;
            var searchLength = length - (searchOffset - offset);
            var endIndex = FindPattern(buffer, searchOffset, searchLength, _options.EndDelimiter);

            if (endIndex < 0)
                return new FrameParseResult { Success = false };

            // 提取帧数据
            int frameStart, frameLength;
            if (_options.StripDelimiters)
            {
                frameStart = startIndex + _options.StartDelimiter.Length;
                frameLength = endIndex - frameStart;
            }
            else
            {
                frameStart = startIndex;
                frameLength = endIndex + _options.EndDelimiter.Length - startIndex;
            }

            var frameData = new byte[frameLength];
            Array.Copy(buffer, frameStart, frameData, 0, frameLength);

            return new FrameParseResult
            {
                Success = true,
                FrameData = frameData,
                ConsumedBytes = endIndex + _options.EndDelimiter.Length - offset
            };
        }

        /// <summary>
        /// 仅结束符模式解析
        /// </summary>
        private FrameParseResult ParseEndDelimiterOnly(byte[] buffer, int offset, int length)
        {
            if (_options.EndDelimiter == null)
                return new FrameParseResult { Success = false };

            var endIndex = FindPattern(buffer, offset, length, _options.EndDelimiter);
            if (endIndex < 0)
                return new FrameParseResult { Success = false };

            int frameLength;
            if (_options.StripDelimiters)
            {
                frameLength = endIndex - offset;
            }
            else
            {
                frameLength = endIndex + _options.EndDelimiter.Length - offset;
            }

            var frameData = new byte[frameLength];
            Array.Copy(buffer, offset, frameData, 0, frameLength);

            return new FrameParseResult
            {
                Success = true,
                FrameData = frameData,
                ConsumedBytes = endIndex + _options.EndDelimiter.Length - offset
            };
        }

        /// <summary>
        /// 固定长度模式解析
        /// </summary>
        private FrameParseResult ParseFixedLength(byte[] buffer, int offset, int length)
        {
            if (length < _options.FixedFrameLength)
                return new FrameParseResult { Success = false };

            var frameData = new byte[_options.FixedFrameLength];
            Array.Copy(buffer, offset, frameData, 0, _options.FixedFrameLength);

            return new FrameParseResult
            {
                Success = true,
                FrameData = frameData,
                ConsumedBytes = _options.FixedFrameLength
            };
        }

        /// <summary>
        /// 长度前缀模式解析
        /// </summary>
        private FrameParseResult ParseLengthPrefixed(byte[] buffer, int offset, int length)
        {
            var headerSize = _options.LengthFieldOffset + _options.LengthFieldSize;
            if (length < headerSize)
                return new FrameParseResult { Success = false };

            // 读取长度字段
            var lengthFieldStart = offset + _options.LengthFieldOffset;
            int frameLength = _options.LengthFieldSize switch
            {
                1 => buffer[lengthFieldStart],
                2 => _options.BigEndian
                    ? (buffer[lengthFieldStart] << 8) | buffer[lengthFieldStart + 1]
                    : buffer[lengthFieldStart] | (buffer[lengthFieldStart + 1] << 8),
                4 => _options.BigEndian
                    ? (buffer[lengthFieldStart] << 24) | (buffer[lengthFieldStart + 1] << 16) |
                      (buffer[lengthFieldStart + 2] << 8) | buffer[lengthFieldStart + 3]
                    : buffer[lengthFieldStart] | (buffer[lengthFieldStart + 1] << 8) |
                      (buffer[lengthFieldStart + 2] << 16) | (buffer[lengthFieldStart + 3] << 24),
                _ => throw new InvalidOperationException($"不支持的长度字段大小: {_options.LengthFieldSize}")
            };

            // 计算总帧长度
            var totalLength = _options.LengthIncludesHeader ? frameLength : headerSize + frameLength;

            if (totalLength > _options.MaxFrameLength)
                throw new InvalidOperationException($"帧长度 {totalLength} 超过最大限制 {_options.MaxFrameLength}");

            if (length < totalLength)
                return new FrameParseResult { Success = false };

            var frameData = new byte[totalLength];
            Array.Copy(buffer, offset, frameData, 0, totalLength);

            return new FrameParseResult
            {
                Success = true,
                FrameData = frameData,
                ConsumedBytes = totalLength
            };
        }

        /// <summary>
        /// 无分隔符模式
        /// </summary>
        private FrameParseResult ParseNone(byte[] buffer, int offset, int length)
        {
            if (length <= 0)
                return new FrameParseResult { Success = false };

            var frameData = new byte[length];
            Array.Copy(buffer, offset, frameData, 0, length);

            return new FrameParseResult
            {
                Success = true,
                FrameData = frameData,
                ConsumedBytes = length
            };
        }

        /// <summary>
        /// 在缓冲区中查找字节模式
        /// </summary>
        private static int FindPattern(byte[] buffer, int offset, int length, byte[] pattern)
        {
            if (pattern.Length == 0 || length < pattern.Length)
                return -1;

            for (int i = offset; i <= offset + length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }
    }
}
