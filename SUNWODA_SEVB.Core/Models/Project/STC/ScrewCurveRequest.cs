
namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 螺丝曲线数据请求
    /// </summary>
    public class ScrewCurveRequest
    {
        /// <summary>
        /// 控制器IP（可选，为空时使用工序配置）
        /// </summary>
        public string? ControllerIP { get; set; }

        ///// <summary>
        ///// 控制器端口（可选，为0时使用工序配置）
        ///// </summary>
        //public int ControllerPort { get; set; }

        /// <summary>
        /// 螺丝枪编号
        /// </summary>
        public string? TightenGun { get; set; }

        /// <summary>
        /// 产品序列号
        /// </summary>
        public string? ProductSn { get; set; }

        /// <summary>
        /// 螺丝标签
        /// </summary>
        public string? ScrewLabel { get; set; }


        /// <summary>
        /// 螺丝作业周期编号
        /// </summary>
        public string? workCycleId { get; set; }

        /// <summary>
        /// 参数排序
        /// </summary>
        public string? ParamSort { get; set; }

        /// <summary>
        /// Eor循环计数（用于查询TraceLogId）
        /// </summary>
        public string? EorCycleCount { get; set; }

        /// <summary>
        /// 曲线日志ID（直接指定时使用）
        /// </summary>
        public int TraceLogId { get; set; }
    }
}
