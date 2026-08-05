using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class MESSettingRepository : MappingRepository<MESSettingModel, MESSetting>, IMESSettingRepository
    {
        public MESSettingRepository(ISqlSugarClient db) : base(db) { }

        #region 接口实现方法

        /// <summary>
        /// 通过ID获取MES设置
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<MESSettingModel?> GetByIDAsync(int id)
        {
            return await GetAsync(model => model.ID == id);
        }

        /// <summary>
        /// 通过配置文件名称获取MES设置列表
        /// </summary>
        /// <param name="profileName"></param>
        /// <returns></returns>
        public async Task<List<MESSettingModel>> GetByProfileNameAsync(string profileName)
        {
            return await GetListAsync(model => model.ProfileName == profileName);
        }

        /// <summary>
        /// 通过键名获取MES设置列表
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<List<MESSettingModel>> GetByKeyAsync(string key)
        {
            return await GetListAsync(model => model.Key == key);
        }

        /// <summary>
        /// 通过类型获取MES设置列表
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public async Task<List<MESSettingModel>> GetByTypeAsync(string type)
        {
            return await GetListAsync(model => model.Type == type);
        }

        /// <summary>
        /// 通过配置文件名称和键名获取唯一的MES设置
        /// </summary>
        /// <param name="profileName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<MESSettingModel?> GetByProfileNameAndKeyAsync(string profileName, string key)
        {
            return await GetAsync(model => model.ProfileName == profileName && model.Key == key);
        }

        /// <summary>
        /// 通过值获取MES设置列表
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<List<MESSettingModel>> GetByValueAsync(string value)
        {
            return await GetListAsync(model => model.Value == value);
        }

        #endregion

        #region 自定义扩展方法

        /// <summary>
        /// 获取指定配置文件的配置值
        /// </summary>
        /// <param name="profileName">配置文件名称</param>
        /// <param name="key">键名</param>
        /// <returns></returns>
        public async Task<dynamic?> GetSettingValueAsync(string profileName, string key)
        {
            var model = await GetByProfileNameAndKeyAsync(profileName, key);
            return model != null ? DataTypeConverter.StringToValue(model.Type, model.Value) : null;
        }

        /// <summary>
        /// 更新指定配置文件的配置值
        /// </summary>
        /// <param name="profileName">配置文件名称</param>
        /// <param name="key">键名</param>
        /// <param name="value">新值</param>
        /// <returns></returns>
        public async Task<bool> UpdateSettingValueAsync(string profileName, string key, dynamic value)
        {
            var model = await GetByProfileNameAndKeyAsync(profileName, key);
            if (model != null)
            {
                model.Value = DataTypeConverter.ValueToString(value);
                return await UpdateAsync(model);
            }
            return false;
        }

        /// <summary>
        /// 获取指定配置文件的所有配置项
        /// </summary>
        /// <param name="profileName">配置文件名称</param>
        /// <returns></returns>
        public async Task<Dictionary<string, dynamic>> GetProfileSettingsAsync(string profileName)
        {
            var models = await GetByProfileNameAsync(profileName);
            var result = new Dictionary<string, dynamic>();

            foreach (var model in models)
            {
                result[model.Key] = DataTypeConverter.StringToValue(model.Type, model.Value);
            }

            return result;
        }

        /// <summary>
        /// 批量更新指定配置文件的配置项
        /// </summary>
        /// <param name="profileName">配置文件名称</param>
        /// <param name="settings">配置项字典</param>
        /// <returns></returns>
        public async Task<bool> UpdateProfileSettingsAsync(string profileName, Dictionary<string, dynamic> settings)
        {
            var existingModels = await GetByProfileNameAsync(profileName);
            var updateTasks = new List<Task<bool>>();

            foreach (var setting in settings)
            {
                var model = existingModels.FirstOrDefault(m => m.Key == setting.Key);
                if (model != null)
                {
                    model.Value = DataTypeConverter.ValueToString(setting.Value);
                    updateTasks.Add(UpdateAsync(model));
                }
            }

            var results = await Task.WhenAll(updateTasks);
            return results.All(r => r);
        }

        #endregion
    }
}
