using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 压花气压算法入参模型
    /// </summary>
    public class PressInputModel
    {
        /// <summary>
        /// 当前卷针的完美电芯极耳角度序列-正极
        /// </summary>
        public string PerfectPosAngles { get; set; } = "[]";

        /// <summary>
        /// 当前卷针的完美电芯极耳角度序列-负极
        /// </summary>
        public string PerfectNegAngles { get; set; } = "[]";

        /// <summary>
        /// 实时电芯极耳角度序列-正极
        /// </summary>
        public string CurrentPosAngles { get; set; } = "[]";

        /// <summary>
        /// 实时电芯极耳角度序列-负极
        /// </summary>
        public string CurrentNegAngles { get; set; } = "[]";

        /// <summary>
        /// 实时压花气压，地址SW_BJ_Data[210]已采集
        /// </summary>
        public float PressReal { get; set; }

        /// <summary>
        /// 压花上限，软件参数（新增）
        /// </summary>
        public float PressUp { get; set; }

        /// <summary>
        /// 压花下限，软件参数（新增）
        /// </summary>
        public float PressDown { get; set; }

        /// <summary>
        /// 正极耳层数，软件参数（已有）
        /// </summary>
        public int Nb { get; set; }

        /// <summary>
        /// 负极耳层数，软件参数（已有）
        /// </summary>
        public int Na { get; set; }

        /// <summary>
        /// 压花调节步长，软件参数（新增）
        /// </summary>
        public float PressStep { get; set; }

        /// <summary>
        /// 压花步长对应错位变化量，软件参数（新增）
        /// </summary>
        public float PressK { get; set; }

        /// <summary>
        /// 调节系数k值，软件参数（已有）
        /// </summary>
        public float K { get; set; }

        /// <summary>
        /// 当前卷针的完美电芯错位量-正极
        /// </summary>
        public float B120 { get; set; }

        /// <summary>
        /// 当前卷针的完美电芯错位量-负极
        /// </summary>
        public float B121 { get; set; }
    }
}
