
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SUNWODA_SEVB.Core.Models.Project.HawkEye
{
    /// <summary>
    /// 设备类
    /// </summary>
    [Serializable]
    public class CameraModel : ModelBase
    {
        private string guidId = Guid.NewGuid().ToString();
        private bool enable;
        private string? name;
        private string? hostIP;
        private string? protocol;
        private string? workstaion;
        private string? remake;
        private bool removeDistortion;


        /// <summary>
        /// 编号
        /// </summary>
        [XmlAttribute("Guid编号")]
        public string GuidId
        {
            get => guidId;
            set => SetProperty(ref guidId, value);
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        [XmlAttribute("是否启用")]
        public bool Enable
        {
            get => enable;
            set => SetProperty(ref enable, value);
        }

        /// <summary>
        /// 名称
        /// </summary>
        [XmlAttribute("名称")]
        public string? Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        /// <summary>
        /// 相机IP
        /// </summary>
        [XmlAttribute("相机IP")]
        public string? HostIP
        {
            get => hostIP;
            set => SetProperty(ref hostIP, value);
        }

        /// <summary>
        /// 通讯协议
        /// </summary>
        [XmlAttribute("通讯协议")]
        public string? Protocol
        {
            get => protocol;
            set => SetProperty(ref protocol, value);
        }

        /// <summary>
        /// 工位名称
        /// </summary>
        [XmlAttribute("工位名称")]
        public string? WorkStaion
        {
            get => workstaion;
            set => SetProperty(ref workstaion, value);
        }

        /// <summary>
        /// 备注
        /// </summary>
        [XmlAttribute("备注")]
        public string? Remake
        {
            get => remake;
            set => SetProperty(ref remake, value);
        }

        /// <summary>
        /// 是否去除畸变
        /// </summary>
        [XmlAttribute("是否去除畸变")]
        public bool RemoveDistortion
        {
            get => removeDistortion;
            set => SetProperty(ref removeDistortion, value);
        }

        /// <summary>
        /// 连接字符串
        /// </summary>
        [XmlIgnore()]
        public string? ConnectionString { get { return $"rtsp://admin:admin@{HostIP}:554/Streaming/Channels/1"; } }

        // $"rtsp://{HostIP}:554/user=admin&password=admin&channel=1"

       }
}
