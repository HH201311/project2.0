using SUNWODA_SEVB.Core.Models.Data;

namespace SUNWODA_SEVB.Core.Interfaces.Data
{
    public interface IPLCAddressConfigRepository : IRepository<PLCAddressConfigModel>
    {
        /// <summary>
        /// 获取指定PLC配置的所有地址
        /// </summary>
        /// <param name="plcID"></param>
        /// <returns></returns>
        Task<List<PLCAddressConfigModel>> GetMonitorAddressesAsync(int plcID);
    }
}
