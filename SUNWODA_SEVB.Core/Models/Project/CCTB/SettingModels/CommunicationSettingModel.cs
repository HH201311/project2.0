using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SUNWODA_SEVB.Core.Interfaces.Project.CCTB.ICommunicationService;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// 通讯设置参数模型类
    /// </summary>
    public class CommunicationSettingModel : ModelBase
    {
        #region 测重仪:WeightMeasure
        private ECommunicationMode wmCommunicationMode = ECommunicationMode.客户端Socket;
        private bool wmSocketClientEnabled;
        private string? wmSocketClientIP = "127.0.0.1";
        private int wmSocketClientPort = 31415;
        private bool wmSocketServerEnabled;
        private int wmSocketServerPort = 6666;

        /// <summary>
        /// 通讯模式
        /// </summary>
        public ECommunicationMode WMCommunicationMode { get => wmCommunicationMode; set => SetProperty(ref wmCommunicationMode, value); }

        #region 客户端
        /// <summary>
        /// 是否启用客户端
        /// </summary>
        [Description("设备A")]
        public bool WMSocketClientEnabled { get => wmSocketClientEnabled; set => SetProperty(ref wmSocketClientEnabled, value); }

        /// <summary>
        /// 远程客户端IP
        /// </summary>
        public string? WMSocketClientIP { get => wmSocketClientIP; set => SetProperty(ref wmSocketClientIP, value); }

        /// <summary>
        /// 远程客户端Port
        /// </summary>
        public int WMSocketClientPort { get => wmSocketClientPort; set => SetProperty(ref wmSocketClientPort, value); }
        #endregion

        #region 服务端
        /// <summary>
        /// 是否启用服务器 
        /// </summary>
        public bool WMSocketServerEnabled { get => wmSocketServerEnabled; set => SetProperty(ref wmSocketServerEnabled, value); }

        /// <summary>
        /// 本地服务端Port
        /// </summary>
        public int WMSocketServerPort { get => wmSocketServerPort; set => SetProperty(ref wmSocketServerPort, value); }
        #endregion
        #endregion

        #region 涂布机:CoatingMachine       
        private string? cmPLCIP = "192.168.0.1";
        private int cmPLCPort = 9600;
        private string? cmBrandSpecificationProtocal = "Omron_Fins_TCP";
        private int cmCycleReadTime = 500;
        private int cmCycleWriteTime = 500;
        private bool cmIsEnable = false;

        /// <summary>
        /// 涂布机IP
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public string? CMPLCIP { get => cmPLCIP; set => SetProperty(ref cmPLCIP, value); }

        /// <summary>
        /// 端口
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public int CMPLCPort { get => cmPLCPort; set => SetProperty(ref cmPLCPort, value); }

        /// <summary>
        /// 品牌规格协议
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public string? CMBrandSpecificationProtocal { get => cmBrandSpecificationProtocal; set => SetProperty(ref cmBrandSpecificationProtocal, value); }

        /// <summary>
        /// 循环读取时间
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public int CMCycleReadTime { get => cmCycleReadTime; set => SetProperty(ref cmCycleReadTime, value); }

        /// <summary>
        /// 循环写入时间
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public int CMCycleWriteTime { get => cmCycleWriteTime; set => SetProperty(ref cmCycleWriteTime, value); }

        /// <summary>
        /// 是否启用
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public bool CMIsEnable { get => cmIsEnable; set => SetProperty(ref cmIsEnable, value); }
        #endregion

        #region 模头:DiaHead
        private ECommunicationMode dhCommunicationMode = ECommunicationMode.服务端Socket;
        private bool dhSocketServerEnabled;
        private int dhSocketServerPort = 5555;
        private bool dhASocketClientEnabled;
        private string? dhASocketClientIP = "192.168.1.30";
        private int dhASocketClientPort = 8989;
        private bool dhBSocketClientEnabled;
        private string? dhBSocketClientIP = "192.168.1.31";
        private int dhBSocketClientPort = 8989;
        private bool dhAMqttClientEnabled;
        private string? dhAMqttClientIP = "192.168.1.31";
        private int dhAMqttClientPort = 8989;
        private bool dhBMqttClientEnabled;
        private string? dhBMqttClientIP = "192.168.1.31";
        private int dhBMqttClientPort = 8989;

        /// <summary>
        /// 通讯模式
        /// </summary>
        public ECommunicationMode DHCommunicationMode { get => dhCommunicationMode; set => SetProperty(ref dhCommunicationMode, value); }

        #region 服务器
        /// <summary>
        /// 是否启用服务器 
        /// </summary>
        public bool DHSocketServerEnabled { get => dhSocketServerEnabled; set => SetProperty(ref dhSocketServerEnabled, value); }

        /// <summary>
        /// 本地服务端Port
        /// </summary>
        public int DHSocketServerPort { get => dhSocketServerPort; set => SetProperty(ref dhSocketServerPort, value); }
        #endregion

        #region 通讯模式：客户端
        #region 客户端：模头A
        /// <summary>
        /// 是否启用客户端：模头A
        /// </summary>
        public bool DHASocketClientEnabled { get => dhASocketClientEnabled; set => SetProperty(ref dhASocketClientEnabled, value); }

        /// <summary>
        /// 模头远程客户端IP：模头A
        /// </summary>
        public string? DHASocketClientIP { get => dhASocketClientIP; set => SetProperty(ref dhASocketClientIP, value); }

        /// <summary>
        /// 模头远程客户端Port：模头A
        /// </summary>
        public int DHASocketClientPort { get => dhASocketClientPort; set => SetProperty(ref dhASocketClientPort, value); }
        #endregion

        #region 客户端：模头B
        /// <summary>
        /// 是否启用客户端：模头B
        /// </summary>
        public bool DHBSocketClientEnabled { get => dhBSocketClientEnabled; set => SetProperty(ref dhBSocketClientEnabled, value); }

        /// <summary>
        /// 模头远程客户端IP：模头B
        /// </summary>
        public string? DHBSocketClientIP { get => dhBSocketClientIP; set => SetProperty(ref dhBSocketClientIP, value); }

        /// <summary>
        /// 模头远程客户端Port：模头B
        /// </summary>
        public int DHBSocketClientPort { get => dhBSocketClientPort; set => SetProperty(ref dhBSocketClientPort, value); }
        #endregion
        #endregion

        #region 通讯模式：Mqtt客户端
        #region Mqtt客户端：模头A
        /// <summary>
        /// 是否启用 Mqtt 客户端：模头A
        /// </summary>
        public bool DHAMqttClientEnabled { get => dhAMqttClientEnabled; set => SetProperty(ref dhAMqttClientEnabled, value); }

        /// <summary>
        /// 模头远程 Mqtt 客户端IP：模头A
        /// </summary>
        public string? DHAMqttClientIP { get => dhAMqttClientIP; set => SetProperty(ref dhAMqttClientIP, value); }

        /// <summary>
        /// 模头远程 Mqtt 客户端Port：模头A
        /// </summary>
        public int DHAMqttClientPort { get => dhAMqttClientPort; set => SetProperty(ref dhAMqttClientPort, value); }
        #endregion

        #region Mqtt客户端：模头B
        /// <summary>
        /// 是否启用 Mqtt 客户端：模头B
        /// </summary>
        public bool DHBMqttClientEnabled { get => dhBMqttClientEnabled; set => SetProperty(ref dhBMqttClientEnabled, value); }

        /// <summary>
        /// 模头远程 Mqtt 客户端IP：模头B
        /// </summary>
        public string? DHBMqttClientIP { get => dhBMqttClientIP; set => SetProperty(ref dhBMqttClientIP, value); }

        /// <summary>
        /// 模头远程 Mqtt 客户端Port：模头B
        /// </summary>
        public int DHBMqttClientPort { get => dhBMqttClientPort; set => SetProperty(ref dhBMqttClientPort, value); }
        #endregion
        #endregion
        #endregion

        #region CCD:CCD
        private bool ccdSocketServerEnabled;
        private int ccdSocketServerPort = 9001;
        private string? opmmSocketClientIP = "127.0.0.1";
        private bool opmmPLCSocketClientEnabled;
        private int opmmPLCSocketClientPort = 8092;
        private bool opmmCCDSocketClientEnabled;
        private int opmmCCDSocketClientPort = 8091;
        private bool opmmWeightSocketClientEnabled;
        private int opmmWeightSocketClientPort = 8090;

        #region 服务端
        /// <summary>
        /// 是否启用服务器 
        /// </summary>
        public bool CCDSocketServerEnabled { get => ccdSocketServerEnabled; set => SetProperty(ref ccdSocketServerEnabled, value); }

        /// <summary>
        /// 本地服务端Port：CCD
        /// </summary>
        public int CCDSocketServerPort { get => ccdSocketServerPort; set => SetProperty(ref ccdSocketServerPort, value); }
        #endregion

        #region 客户端
        /// <summary>
        /// CCD远程客户端IP
        /// </summary>
        public string? OPMMSocketClientIP { get => opmmSocketClientIP; set => SetProperty(ref opmmSocketClientIP, value); }

        /// <summary>
        /// 是否启用客户端：PLC数据
        /// </summary>
        public bool OPMMPLCSocketClientEnabled { get => opmmPLCSocketClientEnabled; set => SetProperty(ref opmmPLCSocketClientEnabled, value); }

        /// <summary>
        /// CCD远程客户端Port：PLC数据
        /// </summary>
        public int OPMMPLCSocketClientPort { get => opmmPLCSocketClientPort; set => SetProperty(ref opmmPLCSocketClientPort, value); }

        /// <summary>
        /// 是否启用客户端：CCD数据
        /// </summary>
        public bool OPMMCCDSocketClientEnabled { get => opmmCCDSocketClientEnabled; set => SetProperty(ref opmmCCDSocketClientEnabled, value); }

        /// <summary>
        /// CCD远程客户端Port：CCD数据
        /// </summary>
        public int OPMMCCDSocketClientPort { get => opmmCCDSocketClientPort; set => SetProperty(ref opmmCCDSocketClientPort, value); }

        /// <summary>
        /// 是否启用客户端：测厚仪数据
        /// </summary>
        public bool OPMMWeightSocketClientEnabled { get => opmmWeightSocketClientEnabled; set => SetProperty(ref opmmWeightSocketClientEnabled, value); }

        /// <summary>
        /// CCD远程客户端Port：测厚仪数据
        /// </summary>
        public int OPMMWeightSocketClientPort { get => opmmWeightSocketClientPort; set => SetProperty(ref opmmWeightSocketClientPort, value); }
        #endregion
        #endregion

        #region 各子模块变化检测方法
        /// <summary>
        /// 判断整个配置是否发生了变化
        /// </summary>
        public bool HasChanged(CommunicationSettingModel? other)
        {
            if (other == null) return true;
            return GetChangedModules(other).Count > 0;
        }

        /// <summary>
        /// 获取发生变更的模块列表
        /// </summary>
        /// <param name="other">旧的配置</param>
        /// <returns>变更的模块名称列表</returns>
        public List<string> GetChangedModules(CommunicationSettingModel? other)
        {
            var changedModules = new List<string>();

            // 如果旧配置为空，返回所有已启用的模块
            if (other == null) return GetAllEnabledModules();

            // 1. 测重仪 (WM) 模块
            if (IsWMSocketServerChanged(other))
                changedModules.Add(ICommunicationService.WMSocketServerDeviceName);

            if (IsWMSocketClientChanged(other))
                changedModules.Add(ICommunicationService.WMSocketClientDeviceName);

            // 2. 模头 (DH) 模块
            if (DHCommunicationMode != other.DHCommunicationMode)
            {
                changedModules.Add(ICommunicationService.DHSocketServerDeviceName);
                changedModules.Add(ICommunicationService.DHASocketClientDeviceName);
                changedModules.Add(ICommunicationService.DHBSocketClientDeviceName);
                changedModules.Add(ICommunicationService.DHAMqttClientDeviceName);
                changedModules.Add(ICommunicationService.DHBMqttClientDeviceName);
            }
            else
            {
                // 同模式下，检查具体参数变化
                if (IsDHSocketServerChanged(other))
                    changedModules.Add(ICommunicationService.DHSocketServerDeviceName);

                if (IsDHASocketClientChanged(other))
                    changedModules.Add(ICommunicationService.DHASocketClientDeviceName);

                if (IsDHBSocketClientChanged(other))
                    changedModules.Add(ICommunicationService.DHBSocketClientDeviceName);

                if (IsDHAMqttClientChanged(other))
                    changedModules.Add(ICommunicationService.DHAMqttClientDeviceName);

                if (IsDHBMqttClientChanged(other))
                    changedModules.Add(DHBMqttClientDeviceName);
            }

            // 3. CCD 模块
            if (IsCCDSocketServerChanged(other))
                changedModules.Add(ICommunicationService.CCDSocketServerDeviceName);

            // 4. OPMM 模块
            if (IsOPMMPLCSocketClientChanged(other))
                changedModules.Add(ICommunicationService.OPMMPLCSocketClientDeviceName);
            if (IsOPMMCCDSocketClientChanged(other))
                changedModules.Add(ICommunicationService.OPMMCCDSocketClientDeviceName);
            if (IsOPMMWeightSocketClientChanged(other))
                changedModules.Add(ICommunicationService.OPMMWeightSocketClientDeviceName);

            return changedModules.Distinct().ToList();
        }

        /// <summary>
        /// 获取所有已启用的模块（用于首次启动或配置为空时）
        /// </summary>
        public List<string> GetAllEnabledModules()
        {
            var modules = new List<string>();

            // WM 模块
            if (WMCommunicationMode == ECommunicationMode.服务端Socket && WMSocketServerEnabled)
                modules.Add(ICommunicationService.WMSocketServerDeviceName);

            if (WMCommunicationMode == ECommunicationMode.客户端Socket && WMSocketClientEnabled)
                modules.Add(ICommunicationService.WMSocketClientDeviceName);

            // DH 模块
            if (DHCommunicationMode == ECommunicationMode.服务端Socket && DHSocketServerEnabled)
                modules.Add(ICommunicationService.DHSocketServerDeviceName);

            if (DHCommunicationMode == ECommunicationMode.客户端Socket && DHASocketClientEnabled)
                modules.Add(ICommunicationService.DHASocketClientDeviceName);

            if (DHCommunicationMode == ECommunicationMode.客户端Socket && DHBSocketClientEnabled)
                modules.Add(ICommunicationService.DHBSocketClientDeviceName);

            if (DHCommunicationMode == ECommunicationMode.客户端Mqtt && DHAMqttClientEnabled)
                modules.Add(ICommunicationService.DHAMqttClientDeviceName);

            if (DHCommunicationMode == ECommunicationMode.客户端Mqtt && DHBMqttClientEnabled)
                modules.Add(ICommunicationService.DHBMqttClientDeviceName);

            // CCD 模块
            if (CCDSocketServerEnabled)
                modules.Add(ICommunicationService.CCDSocketServerDeviceName);

            // OPMM 模块
            if (OPMMPLCSocketClientEnabled)
                modules.Add(ICommunicationService.OPMMPLCSocketClientDeviceName);

            if (OPMMCCDSocketClientEnabled)
                modules.Add(ICommunicationService.OPMMCCDSocketClientDeviceName);

            if (OPMMWeightSocketClientEnabled)
                modules.Add(ICommunicationService.OPMMWeightSocketClientDeviceName);

            return modules;
        }

        #region 配置比较方法
        /// <summary>
        /// 检测 WM Socket 服务端是否变化
        /// </summary>
        public bool IsWMSocketServerChanged(CommunicationSettingModel other)
        {
            return WMCommunicationMode != other.WMCommunicationMode ||
                   WMSocketServerEnabled != other.WMSocketServerEnabled ||
                   WMSocketServerPort != other.WMSocketServerPort;
        }

        /// <summary>
        /// 检测 WM Socket 客户端是否变化
        /// </summary>
        public bool IsWMSocketClientChanged(CommunicationSettingModel other)
        {
            return WMCommunicationMode != other.WMCommunicationMode ||
                   WMSocketClientEnabled != other.WMSocketClientEnabled ||
                   WMSocketClientIP != other.WMSocketClientIP ||
                   WMSocketClientPort != other.WMSocketClientPort;
        }

        /// <summary>
        /// 检测 DH Socket 服务端是否变化
        /// </summary>
        public bool IsDHSocketServerChanged(CommunicationSettingModel other)
        {
            return DHCommunicationMode != other.DHCommunicationMode ||
                   DHSocketServerEnabled != other.DHSocketServerEnabled ||
                   DHSocketServerPort != other.DHSocketServerPort;
        }

        /// <summary>
        /// 检测 DH Socket 客户端 A 是否变化
        /// </summary>
        public bool IsDHASocketClientChanged(CommunicationSettingModel other)
        {
            return DHCommunicationMode != other.DHCommunicationMode ||
                   DHASocketClientEnabled != other.DHASocketClientEnabled ||
                   DHASocketClientIP != other.DHASocketClientIP ||
                   DHASocketClientPort != other.DHASocketClientPort;
        }

        /// <summary>
        /// 检测 DH Socket 客户端 B 是否变化
        /// </summary>
        public bool IsDHBSocketClientChanged(CommunicationSettingModel other)
        {
            return DHCommunicationMode != other.DHCommunicationMode ||
                   DHBSocketClientEnabled != other.DHBSocketClientEnabled ||
                   DHBSocketClientIP != other.DHBSocketClientIP ||
                   DHBSocketClientPort != other.DHBSocketClientPort;
        }

        /// <summary>
        /// 检测 DH MQTT 客户端 A 是否变化
        /// </summary>
        public bool IsDHAMqttClientChanged(CommunicationSettingModel other)
        {
            return DHCommunicationMode != other.DHCommunicationMode ||
                   DHAMqttClientEnabled != other.DHAMqttClientEnabled ||
                   DHAMqttClientIP != other.DHAMqttClientIP ||
                   DHAMqttClientPort != other.DHAMqttClientPort;
        }

        /// <summary>
        /// 检测 DH MQTT 客户端 B 是否变化
        /// </summary>
        public bool IsDHBMqttClientChanged(CommunicationSettingModel other)
        {
            return DHCommunicationMode != other.DHCommunicationMode ||
                   DHBMqttClientEnabled != other.DHBMqttClientEnabled ||
                   DHBMqttClientIP != other.DHBMqttClientIP ||
                   DHBMqttClientPort != other.DHBMqttClientPort;
        }

        /// <summary>
        /// 检测 CCD Socket 服务端是否变化
        /// </summary>
        public bool IsCCDSocketServerChanged(CommunicationSettingModel other)
        {
            return CCDSocketServerEnabled != other.CCDSocketServerEnabled ||
                   CCDSocketServerPort != other.CCDSocketServerPort;
        }

        /// <summary>
        /// 检测 OPMM PLC Socket 客户端是否变化
        /// </summary>
        public bool IsOPMMPLCSocketClientChanged(CommunicationSettingModel other)
        {
            return OPMMSocketClientIP != other.OPMMSocketClientIP ||
                   OPMMPLCSocketClientEnabled != other.OPMMPLCSocketClientEnabled ||
                   OPMMPLCSocketClientPort != other.OPMMPLCSocketClientPort;
        }

        /// <summary>
        /// 检测 OPMM CCD Socket 客户端是否变化
        /// </summary>
        public bool IsOPMMCCDSocketClientChanged(CommunicationSettingModel other)
        {
            return OPMMSocketClientIP != other.OPMMSocketClientIP ||
                   OPMMCCDSocketClientEnabled != other.OPMMCCDSocketClientEnabled ||
                   OPMMCCDSocketClientPort != other.OPMMCCDSocketClientPort;
        }

        /// <summary>
        /// 检测 OPMM Weight Socket 客户端是否变化
        /// </summary>
        public bool IsOPMMWeightSocketClientChanged(CommunicationSettingModel other)
        {
            return OPMMSocketClientIP != other.OPMMSocketClientIP ||
                   OPMMWeightSocketClientEnabled != other.OPMMWeightSocketClientEnabled ||
                   OPMMWeightSocketClientPort != other.OPMMWeightSocketClientPort;
        }
        #endregion
        #endregion
    }
}
