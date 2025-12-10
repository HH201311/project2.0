using SqlSugar;

namespace SUNWODA_SEVB.Data.Models
{
    /// <summary>
    /// 应用程序日志表
    /// </summary>
    //[SugarIndex("app_logs_logtime", nameof(LogTime), OrderByType.Desc)]
    //[SugarIndex("app_logs_loglevel", nameof(LogLevel), OrderByType.Asc)]
    [SugarTable("app_logs", "应用程序日志表")]
    public class AppLog
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }

        [SugarColumn(ColumnName = "logtime", ColumnDescription = "日志时间", IsNullable =false)]
        public DateTime LogTime { get; set; }

        // C# 属性名使用 Level，映射到数据库的 LogLever 列
        [SugarColumn(ColumnName = "loglever", ColumnDescription = "日志级别")]
        public string LogLevel { get; set; } = null!;

        [SugarColumn(ColumnName = "logger", ColumnDescription = "日志记录器名称")]
        public string Logger { get; set; } = null!;

        [SugarColumn(ColumnName = "message", ColumnDescription = "日志消息", ColumnDataType = "text")]
        public string Message { get; set; } = null!;

        [SugarColumn(ColumnName = "exception", ColumnDescription = "异常信息", IsNullable = true, ColumnDataType = "text")]
        public string? Exception { get; set; }

    }
}
