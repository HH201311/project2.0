using System;

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
}