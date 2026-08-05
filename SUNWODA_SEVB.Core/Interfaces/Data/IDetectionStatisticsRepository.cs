using SUNWODA_SEVB.Core.Models.Data;

namespace SUNWODA_SEVB.Core.Interfaces.Data
{
    public interface IDetectionStatisticsRepository : IRepository<DetectionStatisticsModel>
    {
        DetectionStatisticsModel? GetByKey(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate);

        Task<DetectionStatisticsModel?> GetByKeyAsync(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate);

        DetectionStatisticsModel GetOrCreate(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate);

        Task<DetectionStatisticsModel> GetOrCreateAsync(
            string moduleCode,
            DetectionStatisticsSource sourceType,
            DetectionStatisticsScope scopeType,
            DateTime statisticsDate);

        /// <summary>
        /// 检测完成后累加统计。
        /// 同时更新：总统计 + 当天统计。
        /// 返回总统计，供界面绑定。
        /// </summary>
        DetectionStatisticsModel Increment(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType,
            string detectionResult,
            IEnumerable<string>? triggeredDefects);

        Task<DetectionStatisticsModel> IncrementAsync(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType,
            string detectionResult,
            IEnumerable<string>? triggeredDefects);

        /// <summary>
        /// 同步清零总统计和当天统计。
        /// </summary>
        bool ClearTotalAndToday(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType = DetectionStatisticsSource.All,
            bool clearAllSources = true);

        Task<bool> ClearTotalAndTodayAsync(
            string moduleCode,
            DateTime today,
            DetectionStatisticsSource sourceType = DetectionStatisticsSource.All,
            bool clearAllSources = true);


        void EnsureTable();

        Task EnsureTableAsync();
    }
}
