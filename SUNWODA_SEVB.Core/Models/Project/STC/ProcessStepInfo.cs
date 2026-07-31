using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 工序工步信息
    /// </summary>
    public class ProcessStepInfo : INotifyPropertyChanged
    {
        private ScrewStatus _status = ScrewStatus.Pending;
        private double? _torqueValue;
        private double? _angleValue;

        /// <summary>
        /// 步骤序号（在完整工序中的顺序）
        /// </summary>
        public int StepNumber { get; set; }

        /// <summary>
        /// 步骤描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 步骤类型
        /// </summary>
        public StepType Type { get; set; } = StepType.Other;

        /// <summary>
        /// 螺丝序号（仅螺丝和复拧步骤有）
        /// </summary>
        public int? ScrewId { get; set; }

        /// <summary>
        /// 扭矩值
        /// </summary>
        [JsonIgnore]
        public double? TorqueValue
        {
            get => _torqueValue;
            set
            {
                if (_torqueValue != value)
                {
                    _torqueValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }

        /// <summary>
        /// 角度值
        /// </summary>
        [JsonIgnore]
        public double? AngleValue
        {
            get => _angleValue;
            set
            {
                if (_angleValue != value)
                {
                    _angleValue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }

        /// <summary>
        /// 拧紧结果状态
        /// </summary>
        public ScrewStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(TooltipText));
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        /// <summary>
        /// 是否为螺丝相关步骤（拧紧或复拧）
        /// </summary>
        [JsonIgnore]
        public bool IsScrewRelated => Type == StepType.Screw || Type == StepType.Retighten;

        /// <summary>
        /// 是否为拧紧步骤（非复拧）
        /// </summary>
        [JsonIgnore]
        public bool IsTightenStep => Type == StepType.Screw;

        /// <summary>
        /// 是否为复拧步骤
        /// </summary>
        [JsonIgnore]
        public bool IsRetightenStep => Type == StepType.Retighten;

        /// <summary>
        /// 状态颜色（十六进制字符串）
        /// </summary>
        [JsonIgnore]
        public string StatusColor => Status switch
        {
            ScrewStatus.OK => "#4CAF50",
            ScrewStatus.NG => "#F44336",
            ScrewStatus.Pending => "#9E9E9E",
            _ => "#9E9E9E"
        };

        /// <summary>
        /// 状态文本
        /// </summary>
        [JsonIgnore]
        public string StatusText => Status switch
        {
            ScrewStatus.OK => "OK",
            ScrewStatus.NG => "NG",
            ScrewStatus.Pending => "—",
            _ => "—"
        };

        /// <summary>
        /// 状态画刷名（XAML绑定用）
        /// </summary>
        [JsonIgnore]
        public string StatusBrush => Status switch
        {
            ScrewStatus.OK => "GreenBrush",
            ScrewStatus.NG => "RedBrush",
            ScrewStatus.Pending => "GrayBrush",
            _ => "GrayBrush"
        };

        /// <summary>
        /// 提示文本
        /// </summary>
        [JsonIgnore]
        public string TooltipText
        {
            get
            {
                var statusText = Status switch
                {
                    ScrewStatus.OK => "合格(OK)",
                    ScrewStatus.NG => "不合格(NG)",
                    _ => "待处理"
                };

                var text = $"步骤{StepNumber}: {Description}\n状态: {statusText}";
                if (TorqueValue.HasValue)
                    text += $"\n扭矩: {TorqueValue:F2} N·m";
                if (AngleValue.HasValue)
                    text += $"\n角度: {AngleValue:F1}°";
                return text;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 工步类型
    /// </summary>
    public enum StepType
    {
        /// <summary>螺丝拧紧</summary>
        Screw,
        /// <summary>复拧</summary>
        Retighten,
        /// <summary>扫码</summary>
        Scan,
        /// <summary>拍照</summary>
        Photo,
        /// <summary>上传</summary>
        Upload,
        /// <summary>确认</summary>
        Confirm,
        /// <summary>初始化</summary>
        Initialize,
        /// <summary>其他</summary>
        Other
    }
}
