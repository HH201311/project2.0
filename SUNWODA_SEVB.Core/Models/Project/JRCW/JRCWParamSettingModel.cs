using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW
{
    /// <summary>
    /// 缺陷参数模型（防栈溢出+双向绑定）
    /// </summary>
    public class JRCWParamSettingModel : ModelBase
    {
        private int nb;
        private int na;
        private double p_θb1;
        private double p_θbn;
        private double p_θa1;
        private double p_θan;
        private double b120;
        private double b121;
        private int s_Nb1;
        private int s_Nbn;
        private int s_Na1;
        private int s_Nan;
        private int t4;
        private int u_limit;
        private double k;
        private double cb1;
        private double cbn;
        private double ca1;
        private double can;
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
        /// 完美电芯的首极耳角度-正极
        /// </summary>
        [Description("德阳专用")]
        public double P_θb1 { get => p_θb1; set => SetProperty(ref p_θb1, value); }

        /// <summary>
        /// 完美电芯的尾极耳角度-正极
        /// </summary>
        [Description("德阳专用")]
        public double P_θbn { get => p_θbn; set => SetProperty(ref p_θbn, value); }

        /// <summary>
        /// 完美电芯的首极耳角度-负极
        /// </summary>
        [Description("德阳专用")]
        public double P_θa1 { get => p_θa1; set => SetProperty(ref p_θa1, value); }

        /// <summary>
        /// 完美电芯的尾极耳角度-负极
        /// </summary>
        [Description("德阳专用")]
        public double P_θan { get => p_θan; set => SetProperty(ref p_θan, value); }

        /// <summary>
        /// 完美电芯错位量-正极
        /// </summary>
        [Description("德阳专用")]
        public double B120 { get => b120; set => SetProperty(ref b120, value); }

        /// <summary>
        /// 完美电芯错位量-负极
        /// </summary>
        [Description("德阳专用")]
        public double B121 { get => b121; set => SetProperty(ref b121, value); }

        /// <summary>
        /// 首极耳第几层-正极
        /// </summary>
        [Description("德阳专用")]

        public int S_Nb1 { get => s_Nb1; set => SetProperty(ref s_Nb1, value); }

        /// <summary>
        /// 尾极耳第几层-正极
        /// </summary>
        [Description("德阳专用")]
        public int S_Nbn { get => s_Nbn; set => SetProperty(ref s_Nbn, value); }

        /// <summary>
        /// 首极耳第几层-负极
        /// </summary>
        [Description("德阳专用")]
        public int S_Na1 { get => s_Na1; set => SetProperty(ref s_Na1, value); }

        /// <summary>
        /// 尾极耳第几层-负极
        /// </summary>
        [Description("德阳专用")]
        public int S_Nan { get => s_Nan; set => SetProperty(ref s_Nan, value); }

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
        /// 稳定首极耳圈周长-正极
        /// </summary>
        [Description("德阳专用")]
        public double Cb1 { get => cb1; set => SetProperty(ref cb1, value); }

        /// <summary>
        /// 稳定尾极耳圈周长-正极
        /// </summary>
        [Description("德阳专用")]
        public double Cbn { get => cbn; set => SetProperty(ref cbn, value); }

        /// <summary>
        /// 稳定首极耳圈周长-负极
        /// </summary>
        [Description("德阳专用")]
        public double Ca1 { get => ca1; set => SetProperty(ref ca1, value); }

        /// <summary>
        /// 稳定尾极耳圈周长-负极
        /// </summary>
        [Description("德阳专用")]
        public double Can { get => can; set => SetProperty(ref can, value); }

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
        [Description("义乌专用")]
        public double P_Switch { get => p_switch; set => SetProperty(ref p_switch, value); }

        /// <summary>
        /// 压花调节系数
        /// </summary>
        [Description("义乌专用")]
        public double P_t { get => p_t; set => SetProperty(ref p_t, value); }

        /// <summary>
        /// 压花调节精度
        /// </summary>
        [Description("义乌专用")]
        public int P_j_d { get => p_j_d; set => SetProperty(ref p_j_d, value); }
    }
}
