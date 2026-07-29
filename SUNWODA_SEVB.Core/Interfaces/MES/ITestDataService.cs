using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// 获取测试数据接口
    /// </summary>
    public interface ITestDataService : IMesService
    {
        Task<GetTestDataResponse> GetTestDataAsync(string sn, CancellationToken cancellationToken = default);
    }
}