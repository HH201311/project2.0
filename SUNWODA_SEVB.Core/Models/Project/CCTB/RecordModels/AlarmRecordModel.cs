using HslCommunication.Profinet.OpenProtocol;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels
{
    public class AlarmRecordModel : ModelBase
    {
        private int id;
        private string? sideType = "Unknown";
        private string? alarmMessage;
        private DateTime? recordTime = null;

        /// <summary>
        /// 排序编号
        /// </summary>
        [JsonIgnore]
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 极面
        /// </summary>
        public string? SideType { get => sideType; set => SetProperty(ref sideType, value); }

        /// <summary>
        /// 报警内容
        /// </summary>
        public string? AlarmMessage { get => alarmMessage; set => SetProperty(ref alarmMessage, value); }

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }
    }
}
