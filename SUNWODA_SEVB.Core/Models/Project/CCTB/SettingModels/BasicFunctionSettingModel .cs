using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// 基础功能参数模型类
    /// </summary>
    public class BasicFunctionSettingModel : ModelBase
    {
        #region 测重仪:WeightMeasure
        private bool isUseCollectFunctionOfWMWeightData;
        private bool isUseMillimeterData;
        private bool isUseDryFilmTotalDataOfPartitioned;
        private bool isUseDryFilmNetDataOfPartitioned;
        private byte collectFrequencyOfWeightData = 1;
        private string? savePathOfWM = "D:\\通用上位机\\闭环数据";

        /// <summary>
        /// 数据采集：测重仪数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfWMWeightData { get => isUseCollectFunctionOfWMWeightData; set => SetProperty(ref isUseCollectFunctionOfWMWeightData, value); }

        /// <summary>
        /// 数据类型选择：毫米数据
        /// </summary>
        public bool IsUseMillimeterData { get => isUseMillimeterData; set => SetProperty(ref isUseMillimeterData, value); }

        /// <summary>
        /// 数据类型选择：分区干膜总重数据
        /// </summary>
        public bool IsUseDryFilmTotalDataOfPartitioned { get => isUseDryFilmTotalDataOfPartitioned; set => SetProperty(ref isUseDryFilmTotalDataOfPartitioned, value); }

        /// <summary>
        /// 数据类型选择：分区干膜净重数据
        /// </summary>
        public bool IsUseDryFilmNetDataOfPartitioned { get => isUseDryFilmNetDataOfPartitioned; set => SetProperty(ref isUseDryFilmNetDataOfPartitioned, value); }

        /// <summary>
        /// 采集频率：面密度数据采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfWeightData { get => collectFrequencyOfWeightData; set => SetProperty(ref collectFrequencyOfWeightData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string? SavePathOfWM { get => savePathOfWM; set => SetProperty(ref savePathOfWM, value); }
        #endregion

        #region 涂布机:CoatingMachine       
        private bool isUseCollectFunctionOfPLCData;
        private byte collectFrequencyOfPLCData = 1;
        private string? savePathOfCM = "D:\\通用上位机\\闭环数据";

        /// <summary>
        /// 数据采集：涂布机PLC数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfPLCData { get => isUseCollectFunctionOfPLCData; set => SetProperty(ref isUseCollectFunctionOfPLCData, value); }

        /// <summary>
        /// 采集频率：PLC采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfPLCData { get => collectFrequencyOfPLCData; set => SetProperty(ref collectFrequencyOfPLCData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string? SavePathOfCM { get => savePathOfCM; set => SetProperty(ref savePathOfCM, value); }
        #endregion

        #region 模头:DiaHead
        private bool isUseCollectFunctionOfDHWeightData;
        private bool isUseCollectFunctionOfDHTBlockData;
        private byte collectFrequencyOfDHWeightData = 1;
        private byte collectFrequencyOfDHTBlockData = 7;
        private string? savePathOfDH = "D:\\通用上位机\\闭环数据";

        /// <summary>
        /// 数据采集：模头面密度数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfDHWeightData { get => isUseCollectFunctionOfDHWeightData; set => SetProperty(ref isUseCollectFunctionOfDHWeightData, value); }

        /// <summary>
        /// 数据采集：T块数据采集功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfDHTBlockData { get => isUseCollectFunctionOfDHTBlockData; set => SetProperty(ref isUseCollectFunctionOfDHTBlockData, value); }

        /// <summary>
        /// 采集频率：模头面密度采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfDHWeightData { get => collectFrequencyOfDHWeightData; set => SetProperty(ref collectFrequencyOfDHWeightData, value); }

        /// <summary>
        /// 采集频率：T块数据采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfDHTBlockData { get => collectFrequencyOfDHTBlockData; set => SetProperty(ref collectFrequencyOfDHTBlockData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string? SavePathOfDH { get => savePathOfDH; set => SetProperty(ref savePathOfDH, value); }
        #endregion

        #region CCD:CCD
        private bool isUseCollectFunctionOfCCDData;
        private byte collectFrequencyOfCCDData = 1;
        private string? savePathOfCCD = "D:\\通用上位机\\闭环数据";

        /// <summary>
        /// 数据采集：涂布机PLC数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfCCDData { get => isUseCollectFunctionOfCCDData; set => SetProperty(ref isUseCollectFunctionOfCCDData, value); }

        /// <summary>
        /// 采集频率：CCD尺寸数据采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfCCDData { get => collectFrequencyOfCCDData; set => SetProperty(ref collectFrequencyOfCCDData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string? SavePathOfCCD { get => savePathOfCCD; set => SetProperty(ref savePathOfCCD, value); }
        #endregion

        #region 闭环:ClosedLoop
        private bool isUseArithmetic;
        private bool isUseWeightDataOfArithmeticParam;
        private bool isUsePLCDataOfArithmeticParam;
        private bool isUseCCDDataOfArithmeticParam;
        private EWeightDataTypeOfArithmeticParam weightDataTypeOfArithmeticParam;
        private byte exportFrequencyOfCL = 7;
        private string? savePathOfCL = "D:\\通用上位机\\闭环数据";

        private bool isUseReminderOfClosedLoopSwitch;
        private bool isUseJudgeOfKnife;
        private bool isUseFTPUpload;
        private EModeOfPumpAdjust modeOfPumpAdjust;
        private ERollNumReading modeOfRollNumReading;
        private string? pathOfRollNumReadingTxt = @"\\DESKTOP-986B1JE\MESClientData\MESClientData.txt";
        private bool isClosedLoopAutoStart;

        /// <summary>
        /// 算法调用：上位机是否调用算法
        /// </summary>
        public bool IsUseArithmetic { get => isUseArithmetic; set => SetProperty(ref isUseArithmetic, value); }

        /// <summary>
        /// 闭环入参数据选择：面密度数据
        /// </summary>
        public bool IsUseWeightDataOfArithmeticParam { get => isUseWeightDataOfArithmeticParam; set => SetProperty(ref isUseWeightDataOfArithmeticParam, value); }

        /// <summary>
        /// 闭环入参数据选择：涂布机PLC数据
        /// </summary>
        public bool IsUsePLCDataOfArithmeticParam { get => isUsePLCDataOfArithmeticParam; set => SetProperty(ref isUsePLCDataOfArithmeticParam, value); }

        /// <summary>
        /// 闭环入参数据选择：CCD尺寸数据
        /// </summary>
        public bool IsUseCCDDataOfArithmeticParam { get => isUseCCDDataOfArithmeticParam; set => SetProperty(ref isUseCCDDataOfArithmeticParam, value); }

        /// <summary>
        /// 闭环入参数据选择：面密度数据类型
        /// </summary>
        public EWeightDataTypeOfArithmeticParam WeightDataTypeOfArithmeticParam { get => weightDataTypeOfArithmeticParam; set => SetProperty(ref weightDataTypeOfArithmeticParam, value); }

        /// <summary>
        /// 数据输出频率：上位机输出数据频率（s/次）
        /// </summary>
        public byte ExportFrequencyOfCL { get => exportFrequencyOfCL; set => SetProperty(ref exportFrequencyOfCL, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string? SavePathOfCL { get => savePathOfCL; set => SetProperty(ref savePathOfCL, value); }

        /// <summary>
        /// 其他："涂布机闭环开关旋钮未开后"提醒功能
        /// </summary>
        public bool IsUseReminderOfClosedLoopSwitch { get => isUseReminderOfClosedLoopSwitch; set => SetProperty(ref isUseReminderOfClosedLoopSwitch, value); }

        /// <summary>
        /// 其他："是否开启进退刀判定"功能
        /// </summary>
        public bool IsUseJudgeOfKnife { get => isUseJudgeOfKnife; set => SetProperty(ref isUseJudgeOfKnife, value); }

        /// <summary>
        /// 其他："是否开启FTP上传"功能
        /// </summary>
        public bool IsUseFTPUpload { get => isUseFTPUpload; set => SetProperty(ref isUseFTPUpload, value); }

        /// <summary>
        /// 其他：泵速下发模式
        /// </summary>
        public EModeOfPumpAdjust ModeOfPumpAdjust { get => modeOfPumpAdjust; set => SetProperty(ref modeOfPumpAdjust, value); }

        /// <summary>
        /// 其他：膜卷号读取方式
        /// </summary>
        public ERollNumReading ModeOfRollNumReading { get => modeOfRollNumReading; set => SetProperty(ref modeOfRollNumReading, value); }

        /// <summary>
        /// 其他：膜卷号读取方式_txt路径
        /// </summary>
        public string? PathOfRollNumReadingTxt { get => pathOfRollNumReadingTxt; set => SetProperty(ref pathOfRollNumReadingTxt, value); }

        /// <summary>
        /// 其他：闭环自动启动
        /// </summary>
        public bool IsClosedLoopAutoStart { get => isClosedLoopAutoStart; set => SetProperty(ref isClosedLoopAutoStart, value); }
        #endregion
    }
}
