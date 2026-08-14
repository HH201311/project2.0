
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// 协议分包模式
    /// </summary>
    public enum FramingMode
    {
        /// <summary>
        /// 起始符+结束符模式 (STX...ETX)
        /// </summary>
        StartEndDelimiter,

        /// <summary>
        /// 仅结束符模式 (...ETX)
        /// </summary>
        EndDelimiterOnly,

        /// <summary>
        /// 固定长度模式
        /// </summary>
        FixedLength,

        /// <summary>
        /// 长度前缀模式 (头部包含长度信息)
        /// </summary>
        LengthPrefixed,

        /// <summary>
        /// 无分隔符，直接处理每次接收的数据
        /// </summary>
        None
    }

    /// <summary>
    /// 协议配置选项
    /// </summary>
    public class ProtocolOptions : ModelBase
    {
        private FramingMode framingMode = FramingMode.StartEndDelimiter;
        private byte[]? startDelimiter = new byte[] { 0x02 };
        private byte[]? endDelimiter = new byte[] { 0x03 };
        private int fixedFrameLength = 1024;
        private int lengthFieldOffset = 0;
        private int lengthFieldSize = 4;
        private bool lengthIncludesHeader = false;
        private bool bigEndian = false;
        private bool stripDelimiters = true;
        private int maxFrameLength = 65536;

        /// <summary>
        /// 分包模式
        /// </summary>
        public FramingMode FramingMode { get => framingMode; set => SetProperty(ref framingMode, value); }

        /// <summary>
        /// 起始符 (可为null表示无起始符)
        /// </summary>
        [Description("FramingMode为StartEndDelimiter")]
        public byte[]? StartDelimiter { get => startDelimiter; set => SetProperty(ref startDelimiter, value); }

        /// <summary>
        /// 结束符 (可为null表示无结束符)
        /// </summary>
        [Description("FramingMode为StartEndDelimiter/EndDelimiterOnly")]
        public byte[]? EndDelimiter { get => endDelimiter; set => SetProperty(ref endDelimiter, value); }

        /// <summary>
        /// 固定长度 (FixedLength模式使用)
        /// </summary>
        [Description("FramingMode为FixedLength")]
        public int FixedFrameLength { get => fixedFrameLength; set => SetProperty(ref fixedFrameLength, value); }

        /// <summary>
        /// 长度字段偏移量 (LengthPrefixed模式使用)
        /// </summary>
        [Description("FramingMode为LengthPrefixed")]
        public int LengthFieldOffset { get => lengthFieldOffset; set => SetProperty(ref lengthFieldOffset, value); }

        /// <summary>
        /// 长度字段字节数 (1, 2, 4)
        /// </summary>
        [Description("FramingMode为LengthPrefixed")]
        public int LengthFieldSize { get => lengthFieldSize; set => SetProperty(ref lengthFieldSize, value); }

        /// <summary>
        /// 长度值是否包含头部长度
        /// </summary>
        [Description("FramingMode为LengthPrefixed")]
        public bool LengthIncludesHeader { get => lengthIncludesHeader; set => SetProperty(ref lengthIncludesHeader, value); }

        /// <summary>
        /// 是否使用大端字节序
        /// </summary>
        [Description("FramingMode为LengthPrefixed")]
        public bool BigEndian { get => bigEndian; set => SetProperty(ref bigEndian, value); }

        /// <summary>
        /// 是否在解析后移除起止符
        /// </summary>
        [Description("FramingMode为StartEndDelimiter/EndDelimiterOnly")]
        public bool StripDelimiters { get => stripDelimiters; set => SetProperty(ref stripDelimiters, value); }

        /// <summary>
        /// 最大帧长度 (防止内存溢出)
        /// </summary>
        [Description("FramingMode为StartEndDelimiter/EndDelimiterOnly/FixedLength/LengthPrefixed")]
        public int MaxFrameLength { get => maxFrameLength; set => SetProperty(ref maxFrameLength, value); }
    }
}
