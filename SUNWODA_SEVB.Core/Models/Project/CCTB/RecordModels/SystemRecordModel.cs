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
    public class SystemRecordModel : ModelBase
    {
        private int id;
        private string? account = "Unknown";
        private string? name = "Unknown";
        private string? role = "Unknown";
        private string? message = "Unknown";
        private string? recordLevel = "Unknown";
        private DateTime? recordTime = null;

        /// <summary>
        /// 排序编号
        /// </summary>
        [JsonIgnore]
        public int ID { get => id; set => SetProperty(ref id, value); }

        /// <summary>
        /// 用户账号
        /// </summary>
        public string? Account { get => account; set => SetProperty(ref account, value); }

        /// <summary>
        /// 用户姓名
        /// </summary>
        public string? Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// 角色
        /// </summary>
        public string? Role { get => role; set => SetProperty(ref role, value); }

        /// <summary>
        /// 操作记录
        /// </summary>
        public string? Message { get => message; set => SetProperty(ref message, value); }

        /// <summary>
        /// 记录等级
        /// </summary>
        [JsonIgnore]
        public string? RecordLevel { get => recordLevel; set => SetProperty(ref recordLevel, value); }

        /// <summary>
        /// 记录时间
        /// </summary>
        [JsonIgnore]
        public DateTime? RecordTime { get => recordTime; set => SetProperty(ref recordTime, value); }
    }
}
