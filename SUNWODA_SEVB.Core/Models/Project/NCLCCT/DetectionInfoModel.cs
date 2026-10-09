using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.NCLCCT
{
    public class DetectionInfoModel : ModelBase
    {
        private DateTime clearTime;
        public DateTime ClearTime
        {
            get => clearTime;
            set => SetProperty(ref clearTime, value);
        }

        private int detectionTotalNum;
        public int DetectionTotalNum
        {
            get => detectionTotalNum;
            set => SetProperty(ref detectionTotalNum, value);
        }

        private int detectionOKNum;
        public int DetectionOKNum
        {
            get => detectionOKNum;
            set => SetProperty(ref detectionOKNum, value);
        }

        private int detectionNGNum;
        public int DetectionNGNum
        {
            get => detectionNGNum;
            set => SetProperty(ref detectionNGNum, value);
        }

        private double detectionOKRate;
        public double DetectionOKRate
        {
            get => detectionOKRate;
            set => SetProperty(ref detectionOKRate, value);
        }

        public DetectionInfoModel(DateTime detectionClearTime, int totalNum, int okNum, int ngNum, double okRate)
        {
            ClearTime = detectionClearTime;
            DetectionTotalNum = totalNum;
            DetectionOKNum = okNum;
            DetectionNGNum = ngNum;
            DetectionOKRate = okRate;
        }
    }
}
