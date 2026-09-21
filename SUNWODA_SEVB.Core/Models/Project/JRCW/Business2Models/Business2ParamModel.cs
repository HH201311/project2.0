using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models
{
    /// <summary>
    /// 缺陷参数模型（防栈溢出+双向绑定）
    /// </summary>
    public class Business2ParamModel : ModelBase
    {
        private ENeedleType needleType;
        private int tabCount, t4, u_limit;
        private float k;
        private PerfectBatteryInfo perfectBattery1 = new(), perfectBattery2 = new(), perfectBattery3 = new();
        private int nb, na, s_Nb1, s_Nbn, s_Na1, s_Nan;
        private float cb1, cbn, ca1, can;
        private int t1, t2, t3, j_d;
        private float ccdErrT = 10, pressT, pressUp, pressDown, pressStep = 0.005f, pressK = -2.25f;
        private byte pressJT = 1;

        #region 参数设置
        /// <summary>
        /// 卷针类型
        /// </summary>
        public ENeedleType NeedleType { get => needleType; set => SetProperty(ref needleType, value); }

        /// <summary>
        /// 数采触发极耳层数
        /// </summary>
        public int TabCount { get => tabCount; set => SetProperty(ref tabCount, value); }

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
        public float K { get => k; set => SetProperty(ref k, value); }
        #endregion

        #region 极耳层数
        /// <summary>
        /// 正极极耳层数
        /// </summary>
        public int Nb { get => nb; set => SetProperty(ref nb, value); }

        /// <summary>
        /// 负极极耳层数
        /// </summary>
        public int Na { get => na; set => SetProperty(ref na, value); }

        /// <summary>
        /// 首极耳第几层-正极
        /// </summary>
        public int S_Nb1 { get => s_Nb1; set => SetProperty(ref s_Nb1, value); }

        /// <summary>
        /// 尾极耳第几层-正极
        /// </summary>
        public int S_Nbn { get => s_Nbn; set => SetProperty(ref s_Nbn, value); }

        /// <summary>
        /// 首极耳第几层-负极
        /// </summary>
        public int S_Na1 { get => s_Na1; set => SetProperty(ref s_Na1, value); }

        /// <summary>
        /// 尾极耳第几层-负极
        /// </summary>
        public int S_Nan { get => s_Nan; set => SetProperty(ref s_Nan, value); }
        #endregion

        #region 完美电芯配置
        /// <summary>
        /// 完美电芯-卷针1
        /// </summary>
        public PerfectBatteryInfo PerfectBattery1 { get => perfectBattery1; set => SetProperty(ref perfectBattery1, value); }

        /// <summary>
        /// 完美电芯-卷针2
        /// </summary>
        public PerfectBatteryInfo PerfectBattery2 { get => perfectBattery2; set => SetProperty(ref perfectBattery2, value); }

        /// <summary>
        /// 完美电芯-卷针3
        /// </summary>
        public PerfectBatteryInfo PerfectBattery3 { get => perfectBattery3; set => SetProperty(ref perfectBattery3, value); }
        #endregion

        #region 极耳圈周长
        /// <summary>
        /// 稳定首极耳圈周长-正极
        /// </summary>
        public float Cb1 { get => cb1; set => SetProperty(ref cb1, value); }

        /// <summary>
        /// 稳定尾极耳圈周长-正极
        /// </summary>
        public float Cbn { get => cbn; set => SetProperty(ref cbn, value); }

        /// <summary>
        /// 稳定首极耳圈周长-负极
        /// </summary>
        public float Ca1 { get => ca1; set => SetProperty(ref ca1, value); }

        /// <summary>
        /// 稳定尾极耳圈周长-负极
        /// </summary>
        public float Can { get => can; set => SetProperty(ref can, value); }
        #endregion

        #region 变径参数设置
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
        /// 压花调节步长（默认值0.005）
        /// </summary>
        public float CCDErrT { get => ccdErrT; set => SetProperty(ref ccdErrT, value); }
        #endregion

        #region 压花参数设置
        /// <summary>
        /// 错位稳定阈值
        /// </summary>
        public float PressT { get => pressT; set => SetProperty(ref pressT, value); }

        /// <summary>
        /// 压花上限
        /// </summary>
        public float PressUp { get => pressUp; set => SetProperty(ref pressUp, value); }

        /// <summary>
        /// 压花下限
        /// </summary>
        public float PressDown { get => pressDown; set => SetProperty(ref pressDown, value); }

        /// <summary>
        /// 压花调节步长（默认值0.005）
        /// </summary>
        public float PressStep { get => pressStep; set => SetProperty(ref pressStep, value); }

        /// <summary>
        /// 压花步长对应错位变化量（默认值-2.25）
        /// </summary>
        public float PressK { get => pressK; set => SetProperty(ref pressK, value); }

        /// <summary>
        /// 压花连续调节次数（默认值1）
        /// </summary>
        public byte PressJT { get => pressJT; set => SetProperty(ref pressJT, value); }
        #endregion
    }

    /// <summary>
    /// 完美电芯信息
    /// </summary>
    public class PerfectBatteryInfo : ModelBase
    {
        private string? batteryCode;
        private List<float> p_θb_List = [], p_θa_List = [];
        private float p_θb1, p_θbn, p_θa1, p_θan, b120, b121;

        /// <summary>
        /// 完美电芯条码
        /// </summary>
        public string? BatteryCode { get => batteryCode; set => SetProperty(ref batteryCode, value); }

        /// <summary>
        /// 完美电芯的极耳角度-正极
        /// </summary>
        public List<float> P_θb_List { get => p_θb_List; set => SetProperty(ref p_θb_List, value); }

        /// <summary>
        /// 完美电芯的极耳角度-负极
        /// </summary>
        public List<float> P_θa_List { get => p_θa_List; set => SetProperty(ref p_θa_List, value); }

        /// <summary>
        /// 完美电芯的首极耳角度-正极
        /// </summary>
        public float P_θb1 { get => p_θb1; set => SetProperty(ref p_θb1, value); }

        /// <summary>
        /// 完美电芯的尾极耳角度-正极
        /// </summary>
        public float P_θbn { get => p_θbn; set => SetProperty(ref p_θbn, value); }

        /// <summary>
        /// 完美电芯的首极耳角度-负极
        /// </summary>
        public float P_θa1 { get => p_θa1; set => SetProperty(ref p_θa1, value); }

        /// <summary>
        /// 完美电芯的尾极耳角度-负极
        /// </summary>
        public float P_θan { get => p_θan; set => SetProperty(ref p_θan, value); }

        /// <summary>
        /// 完美电芯错位量-正极
        /// </summary>
        public float B120 { get => b120; set => SetProperty(ref b120, value); }

        /// <summary>
        /// 完美电芯错位量-负极
        /// </summary>
        public float B121 { get => b121; set => SetProperty(ref b121, value); }
    }
}
