
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;

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
        private bool isEnable;
        private string? name;
        private string? hostIP;
        private string? protocol = "RTSP";
        private string? workstation;
        private string? remake;
        private bool removeDistortion;
        private string? calibrationFilePath;
        private double resolution = 25;
        private bool videoSaving = false;
        private bool realTimeDisplay = false;
        private ImageSource? imageSource;

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
            get
            {
                IsEnable = !enable;
                return enable;
            }
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
        public string? WorkStation
        {
            get => workstation;
            set => SetProperty(ref workstation, value);
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
        /// 标定文件路径
        /// </summary>
        [XmlAttribute("标定文件路径")]
        public string? CalibrationFilePath
        {
            get => calibrationFilePath;
            set => SetProperty(ref calibrationFilePath, value);
        }

        /// <summary>
        /// 分辨率
        /// </summary>
        [XmlAttribute("分辨率")]
        public double Resolution
        {
            get => resolution;
            set => SetProperty(ref resolution, value);
        }

        /// <summary>
        /// 视频保存
        /// </summary>
        [XmlAttribute("视频保存")]
        public bool VideoSaving
        {
            get => videoSaving;
            set => SetProperty(ref videoSaving, value);
        }

        /// <summary>
        /// 实时界面展示
        /// </summary>
        [XmlAttribute("实时界面展示")]
        public bool RealTimeDisplay
        {
            get => realTimeDisplay;
            set => SetProperty(ref realTimeDisplay, value);
        }

        /// <summary>
        /// 通讯协议列表
        /// </summary>
        [XmlIgnore()]
        public List<string> ProtocolList { get; } = ["RTSP", "USB"];

        /// <summary>
        /// 是否可以设置
        /// </summary>
        [XmlIgnore()]
        public bool IsEnable
        {
            get => isEnable;
            set => SetProperty(ref isEnable, value);
        }

        /// <summary>
        /// 实时显示图片源
        /// </summary>
        [XmlIgnore()]
        public ImageSource? ImageSource
        {
            get => imageSource;
            set => SetProperty(ref imageSource, value);
        }

        private string? imageSources = "D:\\单机数字化平台\\sunwoda_Sevb\\SUNWODA_SEVB\\bin\\Debug\\Configs\\PTO叠片取放料数据\\5mm\\0167_Start.jpg";
        /// <summary>
        /// 实时显示图片源
        /// </summary>
        [XmlIgnore()]
        public string? ImageSources
        {
            get => imageSources;
            set => SetProperty(ref imageSources, value);
        }
    }
}
