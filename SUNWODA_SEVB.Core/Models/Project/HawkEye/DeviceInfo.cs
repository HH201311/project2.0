using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.HawkEye
{
    /// <summary>
    /// SDK的设备信息
    /// </summary>
    public class DeviceInfo
    {
        public int AuxFunctionMask { get; set; }
        public string? BuildDate { get; set; }
        public int ChannelNum { get; set; }
        public int DeviceType { get; set; }
        public string? GateWay { get; set; }
        public string? HostIP { get; set; }
        public string? HostName { get; set; }
        public int HttpPort { get; set; }
        public string? MAC { get; set; }
        public string? MonMode { get; set; }
        public int NetConnectState { get; set; }
        public string? OtherFunction { get; set; }
        public string? SN { get; set; }
        public int SSLPort { get; set; }
        public string? Submask { get; set; }
        public int TCPMaxConn { get; set; }
        public int TCPPort { get; set; }
        public int UDPPort { get; set; }
        public bool UseHSDownLoad { get; set; }
        public string? Version { get; set; }

        public override int GetHashCode()
        {
            return HostIP?.GetHashCode() ?? 0;
        }
    }
}
