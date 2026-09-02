using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System.IO;

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
        private string savePathOfWM = "D:\\通用上位机\\闭环数据";

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
        public string SavePathOfWM { get => savePathOfWM; set => SetProperty(ref savePathOfWM, value); }
        #endregion

        #region 涂布机:CoatingMachine       
        private bool isUseCollectFunctionOfPLCData;
        private byte collectFrequencyOfPLCData = 1;
        private string savePathOfCM = "D:\\通用上位机\\闭环数据";

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
        public string SavePathOfCM { get => savePathOfCM; set => SetProperty(ref savePathOfCM, value); }
        #endregion

        #region 模头:DiaHead
        private bool isUseCollectFunctionOfDHWeightData;
        private bool isUseCollectFunctionOfDHTBlockData;
        private byte collectFrequencyOfDHTBlockData = 7;
        private string savePathOfDH = "D:\\通用上位机\\闭环数据";

        /// <summary>
        /// 数据采集：模头面密度数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfDHWeightData { get => isUseCollectFunctionOfDHWeightData; set => SetProperty(ref isUseCollectFunctionOfDHWeightData, value); }

        /// <summary>
        /// 数据采集：T块数据采集功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfDHTBlockData { get => isUseCollectFunctionOfDHTBlockData; set => SetProperty(ref isUseCollectFunctionOfDHTBlockData, value); }

        /// <summary>
        /// 采集频率：T块数据采集频率（s/次）
        /// </summary>
        public byte CollectFrequencyOfDHTBlockData { get => collectFrequencyOfDHTBlockData; set => SetProperty(ref collectFrequencyOfDHTBlockData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string SavePathOfDH { get => savePathOfDH; set => SetProperty(ref savePathOfDH, value); }
        #endregion

        #region CCD:CCD
        private bool isUseCollectFunctionOfCCDData;
        private string savePathOfCCD = "D:\\通用上位机\\闭环数据";
        private bool dataTransferOfOpmmPLCDataEnable;
        private bool dataTransferOfOpmmCCDDataEnable;
        private bool dataTransferOfOpmmWeightDataEnable;

        /// <summary>
        /// 数据采集：涂布机PLC数采功能是否开启
        /// </summary>
        public bool IsUseCollectFunctionOfCCDData { get => isUseCollectFunctionOfCCDData; set => SetProperty(ref isUseCollectFunctionOfCCDData, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string SavePathOfCCD { get => savePathOfCCD; set => SetProperty(ref savePathOfCCD, value); }

        /// <summary>
        /// 数据中转（一人多机）：PLC数据中转至一人多机是否启用
        /// </summary>
        public bool DataTransferOfOpmmPLCDataEnable { get => dataTransferOfOpmmPLCDataEnable; set => SetProperty(ref dataTransferOfOpmmPLCDataEnable, value); }

        /// <summary>
        /// 数据中转（一人多机）：CCD数据中转至一人多机是否启用
        /// </summary>
        public bool DataTransferOfOpmmCCDDataEnable { get => dataTransferOfOpmmCCDDataEnable; set => SetProperty(ref dataTransferOfOpmmCCDDataEnable, value); }

        /// <summary>
        /// 数据中转（一人多机）：重量数据中转至一人多机是否启用
        /// </summary>
        public bool DataTransferOfOpmmWeightDataEnable { get => dataTransferOfOpmmWeightDataEnable; set => SetProperty(ref dataTransferOfOpmmWeightDataEnable, value); }
        #endregion

        #region 闭环:ClosedLoop
        private EDataSourceOfClosedLoop coatingDataSource = EDataSourceOfClosedLoop.WMSocketClient;
        private EDataSourceOfClosedLoop tBlockDataSource = EDataSourceOfClosedLoop.DHSocketServer;
        private string savePathOfCL = "D:\\通用上位机\\闭环数据";

        private bool isUseReminderOfClosedLoopSwitch;
        private bool isUseJudgeOfKnife;
        private bool isUseFTPUpload;
        private EModeOfPumpAdjust modeOfPumpAdjust;
        private ERollNumReading modeOfRollNumReading;
        private string pathOfRollNumReadingTxt = @"\\DESKTOP-986B1JE\MESClientData\MESClientData.txt";
        private bool isClosedLoopAutoStart;

        /// <summary>
        /// 闭环入参数据选择：面密度数据类型
        /// </summary>
        public EDataSourceOfClosedLoop CoatingDataSource { get => coatingDataSource; set => SetProperty(ref coatingDataSource, value); }

        /// <summary>
        /// 闭环入参数据选择：T块数据类型
        /// </summary>
        public EDataSourceOfClosedLoop TBlockDataSource { get => tBlockDataSource; set => SetProperty(ref tBlockDataSource, value); }

        /// <summary>
        /// 数据保存：保存路径
        /// </summary>
        public string SavePathOfCL { get => savePathOfCL; set => SetProperty(ref savePathOfCL, value); }

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
        public string PathOfRollNumReadingTxt { get => pathOfRollNumReadingTxt; set => SetProperty(ref pathOfRollNumReadingTxt, value); }

        /// <summary>
        /// 其他：闭环自动启动
        /// </summary>
        public bool IsClosedLoopAutoStart { get => isClosedLoopAutoStart; set => SetProperty(ref isClosedLoopAutoStart, value); }
        #endregion

        #region 辅助方法
        #region 面密度路径
        /// <summary>
        /// 获取面密度数据保存文件夹路径
        /// </summary>
        /// <returns></returns>
        public string GetFullPathOfCoatingData()
        {
            var folderName = GetFolderNameOfCoatingData(CoatingDataSource);
            return CoatingDataSource switch
            {
                EDataSourceOfClosedLoop.WMSocketClient => Path.Combine(SavePathOfWM, folderName),
                EDataSourceOfClosedLoop.WMSocketServer => Path.Combine(SavePathOfWM, folderName),
                EDataSourceOfClosedLoop.DHSocketServer => Path.Combine(SavePathOfDH, folderName),
                EDataSourceOfClosedLoop.DHMqttClient => Path.Combine(SavePathOfDH, folderName),
                _ => Path.Combine(SavePathOfCL, folderName)
            };
        }

        /// <summary>
        /// 获取面密度数据保存文件夹名称
        /// </summary>
        /// <param name="dataSource">数据源类型-关联设备</param>
        /// <returns></returns>
        private string GetFolderNameOfCoatingData(EDataSourceOfClosedLoop dataSource)
        {
            return dataSource switch
            {
                EDataSourceOfClosedLoop.WMSocketClient => "测重仪客户端面密度采集数据",
                EDataSourceOfClosedLoop.WMSocketServer => "测重仪服务端面密度采集数据",
                EDataSourceOfClosedLoop.DHSocketServer => "模头服务端面密度采集数据",
                EDataSourceOfClosedLoop.DHMqttClient => "Matt客户端面密度采集数据",
                _ => $"{dataSource}面密度采集数据"
            };
        }
        #endregion

        #region T块路径
        /// <summary>
        /// 获取T块数据保存文件夹路径
        /// </summary>
        /// <returns></returns>
        public string GetFullPathOfTBlockData()
        {
            var folderName = GetFolderNameOfTBlockData(TBlockDataSource);
            return TBlockDataSource switch
            {
                EDataSourceOfClosedLoop.WMSocketClient => Path.Combine(SavePathOfWM, folderName),
                EDataSourceOfClosedLoop.WMSocketServer => Path.Combine(SavePathOfWM, folderName),
                EDataSourceOfClosedLoop.DHSocketServer => Path.Combine(SavePathOfDH, folderName),
                EDataSourceOfClosedLoop.DHMqttClient => Path.Combine(SavePathOfDH, folderName),
                _ => Path.Combine(SavePathOfCL, folderName)
            };
        }

        /// <summary>
        /// 获取T块数据保存文件夹名称
        /// </summary>
        /// <param name="dataSource">数据源类型-关联设备</param>
        /// <returns></returns>
        private string GetFolderNameOfTBlockData(EDataSourceOfClosedLoop dataSource)
        {
            return dataSource switch
            {
                EDataSourceOfClosedLoop.WMSocketServer => "测重仪服务端T块采集数据",
                EDataSourceOfClosedLoop.DHMqttClient => "Matt客户端T块采集数据",
                _ => $"{dataSource}T块采集数据"
            };
        }
        #endregion
        #endregion
    }
}
