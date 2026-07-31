using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 上位机发送的螺丝拧紧数据包（文本协议 + JSON）
    /// 原始格式: \x02{站号}\tScrewData\t{JSON}\t{结果码}\x03
    /// </summary>
    public class ScrewTighteningPackageResponse
    {
        /// <summary>设备名称（如 "P07强电连接1"）</summary>
        [JsonPropertyName("DeviceName")]
        public string? DeviceName { get; set; }

        [JsonPropertyName("Sessionid")]
        public string? Sessionid { get; set; }

        [JsonPropertyName("GroupCode")]
        public string? GroupCode { get; set; }

        /// <summary>产品型号 / 编码</summary>
        [JsonPropertyName("ProductModel")]
        public string? ProductModel { get; set; }

        [JsonPropertyName("ProductCode")]
        public string? ProductCode { get; set; }

        [JsonPropertyName("EntryTime")]
        public string? EntryTime { get; set; }

        [JsonPropertyName("ExitTime")]
        public string? ExitTime { get; set; }

        /// <summary>螺丝总数（字符串）</summary>
        [JsonPropertyName("ScrewCount")]
        public string? ScrewCount { get; set; }

        /// <summary>整体结果 "OK" / "NG"</summary>
        [JsonPropertyName("OverallResult")]
        public string? OverallResult { get; set; }

        /// <summary>螺丝拧紧信息列表</summary>
        [JsonPropertyName("ScrewTighteningInfoLis")]
        public List<ScrewTighteningInfoItem>? ScrewTighteningInfoLis { get; set; }

        /// <summary>包结果是否OK</summary>
        [JsonIgnore]
        public bool IsOverallOk => string.Equals(OverallResult, "OK", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public int ScrewCountInt => int.TryParse(ScrewCount, out var c) ? c : 0;
    }

    /// <summary>
    /// 单个螺丝的拧紧信息
    /// </summary>
    public class ScrewTighteningInfoItem
    {
        /// <summary>螺丝序号（1-based）</summary>
        [JsonPropertyName("ScrewNumber")]
        public string? ScrewNumber { get; set; }

        [JsonPropertyName("ScrwType")]
        public string? ScrwType { get; set; }

        [JsonPropertyName("TorqueUpLimit")]
        public string? TorqueUpLimit { get; set; }

        [JsonPropertyName("TorqueLowLimit")]
        public string? TorqueLowLimit { get; set; }

        /// <summary>实际扭矩值</summary>
        [JsonPropertyName("TorqueActual")]
        public string? TorqueActual { get; set; }

        /// <summary>
        /// 目标扭矩值（上位机传值，非实际测量值）
        /// </summary>
        [JsonPropertyName("TorqueTarget")]
        public string? TorqueTarget { get; set; }

        [JsonPropertyName("AngleMaxLimit")]
        public string? AngleMaxLimit { get; set; }

        /// <summary>实际角度值</summary>
        [JsonPropertyName("AngleActual")]
        public string? AngleActual { get; set; }

        /// <summary>螺丝枪名称（如 "螺丝枪1"）</summary>
        [JsonPropertyName("TightenControllerName")]
        public string? TightenControllerName { get; set; }

        /// <summary>控制器IP地址</summary>
        [JsonPropertyName("TightenControllerIP")]
        public string? TightenControllerIP { get; set; }

        [JsonPropertyName("TightenControllerPort")]
        public string? TightenControllerPort { get; set; }

        /// <summary>
        /// 周期编号（即TraceLogId，用于曲线下载）
        /// </summary>
        [JsonPropertyName("EorProcessCycleNumber")]
        public string? EorProcessCycleNumber { get; set; }

        /// <summary>螺丝位置（后续补充）</summary>
        [JsonPropertyName("ScrewPosition")]
        public string? ScrewPosition { get; set; }

        /// <summary>拧紧结果 "OK" / "NG"</summary>
        [JsonPropertyName("ScrewResult")]
        public string? ScrewResult { get; set; }

        [JsonPropertyName("ErrorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("ScrewTime")]
        public string? ScrewTime { get; set; }

      

        // ── 便捷解析属性 ──

        [JsonIgnore]
        public int ScrewNumberInt => int.TryParse(ScrewNumber, out var n) ? n : 0;

        [JsonIgnore]
        public double TorqueActualDouble => double.TryParse(TorqueActual, out var v) ? v : 0;


        [JsonIgnore]
        public double AngleActualDouble => double.TryParse(AngleActual, out var v) ? v : 0;

        [JsonIgnore]
        public bool IsScrewOk => string.Equals(ScrewResult, "OK", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public int EorCycleNumberInt => int.TryParse(EorProcessCycleNumber, out var n) ? n : 0;
    }

    /// <summary>
    /// 文本协议解析结果
    /// </summary>
    public class TextProtocolMessage
    {
        /// <summary>站号（01等）</summary>
        public string StationId { get; set; } = "";

        /// <summary>消息类型（ScrewData等）</summary>
        public string MessageType { get; set; } = "";

        /// <summary>JSON正文</summary>
        public string JsonBody { get; set; } = "";

        /// <summary>结果码</summary>
        public string ResultCode { get; set; } = "";

        /// <summary>
        /// 解析文本协议消息
        /// 格式: STX + 站号 + TAB + 消息类型 + TAB + JSON + TAB + 结果码 + ETX
        /// </summary>
        public static TextProtocolMessage Parse(string raw)
        {
            var result = new TextProtocolMessage();

            if (string.IsNullOrEmpty(raw))
                return result;

            // 去除STX/ETX控制字符
            var clean = raw.Trim('\x02', '\x03', '\0', '\n', '\r');

            // 按TAB分割
            var parts = clean.Split('\t');
            if (parts.Length < 3)
            {
                // 无法按协议解析，尝试直接当成JSON
                result.JsonBody = clean;
                return result;
            }

            result.StationId = parts[0];        // "01" 等
            result.MessageType = parts[1];      // "ScrewData"
            // 中间部分可能包含TAB，所以用String.Join从索引2到倒数第2
            result.JsonBody = string.Join("\t", parts.Skip(2).Take(parts.Length - 3));
            if (parts.Length >= 4)
                result.ResultCode = parts[^1]; // 结果码

            return result;
        }
    }
}
