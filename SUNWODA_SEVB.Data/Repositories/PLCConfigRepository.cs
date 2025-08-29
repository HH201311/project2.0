using Mapster;
using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class PLCConfigRepository : MappingRepository<PLCConfigModel, PLCConfig>, IPLCConfigRepository
    {
        public PLCConfigRepository(ISqlSugarClient db) : base(db) { }

        public async Task<List<PLCConfigModel>> GetEnabledConfigsAsync()
        {
            //var modelList = await _db.Queryable<PLCConfig>().Where(model => model.IsEnable).ToListAsync();
            //return modelList.Adapt<List<PLCConfigModel>>();
            return await GetListAsync(model => model.IsEnable);
        }
    }
}
