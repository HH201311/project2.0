using Mapster;
using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Data.Models;
using SUNWODA_SEVB.Tool.Extension;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Data.Repositories
{
    public class AppLogRepository : MappingRepository<AppLogModel, AppLog>, IAppLogRepository
    {
        private readonly ILoggerService<AppLogRepository> _logger;

        public AppLogRepository(ISqlSugarClient db, ILoggerService<AppLogRepository> logger) : base(db)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            //_logger.Info("AppLogRepository 已初始化", true);
        }

        public async Task<bool> BulkInsertAsync(List<AppLogModel> logs)
        {
            if (logs == null || logs.Count == 0) return true;

            try
            {
                var models = logs.Adapt<List<AppLog>>();

                // 使用 SqlSugar 的批量插入
                var result = await _db.Insertable(models)
                    .ExecuteCommandAsync();

                return result > 0;
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出，避免影响应用运行
                _logger.Error($"批量插入日志失败，日志数量：{logs.Count}", ex);
                return false;
            }
        }

        public async Task<int> DeleteOldLogsAsync(int daysToKeep)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                return await _db.Deleteable<AppLog>()
                    .Where(log => log.LogTime < cutoffDate)
                    .ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("删除过期日志失败", ex);
                return 0;
            }
        }

        /// <summary>
        /// 获取应用日志表的大小（MB）
        /// </summary>
        public async Task<double> GetDatabaseSizeAsync()
        {
            try
            {
                // 查询表的大小信息
                var sql = @"
                SELECT 
                    ROUND(
                        (SUM(LENGTH(loglever) + LENGTH(logger) + LENGTH(Message) + 
                             IFNULL(LENGTH(exception), 0)) + COUNT(*) * 50) / 1024.0 / 1024.0, 2
                    ) as SizeMB
                FROM app_logs";

                var result = await _db.Ado.SqlQuerySingleAsync<double>(sql);
                return result;
            }
            catch (Exception)
            {
                // 如果查询失败，返回0
                return 0;
            }
        }

        /// <summary>
        /// 按大小删除日志
        /// </summary>
        public async Task<int> DeleteLogsBySize(double targetSizeMB)
        {
            int totalDeleted = 0;
            const int batchSize = 1000;

            try
            {
                while (true)
                {
                    var currentSize = await GetDatabaseSizeAsync();
                    if (currentSize <= targetSizeMB)
                    {
                        break;
                    }

                    // 批量删除最旧的记录
                    var idsToDelete = await _db.Queryable<AppLog>()
                        .OrderBy(log => log.LogTime) // 按时间排序，确保删除最旧的
                        .Take(batchSize)
                        .Select(log => log.ID)
                        .ToListAsync();

                    if (idsToDelete == null || idsToDelete.Count == 0)
                    {
                        break; // 没有更多记录可删除
                    }

                    // 2. 根据ID列表精确删除
                    var deletedCount = await _db.Deleteable<AppLog>()
                        .Where(log => idsToDelete.Contains(log.ID))
                        .ExecuteCommandAsync();

                    if (deletedCount == 0)
                    {
                        // 如果idsToDelete不为空但删除数量为0，说明可能存在数据不一致，跳出循环避免死循环
                        break;
                    }

                    totalDeleted += deletedCount;

                    // 设置一个上限，防止意外的无限循环
                    if (totalDeleted > 1000000)
                    {
                        _logger.Warn("按大小删除日志操作已删除超过100万条记录，已自动中止。");
                        break;
                    }

                }
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                _logger.Warn($"按大小删除日志时发生错误: {ex.Message}");
            }

            return totalDeleted;
        }

        /// <summary>
        /// 获取 用户/系统操作 日志记录个数（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<int> GetUserRecordsCountAsync(DateTime? startTime = null, DateTime? endTime = null)
        {
            var header = $"SpecialFile.{LoggerExtension.UserLog}.";
            return await CountAsync(model => model.Logger.StartsWith(header)
                                  && (startTime == null || model.LogTime >= startTime)
                                  && (endTime == null || model.LogTime <= endTime));
        }

        /// <summary>
        /// 获取 用户/系统操作 日志记录个数（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public int GetUserRecordsCount(DateTime? startTime = null, DateTime? endTime = null)
        {
            var header = $"SpecialFile.{LoggerExtension.UserLog}.";
            return Count(model => model.Logger.StartsWith(header)
                         && (startTime == null || model.LogTime >= startTime)
                         && (endTime == null || model.LogTime <= endTime));
        }

        /// <summary>
        /// 用户记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public (List<AppLogModel> Items, int Total) GetUserRecordsPaged(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null)
        {
            var header = $"SpecialFile.{LoggerExtension.UserLog}.";
            return GetPaged(model => model.Logger.StartsWith(header)
                            && (startTime == null || model.LogTime >= startTime)
                            && (endTime == null || model.LogTime <= endTime),
                            pageIndex,
                            pageSize,
                            model => model.LogTime, true);
        }

        /// <summary>
        /// 用户记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<(List<AppLogModel> Items, int Total)> GetUserRecordsPagedAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null)
        {
            var header = $"SpecialFile.{LoggerExtension.UserLog}.";
            return await GetPagedAsync(model => model.Logger.StartsWith(header)
                                       && (startTime == null || model.LogTime >= startTime)
                                       && (endTime == null || model.LogTime <= endTime),
                                       pageIndex,
                                       pageSize,
                                       model => model.LogTime, true);
        }
    }
}
