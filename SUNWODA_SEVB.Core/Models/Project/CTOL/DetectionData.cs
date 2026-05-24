using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    public class DetectionData : ModelBase
    {
        private string? _cellBarCode;
        private string? _detectionResult;
        private string? _defectType;
        private string? _station;

        /// <summary>
        /// 电芯条码
        /// </summary>
        public string? CellBarCode
        {
            get => _cellBarCode;
            set => SetProperty(ref _cellBarCode, value);
        }

        /// <summary>
        /// 检测结果
        /// </summary>
        public string? DetectionResult
        {
            get => _detectionResult;
            set => SetProperty(ref _detectionResult, value);
        }

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string? DefectType
        {
            get => _defectType;
            set => SetProperty(ref _defectType, value);
        }

        /// <summary>
        /// 工位
        /// </summary>
        public string? Station
        {
            get => _station;
            set => SetProperty(ref _station, value);
        }
    }
}
