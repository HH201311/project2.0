using HslCommunication.Profinet.OpenProtocol;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Data;
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
        private string code = "Unknown";
        private string name = "Unknown";
        private string? alarmMessage;
        private DateTime? recordTime = null;

        /// <summary>
        /// 排序编号
        /// </summary>
        [JsonIgnore]
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 报警编号
        /// </summary>
        public string Code { get => code; set => SetProperty(ref code, value); }

        /// <summary>
        /// 报警名称
        /// </summary>
        [JsonIgnore]
        public string Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// 报警内容
        /// </summary>
        public string? AlarmMessage { get => alarmMessage; set => SetProperty(ref alarmMessage, value); }

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }

        #region 辅助方法

        /// <summary>
        /// 报警记录转换
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static AlarmRecordModel ConvertToRecord(AppLogModel log)
        {
            AlarmRecordModel? record;
            try
            {
                record = JsonConvert.DeserializeObject<AlarmRecordModel>(log.Message);
            }
            catch
            {
                record = null;
            }

            if (record == null)
            {
                record = new AlarmRecordModel();
                record.AlarmMessage = "DeserializeObjectError";
            }
            var ac = CCTBCore.AlarmConfigs.FirstOrDefault(o => o.Code == record.Code);
            record.Name = ac?.Name ?? "Unknow";
            record.RecordTime = log.LogTime;

            return record;
        }
        #endregion
    }
}
