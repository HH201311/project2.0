using SUNWODA_SEVB.Core.Interfaces.MES;

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// MES请求基类
    /// </summary>
    public abstract class BaseRequest : IMesRequest
    {        
        /// <summary>
        /// 验证请求参数
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public abstract bool Validate(out string errorMessage);
    }
}