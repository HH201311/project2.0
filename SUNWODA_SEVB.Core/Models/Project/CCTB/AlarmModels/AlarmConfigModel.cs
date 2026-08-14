using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels
{
    /// <summary>
    /// 报警配置模型
    /// </summary>
    public class AlarmConfigModel : ModelBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public AlarmConfigModel() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        public AlarmConfigModel(string? code, string? name, EAlarmLevel level, bool isEnabled)
        {
            this.code = code;
            this.name = name;
            this.level = level;
            this.isEnabled = isEnabled;
        }

        private string? code;
        private string? name;
        private EAlarmLevel level;
        private bool isEnabled;

        /// <summary>
        /// 报警标识符
        /// </summary>
        public string? Code { get => code; set => SetProperty(ref code, value); }

        /// <summary>
        /// 报警信息
        /// </summary>
        [JsonIgnore]
        public string? Name { get => name; set => SetProperty(ref name, value); }

        /// <summary>
        /// 报警等级
        /// </summary>
        public EAlarmLevel Level { get => level; set => SetProperty(ref level, value); }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get => isEnabled; set => SetProperty(ref isEnabled, value); }
    }
}
