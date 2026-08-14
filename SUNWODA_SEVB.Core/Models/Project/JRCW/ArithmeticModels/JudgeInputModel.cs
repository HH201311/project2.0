using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.ArithmeticModels
{
    /// <summary>
    /// 闭环算法Judge入参模型
    /// </summary>
    public class JudgeInputModel
    {
        /// <summary>
        /// 阈值t1
        /// </summary>
        public float T1 { get; set; }

        /// <summary>
        /// 阈值t2
        /// </summary>
        public float T2 { get; set; }

        /// <summary>
        /// 阈值t3
        /// </summary>
        public float T3 { get; set; }

        /// <summary>
        /// 当前时刻卷针号
        /// </summary>
        public string? N { get; set; }

        /// <summary>
        /// 近期正极错位数组，最大长度为3
        /// </summary>
        public float[]? Queue_P { get; set; }

        /// <summary>
        /// 近期负极错位数组，最大长度为3
        /// </summary>
        public float[]? Queue_N { get; set; }

        /// <summary>
        /// 近期1号针正极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue1_P { get; set; }

        /// <summary>
        /// 近期1号针负极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue1_N { get; set; }

        /// <summary>
        /// 近期2号针正极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue2_P { get; set; }

        /// <summary>
        /// 近期2号针负极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue2_N { get; set; }

        /// <summary>
        /// 近期3号针正极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue3_P { get; set; }

        /// <summary>
        /// 近期3号针负极错位数组，最大长度为6
        /// </summary>
        public float[]? Queue3_N { get; set; }

        /// <summary>
        /// 是否换卷or接带信号
        /// </summary>
        public int Change { get; set; }

        /// <summary>
        /// 调节系数k*（正极层数+负极层数）/2
        /// </summary>
        public double T { get; set; }

        /// <summary>
        /// 单次调节上限
        /// </summary>
        public int U_Limit { get; set; }

        /// <summary>
        /// 变径调节精度
        /// </summary>
        public double J_d { get; set; }

        /// <summary>
        /// 两针差异调节记号
        /// </summary>
        public int J_Signal { get; set; }

        /// <summary>
        /// 超规格调节记号
        /// </summary>
        public int Over_J_Num { get; set; }
    }
}
