using SUNWODA_SEVB.Core.Models.Data;

namespace SUNWODA_SEVB.Core.Interfaces.Data
{
    /// <summary>
    /// 应用日志仓储接口
    /// </summary>
    public interface IAppLogRepository : IRepository<AppLogModel>
    {
        /// <summary>
        /// 批量插入日志
        /// </summary>
        Task<bool> BulkInsertAsync(List<AppLogModel> logs);

        /// <summary>
        /// 删除过期日志
        /// </summary>
        Task<int> DeleteOldLogsAsync(int daysToKeep);

        /// <summary>
        /// 获取数据库大小（MB）
        /// </summary>
        Task<double> GetDatabaseSizeAsync();

        /// <summary>
        /// 按大小删除日志，删除到指定大小以下
        /// </summary>
        /// <param name="targetSizeMB">目标大小（MB）</param>
        /// <returns>删除的记录数</returns>
        Task<int> DeleteLogsBySize(double targetSizeMB);

        #region 用户记录查询
        /// <summary>
        /// 用户记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<int> GetUserRecordsCountAsync(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 用户记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        int GetUserRecordsCount(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 用户记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<(List<AppLogModel> Items, int Total)> GetUserRecordsPagedAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 用户记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        (List<AppLogModel> Items, int Total) GetUserRecordsPaged(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);
        #endregion

        #region 调机记录查询
        /// <summary>
        /// 调机记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<int> GetAdjustmentRecordsCountAsync(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 调机记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        int GetAdjustmentRecordsCount(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 调机记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<(List<AppLogModel> Items, int Total)> GetAdjustmentRecordsPagedAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 调机记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        (List<AppLogModel> Items, int Total) GetAdjustmentRecordsPaged(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);
        #endregion

        #region 报警记录查询
        /// <summary>
        /// 报警记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<int> GetAlarmRecordsCountAsync(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 报警记录查询（支持时间过滤）
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        int GetAlarmRecordsCount(DateTime? startTime, DateTime? endTime);

        /// <summary>
        /// 报警记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        Task<(List<AppLogModel> Items, int Total)> GetAlarmRecordsPagedAsync(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 报警记录分页查询（支持时间过滤）
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        (List<AppLogModel> Items, int Total) GetAlarmRecordsPaged(int pageIndex, int pageSize, DateTime? startTime = null, DateTime? endTime = null);
        #endregion
    }
}
