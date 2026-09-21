using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels
{
    /// <summary>
    /// MC采集数据类
    /// </summary>
    public class MCGatherData : ModelBase
    {
        private List<(DateTime time, float detValue, float tarValue)> values = [];
        private float setValue;

        /// <summary>
        /// 纠偏数据：采集时间/检测值/目标值
        /// </summary>
        public List<(DateTime time, float detValue, float tarValue)> Values { get => values; set => SetProperty(ref values, value); }

        /// <summary>
        /// 纠偏设定值
        /// </summary>
        public float SetValue { get => setValue; set => SetProperty(ref setValue, value); }
    }
}
