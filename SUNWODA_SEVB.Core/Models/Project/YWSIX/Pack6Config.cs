namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
{
    /// <summary>
    /// PACK 六面检运行配置（义乌 YWSIX）。
    /// 视觉：YWX01Pack；收图：单一 SaveImage 根；6 相机共 31 张 OK。
    /// </summary>
    public class Pack6Config
    {
        public const string VmClassName = "VM_YWSIXPage";

        /// <summary>结果与日志根目录。</summary>
        public string LocalPathPre { get; set; } = @"E:\六面检上位机软件";

        /// <summary>
        /// 收图根目录：\\服务器\SaveImage\SaveImage\{日期}\{SN}\{相机}\原始图像\
        /// </summary>
        public string SaveImageRoot { get; set; } = @"\\10.130.105.114\SaveImage\SaveImage";

        /// <summary>上盖 YOLO 模型（相对 exe 或绝对路径）。默认 Dlls\YWSIX\YW_X01_TOP.onnx。</summary>
        public string TopCoverModelPath { get; set; } = @"Dlls\YWSIX\YW_X01_TOP.onnx";

        /// <summary>上盖 DetectTopCover 是否 GPU。</summary>
        public bool TopCoverUseGpu { get; set; } = false;

        // —— 连接器 PIN 歪斜阈值（文档示例，功能设置可改）——
        public double PinLongAngleMax { get; set; } = 2.0;
        public double PinLongColDiffMax { get; set; } = 8.0;
        public double PinLongRowDiffMin { get; set; } = 240.0;
        public double PinLongRowDiffMax { get; set; } = 260.0;
        public double PinShortRowDiffMax { get; set; } = 5.0;
        public double PinShortColDiffMin { get; set; } = 45.0;
        public double PinShortColDiffMax { get; set; } = 65.0;

        /// <summary>兼容配置键：映射到 SaveImageRoot。</summary>
        public string YOkRootPath
        {
            get => SaveImageRoot;
            set => SaveImageRoot = value;
        }

        /// <summary>兼容配置键：映射到 SaveImageRoot。</summary>
        public string ZOkRootPath
        {
            get => SaveImageRoot;
            set => SaveImageRoot = value;
        }

        /// <summary>兼容旧键：映射到 SaveImageRoot。</summary>
        public string TopCamPath
        {
            get => SaveImageRoot;
            set => SaveImageRoot = value;
        }

        /// <summary>兼容旧键：映射到 SaveImageRoot。</summary>
        public string BottomCamPath
        {
            get => SaveImageRoot;
            set => SaveImageRoot = value;
        }

        /// <summary>兼容配置键：映射到 SaveImageRoot。</summary>
        public string SideCamPath
        {
            get => SaveImageRoot;
            set => SaveImageRoot = value;
        }

        public string ImageFilter { get; set; } = "*.jpg;*.bmp;*.png;*.jpeg";

        public string TopCamFilter
        {
            get => ImageFilter;
            set => ImageFilter = value;
        }

        public string BottomCamFilter
        {
            get => ImageFilter;
            set => ImageFilter = value;
        }

        public string SideCamFilter
        {
            get => ImageFilter;
            set => ImageFilter = value;
        }

        /// <summary>true=不调 YWX01Pack，走模拟。</summary>
        public bool UseSimulation { get; set; } = false;

        public int PackTimeoutMinutes { get; set; } = 10;

        /// <summary>缺张最长等待秒数；默认 600（10 分钟）。</summary>
        public int PackMissingTimeoutSeconds { get; set; } = 600;

        public string PlcHeartBeatKey { get; set; } = "PLC_HeartBeat";
        public string AlarmSignalKey { get; set; } = "AlarmSignal";

        public string CellType { get; set; } = "TW";
        /// <summary>算法资料适用工位 P03。</summary>
        public string ProductionLine { get; set; } = "P03";
        public string Workshop { get; set; } = "YW";

        /// <summary>产品编码按位校正规则。</summary>
        public string ProductSNRule { get; set; } = string.Empty;

        /// <summary>白标签数模版本号期望值；空串=不校验。义乌默认 M00。</summary>
        public string ExpectedWhiteLabelVersion { get; set; } = "M00";

        public int PartSNMinLength { get; set; } = 0;

        /// <summary>产品编码必须位数；默认 24。</summary>
        public int ProductSNExactLength { get; set; } = 24;

        public int ProductSNMinLength
        {
            get => ProductSNExactLength;
            set => ProductSNExactLength = value;
        }

        public string WhiteLabelKeywords { get; set; } = "白标签;whiteLabel";
        public string NameplateKeywords { get; set; } = "铭牌;nameplate";

        public bool WarmupOnInit { get; set; } = false;

        public bool EnforceProductCodeProfile { get; set; } = false;

        public string ProductCodeProfile { get; set; } = Pack6ProductCodeCatalog.Default.Name;

        public string ExpectedPrefix10 =>
            Pack6ProductCodeCatalog.Resolve(ProductCodeProfile).Prefix10;

        public int IngressLookbackDays { get; set; } = 2;

        public int IngressPollSeconds { get; set; } = 60;

        public bool StoreResultImageOK { get; set; } = true;
        public bool StoreResultImageNG { get; set; } = true;
        public int ImageArchiveRetainYears { get; set; } = 2;
        public int ImageArchiveDiskUsagePercent { get; set; } = 90;

        public string PlcDeviceName { get; set; } = "六面检PLC";
        public string PlcNgAlarmBitAddress { get; set; } = "D1996.0";
        public string PlcHeartbeatOutBitAddress { get; set; } = "D1996.1";
        public string PlcHeartbeatInBitAddress { get; set; } = "D1996.2";
        public int PlcHeartbeatIntervalMs { get; set; } = 1000;
        public int PlcHeartbeatTimeoutMs { get; set; } = 5000;
    }

    public enum Pack6OcrMode
    {
        None = 0,
        /// <summary>白标签四码（后侧-1 / 右侧-2）。</summary>
        WhiteLabelThreeCode = 1,
        /// <summary>铭牌（后侧-3）。</summary>
        NameplateProductSN = 2,
        /// <summary>同图白标签+铭牌（义乌不用）。</summary>
        WhiteLabelAndNameplate = 3,
        /// <summary>上盖 YOLO（顶面 1～4）。</summary>
        TopCover = 4,
        /// <summary>连接器 PIN 歪斜（后侧-2）。</summary>
        ConnectorPinSkew = 5
    }

    public enum Pack6DetectChannelKind
    {
        FrameReceive = 0,
        WhiteLabel = 1,
        Nameplate = 2,
        WhiteLabelAndNameplate = 3,
        TopCover = 4,
        ConnectorPinSkew = 5
    }
}
