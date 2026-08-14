using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels
{
    /// <summary>
    /// Mqtt协议的PLC地址配置
    /// </summary>
    public class MqttAddressModel : ModelBase
    {
        private string protocol = "fins";
        private string pumpAddress1 = "8176";
        private string pumpAddress2 = string.Empty;
        private string pumpAddress3 = string.Empty;
        private string pumpAddress4 = string.Empty;
        private string pumpAddress5 = string.Empty;
        private string flagAddress = "8186";

        /// <summary>
        /// 通讯协议名称
        /// 备注：目前支持的协议有fins，s7，mc
        /// </summary>
        public string Protocol { get => protocol; set => SetProperty(ref protocol, value); }

        /// <summary>
        /// 泵1地址
        /// </summary>
        public string PumpAddress1 { get => pumpAddress1; set => SetProperty(ref pumpAddress1, value); }

        /// <summary>
        /// 泵2地址
        /// </summary>
        public string PumpAddress2 { get => pumpAddress2; set => SetProperty(ref pumpAddress2, value); }

        /// <summary>
        /// 泵3地址
        /// </summary>
        public string PumpAddress3 { get => pumpAddress3; set => SetProperty(ref pumpAddress3, value); }

        /// <summary>
        /// 泵4地址
        /// </summary>
        public string PumpAddress4 { get => pumpAddress4; set => SetProperty(ref pumpAddress4, value); }

        /// <summary>
        /// 泵5地址
        /// </summary>
        public string PumpAddress5 { get => pumpAddress5; set => SetProperty(ref pumpAddress5, value); }

        /// <summary>
        /// 下发信号地址
        /// </summary>
        public string FlagAddress { get => flagAddress; set => SetProperty(ref flagAddress, value); }

        /// <summary>
        /// 泵速地址列表
        /// </summary>
        [JsonIgnore]
        public List<string> PumpAddresses => new List<string>()
        {
            PumpAddress1,pumpAddress2,pumpAddress3,pumpAddress4,pumpAddress5
        };
    }
}
