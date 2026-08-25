using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.CCDSocketServer;
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
    public class CCDMessageModel : ModelBase
    {
        private DateTime gatherTime;
        private ESideType sideType;
        private bool isDryFilmData;
        private double[] batteryTabDatas = [];
        private double[] ceramicDatas = [];
        private double[] filmDatas = [];
        private double[] malpositionDatas = [];

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
        /// AB面
        /// </summary>
        public ESideType SideType { get => sideType; set => SetProperty(ref sideType, value); }

        /// <summary>
        /// 是否是干膜数据
        /// </summary>
        public bool IsDryFilmData { get => isDryFilmData; set => SetProperty(ref isDryFilmData, value); }

        /// <summary>
        /// 极耳尺寸数据
        /// </summary>
        public double[] BatteryTabDatas
        {
            get => batteryTabDatas;
            set => SetProperty(ref batteryTabDatas, value);
        }

        /// <summary>
        /// 陶瓷尺寸数据
        /// </summary>
        public double[] CeramicDatas
        {
            get => ceramicDatas;
            set => SetProperty(ref ceramicDatas, value);
        }

        /// <summary>
        /// 涂膜尺寸数据
        /// </summary>
        public double[] FilmDatas
        {
            get => filmDatas;
            set => SetProperty(ref filmDatas, value);
        }

        /// <summary>
        /// 错位尺寸数据
        /// </summary>
        public double[] MalpositionDatas
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

        /// <summary>
        /// 根据CCD尺寸数据协议模型生成CCD尺寸数据界面表格模型
        /// </summary>
        /// <param name="gatherTime"></param>
        /// <param name="commandName"></param>
        /// <param name="data"></param>
        public CCDMessageModel(DateTime gatherTime, string commandName, WeightMessageModel data)
        {
            GatherTime = gatherTime;

            var (sideType, isDry) = commandName switch
            {
                "data_a_wet" => (ESideType.A, false),
                "data_b_wet" => (ESideType.B, false),
                "data_b_dry" => (ESideType.B, true),
                _ => throw new ArgumentOutOfRangeException(nameof(commandName), $"Unexpected command name: {commandName}"),
            };

            SideType = sideType;
            IsDryFilmData = isDry;

            if (data.TabWidth != null) BatteryTabDatas = data.TabWidth.ConvertAll(o => double.Parse(o.PValue ?? "0")).ToArray();
            if (data.CilWidth != null) CeramicDatas = data.CilWidth.ConvertAll(o => double.Parse(o.PValue ?? "0")).ToArray();
            if (data.CoatingWidth != null) FilmDatas = data.CoatingWidth.ConvertAll(o => double.Parse(o.PValue ?? "0")).ToArray();
            if (data.Misalign != null) MalpositionDatas = data.Misalign.ConvertAll(o => double.Parse(o.PValue ?? "0")).ToArray();
            if (data.TabSpecification != null)
            {
                var target = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳目标值")?.PValue ?? "0");
                var upper = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.TabSpecification.FirstOrDefault(o => o.PName == "中间极耳下限公差")?.PValue ?? "0");
                BatteryTabLimitDown = target + lower;
                BatteryTabLimitUp = target + upper;
            }

            if (data.CilSpecification != null)
            {
                var target = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷目标值")?.PValue ?? "0");
                var upper = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.CilSpecification.FirstOrDefault(o => o.PName == "陶瓷下限公差")?.PValue ?? "0");
                CeramicLimitDown = target + lower;
                CeramicLimitUp = target + upper;
            }

            if (data.CoatingSpecification != null)
            {
                var target = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜目标值")?.PValue ?? "0");
                var upper = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.CoatingSpecification.FirstOrDefault(o => o.PName == "涂膜下限公差")?.PValue ?? "0");
                FilmLimitDown = target + lower;
                FilmLimitUp = target + upper;
            }

            if (data.MisalignSpecification != null)
            {
                var target = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位目标值")?.PValue ?? "0");
                var upper = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位上限公差")?.PValue ?? "0");
                var lower = double.Parse(data.MisalignSpecification.FirstOrDefault(o => o.PName == "错位下限公差")?.PValue ?? "0");
                MalpositionLimitDown = target + lower;
                MalpositionLimitUp = target + upper;
            }
        }
    }
}
