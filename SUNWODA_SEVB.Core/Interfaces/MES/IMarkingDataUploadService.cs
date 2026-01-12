using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// Marking数据上传服务接口
    /// </summary>
    public interface IMarkingDataUploadService : IMesService
    {
        /// <summary>
        /// 上传Marking数据
        /// </summary>
        /// <param name="serialNumber">序列号</param>
        /// <param name="markingData">Marking数据</param>
        /// <returns>上传结果</returns>
        Task<bool> UploadMarkingDataAsync(string serialNumber, string markingData);


    }
}
