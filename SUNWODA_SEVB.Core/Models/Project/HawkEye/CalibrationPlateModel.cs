using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.HawkEye
{
    /// <summary>
    /// 相机标定模型
    /// </summary>
    [Serializable]
    public class CalibrationPlateModel : ModelBase
    {
        private int proceedNumber = 0;
        private int listNumber = 0;
        private double checkerboardSize = 0;
        private double pixelPrecision = 0;

        /// <summary>
        /// 行角点数
        /// </summary>
        [XmlAttribute("行角点数")]
        public int ProceedNumber
        {
            get => proceedNumber;
            set => SetProperty(ref proceedNumber, value);
        }

        /// <summary>
        /// 列角点数
        /// </summary>
        [XmlAttribute("列角点数")]
        public int ListNumber
        {
            get => listNumber;
            set => SetProperty(ref listNumber, value);
        }

        /// <summary>
        /// 棋盘格单元尺寸
        /// </summary>
        [XmlAttribute("棋盘格单元尺寸")]
        public double CheckerboardSize
        {
            get => checkerboardSize;
            set => SetProperty(ref checkerboardSize, value);
        }

        /// <summary>
        /// 像素精度
        /// </summary>
        [XmlAttribute("像素精度")]
        public double PixelPrecision
        {
            get => pixelPrecision;
            set => SetProperty(ref pixelPrecision, value);
        }
    }
}
