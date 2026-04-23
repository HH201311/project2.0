using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;
using System.Windows.Media.Media3D;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class ProductionInfoModel : ModelBase
    {
        private string? cellModel;
        private string? filmRollNumber;
        private double netWeightSpecification_A;
        private double netWeightSpecificationLimit_A;
        private double netWeightSpecification_B;
        private double netWeightSpecificationLimit_B;
        private double ccdSpecification_MembraneZone;
        private double ccdSpecificationLimit_MembraneZone;
        private double ccdSpecification_BatteryTabSide;
        private double ccdSpecificationLimit_BatteryTabSide;
        private double ccdSpecification_BatteryTabMiddle;
        private double ccdSpecificationLimit_BatteryTabMiddle;
        private double ccdSpecification_Ceramic;
        private double ccdSpecificationLimit_Ceramic;
        private double ccdSpecificationLimit_Mismatch;

        /// <summary>
        /// 生产型号
        /// </summary>
        public string? CellModel { get => cellModel; set => SetProperty(ref cellModel, value); }

        /// <summary>
        /// 膜卷号
        /// </summary>
        public string? FilmRollNumber { get => filmRollNumber; set => SetProperty(ref filmRollNumber, value); }

        #region 面密度净重规格
        /// <summary>
        /// 面密度净重规格-单面
        /// </summary>
        public double NetWeightSpecification_A { get => netWeightSpecification_A; set => SetProperty(ref netWeightSpecification_A, value); }

        /// <summary>
        /// 面密度净重规格-单面偏移量
        /// </summary>
        public double NetWeightSpecificationLimit_A { get => netWeightSpecificationLimit_A; set => SetProperty(ref netWeightSpecificationLimit_A, value); }

        /// <summary>
        /// 面密度净重规格-双面
        /// </summary>
        public double NetWeightSpecification_B { get => netWeightSpecification_B; set => SetProperty(ref netWeightSpecification_B, value); }

        /// <summary>
        /// 面密度净重规格-双面偏移量
        /// </summary>
        public double NetWeightSpecificationLimit_B { get => netWeightSpecificationLimit_B; set => SetProperty(ref netWeightSpecificationLimit_B, value); }
        #endregion

        #region 尺寸规格
        /// <summary>
        /// 尺寸规格-膜区
        /// </summary>
        public double CCDSpecification_MembraneZone { get => ccdSpecification_MembraneZone; set => SetProperty(ref ccdSpecification_MembraneZone, value); }

        /// <summary>
        /// 尺寸规格-膜区偏移量
        /// </summary>
        public double CCDSpecificationLimit_MembraneZone { get => ccdSpecificationLimit_MembraneZone; set => SetProperty(ref ccdSpecificationLimit_MembraneZone, value); }

        /// <summary>
        /// 尺寸规格-极耳(两边)
        /// </summary>
        public double CCDSpecification_BatteryTabSide { get => ccdSpecification_BatteryTabSide; set => SetProperty(ref ccdSpecification_BatteryTabSide, value); }

        /// <summary>
        /// 尺寸规格-极耳(两边)偏移量
        /// </summary>
        public double CCDSpecificationLimit_BatteryTabSide { get => ccdSpecificationLimit_BatteryTabSide; set => SetProperty(ref ccdSpecificationLimit_BatteryTabSide, value); }

        /// <summary>
        /// 尺寸规格-极耳(中间)
        /// </summary>
        public double CCDSpecification_BatteryTabMiddle { get => ccdSpecification_BatteryTabMiddle; set => SetProperty(ref ccdSpecification_BatteryTabMiddle, value); }

        /// <summary>
        /// 尺寸规格-极耳(中间)偏移量
        /// </summary>
        public double CCDSpecificationLimit_BatteryTabMiddle { get => ccdSpecificationLimit_BatteryTabMiddle; set => SetProperty(ref ccdSpecificationLimit_BatteryTabMiddle, value); }

        /// <summary>
        /// 尺寸规格-陶瓷
        /// </summary>
        public double CCDSpecification_Ceramic { get => ccdSpecification_Ceramic; set => SetProperty(ref ccdSpecification_Ceramic, value); }

        /// <summary>
        /// 尺寸规格-陶瓷偏移量
        /// </summary>
        public double CCDSpecificationLimit_Ceramic { get => ccdSpecificationLimit_Ceramic; set => SetProperty(ref ccdSpecificationLimit_Ceramic, value); }

        /// <summary>
        /// 尺寸规格-A/B错位偏移量
        /// </summary>
        public double CCDSpecificationLimit_Mismatch { get => ccdSpecificationLimit_Mismatch; set => SetProperty(ref ccdSpecificationLimit_Mismatch, value); }
        #endregion
    }
}
