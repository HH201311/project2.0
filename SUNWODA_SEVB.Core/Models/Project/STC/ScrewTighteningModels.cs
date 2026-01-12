

using SUNWODA_SEVB.Core.Enumerations.Logging;
using System.Text.Json.Serialization;

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
    public class TighteningResult
    {
        [JsonPropertyName("operatorId")]
        public string? OperatorId { get; set; }

        [JsonPropertyName("productSn")]
        public string? ProductSn { get; set; }

        [JsonPropertyName("startStamp")]
        public string? StartStamp { get; set; }

        [JsonPropertyName("stopStamp")]
        public string? StopStamp { get; set; }

        [JsonPropertyName("testResult")]
        public string? TestResult { get; set; }

        [JsonPropertyName("testData")]
        public List<TighteningParam>? TestData { get; set; }

        /// <summary>
        /// 判断整体结果是否OK (testResult == "0" 表示OK)
        /// </summary>
        [JsonIgnore]
        public bool IsOk => TestResult == "0";
        /// <summary>
        /// 解析后的开始时间
        /// </summary>
        [JsonIgnore]
        public DateTime? StartTime => DateTime.TryParse(StartStamp, out var dt) ? dt : null;

        /// <summary>
        /// 解析后的结束时间
        /// </summary>
        [JsonIgnore]
        public DateTime? StopTime => DateTime.TryParse(StopStamp, out var dt) ? dt : null;
    }

    /// <summary>
    /// 螺丝拧紧参数数据
    /// </summary>
    public class TighteningParam
    {
        [JsonPropertyName("paramName")]
        public string? ParamName { get; set; }

        [JsonPropertyName("TightenGun")]
        public string? TightenGun { get; set; }

        [JsonPropertyName("paramSort")]
        public string? ParamSort { get; set; }

        [JsonPropertyName("paramValue")]
        public string? ParamValue { get; set; }

        /// <summary>
        /// 参数结果: "0"=OK, "1"=NG
        /// </summary>
        [JsonPropertyName("paramResult")]
        public string? ParamResult { get; set; }

        [JsonPropertyName("paramFirstLower")]
        public string? ParamFirstLower { get; set; }

        [JsonPropertyName("paramFirstUpper")]
        public string? ParamFirstUpper { get; set; }

        [JsonPropertyName("paramUnit")]
        public string? ParamUnit { get; set; }

        /// <summary>
        /// 单项结果是否OK
        /// </summary>
        [JsonIgnore]
        public bool IsOk => ParamResult == "0";

        /// <summary>
        /// 数值类型的参数值
        /// </summary>
        [JsonIgnore]
        public double NumericValue => double.TryParse(ParamValue, out var v) ? v : 0;

        /// <summary>
        /// 获取下限数值
        /// </summary>
        [JsonIgnore]
        public double LowerLimit => double.TryParse(ParamFirstLower, out var v) ? v : 0;

        /// <summary>
        /// 获取上限数值
        /// </summary>
        [JsonIgnore]
        public double UpperLimit => double.TryParse(ParamFirstUpper, out var v) ? v : 0;
    }

    /// <summary>
    /// 螺丝拧紧信息（包含控制器连接信息）
    /// </summary>
    public class ScrewTighteningInfo
    {
        /// <summary>
        /// 螺丝编号
        /// </summary>
        public string? ScrewNumber { get; set; }

        /// <summary>
        /// MES参数名称
        /// </summary>
        public string? MesParamName { get; set; }

        /// <summary>
        /// MES参数编码
        /// </summary>
        public int MesParamCode { get; set; }

        /// <summary>
        /// 拧紧控制器名称
        /// </summary>
        public string? TightenControllerName { get; set; }

        /// <summary>
        /// 拧紧控制器IP
        /// </summary>
        public string? TightenControllerIP { get; set; }

        /// <summary>
        /// 拧紧控制器端口
        /// </summary>
        public string? TightenControllerPort { get; set; }

        /// <summary>
        /// EOR周期计数（用于查询曲线数据）
        /// </summary>
        public int EorCycleCount { get; set; }

        /// <summary>
        /// 扭矩上限
        /// </summary>
        public string? TorqueUpLimit { get; set; }

        /// <summary>
        /// 扭矩下限
        /// </summary>
        public string? TorqueLowLimit { get; set; }

        /// <summary>
        /// 角度最大限制
        /// </summary>
        public string? AngleMaxLimit { get; set; }
    }

    /// <summary>
    /// 曲线数据点
    /// </summary>
    public class ScrewCurveDataPoint
    {
        /// <summary>
        /// 角度值
        /// </summary>
        public double Angle { get; set; }

        /// <summary>
        /// 扭矩值
        /// </summary>
        public double Torque { get; set; }

        /// <summary>
        /// 电流值
        /// </summary>
        public double Current { get; set; }
    }

    /// <summary>
    /// 曲线采集结果
    /// </summary>
    public class CurveCollectionResult
    {
        /// <summary>
        /// 产品编码
        /// </summary>
        public string? ProductSn { get; set; }

        /// <summary>
        /// 螺丝标签
        /// </summary>
        public string? ScrewLabel { get; set; }

        /// <summary>
        /// 采集时间
        /// </summary>
        public DateTime CollectionTime { get; set; }

        /// <summary>
        /// 曲线数据点集合
        /// </summary>
        public List<ScrewCurveDataPoint> DataPoints { get; set; } = new();

        /// <summary>
        /// 最终扭矩值
        /// </summary>
        public double FinalTorque { get; set; }

        /// <summary>
        /// 最终角度值
        /// </summary>
        public double FinalAngle { get; set; }

        /// <summary>
        /// 是否OK
        /// </summary>
        public bool IsOk { get; set; }

        /// <summary>
        /// 控制器IP
        /// </summary>
        public string? ControllerIP { get; set; }

        /// <summary>
        /// 周期编号
        /// </summary>
        public int ProcessCycleNumber { get; set; }
    }
}
