using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JRCW.FormModels
{
    public class ProductInfoModel : ModelBase
    {
        private string plcConnectStatus = "未启用";
        private string mesConnectStatus = "未启用";
        private string webConnectStatus = "未启用";
        private string deviceSn = "--";
        private string deviceName = "--";
        private string lineName = "--";
        private string productionOrder = "--";
        private string processCode = "--";
        private string materialsInfo = "--";
        private string operationSheet = "--";

        /// <summary>
        /// PLC连接状态
        /// </summary>
        public string PLCConnectStatus { get => plcConnectStatus; set => SetProperty(ref plcConnectStatus, value); }

        /// <summary>
        /// MES连接状态
        /// </summary>
        public string MESConnectStatus { get => mesConnectStatus; set => SetProperty(ref mesConnectStatus, value); }

        /// <summary>
        /// WEB连接状态
        /// </summary>
        public string WEBConnectStatus { get => webConnectStatus; set => SetProperty(ref webConnectStatus, value); }

        /// <summary>
        /// 设备编号
        /// </summary>
        public string DeviceSn { get => deviceSn; set => SetProperty(ref deviceSn, value); }

        /// <summary>
        /// 设备名称
        /// </summary>
        public string DeviceName { get => deviceName; set => SetProperty(ref deviceName, value); }

        /// <summary>
        /// 拉线名称
        /// </summary>
        public string LineName { get => lineName; set => SetProperty(ref lineName, value); }

        /// <summary>
        /// 制令单
        /// </summary>
        public string ProductionOrder { get => productionOrder; set => SetProperty(ref productionOrder, value); }

        /// <summary>
        /// 工序代码
        /// </summary>
        public string ProcessCode { get => processCode; set => SetProperty(ref processCode, value); }

        /// <summary>
        /// 物料信息
        /// </summary>
        public string MaterialsInfo { get => materialsInfo; set => SetProperty(ref materialsInfo, value); }

        /// <summary>
        /// 操作单
        /// </summary>
        public string OperationSheet { get => operationSheet; set => SetProperty(ref operationSheet, value); }
    }
}
