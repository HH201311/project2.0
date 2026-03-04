using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Core.Common;
using System.ComponentModel;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB
{
    public class LineInfoModel : ModelBase
    {
        private string? lineBase;
        private string? electrode;
        private string? lineName;
        private string? deviceName;
        private string? deviceIp;

        [Description("基地")]
        public string? LineBase
        {
            get => lineBase;
            set => SetProperty(ref lineBase, value);
        }

        [Description("电极")]
        public string? Electrode
        {
            get => electrode;
            set => SetProperty(ref electrode, value);
        }

        [Description("拉线")]
        public string? LineName
        {
            get => lineName;
            set => SetProperty(ref lineName, value);
        }

        [Description("机台")]
        public string? DeviceName
        {
            get => deviceName;
            set => SetProperty(ref deviceName, value);
        }

        [Description("本机IP")]
        public string? DeviceIp
        {
            get => deviceIp;
            set => SetProperty(ref deviceIp, value);
        }
    }
}
