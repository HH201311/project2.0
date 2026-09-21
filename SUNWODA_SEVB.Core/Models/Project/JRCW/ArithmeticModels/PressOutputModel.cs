using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 压花气压算法出参模型
    /// </summary>
    public class PressOutputModel
    {
        /// <summary>
        /// 1号针错位调节量，需要保存
        /// </summary>
        public float J1 { get; set; }

        /// <summary>
        /// 2号针错位调节量，需要保存
        /// </summary>
        public float J2 { get; set; }

        /// <summary>
        /// 3号针错位调节量，需要保存，若J1!=0 or J2!=0 or J3!=0，下发调节量并弹窗输出dialog
        /// </summary>
        public float J3 { get; set; }

        /// <summary>
        /// 压花调节量，需要保存并下发
        /// </summary>
        public float Press_J { get; set; }

        /// <summary>
        /// 判定结果，需要保存
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// 调节提示，弹窗输出
        /// </summary>
        public string? Dialog { get; set; }

        /// <summary>
        /// 接收后缓存，下次调用算法时作为参数17传入
        /// </summary>
        public int J_Signal { get; set; }

        /// <summary>
        /// 接收后缓存，下次调用算法时作为参数18传入
        /// </summary>
        public int Over_J_Num { get; set; }
    }
}
