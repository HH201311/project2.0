using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.PTO
{
    /// <summary>
    /// 缺陷参数模型（防栈溢出+双向绑定）
    /// </summary>
    public class DefectParameter : ModelBase
    {
        private string? _defectName;
        private double _width;
        private double _height;
        private double _area;
        private double _credibility;
        private bool _isEnable;
        private int _ngCount;
        private double _yieldRate = 100.00;

        public string? DefectName
        {
            get => _defectName;
            set => SetProperty(ref _defectName, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
        }

        public double Credibility
        {
            get => _credibility;
            set => SetProperty(ref _credibility, value);
        }

        public bool IsEnable
        {
            get => _isEnable;
            set => SetProperty(ref _isEnable, value);
        }

        public int NGCount
        {
            get => _ngCount;
            set => SetProperty(ref _ngCount, value);
        }

        public double YieldRate
        {
            get => _yieldRate;
            set => SetProperty(ref _yieldRate, value);

        }
    }

    /// <summary>
    /// 检测模式
    /// </summary>
    public enum DetectionMode
    {
        全检,
        OK,
        NG
    }

}
