using System.Text;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Core.Models.Project.CoatingOPMM
{
    /// <summary>
    /// 涂布协议解析数据类
    /// </summary>
    public class TBProtocolData
    {
        #region 缓存变量
        /// <summary>
        /// 解析的数据
        /// </summary>
        public string ProtocolData { get; }
        #endregion

        #region 报文常量
        /// <summary>
        /// 头部标识
        /// </summary>
        public const string Header = "\u0002";
        /// <summary>
        /// 分隔符
        /// </summary>
        public const string Interval = "\u0009";
        /// <summary>
        /// 尾部标识
        /// </summary>
        public const string Tail = "\u0003";
        #endregion

        #region 报文解析
        /// <summary>
        /// 消息序列
        /// </summary>
        public int MessageSequence { get; }
        /// <summary>
        /// 指令名称
        /// </summary>
        public string CommandName { get; }
        /// <summary>
        /// 涂布数据
        /// </summary>
        public string DataJson { get; }
        /// <summary>
        /// 涂布数据CRC验证
        /// </summary>
        public uint DataCrc { get; }

        #region 辅助标识
        /// <summary>
        /// 解析时是否需要Crc校验
        /// </summary>
        public bool NeedCrcCheck { get; } = false;
        /// <summary>
        /// 涂布数据CRC验证结果
        /// </summary>
        public bool DataCrcFlag { get; } = false;
        /// <summary>
        /// 涂布数据解析结果
        /// </summary>
        public bool AnalysisFlag { get; } = false;
        #endregion
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        private TBProtocolData(string protocolData, bool needCrcCheck = false)
        {
            ProtocolData = protocolData;
            NeedCrcCheck = needCrcCheck;

            if (string.IsNullOrEmpty(protocolData)) return;
            if (!protocolData.StartsWith(Header)) return;
            var _protocolData = protocolData.Remove(0, Header.Length);
            _protocolData = RemoveLast(_protocolData, Tail.Length);
            var dataArr = Regex.Split(_protocolData, Interval);
            if (dataArr.Length != 4) return;
            MessageSequence = int.Parse(dataArr[0]);
            CommandName = dataArr[1];
            DataJson = dataArr[2];
            DataCrc = uint.Parse(dataArr[3]);
            if (!needCrcCheck)
            {
                AnalysisFlag = true;
                return;
            }
            var dataCrc = Sunwoda_ArqCrcHelper.Get_Crc32(Encoding.UTF8.GetBytes(DataJson));
            if (dataCrc.Equals(DataCrc))
            {
                DataCrcFlag = true;
                AnalysisFlag = true;
            }
        }

        private TBProtocolData(int messageSequence, string commandName, string dataJson)
        {
            MessageSequence = messageSequence;
            CommandName = commandName;
            DataJson = dataJson;
            DataCrc = Sunwoda_ArqCrcHelper.Get_Crc32(Encoding.UTF8.GetBytes(dataJson));
            ProtocolData = Header + MessageSequence + Interval + CommandName + Interval + DataJson + Interval + DataCrc + Tail;
        }
        
        private static string RemoveLast(string value, int length)
        {
            if (value == null)
            {
                return null;
            }

            if (value.Length < length)
            {
                return string.Empty;
            }

            return value.Remove(value.Length - length);
        }

        /// <summary>
        /// 涂布协议解析
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static List<TBProtocolData> ProtocolAnalysis(string data, bool needCrcCheck = false)
        {
            var reLis = new List<TBProtocolData>();
            if (string.IsNullOrEmpty(data)) return reLis;
            var dataLis = Regex.Split(data, Tail).ToList();
            dataLis.ForEach(o =>
            {
                if (!string.IsNullOrEmpty(o))
                    reLis.Add(new TBProtocolData(o + Tail, needCrcCheck));
            });

            return reLis;
        }

        /// <summary>
        /// 拼接协议
        /// </summary>
        /// <param name="messageSequence">消息序列</param>
        /// <param name="commonandName">指令名称</param>
        /// <param name="dataJson">json数据</param>
        /// <returns></returns>
        public static string ProtocolSplicing(int messageSequence, string commonandName, string dataJson)
        {
            var tbpd = new TBProtocolData(messageSequence, commonandName, dataJson);
            return tbpd.ProtocolData;
        }
    }

    #region 指令
    #region 心跳指令
    /// <summary>
    /// 心跳数据
    /// </summary>
    public class CCTBHEARTQUEST
    {
        /// <summary>
        /// 设备编号
        /// </summary>
        public string DEVICESN { get; set; }
        /// <summary>
        /// 心跳信息(默认写值1)
        /// </summary>
        public string HEARTMESSAGE { get; set; }
    }
    #endregion

    #region 涂布重量数据
    /// <summary>
    /// 重量数据
    /// </summary>
    public class CCTBWEIGHTQUEST
    {
        /// <summary>
        /// 数据类型
        /// </summary>
        public string DEVICENAME { get; set; }
        /// <summary>
        /// 设备编号
        /// </summary>
        public string DEVICESN { get; set; }
        /// <summary>
        /// 工艺公差
        /// </summary>
        public string TECHNIQUE { get; set; }
        /// <summary>
        /// 净重目标值
        /// </summary>
        public string TARGET_VALUE { get; set; }
        /// <summary>
        /// 基材值
        /// </summary>
        public string BASEVALUE { get; set; }
        /// <summary>
        /// 净重规格上限
        /// </summary>
        public string SPEC_UP_LIM { get; set; }
        /// <summary>
        /// 净重规格下限
        /// </summary>
        public string SPEC_DW_LIM { get; set; }
        /// <summary>
        /// 净重目标预警公差
        /// </summary>
        public double NET_TARGETWARN_TOLERANCE { get; set; }
        /// <summary>
        /// 净重目标预警上限
        /// </summary>
        public double NET_TARGETWARN_UPPER { get; set; }
        /// <summary>
        /// 净重目标预警下限
        /// </summary>
        public double NET_TARGETWARN_LOWER { get; set; }
        /// <summary>
        /// CPK
        /// </summary>
        public double CPK { get; set; }
        /// <summary>
        /// COV
        /// </summary>
        public double COV { get; set; }
        /// <summary>
        /// 单趟标准差
        /// </summary>
        public double STDDEV { get; set; }
        /// <summary>
        /// K值
        /// </summary>
        public string K_VALUE { get; set; }
        /// <summary>
        /// b值
        /// </summary>
        public string B_VALUE { get; set; }
        /// <summary>
        /// 卷长
        /// </summary>
        public double VOL_LENGTH { get; set; }
        /// <summary>
        /// 平均值
        /// </summary>
        public string AVERAGE_VALUE { get; set; }
        /// <summary>
        /// 趟数
        /// </summary>
        public string SCANCOUNT { get; set; }
        /// <summary>
        /// 有效膜区分区
        /// </summary>
        public List<TBAreaInfo> TBAREAINFO { get; set; }
        /// <summary>
        /// 分区数
        /// </summary>
        public string WEIGHT_COUNT { get; set; }
        /// <summary>
        /// 重量数据(数组)
        /// </summary>
        public string[] WEIGHT { get; set; }
        /// <summary>
        /// 报警信息(同时可能有多条报警信息，没有报警为空列表)
        /// </summary>
        public List<string> ALARMS { get; set; }
    }

    /// <summary>
    /// 膜区分区信息
    /// </summary>
    public class TBAreaInfo
    {
        /// <summary>
        /// 膜区编号
        /// 备注：
        ///     保持和T块编号顺序一样
        /// </summary>
        public int AreaIndex { get; set; }
        /// <summary>
        /// 小分区个数
        /// </summary>
        public int AreaCount { get; set; }
        /// <summary>
        /// 膜区起点
        /// 备注：
        ///     界面配置的参数
        /// </summary>
        public int AreaHead { get; set; }
        /// <summary>
        /// 膜区终点
        /// 备注：
        ///     界面配置的参数
        /// </summary>
        public int AreaEnd { get; set; }
    }
    #endregion
    #endregion

    /// <summary>
    /// crc检验Helper
    /// </summary>
    public static class Sunwoda_ArqCrcHelper
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        static Sunwoda_ArqCrcHelper()
        {
            Init_CRC32_Table();
        }

        #region CRC32
        /// <summary>
        /// crc32表缓存
        /// </summary>
        private static readonly uint[] crc32_Table = new uint[256];

        /// <summary>
        /// 初始化CRC32表缓存
        /// </summary>
        private static void Init_CRC32_Table()
        {
            // Call this function only once to initialize the CRC table. 
            // This is the official polynomial used by CRC-32 
            // in PKZip, WinZip and Ethernet. 
            uint ulPolynomial = 0x04c11db7;

            // 256 values representing ASCII character codes. 
            for (int i = 0; i <= 0xFF; i++)
            {
                crc32_Table[i] = Reflect((uint)i, (char)8) << 24;
                for (int j = 0; j < 8; j++)
                {
                    //crc32_table[i] = ((crc32_table[i] << 1) ^ (crc32_table[i] & (1 << 31))) > 0 ? ulPolynomial : 0;
                    crc32_Table[i] = (crc32_Table[i] << 1) ^ ((crc32_Table[i] & (1 << 31)) > 0 ? ulPolynomial : 0);
                }

                crc32_Table[i] = Reflect(crc32_Table[i], (char)32);
            }
        }

        /// <summary>
        /// 反射
        /// </summary>
        /// <param name="re"></param>
        /// <param name="ch"></param>
        /// <returns></returns>
        private static uint Reflect(uint re, char ch)
        {
            uint value = 0;

            // Swap bit 0 for bit 7 
            // bit 1 for bit 6, etc. 
            for (int i = 1; i < (ch + 1); i++)
            {
                if (((re & 0x01) == 0x01))
                    value |= (uint)(1 << (ch - i));
                re >>= 1;
            }
            return value;
        }

        #region Method
        /// <summary>
        /// 对消息计算，获取crc32
        /// </summary>
        /// <param name="puchMsg">要进行CRC 校验的消息</param>
        /// <returns></returns>
        public static uint Get_Crc32(byte[] puchMsg)
        {
            return Get_Crc32(puchMsg, 0, (uint)puchMsg?.Length);
        }

        /// <summary>
        /// 对消息计算，获取crc32
        /// </summary>
        /// <param name="puchMsg">要进行CRC 校验的消息</param>
        /// <param name="usDataLen">消息中的字节数</param>
        /// <returns></returns>
        public static uint Get_Crc32(byte[] puchMsg, uint usDataLen)
        {
            return Get_Crc32(puchMsg, 0, usDataLen);
        }

        /// <summary>
        /// 对消息计算，获取crc32
        /// </summary>
        /// <param name="puchMsg">要进行CRC校验的消息</param>
        /// <param name="headIndex">消息的起始索引</param>
        /// <param name="usDataLen">从起始索引处截取的字节数</param>
        /// <returns></returns>
        public static uint Get_Crc32(byte[] puchMsg, uint headIndex, uint usDataLen)
        {
            if (puchMsg is null || puchMsg.Length <= 0)
            {
                throw new ArgumentException("校验消息不可为空！");
            }
            if (headIndex > puchMsg?.Length - 1)
            {
                throw new ArgumentException("起始索引超过消息实际字节长度！");
            }
            if (headIndex + usDataLen > puchMsg?.Length)
            {
                throw new ArgumentException("截取的字节数长度超过消息实际字节长度！");
            }

            uint ulCRC = 0xffffffff;
            int i = 0;
            while (usDataLen-- > 0)
            {
                ulCRC = (ulCRC >> 8) ^ crc32_Table[(ulCRC & 0xFF) ^ puchMsg[i++]];
            }
            // Exclusive OR the result with the beginning value. 
            return ulCRC ^ 0xffffffff;
        }
        #endregion
        #endregion

        #region CRC16
        /// <summary>
        /// CRC 高位字节值表
        /// </summary>
        private static readonly byte[] auchCRCHi = {
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40,
                0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1,
                0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
                0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
                0x80, 0x41, 0x00, 0xC1, 0x81, 0x40
            };

        /// <summary>
        /// CRC低位字节值表
        /// </summary>
        private static readonly byte[] auchCRCLo = {
                0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06,
                0x07, 0xC7, 0x05, 0xC5, 0xC4, 0x04, 0xCC, 0x0C, 0x0D, 0xCD,
                0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
                0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A,
                0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC, 0x14, 0xD4,
                0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
                0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3,
                0xF2, 0x32, 0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4,
                0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
                0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29,
                0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF, 0x2D, 0xED,
                0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
                0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60,
                0x61, 0xA1, 0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67,
                0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
                0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68,
                0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA, 0xBE, 0x7E,
                0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
                0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71,
                0x70, 0xB0, 0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92,
                0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
                0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B,
                0x99, 0x59, 0x58, 0x98, 0x88, 0x48, 0x49, 0x89, 0x4B, 0x8B,
                0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
                0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42,
                0x43, 0x83, 0x41, 0x81, 0x80, 0x40
            };

        #region Method
        /// <summary>
        /// 对消息计算，获取crc16
        /// </summary>
        /// <param name="puchMsg">要进行CRC 校验的消息</param>
        /// <returns></returns>
        public static ushort Get_Crc16(byte[] puchMsg)
        {
            return Get_Crc16(puchMsg, 0, (uint)puchMsg?.Length);
        }

        /// <summary>
        /// 对消息计算，获取crc16
        /// </summary>
        /// <param name="puchMsg">要进行CRC 校验的消息</param>
        /// <param name="usDataLen">从0索引处截取的字节数</param>
        /// <returns></returns>
        public static ushort Get_Crc16(byte[] puchMsg, uint usDataLen)
        {
            return Get_Crc16(puchMsg, 0, usDataLen);
        }

        /// <summary>
        /// 对消息计算，获取crc16
        /// </summary>
        /// <param name="puchMsg">要进行CRC 校验的消息</param>
        /// <param name="headIndex">消息的起始索引</param>
        /// <param name="usDataLen">从起始索引处截取的字节数</param>
        /// <returns></returns>
        public static ushort Get_Crc16(byte[] puchMsg, uint headIndex, uint usDataLen)
        {
            if (puchMsg is null || puchMsg.Length <= 0)
            {
                throw new ArgumentException("校验消息不可为空！");
            }
            if (headIndex > puchMsg?.Length - 1)
            {
                throw new ArgumentException("起始索引超过消息实际字节长度！");
            }
            if (headIndex + usDataLen > puchMsg?.Length)
            {
                throw new ArgumentException("截取的字节数长度超过消息实际字节长度！");
            }

            /* 高CRC字节初始化 */
            byte uchCRCHi = 0xFF;
            /* 低CRC 字节初始化 */
            byte uchCRCLo = 0xFF;
            /* CRC循环中的索引 */
            uint uIndex;
            int i = 0;
            while (usDataLen-- > 0) /* 传输消息缓冲区 */
            {
                uIndex = (uint)uchCRCHi ^ puchMsg[i++]; /* 计算CRC */
                uchCRCHi = (byte)(uchCRCLo ^ auchCRCHi[uIndex]);
                uchCRCLo = auchCRCLo[uIndex];
            }
            return (ushort)(uchCRCHi << 8 | uchCRCLo);
        }
        #endregion
        #endregion
    }
}
