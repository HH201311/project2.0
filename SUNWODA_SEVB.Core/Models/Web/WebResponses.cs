using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Web
{
    /// <summary>
    /// 基础响应模型
    /// </summary>
    public class BaseResponse
    {
        [JsonPropertyName("code")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Code { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("success")]
        public bool IsSuccess { get; set; }
    }

    /// <summary>
    /// 设备绑定响应
    /// </summary>
    public class DeviceBindingResponse : BaseResponse
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; } // UUID
    }

    /// <summary>
    /// 心跳响应
    /// </summary>
    public class HeartbeatResponse : BaseResponse
    {
    }

    /// <summary>
    /// 上位机预警信息接口
    /// </summary>
    public class MachineErrorResponse : BaseResponse
    {
    }


    public class WebApiSendResponse : BaseResponse
    {
    }

    /// <summary>
    /// PC信息响应
    /// </summary>
    public class PcInfoResponse : BaseResponse
    {
    }

    /// <summary>
    /// 版本检查响应
    /// </summary>
    public class CheckVersionResponse : BaseResponse
    {
        public VersionData? Data { get; set; }

        public class VersionData
        {
            public string? Code { get; set; }
            public string? Info { get; set; } // 新版本号
            public bool NeedUpdate => Code == "C002";
        }
    }

    /// <summary>
    /// 文件下载响应
    /// </summary>
    public class DownloadFileResponse : BaseResponse
    {
        public byte[]? FileData { get; set; }
    }

    /// <summary>
    /// 文件上传响应
    /// </summary>
    public class UploadFileResponse : BaseResponse
    {
        public string? FileId { get; set; }
    }

    /// <summary>
    /// SysCenter文件响应
    /// </summary>
    public class SysCenterFileResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? DataStr { get; set; }
    }

    /// <summary>
    /// 库文件.dll下载接口响应
    /// </summary>
    public class DownloadDllResponse : BaseResponse
    {
        /// <summary>
        /// 接收到的字节流文件数据
        /// </summary>
        [JsonIgnore]
        public byte[]? DataStr { get; set; }
    }

    /// <summary>
    /// 文件版本响应
    /// </summary>
    public class FileVersionResponse
    {
        /// <summary>
        /// 返回代码
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        /// <summary>
        /// 结果
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// 信息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        [JsonPropertyName("data")]
        public string? Version { get; set; }
    }

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：视觉机损检测
    /// </summary>
    public class AiDetectionDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    /// <summary>
    /// 摄像头新增接口响应
    /// </summary>
    public class AddCameraResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：轨迹检测
    /// </summary>
    public class TrajectoryDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：轨迹检测
    /// </summary>
    public class CapacityPredictionDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    #region 螺丝拧紧曲线相关

    /// <summary>
    /// 螺丝曲线数据响应
    /// </summary>
    public class ScrewCurveResponse : BaseResponse
    {
        /// <summary>
        /// 螺丝标签
        /// </summary>
        [JsonPropertyName("screwLabel")]
        public string? ScrewLabel { get; set; }

        /// <summary>
        /// 采集时间
        /// </summary>
        [JsonPropertyName("collectionTime")]
        public DateTime CollectionTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 角度数据数组
        /// </summary>
        [JsonPropertyName("angles")]
        public double[]? Angles { get; set; }

        /// <summary>
        /// 扭矩数据数组
        /// </summary>
        [JsonPropertyName("torques")]
        public double[]? Torques { get; set; }

        /// <summary>
        /// 电流数据数组（可选）
        /// </summary>
        [JsonPropertyName("currents")]
        public double[]? Currents { get; set; }

        /// <summary>
        /// 最终扭矩值
        /// </summary>
        [JsonPropertyName("finalTorque")]
        public double FinalTorque { get; set; }

        /// <summary>
        /// 最终角度值
        /// </summary>
        [JsonPropertyName("finalAngle")]
        public double FinalAngle { get; set; }

        /// <summary>
        /// 是否合格
        /// </summary>
        [JsonPropertyName("isOk")]
        public bool IsOk { get; set; }

        /// <summary>
        /// 控制器IP
        /// </summary>
        [JsonPropertyName("controllerIP")]
        public string? ControllerIP { get; set; }

        /// <summary>
        /// 周期编号
        /// </summary>
        [JsonPropertyName("cycleNumber")]
        public int CycleNumber { get; set; }

        /// <summary>
        /// 获取曲线数据点数量
        /// </summary>
        [JsonIgnore]
        public int DataPointCount => Angles?.Length ?? 0;

        /// <summary>
        /// 是否有有效的曲线数据
        /// </summary>
        [JsonIgnore]
        public bool HasCurveData => Angles != null && Torques != null && Angles.Length > 0 && Angles.Length == Torques.Length;
    }

    #endregion


}
