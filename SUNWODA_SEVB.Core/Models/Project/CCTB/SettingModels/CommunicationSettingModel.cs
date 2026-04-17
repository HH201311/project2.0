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
    /// 通讯设置参数模型类
    /// </summary>
    public class CommunicationSettingModel : ModelBase
    {
        #region 测重仪:WeightMeasure
        private ECommunicationMode communicationMode;
        private string? wmIP = "127.0.0.1";
        private int wmPort = 5555;

        /// <summary>
        /// C/S 架构
        /// </summary>
        public ECommunicationMode CommunicationMode { get => communicationMode; set => SetProperty(ref communicationMode, value); }

        /// <summary>
        /// 端口IP
        /// </summary>
        public string? WMIP { get => wmIP; set => SetProperty(ref wmIP, value); }

        /// <summary>
        /// 端口
        /// </summary>
        public int WMPort { get => wmPort; set => SetProperty(ref wmPort, value); }
        #endregion

        #region 涂布机:CoatingMachine       
        private string? cmIP = "192.168.0.1";
        private int cmPort = 9600;

        /// <summary>
        /// 涂布机IP
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore]
        public string? CMIP { get => cmIP; set => SetProperty(ref cmIP, value); }

        /// <summary>
        /// 端口
        ///     注：PLC配置信息，在plc_config中配置
        /// </summary>
        [JsonIgnore]
        public int CMPort { get => cmPort; set => SetProperty(ref cmPort, value); }
        #endregion

        #region 模头:DiaHead
        #region A头
        private string? dhIPA = "192.168.1.30";
        private int dhServerPortA = 8989;
        private int dhClientPortA = 5555;

        /// <summary>
        /// A头：模头IP
        /// </summary>
        public string? DHIPA { get => dhIPA; set => SetProperty(ref dhIPA, value); }

        /// <summary>
        /// A头：服务端端口
        /// </summary>
        public int DHServerPortA { get => dhServerPortA; set => SetProperty(ref dhServerPortA, value); }

        /// <summary>
        /// A头：客户端端口
        /// </summary>
        public int DHClientPortA { get => dhClientPortA; set => SetProperty(ref dhClientPortA, value); }
        #endregion

        #region B头
        private string? dhIPB = "192.168.1.31";
        private int dhServerPortB = 8989;
        private int dhClientPortB = 5555;

        /// <summary>
        /// B头：模头IP
        /// </summary>
        public string? DHIPB { get => dhIPB; set => SetProperty(ref dhIPB, value); }

        /// <summary>
        /// B头：服务端端口
        /// </summary>
        public int DHServerPortB { get => dhServerPortB; set => SetProperty(ref dhServerPortB, value); }

        /// <summary>
        /// B头：客户端端口
        /// </summary>
        public int DHClientPortB { get => dhClientPortB; set => SetProperty(ref dhClientPortB, value); }
        #endregion
        #endregion

        #region CCD:CCD
        private string? ccdIP = "127.0.0.1";
        private int ccdPort = 9001;

        /// <summary>
        /// CCD: IP
        /// </summary>
        public string? CCDIP { get => ccdIP; set => SetProperty(ref ccdIP, value); }

        /// <summary>
        /// CCD: 端口
        /// </summary>
        public int CCDPort { get => ccdPort; set => SetProperty(ref ccdPort, value); }
        #endregion
    }
}
