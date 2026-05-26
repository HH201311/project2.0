using SqlSugar;

namespace SUNWODA_SEVB.Data.Models
{
    [SugarTable("detection_statistics", "雅康叠片机损AI检测统计表")]
    [SugarIndex(
        "detection_statistics_key",
        nameof(ModuleCode), OrderByType.Asc,
        nameof(ScopeType), OrderByType.Asc,
        nameof(StatisticsDate), OrderByType.Asc,
        true)]
    public class DetectionStatistics
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }

        [SugarColumn(ColumnName = "module_code", ColumnDescription = "所属模块编码")]
        public string ModuleCode { get; set; } = null!;

        [SugarColumn(ColumnName = "scope_type", ColumnDescription = "统计范围：0=总统计，1=每日统计")]
        public int ScopeType { get; set; }

        [SugarColumn(ColumnName = "statistics_date", ColumnDescription = "统计日期；总统计固定为1900-01-01")]
        public DateTime StatisticsDate { get; set; }

        [SugarColumn(ColumnName = "total_count", ColumnDescription = "总检测数")]
        public int TotalCount { get; set; }

        [SugarColumn(ColumnName = "ok_count", ColumnDescription = "OK数量")]
        public int OKCount { get; set; }

        [SugarColumn(ColumnName = "ng_count", ColumnDescription = "NG数量")]
        public int NGCount { get; set; }

        [SugarColumn(ColumnName = "ok_rate", ColumnDescription = "OK率")]
        public double OKRate { get; set; }

        [SugarColumn(ColumnName = "defect_ng_json", ColumnDescription = "各缺陷NG统计JSON", ColumnDataType = "text", IsNullable = true)]
        public string? DefectNGJson { get; set; }

        [SugarColumn(ColumnName = "last_update_time", ColumnDescription = "最后更新时间")]
        public DateTime LastUpdateTime { get; set; }

        [SugarColumn(ColumnName = "reset_time", ColumnDescription = "最后清零时间", IsNullable = true)]
        public DateTime? ResetTime { get; set; }

        [SugarColumn(ColumnName = "remark", ColumnDescription = "备注", IsNullable = true)]
        public string? Remark { get; set; }
    }
}
