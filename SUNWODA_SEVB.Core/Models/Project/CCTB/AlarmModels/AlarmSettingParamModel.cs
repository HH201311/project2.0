using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.AlarmModels
{
    /// <summary>
    /// 报警配置参数模型
    /// </summary>
    public class AlarmConfigParamModel : ModelBase
    {
        private float weightDataLimit = 20;
        private float rangeLimit = 10;

        /// <summary>
        /// 面密度异常报警：异常值
        /// </summary>
        public float WeightDataLimit { get => weightDataLimit; set => SetProperty(ref weightDataLimit, value); }

        /// <summary>
        /// 面密度极差过大报警：限定极差值
        /// </summary>
        public float RangeLimit { get => rangeLimit; set => SetProperty(ref rangeLimit, value); }
    }
}
