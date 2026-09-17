namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>
    /// PACK 六面检运行配置。
    /// 视觉：ZZTWPack（前1 白标签 / 前3 铭牌）；收图：Y/Z OK原图 + 12 相机。
    /// </summary>
    public class Pack6Config
    {
        public const string VmClassName = "VM_ZZSIXPage";

        /// <summary>结果与日志根目录（对应旧 LocalPathPre）。</summary>
        public string LocalPathPre { get; set; } = @"D:\六面检上位机软件";

        /// <summary>Y 盘 OK 原图根（现场 \\192.168.100.8）。</summary>
        public string YOkRootPath { get; set; } = @"Y:\OK原图";

        /// <summary>Z 盘 OK 原图根（现场 \\192.168.100.6）。</summary>
        public string ZOkRootPath { get; set; } = @"Z:\OK原图";

        /// <summary>兼容旧键：映射到 YOkRootPath（读库后由 ConfigService 处理）。</summary>
        public string TopCamPath
        {
            get => YOkRootPath;
            set => YOkRootPath = value;
        }

        /// <summary>兼容旧键：映射到 ZOkRootPath。</summary>
        public string BottomCamPath
        {
            get => ZOkRootPath;
            set => ZOkRootPath = value;
        }

        /// <summary>兼容旧键：侧面已并入 Y/Z，保留字段避免旧库报错。</summary>
        public string SideCamPath { get; set; } = string.Empty;

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

        /// <summary>true=不调 ZZTWPack，走模拟；false=OCR 工位调真实算法。</summary>
        public bool UseSimulation { get; set; } = false;

        /// <summary>Pack 未齐套超时（分钟，兼容旧键；调度以 PackMissingTimeoutSeconds 为准）。</summary>
        public int PackTimeoutMinutes { get; set; } = 10;

        /// <summary>缺张最长等待秒数；默认 600（10 分钟）。</summary>
        public int PackMissingTimeoutSeconds { get; set; } = 600;

        public string PlcHeartBeatKey { get; set; } = "PLC_HeartBeat";
        public string AlarmSignalKey { get; set; } = "AlarmSignal";

        public string CellType { get; set; } = "TW";
        /// <summary>资料适用工位 P05。</summary>
        public string ProductionLine { get; set; } = "P05";
        public string Workshop { get; set; } = "ZZ";

        /// <summary>产品编码按位校正规则（算法 §4.3）。</summary>
        public string ProductSNRule { get; set; } = string.Empty;

        public int PartSNMinLength { get; set; } = 0;

        /// <summary>产品编码必须位数；默认 24。</summary>
        public int ProductSNExactLength { get; set; } = 24;

        public int ProductSNMinLength
        {
            get => ProductSNExactLength;
            set => ProductSNExactLength = value;
        }

        public string WhiteLabelKeywords { get; set; } = "白标签;whiteLabel;前-1;前1;Front1";
        public string NameplateKeywords { get; set; } = "铭牌;nameplate;前-3;前3;Front3";

        public bool WarmupOnInit { get; set; } = false;

        // —— 条码规则：五种产品档位（名称 + 前 10 位）——

        /// <summary>当前产品编码档位显示名（如 TW国内两驱）。</summary>
        public string ProductCodeProfile { get; set; } = Pack6ProductCodeCatalog.Default.Name;

        /// <summary>当前档位对应前 10 位（只读推导，便于日志/叠字）。</summary>
        public string ExpectedPrefix10 =>
            Pack6ProductCodeCatalog.Resolve(ProductCodeProfile).Prefix10;

        /// <summary>
        /// （已废弃回看补扫）现仅作标注预览目录保留天数基数（实际约基数×2）。
        /// </summary>
        public int IngressLookbackDays { get; set; } = 2;

        /// <summary>（已废弃）定时补扫间隔秒；收图仅 FileSystemWatcher，不再轮询。</summary>
        public int IngressPollSeconds { get; set; } = 60;

        // —— 图像归档（对齐枣庄 原始图片/结果图片）——

        /// <summary>是否保存 OK 结果图。</summary>
        public bool StoreResultImageOK { get; set; } = true;

        /// <summary>是否保存 NG 结果图。</summary>
        public bool StoreResultImageNG { get; set; } = true;

        /// <summary>图像至少保留年数。</summary>
        public int ImageArchiveRetainYears { get; set; } = 2;

        /// <summary>磁盘占用超过该百分比则删除最早日期图像（1–99）。</summary>
        public int ImageArchiveDiskUsagePercent { get; set; } = 90;
    }

    public enum Pack6OcrMode
    {
        None = 0,
        WhiteLabelThreeCode = 1,
        NameplateProductSN = 2
    }

    /// <summary>
    /// 检测通道（一检项一通道）。日后新 DLL 在此增枚举并注册工人即可。
    /// </summary>
    public enum Pack6DetectChannelKind
    {
        /// <summary>无视觉 DLL：仅收图齐套。</summary>
        FrameReceive = 0,
        /// <summary>前1 白标签 OCR。</summary>
        WhiteLabel = 1,
        /// <summary>前3 铭牌 OCR。</summary>
        Nameplate = 2
    }
}
