using HslCommunication;
using HslCommunication.Core;
using HslCommunication.Core.Device;
using SUNWODA_SEVB.Core.Enumerations.PLC;


namespace SUNWODA_SEVB.Core.Models.PLC
{
    /// <summary>
    /// PLC 设备model
    /// </summary>
    public class PLCDevice : IDisposable
    {
        private DataSortRule byteDataSortRule;

        public int ID { get; set; }
        public string Name { get; set; }
        public PLCBrandSpecificationProtocal BrandSpecificationProtocal { get; set; }
        public DataFormat? ByteDataFormat { get; private set; } = null;

        public DataSortRule ByteDataSortRule
        {
            get => byteDataSortRule;
            set
            {
                byteDataSortRule = value;
                switch (byteDataSortRule)
                {
                    case DataSortRule.Default: ByteDataFormat = null; break;
                    case DataSortRule.ABCD: ByteDataFormat = DataFormat.ABCD; break;
                    case DataSortRule.BADC: ByteDataFormat = DataFormat.BADC; break;
                    case DataSortRule.CDAB: ByteDataFormat = DataFormat.CDAB; break;
                    case DataSortRule.DCBA: ByteDataFormat = DataFormat.DCBA; break;
                }
            }
        }

        public DeviceTcpNet? Device { get; set; }
        public int AddressWordLength { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public bool IsConnect { get; set; }
        public IByteTransform? ByteTransform { get; set; }
        public int CycleReadTime { get; set; }
        public int CycleWriteTime { get; set; }

        public PLCDevice(
            int id,
            string name,
            PLCBrandSpecificationProtocal brandSpecificationProtocal,
            string ip,
            DataSortRule byteDataSortRule = DataSortRule.Default,
            int cycleReadTime = 500,
            int cycleWriteTime = 500)
        {
            ID = id;
            Name = name;
            BrandSpecificationProtocal = brandSpecificationProtocal;
            IP = ip;
            ByteDataSortRule = byteDataSortRule;
            CycleReadTime = Math.Max(cycleReadTime, 25);
            CycleWriteTime = Math.Max(cycleWriteTime, 25);
        }

        public PLCDevice(
            int id,
            string name,
            PLCBrandSpecificationProtocal brandSpecificationProtocal,
            string ip,
            int port,
            DataSortRule byteDataSortRule = DataSortRule.Default,
            int cycleReadTime = 500,
            int cycleWriteTime = 500)
            : this(id, name, brandSpecificationProtocal, ip, byteDataSortRule, cycleReadTime, cycleWriteTime)
        {
            Port = port;
        }

        ~PLCDevice()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Device != null)
            {
                Device.ConnectClose();
                Device.Dispose();
                Device = null;
            }
        }

        public async Task<OperateResult<byte[]>?> ReadAsync(string areaStartAddress, ushort length)
        {
            if (Device is not null)
            {
                OperateResult<byte[]>? result = await Device.ReadAsync(areaStartAddress, length);
                IsConnect = result?.IsSuccess ?? false;
                if (!IsConnect)
                    throw new Exception($"读取PLC字节数组失败，原因：{result?.Message}");
                return IsConnect ? result : null;
            }
            IsConnect = false;
            return null;
        }

        public async Task<OperateResult> WriteAsync(string address, string valueType, dynamic value)
        {
            if (Device == null || value == null)
                return new OperateResult { IsSuccess = false, Message = "设备未初始化或值为空" };

            var expectedType = ConvertTypeKeyword(valueType);
            var actualType = value!.GetType().Name.ToUpperInvariant();

            if (expectedType != actualType && valueType.ToUpperInvariant() != actualType)
            {
                return new OperateResult
                {
                    IsSuccess = false,
                    Message = $"类型不匹配: 期望 {expectedType}, 实际 {actualType}",
                };
            }

            try
            {
                var result = await Device?.WriteAsync(address, value);
                IsConnect = result?.IsSuccess ?? false;
                return result ?? new OperateResult { IsSuccess = false };
            }
            catch (Exception ex)
            {
                return new OperateResult { IsSuccess = false, Message = ex.Message };
            }
        }

        private static string ConvertTypeKeyword(string keyword) =>
            keyword.ToUpperInvariant() switch
            {
                "FLOAT" => "SINGLE",
                "DOUBLE" => "DOUBLE",
                "BYTE" => "BYTE",
                "SHORT" => "INT16",
                "USHORT" => "UINT16",
                "INT" => "INT32",
                "UINT" => "UINT32",
                "LONG" => "INT64",
                "ULONG" => "UINT64",
                "BOOL" => "BOOLEAN",
                _ => "STRING",
            };
    }
}

