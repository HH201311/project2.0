using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient
{
    public sealed class MeasurementFrame
    { // ==================== 协议常量 ====================
        public static ReadOnlySpan<byte> StartMarker => new byte[] { 0xB6, 0x1D, 0x27, 0xF6, 0xDB, 0x7D, 0xF2, 0x3F };
        public static ReadOnlySpan<byte> EndMarker => new byte[] { 0x71, 0xD4, 0x45, 0x0B, 0x58, 0x14, 0x21, 0x40 };

        public const int HeaderSize = 8;
        public const int DescriptionSize = 24;
        public const int DataLengthFieldSize = 4;  // uint
        public const int CrcSize = 4;
        public static readonly int MinFrameLength = HeaderSize + DescriptionSize + DataLengthFieldSize + CrcSize + StartMarker.Length + EndMarker.Length;

        // ==================== 帧数据 ====================
        public FrameHeader Header { get; }
        public string Description { get; }
        public uint DataLength { get; }
        public byte[] Data { get; }
        public uint Crc { get; }

        // ==================== 构造函数（仅内部构造，外部通过工厂方法创建） ====================
        private MeasurementFrame(FrameHeader header, string description, uint dataLength, byte[] data, uint crc)
        {
            Header = header;
            Description = description;
            DataLength = dataLength;
            Data = data ?? Array.Empty<byte>();
            Crc = crc;
        }

        /// <summary>帧头 8 字节结构（不可变记录结构体）</summary>
        public readonly record struct FrameHeader(byte H0, byte H1, byte H2, byte H3, byte H4, byte H5, byte H6, byte H7)
        {
            /// <summary>
            /// 常用的“读取数据请求”帧头，对应字符串 "12R00000"
            /// </summary>
            public static FrameHeader ReadRequest { get; } = new(1, 2, (byte)'R', 0, 0, 0, 0, 0);

            public void WriteTo(Span<byte> destination)
            {
                Debug.Assert(destination.Length >= 8);
                destination[0] = H0;
                destination[1] = H1;
                destination[2] = H2;
                destination[3] = H3;
                destination[4] = H4;
                destination[5] = H5;
                destination[6] = H6;
                destination[7] = H7;
            }

            public static FrameHeader ReadFrom(ReadOnlySpan<byte> source) =>
                new(source[0], source[1], source[2], source[3], source[4], source[5], source[6], source[7]);
        }

        // ==================== 工厂方法：创建请求/响应帧 ====================
        public static MeasurementFrame Create(FrameHeader header, string description, ReadOnlySpan<byte> data)
        {
            byte[] payload = data.ToArray();
            return new MeasurementFrame(header, description, (uint)payload.Length, payload, 0); // CRC 在序列化时计算
        }

        // ==================== 序列化 ====================
        public byte[] Serialize()
        {
            int totalLength = StartMarker.Length + HeaderSize + DescriptionSize + DataLengthFieldSize + Data.Length + CrcSize + EndMarker.Length;
            byte[] buffer = new byte[totalLength];
            Span<byte> span = buffer;

            // 1. 起始标识
            StartMarker.CopyTo(span);
            int offset = StartMarker.Length;

            // 2. 帧头
            Header.WriteTo(span.Slice(offset, HeaderSize));
            offset += HeaderSize;

            // 3. 头说明信息（24字节ASCII，右补空格）
            string desc = (Description ?? "").PadRight(DescriptionSize)[..DescriptionSize];
            Encoding.ASCII.GetBytes(desc, span.Slice(offset, DescriptionSize));
            offset += DescriptionSize;

            // 4. 数据长度（小端）
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, DataLengthFieldSize), (uint)Data.Length);
            offset += DataLengthFieldSize;

            // 5. 数据
            if (Data.Length > 0)
            {
                Data.CopyTo(span.Slice(offset, Data.Length));
                offset += Data.Length;
            }

            // 6. CRC32（覆盖起始标识到数据末尾）
            var arqCRC32 = new ArqCRC32();
            var crc = arqCRC32.Get_CRC(span[..offset].ToArray(), offset);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, CrcSize), crc);
            offset += CrcSize;

            // 7. 结束标识
            EndMarker.CopyTo(span.Slice(offset));

            return buffer;
        }

        // ==================== 反序列化：TryParse 避免异常驱动 ====================
        public static bool TryParse(ReadOnlySpan<byte> buffer, out MeasurementFrame? frame, out string error)
        {
            frame = null;
            error = string.Empty;

            if (buffer.Length < MinFrameLength)
            {
                error = "数据长度不足最小帧长";
                return false;
            }

            // 验证起始/结束标识
            if (!buffer[..StartMarker.Length].SequenceEqual(StartMarker))
            {
                error = "起始标识不匹配";
                return false;
            }
            if (!buffer[^EndMarker.Length..].SequenceEqual(EndMarker))
            {
                error = "结束标识不匹配";
                return false;
            }

            int offset = StartMarker.Length;

            // 头信息
            var header = FrameHeader.ReadFrom(buffer.Slice(offset, HeaderSize));
            offset += HeaderSize;

            // 头说明信息（去除尾部空格）
            string description = Encoding.ASCII.GetString(buffer.Slice(offset, DescriptionSize)).TrimEnd();
            offset += DescriptionSize;

            // 数据长度
            uint dataLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, DataLengthFieldSize));
            offset += DataLengthFieldSize;

            // 总长度校验
            int expectedTotal = StartMarker.Length + HeaderSize + DescriptionSize + DataLengthFieldSize + (int)dataLength + CrcSize + EndMarker.Length;
            if (buffer.Length != expectedTotal)
            {
                error = $"帧总长度不匹配，期望: {expectedTotal}, 实际: {buffer.Length}";
                return false;
            }

            // 数据段
            byte[] data = dataLength > 0 ? buffer.Slice(offset, (int)dataLength).ToArray() : Array.Empty<byte>();
            offset += (int)dataLength;

            // CRC32 读取与校验
            uint receivedCrc = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, CrcSize));
            var arqCRC32 = new ArqCRC32();
            var calculatedCrc = arqCRC32.Get_CRC(buffer[..offset].ToArray(), offset);
            if (calculatedCrc != receivedCrc)
            {
                error = $"CRC校验失败，计算值: 0x{calculatedCrc:X8}, 接收值: 0x{receivedCrc:X8}";
                return false;
            }

            frame = new MeasurementFrame(header, description, dataLength, data, receivedCrc);
            return true;
        }

        /// <summary>将数据段解析为单精度浮点数组</summary>
        public float[] ToFloatArray()
        {
            if (Data.Length % 4 != 0)
                throw new InvalidOperationException("数据长度不是4的倍数，无法转换为float数组");
            float[] floats = new float[Data.Length / 4];
            Buffer.BlockCopy(Data, 0, floats, 0, Data.Length);
            return floats;
        }

        public override string ToString() =>
            $"H0:{Header.H0} H1:{Header.H1} H2:{(char)Header.H2} H3:{Header.H3} " +
            $"Desc:\"{Description}\" DataLen:{DataLength} CRC:0x{Crc:X8}";
    }
}