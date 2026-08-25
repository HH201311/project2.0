using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class PIDModel : ModelBase
    {
        private float t = 0.05f;
        private float p = 0.005f;
        private float i = 0.001f;
        private float d = 0.001f;

        /// <summary>
        /// 间隔时间T
        /// </summary>
        public float T { get => t; set => SetProperty(ref t, value); }

        /// <summary>
        /// 比例P
        /// </summary>
        public float P { get => p; set => SetProperty(ref p, value); }


        /// <summary>
        /// 积分I
        /// </summary>
        public float I { get => i; set => SetProperty(ref i, value); }


        /// <summary>
        /// 微分D
        /// </summary>
        public float D { get => d; set => SetProperty(ref d, value); }
    }
}
