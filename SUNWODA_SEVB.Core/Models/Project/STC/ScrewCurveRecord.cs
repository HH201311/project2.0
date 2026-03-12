

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 螺丝曲线数据点记录（用于CSV导出）
    /// </summary>
    public class ScrewCurveRecord
    {
        // ===== 基础信息 =====
        public int PointIndex { get; set; }
        public DateTime RecordTime { get; set; }

        // ===== 螺丝标识 =====
        public int ScrewIndex { get; set; }
        public string? ScrewLabel { get; set; }
        public string? TightenGun { get; set; }

        // ===== 实测值 =====
        public double Angle { get; set; }
        public double Torque { get; set; }
        public double? Current { get; set; }

        // ===== 工艺参数（上下限） =====
        public double? TorqueLower { get; set; }      // 扭矩下限
        public double? TorqueUpper { get; set; }      // 扭矩上限
        public double? TorqueTarget { get; set; }     // 扭矩标准值

        public double? AngleLower { get; set; }       // 角度下限
        public double? AngleUpper { get; set; }       // 角度上限
        public double? AngleTarget { get; set; }      // 角度标准值

        public double? CurrentLower { get; set; }     // 电流下限
        public double? CurrentUpper { get; set; }     // 电流上限

        // ===== 结果判定 =====
        public bool IsOk { get; set; }
        public string? NgReason { get; set; }         // NG原因（可选）


    }
}
