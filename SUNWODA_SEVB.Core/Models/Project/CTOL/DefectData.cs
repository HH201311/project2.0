using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    public class DefectData : ModelBase
    {
        private string? _defectType;
        private int _count;
        private double _ratio;

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string? DefectType
        {
            get => _defectType;
            set => SetProperty(ref _defectType, value);
        }

        /// <summary>
        /// 数量
        /// </summary>
        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        /// <summary>
        /// 占比
        /// </summary>
        public double Ratio
        {
            get => _ratio;
            set => SetProperty(ref _ratio, value);
        }
    }
}
