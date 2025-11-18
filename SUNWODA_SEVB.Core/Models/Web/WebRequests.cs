using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using static System.Formats.Asn1.AsnWriter;

namespace SUNWODA_SEVB.Core.Models.Web
{
    /// <summary>
    /// 设备绑定请求
    /// </summary>
    public class DeviceBindingRequest
    {
        public string? park { get; set; }
        public string? stage { get; set; }
        public string? line { get; set; }
        public string? workship { get; set; }
        public string? proceduce { get; set; }
        public string? devId { get; set; }
        public string? softName { get; set; }
        public string? softVersion { get; set; }
        public string? macId { get; set; }
        public string? ip { get; set; }
    }

    /// <summary>
    /// PC信息请求
    /// </summary>
    /// <summary>
    /// PC信息请求模型
    /// </summary>
    public class PcInfoRequest
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string? DevId { get; set; }

        /// <summary>
        /// CPU使用率
        /// </summary>
        public string? Cpu { get; set; }

        /// <summary>
        /// 内存使用情况（进程独占的私有内存，保留用于兼容）
        /// </summary>
        public string? Memory { get; set; }

        ///// <summary>
        ///// 私有内存（进程独占的内存）
        ///// </summary>
        //public string? PrivateMemory { get; set; }

        /// <summary>
        /// 物理内存（工作集）
        /// </summary>
        public string? PhysicalMemory { get; set; }

        /// <summary>
        /// 虚拟内存
        /// </summary>
        public string? VirtualMemory { get; set; }

        /// <summary>
        /// GC托管内存（.NET管理的内存）
        /// </summary>
        public string? ManagedMemory { get; set; }

        /// <summary>
        /// 系统内存使用情况
        /// </summary>
        public string? SystemMemory { get; set; }

        /// <summary>
        /// 磁盘使用情况
        /// </summary>
        public string? Disk { get; set; }

        /// <summary>
        /// 设备状态
        /// </summary>
        public string? EquipmentState { get; set; }

        /// <summary>
        /// 采集时间
        /// </summary>
        public string? Time { get; set; }
    }

    /// <summary>
    /// 版本检查请求
    /// </summary>
    public class CheckVersionRequest
    {
        public string? SoftName { get; set; }
        public string? SoftVersion { get; set; }
        public string? DevId { get; set; }
    }

    /// <summary>
    /// 文件下载请求
    /// </summary>
    public class DownloadFileRequest
    {
        public string? SoftName { get; set; }
        public string? SoftVersion { get; set; }
        public string? UUID { get; set; }
    }

    /// <summary>
    /// SysCenter文件请求
    /// </summary>
    public class SysCenterFileRequest
    {
        public string? AppKey { get; set; }
        public string? Signature { get; set; }
        public string? PythonCode { get; set; }
        public string? ExecuteType { get; set; }
    }

    /// <summary>
    /// dll文件请求
    /// </summary>
    public class DownloadDllRequest
    {
        public string? AppKey { get; set; }
        public string? Signature { get; set; }
        public string? PythonCode { get; set; }
        public string? ExecuteType { get; set; }
    }

    #region 鹰眼项目
    /// <summary>
    /// UE端数据接收接请求类
    /// 备注：视觉机损检测
    /// </summary>
    public class AiDetectionDataRequest
    {
        /// <summary>
        /// 基地
        /// </summary>
        [JsonPropertyName("productionBase")]
        public string? ProductionBase { get; set; }

        /// <summary>
        /// 拉线
        /// </summary>
        [JsonPropertyName("productionLine")]
        public string? ProductionLine { get; set; }

        /// <summary>
        /// 工序
        /// </summary>
        [JsonPropertyName("process")]
        public string? Process { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// 工位
        /// </summary>
        [JsonPropertyName("position")]
        public string? Position { get; set; }

        /// <summary>
        /// 电芯条码
        /// </summary>
        [JsonPropertyName("barCode")]
        public string? BarCode { get; set; }

        /// <summary>
        ///  异常对应的时间戳
        /// </summary>
        [JsonPropertyName("abnormalTimeStamp")]
        public long AbnormalTimeStamp { get; set; }

        /// <summary>
        /// 缺陷置信度
        /// </summary>
        [JsonPropertyName("score")]
        public float Score { get; set; }

        /// <summary>
        /// 缺陷宽度
        /// </summary>
        [JsonPropertyName("defectWidth")]
        public float DefectWidth { get; set; }

        /// <summary>
        /// 缺陷高度
        /// </summary>
        [JsonPropertyName("defectHeight")]
        public float DefectHeight { get; set; }

        /// <summary>
        /// 缺陷面积
        /// </summary>
        [JsonPropertyName("defectArea")]
        public float DefectArea { get; set; }

        /// <summary>
        /// 特征提取结果
        /// </summary>
        [JsonPropertyName("detectionResult")]
        public string? DetectionResult { get; set; }       
    }

    /// <summary>
    /// 摄像头新增接口请求类
    /// </summary>
    public class AddCameraRequest
    {
        /// <summary>
        /// 基地
        /// </summary>
        [JsonPropertyName("productionBase")]
        public string? ProductionBase { get; set; }

        /// <summary>
        /// 拉线
        /// </summary>
        [JsonPropertyName("productionLine")]
        public string? ProductionLine { get; set; }

        /// <summary>
        /// 工序
        /// </summary>
        [JsonPropertyName("process")]
        public string? Process { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// 工位
        /// </summary>
        [JsonPropertyName("position")]
        public string? Position { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        /// <summary>
        /// 摄像头SN码
        /// </summary>
        [JsonPropertyName("cameraSn")]
        public string? CameraSn { get; set; }
    }

    /// <summary>
    /// 摄像头更新接口请求类
    /// </summary>
    public class UpdateCameraRequest
    {
        /// <summary>
        /// 基地
        /// </summary>
        [JsonPropertyName("productionBase")]
        public string? ProductionBase { get; set; }

        /// <summary>
        /// 拉线
        /// </summary>
        [JsonPropertyName("productionLine")]
        public string? ProductionLine { get; set; }

        /// <summary>
        /// 工序
        /// </summary>
        [JsonPropertyName("process")]
        public string? Process { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// 工位
        /// </summary>
        [JsonPropertyName("position")]
        public string? Position { get; set; }

        /// <summary>
        /// IP地址
        /// </summary>
        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        /// <summary>
        /// 摄像头SN码
        /// </summary>
        [JsonPropertyName("cameraSn")]
        public string? CameraSn { get; set; }

        /// <summary>
        /// 1表示是正常启用，0表示禁用
        /// </summary>
        [JsonPropertyName("enable")]
        public int Enable { get; set; }
    }

    /// <summary>
    /// UE端数据接收接请求类
    /// 备注：轨迹检测
    /// </summary>
    public class TtrajectoryDataRequest
    {
        /// <summary>
        /// 基地
        /// </summary>
        [JsonPropertyName("productionBase")]
        public string? ProductionBase { get; set; }

        /// <summary>
        /// 拉线
        /// </summary>
        [JsonPropertyName("productionLine")]
        public string? ProductionLine { get; set; }

        /// <summary>
        /// 工序
        /// </summary>
        [JsonPropertyName("process")]
        public string? Process { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// 工位
        /// </summary>
        [JsonPropertyName("position")]
        public string? Position { get; set; }

        /// <summary>
        /// 电芯条码
        /// </summary>
        [JsonPropertyName("barCode")]
        public string? BarCode { get; set; }

        /// <summary>
        /// 异常开始对应的时间戳
        /// </summary>
        [JsonPropertyName("abnormalTimeStamp")]
        public long AbnormalTimeStamp { get; set; }

        /// <summary>
        /// 异常结束对应的时间戳
        /// </summary>
        [JsonPropertyName("abnormalTimeStampEnd")]
        public long AbnormalTimeStampEnd { get; set; }

        /// <summary>
        /// 特征提取结果
        /// </summary>
        [JsonPropertyName("detectionResult")]
        public string? DetectionResult { get; set; }

        /// <summary>
        /// 报警等级，2轻微，1严重
        /// </summary>
        [JsonPropertyName("alertLevel")]
        public int AlertLevel { get; set; }
        
        /// <summary>
        /// 起止信号，1为开始，2为结束，其余为0
        /// </summary>
        [JsonPropertyName("statusFlag")]
        public int StatusFlag { get; set; }

        /// <summary>
        /// 轨迹坐标数据Json数据
        /// </summary>
        [JsonPropertyName("frameDtoList")]
        public List<FrameDtoList>? FrameDtoList { get; set; }
    }

    public class FrameDtoList
    {
        /// <summary>
        /// 每一帧的时间戳
        /// </summary>
        [JsonPropertyName("frameTimeStamp")]
        public long FrameTimeStamp { get; set; }
        /// <summary>
        /// 类别点位坐标
        /// </summary>
        [JsonPropertyName("categoryDtoList")]
        public List<CategoryDtoList>? CategoryDtoList { get; set; }
    }

    public class CategoryDtoList
    {
        /// <summary>
        /// 类别:电芯、夹爪、载具
        /// </summary>
        [JsonPropertyName("category")]
        public string? Category { get; set; }
        /// <summary>
        /// 轨迹坐标
        /// </summary>
        [JsonPropertyName("dataList")]
        public List<DataList>? DataList { get; set; }
    }

    public class DataList
    {
        /// <summary>
        /// 轨迹点位名称
        /// </summary>
        [JsonPropertyName("pointName")]
        public string? PointName { get; set; }

        /// <summary>
        /// 轨迹坐标
        /// </summary>
        [JsonPropertyName("aiPointXYZDtoList")]
        public List<AIPointXYZDto>? AiPointXYZDtoList { get; set; }
    }

    public class AIPointXYZDto
    {
        /// <summary>
        /// 实时标题
        /// </summary>
        [JsonPropertyName("realtimeTitle")]
        public string RealtimeTitle { get; set; } = "实时";

        /// <summary>
        /// x轴坐标
        /// </summary>
        [JsonPropertyName("x")]
        public float X { get; set; }

        /// <summary>
        /// y轴坐标
        /// </summary>
        [JsonPropertyName("y")]
        public float Y { get; set; }

        /// <summary>
        /// z轴坐标
        /// </summary>
        [JsonPropertyName("z")]
        public float Z { get; set; }

        /// <summary>
        /// 标准标题
        /// </summary>
        [JsonPropertyName("standardTitle")]
        public string StandardTitle { get; set; } = "标准";

        /// <summary>
        /// x轴坐标差异，无差异用0
        /// </summary>
        [JsonPropertyName("xDiff")]
        public float XDiff { get; set; }

        /// <summary>
        /// y轴坐标差异，无差异用0
        /// </summary>
        [JsonPropertyName("yDiff")]
        public float YDiff { get; set; }

        /// <summary>
        /// z轴坐标差异，无差异用0
        /// </summary>
        [JsonPropertyName("zDiff")]
        public float ZDiff { get; set; }

        /// <summary>
        /// 偏差值标题
        /// </summary>
        [JsonPropertyName("diffTitle")]
        public string DiffTitle { get; set; } = "偏差";

        /// <summary>
        /// x标准值
        /// </summary>
        [JsonPropertyName("xStandard")]
        public float XStandard { get; set; }

        /// <summary>
        /// y标准值
        /// </summary>
        [JsonPropertyName("yStandard")]
        public float YStandard { get; set; }

        /// <summary>
        /// z标准值
        /// </summary>
        [JsonPropertyName("zStandard")]
        public float ZStandard { get; set; }

        /// <summary>
        /// 是否异常，1正常，0异常
        /// </summary>
        [JsonPropertyName("abnormal")]
        public int Abnormal { get; set; }

        /// <summary>
        /// 图片帧对应的时间戳
        /// </summary>
        [JsonPropertyName("pictureTimeStamp")]
        public long PictureTimeStamp { get; set; }

        /// <summary>
        /// 速度
        /// </summary>
        [JsonPropertyName("speed")]
        public float Speed { get; set; }

        /// <summary>
        /// 移动方向
        /// </summary>
        [JsonPropertyName("moveDirection")]
        public string? MoveDirection { get; set; }        
    }

    /// <summary>
    /// 4. 实时轨迹接口文档-SOCKET
    /// </summary>
    public class HawkEyeSocketRequest
    {
        /// <summary>
        /// 基地
        /// </summary>
        [JsonPropertyName("productionBase")]
        public string? ProductionBase { get; set; }

        /// <summary>
        /// 拉线
        /// </summary>
        [JsonPropertyName("productionLine")]
        public string? ProductionLine { get; set; }

        /// <summary>
        /// 工序
        /// </summary>
        [JsonPropertyName("process")]
        public string? Process { get; set; }

        /// <summary>
        /// 设备ID
        /// </summary>
        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        /// <summary>
        /// 工位
        /// </summary>
        [JsonPropertyName("position")]
        public string? Position { get; set; }

        /// <summary>
        /// 电芯条码
        /// </summary>
        [JsonPropertyName("barCode")]
        public string? BarCode { get; set; }

        /// <summary>
        /// 完整性校验码
        /// </summary>
        [JsonPropertyName("checkSum")]
        public int CheckSum { get; set; }

        /// <summary>
        /// 图片对应的时间戳
        /// </summary>
        [JsonPropertyName("detectionTimeStamp")]
        public long DetectionTimeStamp { get; set; }

        /// <summary>
        /// 特征提取结果
        /// </summary>
        [JsonPropertyName("detectionResult")]
        public string? DetectionResult { get; set; }

        /// <summary>
        /// 报警等级，2轻微，1严重
        /// </summary>
        [JsonPropertyName("alertLevel")]
        public int AlertLevel { get; set; }

        /// <summary>
        /// 轨迹坐标数据Json数据
        /// </summary>
        [JsonPropertyName("frameDtoList")]
        public List<FrameDtoList>? FrameDtoList { get; set; }

        /// <summary>
        /// 起止信号，1为开始，2为结束，其余为0
        /// </summary>
        [JsonPropertyName("statusFlag")]
        public int StatusFlag { get; set; }
    }
    #endregion
}