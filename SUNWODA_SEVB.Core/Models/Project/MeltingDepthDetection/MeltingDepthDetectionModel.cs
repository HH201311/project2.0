using System.Windows;
using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.MeltingDepthDetection
{
    /// <summary>
    /// 激光焊接参数模型
    /// </summary>
    public class MeltingDepthDetectionModel : ModelBase
    {
        private bool isOk;        

        /// <summary>
        /// 序号
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// 时间/日期
        /// </summary>
        public DateTime TimeDate { get; set; }

        /// <summary>
        /// PACK码
        /// </summary>
        public string? PackCode { get; set; }

        /// <summary>
        /// 离焦量
        /// </summary>
        public double DefocusAmount { get; set; }

        /// <summary>
        /// 保护气流量
        /// </summary>
        public double ProtectiveGasFlow { get; set; }

        /// <summary>
        /// 焊接能量平均值
        /// </summary>
        public double WeldingEnergyAverage { get; set; }

        /// <summary>
        ///  焊接能量最大值
        /// </summary>
        public double WeldingEnergyMax { get; set; }

        /// <summary>
        /// 焊接能量最小值
        /// </summary>
        public double WeldingEnergyMin { get; set; }

        /// <summary>
        /// 焊接速度
        /// </summary>
        public double WeldingSpeed { get; set; }

        /// <summary>
        /// 焊接拉压力值
        /// </summary>
        public double WeldingTensionPressure { get; set; }

        /// <summary>
        /// 第一摆动幅度
        /// </summary>
        public double FirstOscillationAmplitude { get; set; }

        /// <summary>
        /// 第一摆动频率
        /// </summary>
        public double FirstOscillationFrequency { get; set; }

        /// <summary>
        /// 第二摆动幅度
        /// </summary>
        public double SecondOscillationAmplitude { get; set; }

        /// <summary>
        /// 熔深值
        /// </summary>
        public double DepthValue { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public bool IsOk
        {
            get => isOk;         
            set => SetProperty(ref isOk, value);
        }               

        #region 惠州基地专属属性

        /// <summary>
        /// 焊接点位（当前焊点序号）
        /// </summary>
        public int WeldingPoint { get; set; }

        /// <summary>
        /// 中心光输出功率
        /// </summary>
        public double CenterOpticalPower { get; set; }

        /// <summary>
        /// 铜嘴压力
        /// </summary>
        public double CopperNozzlePressure { get; set; }

        /// <summary>
        /// 除尘风速
        /// </summary>
        public double DustRemovalAirVelocity { get; set; }

        /// <summary>
        /// 环形光输出功率
        /// </summary>
        public double RingOpticalPower { get; set; }

        /// <summary>
        /// 总功率（中心 + 环形）
        /// </summary>
        public double TotalPower { get; set; }

        #endregion
    }
}
