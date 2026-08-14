using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class DataLimitParamModel : ModelBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public DataLimitParamModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="downDanger"></param>
        /// <param name="dowmWarning"></param>
        /// <param name="center"></param>
        /// <param name="upWarning"></param>
        /// <param name="upDanger"></param>
        public DataLimitParamModel(float downDanger, float dowmWarning, float center, float upWarning, float upDanger)
        {
            upDangerLimit = upDanger;
            upWarningLimit = upWarning;
            centerLimit = center;
            downWarningLimit = dowmWarning;
            downDangerLimit = downDanger;
        }

        private float upDangerLimit;
        private float upWarningLimit;
        private float centerLimit;
        private float downWarningLimit;
        private float downDangerLimit;

        /// <summary>
        /// 危险上限
        /// </summary>
        public float UpDangerLimit { get => upDangerLimit; set => SetProperty(ref upDangerLimit, value); }

        /// <summary>
        /// 警告上限
        /// </summary>
        public float UpWarningLimit { get => upWarningLimit; set => SetProperty(ref upWarningLimit, value); }

        /// <summary>
        /// 中线
        /// </summary>
        public float CenterLimit { get => centerLimit; set => SetProperty(ref centerLimit, value); }

        /// <summary>
        /// 警告下限
        /// </summary>
        public float DownWarningLimit { get => downWarningLimit; set => SetProperty(ref downWarningLimit, value); }

        /// <summary>
        /// 危险下限
        /// </summary>
        public float DownDangerLimit { get => downDangerLimit; set => SetProperty(ref downDangerLimit, value); }
    }
}
