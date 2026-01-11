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
            {
                return new FrameParseResult { Success = false };
            }

            //// 添加调试信息
            //var searchData = new byte[Math.Min(length, 50)]; // 只显示前50字节
            //Array.Copy(buffer, offset, searchData, 0, searchData.Length);
            //var searchHex = BitConverter.ToString(searchData);
            //var startDelimHex = BitConverter.ToString(_options.StartDelimiter);
            //var endDelimHex = BitConverter.ToString(_options.EndDelimiter);

            //Console.WriteLine($"[DEBUG] 搜索数据: [{searchHex}]");
            //Console.WriteLine($"[DEBUG] 起始符: [{startDelimHex}], 结束符: [{endDelimHex}]");

            var startIndex = FindPattern(buffer, offset, length, _options.StartDelimiter);
            //Console.WriteLine($"[DEBUG] startIndex = {startIndex}");

            if (startIndex < 0)
            {
                var keepBytes = Math.Min(length, _options.StartDelimiter.Length - 1);
                var skipBytes = length - keepBytes;

                return new FrameParseResult
                {
                    Success = false,
                    ConsumedBytes = skipBytes > 0 ? skipBytes : 0
                };
            }

            // 起始符之前有无效数据，先跳过
            if (startIndex > offset)
            {
                return new FrameParseResult
                {
                    Success = false,
                    ConsumedBytes = startIndex - offset  // 跳过起始符之前的无效数据
                };
            }

            // 在起始符之后搜索结束符
            var searchOffset = startIndex + _options.StartDelimiter.Length;
            var searchLength = length - (searchOffset - offset);

            if (searchLength <= 0)
            {
                // 数据不足，等待更多数据
                return new FrameParseResult { Success = false, ConsumedBytes = 0 };
            }
            var endIndex = FindPattern(buffer, searchOffset, searchLength, _options.EndDelimiter);

            // 找不到结束符 - 数据不完整，等待更多数据
            if (endIndex < 0)
            {
                // 检查是否超过最大帧长度
                var currentFrameLength = length - (startIndex - offset);
                if (currentFrameLength > _options.MaxFrameLength)
                {
                    // 帧太长，丢弃起始符，重新搜索
                    return new FrameParseResult
                    {
                        Success = false,
                        ConsumedBytes = _options.StartDelimiter.Length,
                        ErrorMessage = $"帧长度超过限制 {_options.MaxFrameLength}"
                    };
                }
                // 正常情况：等待更多数据
                return new FrameParseResult { Success = false, ConsumedBytes = 0 };
            }

            // 找到完整帧，提取数据
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
