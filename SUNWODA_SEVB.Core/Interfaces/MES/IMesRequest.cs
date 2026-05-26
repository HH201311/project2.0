
namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// MES请求接口
    /// </summary>
    public interface IMesRequest
    {        
        /// <summary>
        /// 验证请求参数
        /// </summary>
        bool Validate(out string errorMessage);
    }
}
