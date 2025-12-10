

using SUNWODA_SEVB.Core.Enumerations.Logging;

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    //internal class ScrewTighteningModels
    //{
    //}
    /// <summary>
    /// 螺丝位置配置
    /// </summary>
    public class ScrewPosition
    {
        public int ScrewId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public required string Label { get; set; }
        public ScrewStatus Status { get; set; }
    }

    /// <summary>
    /// 螺丝状态枚举
    /// </summary>
    public enum ScrewStatus
    {
        Pending,    // 待拧紧
        OK,         // 合格
        NG          // 异常
    }

    /// <summary>
    /// 工序配置
    /// </summary>
    public class ProcessConfig
    {
        public required string ProcessId { get; set; }
        public required string ProcessName { get; set; }
        public required string ImagePath { get; set; }
        public required List<ScrewPosition> ScrewPositions { get; set; }
    }

    /// <summary>
    /// 单个螺丝拧紧数据
    /// </summary>
    public class ScrewTighteningData
    {
        public int ScrewId { get; set; }
        public required string ScrewLabel { get; set; }
        public DateTime TighteningTime { get; set; }
        public required double[] TorqueValues { get; set; }      // 扭矩值数组
        public required double[] AngleValues { get; set; }        // 角度值数组
        public double FinalTorque { get; set; }          // 最终扭矩
        public double FinalAngle { get; set; }           // 最终角度
        public double TorqueMin { get; set; }            // 扭矩下限
        public double TorqueMax { get; set; }            // 扭矩上限
        public double AngleMin { get; set; }             // 角度下限
        public double AngleMax { get; set; }             // 角度上限
        public ScrewStatus Result { get; set; }          // 拧紧结果
        public string? ErrorCode { get; set; }            // 错误码
        public string? ErrorMessage { get; set; }         // 错误信息
    }

    /// <summary>
    /// Pack包拧紧数据（包含多个螺丝）
    /// </summary>
    public class PackTighteningData
    {
        public required string PackId { get; set; }               // Pack包ID
        public required string ProcessId { get; set; }            // 工序ID
        public required string ProcessName { get; set; }          // 工序名称
        public DateTime StartTime { get; set; }          // 开始时间
        public DateTime EndTime { get; set; }            // 结束时间
        public List<ScrewTighteningData> ScrewDataList { get; set; } = new List<ScrewTighteningData>(); // 所有螺丝拧紧数据
        public bool IsAllOK => ScrewDataList.TrueForAll(s => s.Result == ScrewStatus.OK);
        public int TotalCount => ScrewDataList?.Count ?? 0;
        public int OKCount => ScrewDataList?.FindAll(s => s.Result == ScrewStatus.OK).Count ?? 0;
        public int NGCount => ScrewDataList?.FindAll(s => s.Result == ScrewStatus.NG).Count ?? 0;


    }

    /// <summary>
    /// 拧紧历史记录项
    /// </summary>
    public class TighteningHistoryItem
    {
        public DateTime Time { get; set; }
        public required string PackId { get; set; }
        public required string ProcessName { get; set; }
        public int TotalCount { get; set; }
        public int OKCount { get; set; }
        public int NGCount { get; set; }
        public required string Result { get; set; }
    }


    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public CoreLogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;

        public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
    }
    /// <summary>
    /// 工序信息
    /// </summary>
    public class ProcessInfo
    {
        public int Id { get; set; }
        public string ProcessCode { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public int ScrewCount { get; set; }
    }

    /// <summary>
    /// 拧紧数据（用于协议解析）
    /// </summary>
    public class TighteningData
    {
        public string? PackCode { get; set; }
        public string? ScrewLabel { get; set; }
        public bool IsOk { get; set; }
        public double[]? CurveData { get; set; }
    }
}
