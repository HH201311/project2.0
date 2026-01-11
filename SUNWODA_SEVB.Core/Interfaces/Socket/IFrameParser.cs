
namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// 帧解析结果
    /// </summary>
    public class FrameParseResult
    {
        /// <summary>
        /// 是否成功解析出完整帧
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 解析出的帧数据（不含分隔符，如果配置了 StripDelimiters）
        /// </summary>
        public byte[]? FrameData { get; set; }

        /// <summary>
        /// 消费的字节数（下次解析应从 offset + ConsumedBytes 开始）
        /// </summary>
        public int ConsumedBytes { get; set; }

        /// <summary>
        /// 错误信息（可选）
        /// </summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// 帧解析器接口
    /// </summary>
    public interface IFrameParser
    {
        /// <summary>
        /// 尝试从缓冲区解析出一个完整帧
        /// </summary>
        FrameParseResult TryParseFrame(byte[] buffer, int offset, int length);
    }
}
