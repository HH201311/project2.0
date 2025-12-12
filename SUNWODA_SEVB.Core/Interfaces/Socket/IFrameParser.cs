
namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// 帧解析结果
    /// </summary>
    public class FrameParseResult
    {
        public bool Success { get; set; }
        public byte[]? FrameData { get; set; }
        public int ConsumedBytes { get; set; }
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
