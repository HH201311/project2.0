
namespace SUNWODA_SEVB.Core.Models.Web
{
    /// <summary>
    /// WEB服务配置
    /// </summary>
    public class WebSettings
    {
        // URL配置
        public string? CentralControlWebUrl { get; set; }
        public string? WebTestUrlSysCenter { get; set; }
        public string? WebTestUrlScrewTighten { get; set; }

        // 设备信息
        public string? DeviceSn { get; set; }
        public string? DeviceName { get; set; }
        public string? Workship { get; set; }
        public string? SoftVersion { get; set; }
        public string? ZipFileName { get; set; }
        public string? UUID { get; set; }

        // 路径配置
        public string? SaveZipFilePath { get; set; }
        public string? ZipedFolderPath { get; set; }
        public string? SqlPath { get; set; }

        // 网络配置
        public string IpHeader { get; set; } = "10.";

        // 任务配置（从数据库GlobalSetting获取）
        public int HeartbeatIntervalSeconds { get; set; } = 10;
        public int DeviceBindingRetrySeconds { get; set; } = 5;
        public int PcInfoUploadIntervalMinutes { get; set; } = 10;
        public int VersionCheckIntervalSeconds { get; set; } = 60;

        // 功能开关
        public bool IsWebEnabled { get; set; }
        public bool EnableHeartbeat { get; set; } = true;
        public bool EnableVersionCheck { get; set; } = false;// 是否启用版本检查,默认为关闭
        public bool EnablePcInfoUpload { get; set; } = true;

        /// <summary>
        /// 验证心跳所需的配置是否完整
        /// </summary>
        public bool IsHeartbeatConfigValid()
        {
            return !string.IsNullOrEmpty(DeviceSn) &&
                   !string.IsNullOrEmpty(UUID);
        }

        /// <summary>
        /// 验证设备绑定所需的配置是否完整
        /// </summary>
        public bool IsDeviceBindingConfigValid()
        {
            return !string.IsNullOrEmpty(DeviceSn) &&
                   !string.IsNullOrEmpty(DeviceName) &&
                   !string.IsNullOrEmpty(UUID);
        }


        #region 螺丝拧紧相关配置

        /// <summary>
        /// 默认拧紧控制器IP
        /// </summary>
        public string? DefaultTightenControllerIP { get; set; }

        /// <summary>
        /// 默认拧紧控制器端口
        /// </summary>
        public string? DefaultTightenControllerPort { get; set; } = "4545";

        /// <summary>
        /// 曲线数据请求超时时间（秒）
        /// </summary>
        public int CurveDataTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// 是否启用曲线数据获取
        /// </summary>
        public bool EnableCurveDataFetch { get; set; } = true;

        #endregion
    }
}
