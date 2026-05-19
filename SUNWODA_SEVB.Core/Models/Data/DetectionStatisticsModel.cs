

namespace SUNWODA_SEVB.Core.Models.Data
{
    public enum DetectionStatisticsScope
    {
        /// <summary>
        /// 总统计，界面绑定使用
        /// </summary>
        Total = 0,

        /// <summary>
        /// 每日统计
        /// </summary>
        Daily = 1
    }

    public class DetectionStatisticsModel
    {
        public int ID { get; set; }

        public string ModuleCode { get; set; } = null!;

        public int ScopeType { get; set; }

        public DateTime StatisticsDate { get; set; }

        public int TotalCount { get; set; }

        public int OKCount { get; set; }

        public int NGCount { get; set; }

        public double OKRate { get; set; }

        public string? DefectNGJson { get; set; }

        public DateTime LastUpdateTime { get; set; }

        public DateTime? ResetTime { get; set; }

        public string? Remark { get; set; }
    }
}
