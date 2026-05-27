using SUNWODA_SEVB.Core.Models.Data;

namespace SUNWODA_SEVB.Core.Interfaces.Data
{
    public interface IMESSettingRepository : IRepository<MESSettingModel>
    {
        /// <summary>
        /// 通过ID获取MES设置
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<MESSettingModel?> GetByIDAsync(int id);

        /// <summary>
        /// 通过配置文件名称获取MES设置列表
        /// </summary>
        /// <param name="profileName"></param>
        /// <returns></returns>
        Task<List<MESSettingModel>> GetByProfileNameAsync(string profileName);

        /// <summary>
        /// 通过键名获取MES设置列表
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<List<MESSettingModel>> GetByKeyAsync(string key);

        /// <summary>
        /// 通过类型获取MES设置列表
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        Task<List<MESSettingModel>> GetByTypeAsync(string type);

        /// <summary>
        /// 通过配置文件名称和键名获取唯一的MES设置
        /// </summary>
        /// <param name="profileName"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<MESSettingModel?> GetByProfileNameAndKeyAsync(string profileName, string key);

        /// <summary>
        /// 通过值获取MES设置列表
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        Task<List<MESSettingModel>> GetByValueAsync(string value);        
    }
}
