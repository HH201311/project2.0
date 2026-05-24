using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class ProjectSettingRepository : MappingRepository<ProjectSettingModel, ProjectSetting>, IProjectSettingRepository
    {
        public ProjectSettingRepository(ISqlSugarClient db) : base(db) { }

        public async Task<ProjectSettingModel?> GetByIDAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        public ProjectSettingModel? GetByID(int id)
        {
            return GetById(id);
        }

        public async Task<List<ProjectSettingModel>?> GetByVMNameAsync(string vmName)
        {
            return await GetListAsync(model => model.BelongToVM == vmName);
        }

        public List<ProjectSettingModel>? GetByVMName(string vmName)
        {
            return GetList(model => model.BelongToVM == vmName);
        }

        public async Task<ProjectSettingModel?> GetByNameAndVMAsync(string name, string vmName)
        {
            return await GetAsync(model => model.Name == name && model.BelongToVM == vmName);
        }

        public ProjectSettingModel? GetByNameAndVM(string name, string vmName)
        {
            return Get(model => model.Name == name && model.BelongToVM == vmName);
        }

        public async Task<dynamic?> GetSettingValueAsync(string name, string vmName)
        {
            var model = await GetByNameAndVMAsync(name, vmName);
            return model != null ? DataTypeConverter.StringToValue(model.Type, model.Value) : null;
        }

        public dynamic? GetSettingValue(string name, string vmName)
        {
            var model = GetByNameAndVM(name, vmName);
            return model != null ? DataTypeConverter.StringToValue(model.Type, model.Value) : null;
        }

        public async Task<bool> UpdateSettingValueAsync(string name, string vmName, dynamic value)
        {
            var model = await GetByNameAndVMAsync(name, vmName);
            if (model != null)
            {
                model.Value = DataTypeConverter.ValueToString(value);
                return await UpdateAsync(model);
            }
            else
            {
                var newModel = new ProjectSettingModel
                {
                    Name = name,
                    BelongToVM = vmName,
                    Value = DataTypeConverter.ValueToString(value),
                    Type = value?.GetType().Name ?? "String"
                };
                return await AddAsync(newModel);
            }
           
        }

        public bool UpdateSettingValue(string name, string vmName, dynamic value)
        {
            var model = GetByNameAndVM(name, vmName);
            if (model != null)
            {
                model.Value = DataTypeConverter.ValueToString(value);
                return Update(model);
            }
            else
            {
                var newModel = new ProjectSettingModel
                {
                    Name = name,
                    BelongToVM = vmName,
                    Value = DataTypeConverter.ValueToString(value),
                    Type = value?.GetType().Name ?? "String"
                };
                return Add(newModel);
            }
            
        }
    }
}