
namespace SUNWODA_SEVB.Core.Models.Project.STC.Results
{
    /// <summary>
    /// TR值结果
    /// </summary>
    public class ScrewTrValueResult
    {
        public string? ControllerIP { get; set; }
        public string? TightenGun { get; set; }
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public float TrValue { get; set; }
        public bool FromCache { get; set; }
        public DateTime? CachedTime { get; set; }
    }

    /// <summary>
    /// 批量TR值结果
    /// </summary>
    public class BatchTrValueResult
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        /// <summary>
        /// 螺丝枪编号 -> TR值结果 映射
        /// </summary>
        public Dictionary<string, ScrewTrValueResult> TrValues { get; set; } = new();

        /// <summary>
        /// 获取指定螺丝枪的TR值
        /// </summary>
        public float GetTrValue(string tightenGun, float defaultValue = 1.0f)
        {
            if (TrValues.TryGetValue(tightenGun, out var result) && result.IsSuccess)
            {
                return result.TrValue;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// 曲线数据结果
    /// </summary>
    public class ScrewCurveDataResult
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public int TraceLogId { get; set; }
        public List<CurveDataPoint> DataPoints { get; set; } = new();

        public double[] Angles => DataPoints.Select(p => p.Angle).ToArray();
        public double[] Torques => DataPoints.Select(p => p.Torque).ToArray();
    }
    /// <summary>
    /// 曲线数据点
    /// </summary>
    public class CurveDataPoint
    {
        public double Angle { get; set; }
        public double Torque { get; set; }
        public double Current { get; set; }
        public double RawValue { get; set; }
    }
    /// <summary>
    /// 作业周期结果
    /// </summary>
    public class ScrewProcessCycleResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// TraceLogId（即eorProcessCycleNumber）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 文档ID（eorDocId）
        /// </summary>
        public int DocId { get; set; }

        /// <summary>
        /// 作业周期编号（eorProcessCycleNumber）
        /// </summary>
        public int ProcessCycleNumber { get; set; }
        public int eorCycleCountNumber {  get; set; }//有线枪新
        /// <summary>
        /// 扭矩结果
        /// </summary>
        public double TorqueResult { get; set; }

        /// <summary>
        /// 角度结果
        /// </summary>
        public double AngleResult { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// 是否通过
        /// </summary>
        public bool IsPass { get; set; }
    }


    /// <summary>
    /// Cookie缓存记录
    /// </summary>
    public class CookieCacheRecord
    {
        public string ControllerIP { get; set; } = string.Empty;
        public string Cookie { get; set; } = string.Empty;
        public DateTime CachedTime { get; set; }
        public DateTime? ExpirationTime { get; set; }
    }

}
