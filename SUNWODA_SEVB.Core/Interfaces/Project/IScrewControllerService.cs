using SUNWODA_SEVB.Core.Models.Project.STC;
using SUNWODA_SEVB.Core.Models.Project.STC.Results;

namespace SUNWODA_SEVB.Core.Interfaces.Project
{
    /// <summary>
    /// 螺丝枪控制器服务接口
    /// </summary>
    public interface IScrewControllerService
    {
        /// <summary>
        /// 加载指定工序的控制器配置
        /// </summary>
        Task<ScrewControllerSettings> LoadSettingsAsync(string vmName);

        /// <summary>
        /// 获取控制器配置详情（包含名称）
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public List<ScrewControllerDetail> GetControllerDetails(ScrewControllerSettings settings);

        /// <summary>
        /// 汇总所有配置的IP地址
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public List<string> GetAllControllerIPs(ScrewControllerSettings settings);

        /// <summary>
        /// 获取TR值
        /// </summary>
        Task<ScrewTrValueResult> GetTrValueAsync(string controllerIP,string? vm_Name=null);

        /// <summary>
        /// 获取曲线数据
        /// </summary>
        Task<ScrewCurveDataResult> GetCurveDataAsync(
            string controllerIP, int traceLogId, float trValue, string? vmName = null);

        /// <summary>
        /// 根据EorCycleCount获取作业信息
        /// </summary>
        Task<ScrewProcessCycleResult> GetProcessCycleInfoAsync(
            string controllerIP, int eorCycleCount);

        /// <summary>
        /// 根据螺丝枪编号获取对应控制器IP
        /// </summary>
        string? GetControllerIPByTightenGun(ScrewControllerSettings settings, string tightenGun);

        /// <summary>
        /// 批量获取指定螺丝枪列表的TR值（每个控制器只请求一次）
        /// </summary>
        Task<BatchTrValueResult> GetBatchTrValuesAsync(
            ScrewControllerSettings settings,
            IEnumerable<string> tightenGuns);

        /// <summary>
        /// 根据EorCycleCount获取EorProcessCycleNumber
        /// </summary>
        /// <param name="controllerIP">控制器IP</param>
        /// <param name="eorCycleCount">周期计数（对应workCycleId）</param>
        /// <returns>包含ProcessCycleNumber的结果</returns>
        Task<ScrewProcessCycleResult> GetProcessCycleNumberByCycleCountAsync(
            string controllerIP, int eorCycleCount);

        /// <summary>
        /// 将TR值保存到数据库
        /// </summary>
        Task<bool> SaveTrValueToDbAsync(string vmName, string controllerIP, string tightenGun, float trValue);

        /// <summary>
        /// 从数据库加载TR值缓存
        /// </summary>
        Task<Dictionary<string, TrValueCacheRecord>> LoadTrValuesFromDbAsync(string vmName);

        /// <summary>
        /// 清除数据库中的TR值缓存
        /// </summary>
        Task<bool> ClearTrValueDbCacheAsync(string vmName, string? controllerIP = null);

        /// <summary>
        /// 清除TR值缓存
        /// </summary>
        void ClearTrCache();
    }
}
