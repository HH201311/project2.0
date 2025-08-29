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
    public class DeviceRepository : MappingRepository<DeviceModel, Device>, IDeviceRepository
    {
        public DeviceRepository(ISqlSugarClient db) : base(db) { }

        public async Task<List<DeviceModel>> GetByBaseNameAsync(string baseName)
        {
            //var modelList = await _db.Queryable<Device>().Where(model => model.BaseName == baseName).ToListAsync();
            //return modelList.Adapt<List<DeviceModel>>();
            return await GetListAsync(model => model.BaseName == baseName);
        }

        public async Task<DeviceModel?> GetByIDAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public async Task<List<DeviceModel>> GetByLineNameAsync(string lineName)
        {
            //var modelList = await _db.Queryable<Device>().Where(model => model.LineName == lineName).ToListAsync();
            //return modelList.Adapt<List<DeviceModel>>();
            return await GetListAsync(model => model.LineName == lineName);
        }

        public async Task<List<DeviceModel>> GetByNameAsync(string name)
        {
            //var modelList = await _db.Queryable<Device>().Where(model => model.Name == name).ToListAsync();
            //return modelList.Adapt<List<DeviceModel>>();
            return await GetListAsync(model => model.Name == name);
        }

        public async Task<List<DeviceModel>> GetByNumberAsync(string number)
        {
            //var modelList = await _db.Queryable<Device>().Where(model => model.Number == number).ToListAsync();
            //return modelList.Adapt<List<DeviceModel>>();
            return await GetListAsync(model => model.Number == number);
        }
    }
}
