using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class TBlockModel : ModelBase
    {
        private double tBlockPosition;
        private bool isAuto;

        public double TBlockPosition
        {
            get => tBlockPosition;
            set => SetProperty(ref tBlockPosition, value);
        }

        public bool IsAuto
        {
            get => isAuto;
            set => SetProperty(ref isAuto, value);
        }
    }
}
