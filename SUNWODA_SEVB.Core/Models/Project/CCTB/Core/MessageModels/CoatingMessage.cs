using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels
{
    public class CoatingMessage
    {
        public ESideType SideType { get; set; }
        public List<double>? Datas { get; set; }
    }
}
