

using SUNWODA_SEVB.Core.Common;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 螺丝位置配置
    /// </summary>
    public class ScrewPositionConfig : ModelBase
    {
        private int _index;
        private string _paramName = string.Empty;
        private double _x; //像素X坐标
        private double _y; //像素Y坐标
        private double _radius = 4;
        private ScrewStatus _status = ScrewStatus.Pending;
        private string _tightenGun = string.Empty;
        private double? _torqueValue;
        private double? _angleValue;
        private double _canvasLeft;
        private double _canvasTop;
        private double _displayRadius; // 实际显示半径（缩放后）

        /// 螺丝序号
        [JsonPropertyName("index")]
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }
        /// 参数名称
        [JsonPropertyName("paramName")]
        public string ParamName
        {
            get => _paramName;
            set => SetProperty(ref _paramName, value);
        }
        /// 像素X坐标（螺丝中心点）
        [JsonPropertyName("x")]
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }
        /// 像素y坐标（螺丝中心点）
        [JsonPropertyName("y")]
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }
        /// 显示半径（像素）
        [JsonPropertyName("radius")]
        public double Radius
        {
            get => _radius;
            set => SetProperty(ref _radius, value);
        }
        /// 螺丝枪编号
        [JsonPropertyName("tightenGun")]
        public string TightenGun
        {
            get => _tightenGun;
            set => SetProperty(ref _tightenGun, value);
        }

        #region 运行时属性
        /// 实际显示半径（缩放后）
        [JsonIgnore]
        public double DisplayRadius
        {
            get => _displayRadius;
            set
            {
                if (SetProperty(ref _displayRadius, value))
                {
                    OnPropertyChanged(nameof(DisplayDiameter));
                }
            }
        }

        /// 实际显示直径（缩放后）
        [JsonIgnore]
        public double DisplayDiameter => DisplayRadius * 2;

        /// 当前状态
        [JsonIgnore]
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
        
        /// 扭矩值
        [JsonIgnore]
        public double? TorqueValue
        {
            get => _torqueValue;
            set
            {
                if (SetProperty(ref _torqueValue, value))
                {
                    // 扭矩变化时通知 TooltipText 更新
                    OnPropertyChanged(nameof(TooltipText));
                }
            }
        }
        /// 角度值
        [JsonIgnore]
        public double? AngleValue
        {
            get => _angleValue;
            set => SetProperty(ref _angleValue, value);
        }
        /// Canvas X坐标
        [JsonIgnore]
        public double CanvasLeft
        {
            get => _canvasLeft;
            set=> SetProperty(ref _canvasLeft, value);
        }
        /// Canvas Y坐标
        [JsonIgnore]
        public double CanvasTop
        {
            get => _canvasTop;
            set { _canvasTop = value; OnPropertyChanged(); }
        }
        /// 显示标签
        [JsonIgnore]
        public string DisplayLabel => $"#{Index}";

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
                    ScrewStatus.OK => "合格",
                    ScrewStatus.NG => "不合格",
                    _ => "待拧紧"
                };

                //var text = $"{DisplayLabel}\n"+$"位置: ({X:F0}, {Y:F0})\n"+状态: {statusText}";
                var text = $"{ParamName} ({DisplayLabel})\n" +
                           $"螺丝枪: {TightenGun}\n" +
                           $"状态: {statusText}";
                if (TorqueValue.HasValue)
                    text += $"\n扭矩: {TorqueValue:F2} N·m";
                if (AngleValue.HasValue)
                    text += $"\n角度: {AngleValue:F1}°";
                return text;
            }
        }

        public string StatusColor => Status switch
        {
            ScrewStatus.OK => "#4CAF50",
            ScrewStatus.NG => "#F44336",
            ScrewStatus.Pending => "#9E9E9E",
            _ => "#9E9E9E"
        };


        #endregion
    }

    /// <summary>
    /// 螺丝位置配置文件模型
    /// </summary>
    public class ScrewPositionConfigFile
    {
        [JsonPropertyName("processCode")]
        public string ProcessCode { get; set; } = string.Empty;

        [JsonPropertyName("processName")]
        public string ProcessName { get; set; } = string.Empty;

        [JsonPropertyName("imageWidth")]
        public int ImageWidth { get; set; }

        [JsonPropertyName("imageHeight")]
        public int ImageHeight { get; set; }

        [JsonPropertyName("defaultRadius")]
        public double DefaultRadius { get; set; } = 10;

        [JsonPropertyName("screwPositions")]
        public List<ScrewPositionConfig> ScrewPositions { get; set; } = new();
    }
}
