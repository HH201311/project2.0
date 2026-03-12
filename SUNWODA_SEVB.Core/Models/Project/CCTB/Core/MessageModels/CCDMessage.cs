using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels
{
    public class CCDMessage
    {
        /// <summary>
        /// AB面
        /// </summary>
        public ESideType SideType { get; set; }

        /// <summary>
        /// 是否是干膜数据
        /// </summary>
        public bool IsDryFilmData { get; set; }

        /// <summary>
        /// CCD尺寸数据
        /// </summary>
        public CCDModel? Data { get; set; }
    }
}
