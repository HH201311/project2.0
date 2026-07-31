using SUNWODA_SEVB.Core.Common;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 螺丝曲线条目：一条 = 一颗螺丝的一次拧紧/复拧曲线。
    /// 由上位机整包 ScrewTighteningInfoItem 投影而来，用于列表展示与按需取曲线。
    /// 工步概念已去除--收到的整包即全部拧紧/复拧结果，按 ScrwType 区分类型。
    /// </summary>
    public class ScrewCurveEntry : ModelBase
    {
        private ScrewStatus _status = ScrewStatus.Pending;
        private bool _isSelected;
        private double? _torqueValue;
        private double? _angleValue;
        private bool _hasCurve;

        /// <summary>螺丝序号（协议 ScrewNumber）</summary>
        public int ScrewNumber { get; set; }

        /// <summary>拧紧类型原文（协议 ScrwType，后续上位机填值后用于区分拧紧/复拧）</summary>
        public string? ScrewType { get; set; }

        /// <summary>拧紧结果原文 "OK"/"NG"</summary>
        public string? ScrewResult { get; set; }

        /// <summary>周期编号（=TraceLogId，用于取曲线）</summary>
        public string? EorProcessCycleNumber { get; set; }
        public string? ScrewTime { get; set; }

        public string? TightenControllerName { get; set; }
        public string? TightenControllerIP { get; set; }
        public string? TorqueUpLimit { get; set; }
        public string? TorqueLowLimit { get; set; }
        public string? TargetTorque { get; set; }

        /// <summary>实际扭矩</summary>
        public double? TorqueValue
        {
            get => _torqueValue;
            set => SetProperty(ref _torqueValue, value);
        }

        /// <summary>实际角度</summary>
        public double? AngleValue
        {
            get => _angleValue;
            set => SetProperty(ref _angleValue, value);
        }

        /// <summary>状态（由 ScrewResult 派生：OK/NG/Pending）</summary>
        public ScrewStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }

        /// <summary>是否已加载曲线</summary>
        public bool HasCurve
        {
            get => _hasCurve;
            set => SetProperty(ref _hasCurve, value);
        }

        /// <summary>是否当前选中（列表高亮）</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        [JsonIgnore]
        public bool IsScrewOk => string.Equals(ScrewResult, "OK", StringComparison.OrdinalIgnoreCase);

        [JsonIgnore]
        public int EorCycleNumberInt => int.TryParse(EorProcessCycleNumber, out var n) ? n : 0;

        /// <summary>
        /// 拧紧/复拧 标签。ScrwType 为空时显示 "-"；
        /// 上位机传值：1=拧紧，2=复拧
        /// </summary>
        [JsonIgnore]
        public string TypeLabel
        {
            get
            {
                var t = (ScrewType ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(t)) return "-";
                if (t == "1" || t.Contains("拧") || string.Equals(t, "T", StringComparison.OrdinalIgnoreCase)) return "拧紧";
                if (t == "2" || t.Contains("复") || string.Equals(t, "R", StringComparison.OrdinalIgnoreCase)) return "复拧";
                return t;
            }
        }

        [JsonIgnore]
        public string StatusColor => Status switch
        {
            ScrewStatus.OK => "#4CAF50",
            ScrewStatus.NG => "#F44336",
            _ => "#9E9E9E"
        };

        [JsonIgnore]
        public string DisplayLabel => $"#{ScrewNumber} {TypeLabel}";

        [JsonIgnore]
        public string TooltipText
        {
            get
            {
                var s = Status switch
                {
                    ScrewStatus.OK => "合格",
                    ScrewStatus.NG => "不合格",
                    _ => "待处理"
                };
                var text = $"{DisplayLabel}\n类型: {TypeLabel}\n结果: {s}";
                if (TorqueValue.HasValue) text += $"\n扭矩: {TorqueValue:F2} N·m";
                if (AngleValue.HasValue) text += $"\n角度: {AngleValue:F1}°";
                text += $"\n周期号: {EorProcessCycleNumber}";
                return text;
            }
        }

        
    }
}