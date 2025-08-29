using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class PLCAddressConfigRepository : MappingRepository<PLCAddressConfigModel, PLCAddressConfig>, IPLCAddressConfigRepository
    {
        public PLCAddressConfigRepository(ISqlSugarClient db) : base(db) { }

        public async Task<List<PLCAddressConfigModel>> GetMonitorAddressesAsync(int plcID)
        {
            //var modelList = await _db.Queryable<PLCAddressConfig>().Where(model => model.PLCID == plcID && model.IsMonitor).ToListAsync();
            //return modelList.Adapt<List<PLCAddressConfigModel>>();
            return await GetListAsync(model => model.PLCID == plcID && model.IsMonitor);
        }
    }
}
