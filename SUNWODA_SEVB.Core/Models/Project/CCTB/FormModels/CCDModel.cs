using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels
{
    /// <summary>
    /// CCD尺寸数据-界面表格
    /// </summary>
    public class CCDModel : ModelBase
    {
        private DateTime gatherTime;
        private double[]? batteryTabDatas;
        private double[]? ceramicDatas;
        private double[]? filmDatas;
        private double[]? malpositionDatas;

        private double batteryTabLimitDown;
        private double batteryTabLimitUp;
        private double ceramicLimitDown;
        private double ceramicLimitUp;
        private double filmLimitDown;
        private double filmLimitUp;
        private double malpositionLimitDown;
        private double malpositionLimitUp;

        public DateTime GatherTime
        {
            get => gatherTime;
            set => SetProperty(ref gatherTime, value);
        }

        /// <summary>
        /// 极耳尺寸数据
        /// </summary>
        public double[]? BatteryTabDatas
        {
            get => batteryTabDatas;
            set => SetProperty(ref batteryTabDatas, value);
        }

        /// <summary>
        /// 陶瓷尺寸数据
        /// </summary>
        public double[]? CeramicDatas
        {
            get => ceramicDatas;
            set => SetProperty(ref ceramicDatas, value);
        }

        /// <summary>
        /// 涂膜尺寸数据
        /// </summary>
        public double[]? FilmDatas
        {
            get => filmDatas;
            set => SetProperty(ref filmDatas, value);
        }

        /// <summary>
        /// 错位尺寸数据
        /// </summary>
        public double[]? MalpositionDatas
        {
            get => malpositionDatas;
            set => SetProperty(ref malpositionDatas, value);
        }

        /// <summary>
        /// 极耳下限
        /// </summary>
        public double BatteryTabLimitDown
        {
            get => batteryTabLimitDown;
            set => SetProperty(ref batteryTabLimitDown, value);
        }

        /// <summary>
        /// 极耳上限
        /// </summary>
        public double BatteryTabLimitUp
        {
            get => batteryTabLimitUp;
            set => SetProperty(ref batteryTabLimitUp, value);
        }

        /// <summary>
        /// 陶瓷下限
        /// </summary>
        public double CeramicLimitDown
        {
            get => ceramicLimitDown;
            set => SetProperty(ref ceramicLimitDown, value);
        }

        /// <summary>
        /// 陶瓷上限
        /// </summary>
        public double CeramicLimitUp
        {
            get => ceramicLimitUp;
            set => SetProperty(ref ceramicLimitUp, value);
        }

        /// <summary>
        /// 涂膜下限
        /// </summary>
        public double FilmLimitDown
        {
            get => filmLimitDown;
            set => SetProperty(ref filmLimitDown, value);
        }

        /// <summary>
        /// 涂膜上限
        /// </summary>
        public double FilmLimitUp
        {
            get => filmLimitUp;
            set => SetProperty(ref filmLimitUp, value);
        }

        /// <summary>
        /// 错位下限
        /// </summary>
        public double MalpositionLimitDown
        {
            get => malpositionLimitDown;
            set => SetProperty(ref malpositionLimitDown, value);
        }

        /// <summary>
        /// 错位上限
        /// </summary>
        public double MalpositionLimitUp
        {
            get => malpositionLimitUp;
            set => SetProperty(ref malpositionLimitUp, value);
        }
    }
}
