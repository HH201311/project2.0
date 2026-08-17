

namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 螺丝控制器 REST API 版本。
    /// </summary>
    public enum ControllerApiVersion
    {
        /// <summary>INSIGHTqc / WbaRest-Py 旧版：session Cookie，Base64 登录体，/rest/db/ CouchDB 路径。</summary>
        LegacyInsightQc = 0,

        /// <summary>Express 新版：connect.sid Cookie，纯 JSON 登录体，/rest/get-eor-logs/ + /rest/get-trace-logs/。</summary>
        Express = 1
    }

    /// <summary>
    /// 螺丝枪控制器数据读取配置
    /// </summary>
    public class ScrewControllerSettings
    {
        /// <summary>
        /// 控制器 API 版本
        /// </summary>
        public ControllerApiVersion ApiVersion { get; set; } = ControllerApiVersion.LegacyInsightQc;

        /// <summary>
        /// 是否启用曲线数据获取
        /// </summary>
        public bool EnableCurveDataFetch { get; set; } = true;

        /// <summary>
        /// 控制器IP地址
        /// </summary>
        public string? ControllerIP { get; set; } = "";

        /// <summary>
        /// 控制器IP地址
        /// </summary>
        public List<string> ControllerIPs { get; set; } = new List<string>();

        /// <summary>
        /// REST API基础路径
        /// </summary>
        public string ApiBasePath { get; set; } = "/rest/db/";

        /// <summary>
        /// 请求超时时间（秒）
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// 重试次数
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒）
        /// </summary>
        public int RetryIntervalMs { get; set; } = 1000;

        /// <summary>
        /// 多控制器配置（JSON格式存储）
        /// </summary>
        public List<ControllerEndpoint> Controllers { get; set; } = new();
        /// <summary>
        /// 螺丝枪与控制器的映射关系
        /// Key: 螺丝枪编号(如t1, t2, t3)
        /// Value: 控制器IP地址
        /// </summary>
        public Dictionary<string, string> TightenGunMapping { get; set; } = new();

        /// <summary>
        /// 是否启用TR值数据库缓存
        /// </summary>
        public bool EnableTrValueDbCache { get; set; } = true;

        /// <summary>
        /// TR值缓存过期时间（小时），0表示永不过期
        /// </summary>
        public int TrValueCacheExpirationHours { get; set; } = 12;
        /// <summary>
        /// 用户名
        /// </summary>
        public string LoginUsername { get; set; } = "admin";
        /// <summary>
        /// 密码
        /// </summary>
        public string LoginPassword { get; set; } = "sollinger";
        /// <summary>
        /// 请求体
        /// </summary>
        public string base64RequestBody { get; set; } = string.Empty;
        /// <summary>
        /// 是否为有线枪
        /// </summary>
        public bool IsWired { get; set; } = true;

        /// <summary>
        /// 无线枪TR值请求路径
        /// </summary>
        public string WirelessTrRequestPath { get; set; } = "/tool_details/toolData";

        /// <summary>
        /// 有线枪TR值请求路径
        /// </summary>
        public string WiredTrRequestPath { get; set; } = "/toolCalibration/";

        /// <summary>
        /// 无线枪曲线数据请求路径
        /// </summary>
        public string WirelessCurveRequestPath { get; set; } = "/rest/db/";

        /// <summary>
        /// 有线枪曲线数据请求路径
        /// </summary>
        public string WiredCurveRequestPath { get; set; } = "/rest/db/";

    }

    /// <summary>
    /// 控制器端点配置
    /// </summary>
    public class ControllerEndpoint
    {
        public string Name { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        //关联的螺丝枪编号列表
        public string TightenGuns { get; set; } = string.Empty;
    }

    /// <summary>
    /// TR值缓存记录
    /// </summary>
    public class TrValueCacheRecord
    {
        public string ControllerIP { get; set; } = string.Empty;
        public string TightenGun { get; set; } = string.Empty;
        public float TrValue { get; set; }
        public DateTime CachedTime { get; set; }
        public DateTime? ExpirationTime { get; set; }
    }
}
