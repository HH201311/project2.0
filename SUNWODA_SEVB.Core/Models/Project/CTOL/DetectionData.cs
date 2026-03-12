using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    public class DetectionData : ModelBase
    {
        private string? _cellBarCode;
        private string? _detectionResult;
        private string? _ngDetails;
        private string? _station;
        private DateTime _detectionTime;
        private string? _resultImagePath;

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
        /// NG信息
        /// </summary>
        public string? NgDetails
        {
            get => _ngDetails;
            set => SetProperty(ref _ngDetails, value);
        }

        /// <summary>
        /// 工位
        /// </summary>
        public string? Station
        {
            get => _station;
            set => SetProperty(ref _station, value);
        }

        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime DetectionTime
        {
            get => _detectionTime;
            set => SetProperty(ref _detectionTime, value);
        }

        /// <summary>
        /// 结果图路径
        /// </summary>
        public string? ResultImagePath
        {
            get => _resultImagePath;
            set => SetProperty(ref _resultImagePath, value);
        }
    }
}
