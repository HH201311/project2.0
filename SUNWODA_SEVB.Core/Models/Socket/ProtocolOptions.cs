
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
    public class ProtocolOptions
    {
        /// <summary>
        /// 分包模式
        /// </summary>
        public FramingMode FramingMode { get; set; } = FramingMode.StartEndDelimiter;

        /// <summary>
        /// 起始符 (可为null表示无起始符)
        /// </summary>
        public byte[]? StartDelimiter { get; set; } = new byte[] { 0x02 };

        /// <summary>
        /// 结束符 (可为null表示无结束符)
        /// </summary>
        public byte[]? EndDelimiter { get; set; } = new byte[] { 0x03 };

        /// <summary>
        /// 固定长度 (FixedLength模式使用)
        /// </summary>
        public int FixedFrameLength { get; set; } = 1024;

        /// <summary>
        /// 长度字段偏移量 (LengthPrefixed模式使用)
        /// </summary>
        public int LengthFieldOffset { get; set; } = 0;

        /// <summary>
        /// 长度字段字节数 (1, 2, 4)
        /// </summary>
        public int LengthFieldSize { get; set; } = 4;

        /// <summary>
        /// 长度值是否包含头部长度
        /// </summary>
        public bool LengthIncludesHeader { get; set; } = false;

        /// <summary>
        /// 是否使用大端字节序
        /// </summary>
        public bool BigEndian { get; set; } = false;

        /// <summary>
        /// 是否在解析后移除起止符
        /// </summary>
        public bool StripDelimiters { get; set; } = true;

        /// <summary>
        /// 最大帧长度 (防止内存溢出)
        /// </summary>
        public int MaxFrameLength { get; set; } = 65536;
    }
}
