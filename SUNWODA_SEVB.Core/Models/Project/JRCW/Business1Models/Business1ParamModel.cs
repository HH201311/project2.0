using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models
{
    /// <summary>
    /// 缺陷参数模型（防栈溢出+双向绑定）
    /// </summary>
    public class Business1ParamModel : ModelBase
    {
        private int nb;
        private int na;
        private int t4;
        private int u_limit;
        private double k;
        private int t1;
        private int t2;
        private int t3;
        private int j_d;
        private double p_switch;
        private double p_t;
        private int p_j_d;

        /// <summary>
        /// 正极极耳层数
        /// </summary>
        public int Nb { get => nb; set => SetProperty(ref nb, value); }

        /// <summary>
        /// 负极极耳层数
        /// </summary>
        public int Na { get => na; set => SetProperty(ref na, value); }

        /// <summary>
        /// 错位异常偏大阈值
        /// </summary>
        public int T4 { get => t4; set => SetProperty(ref t4, value); }

        /// <summary>
        /// 单次调节量上限
        /// </summary>
        public int U_Limit { get => u_limit; set => SetProperty(ref u_limit, value); }

        /// <summary>
        /// 0.1变径与周长对应变化关系
        /// </summary>
        public double K { get => k; set => SetProperty(ref k, value); }

        /// <summary>
        /// 闭环判定调节阈值t1
        /// </summary>
        public int T1 { get => t1; set => SetProperty(ref t1, value); }

        /// <summary>
        /// 闭环判定超规格阈值t2
        /// </summary>
        public int T2 { get => t2; set => SetProperty(ref t2, value); }

        /// <summary>
        /// 闭环判定调节阈值t3
        /// </summary>
        public int T3 { get => t3; set => SetProperty(ref t3, value); }

        /// <summary>
        /// 变径调节精度
        /// </summary>
        public int J_d { get => j_d; set => SetProperty(ref j_d, value); }

        /// <summary>
        /// 压花调节阈值
        /// </summary>
        public double P_Switch { get => p_switch; set => SetProperty(ref p_switch, value); }

        /// <summary>
        /// 压花调节系数
        /// </summary>
        public double P_t { get => p_t; set => SetProperty(ref p_t, value); }

        /// <summary>
        /// 压花调节精度
        /// </summary>
        public int P_j_d { get => p_j_d; set => SetProperty(ref p_j_d, value); }
    }
}
