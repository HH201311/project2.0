

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

    public enum DetectionStatisticsSource
    {
        /// <summary>
        /// 全部统计：在线 + 离线
        /// 用于保持原有界面总统计兼容
        /// </summary>
        All = 0,

        /// <summary>
        /// 在线检测统计
        /// </summary>
        Online = 1,

        /// <summary>
        /// 离线检测统计
        /// </summary>
        Offline = 2
    }

    public class DetectionStatisticsModel
    {
        public int ID { get; set; }

        public string ModuleCode { get; set; } = null!;

        public int SourceType { get; set; }// 数据来源：0=全部，1=在线，2=离线

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
