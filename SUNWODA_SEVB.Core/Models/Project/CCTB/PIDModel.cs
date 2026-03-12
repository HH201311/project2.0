using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public struct PIDModel
    {
        /// <summary>
        /// 间隔时间T
        /// </summary>
        public float T { get; set; }
        /// <summary>
        /// 比例P
        /// </summary>
        public float P { get; set; }

        /// <summary>
        /// 积分I
        /// </summary>
        public float I { get; set; }

        /// <summary>
        /// 微分D
        /// </summary>
        public float D { get; set; }
    }
}
