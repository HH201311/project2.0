namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
{
    /// <summary>
    /// PACK 六面检运行配置（南京）。
    /// 视觉：NJX13Pack；收图：Z 上盖 / T 底部 / S 四周；10 相机共 22 张 OK。
    /// </summary>
    public class Pack6Config
    {
        public const string VmClassName = "VM_NJSIXPage";

        /// <summary>结果与日志根目录（对应旧 LocalPathPre）。</summary>
        public string LocalPathPre { get; set; } = @"E:\六面检上位机软件";

        /// <summary>上盖 OK 根（现场 Z:，映射 \\10.101.109.61\OKImage）。配置键仍用 YOkRootPath 兼容旧库。</summary>
        public string YOkRootPath { get; set; } = @"Z:\";

        /// <summary>底部 OK 根（现场 T:）。配置键仍用 ZOkRootPath 兼容旧库。</summary>
        public string ZOkRootPath { get; set; } = @"T:\";

        /// <summary>兼容旧键：映射到上盖根 YOkRootPath。</summary>
        public string TopCamPath
        {
            get => YOkRootPath;
            set => YOkRootPath = value;
        }

        /// <summary>兼容旧键：映射到底部根 ZOkRootPath。</summary>
        public string BottomCamPath
        {
            get => ZOkRootPath;
            set => ZOkRootPath = value;
        }

        /// <summary>四周 OK 根（现场 S:）。</summary>
        public string SideCamPath { get; set; } = @"S:\";

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

        /// <summary>true=不调 NJX13Pack，走模拟；false=OCR 工位调真实算法。</summary>
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
        public string Workshop { get; set; } = "NJ";

        /// <summary>产品编码按位校正规则（算法 §4.3）。</summary>
        public string ProductSNRule { get; set; } = string.Empty;

        /// <summary>
        /// 白标签数模版本号期望值（字段⑤，如 M06）；与 DLL 输出 version 比对。
        /// 空串 = 不做版本校验。
        /// </summary>
        public string ExpectedWhiteLabelVersion { get; set; } = "M06";

        public int PartSNMinLength { get; set; } = 0;

        /// <summary>产品编码必须位数；默认 24。</summary>
        public int ProductSNExactLength { get; set; } = 24;

        public int ProductSNMinLength
        {
            get => ProductSNExactLength;
            set => ProductSNExactLength = value;
        }

        /// <summary>南京默认按相机+张序路由 OCR，关键词仅作非标准文件名兜底。</summary>
        public string WhiteLabelKeywords { get; set; } = "白标签;whiteLabel";
        public string NameplateKeywords { get; set; } = "铭牌;nameplate";

        public bool WarmupOnInit { get; set; } = false;

        // —— 条码规则：五种产品档位（名称 + 前 10 位）——

        /// <summary>
        /// 是否按产品档位校验 SN 前 10 位。南京暂无（界面隐藏），默认 false。
        /// </summary>
        public bool EnforceProductCodeProfile { get; set; } = false;

        /// <summary>当前产品编码档位显示名（如 TW国内两驱）；南京暂不强制校验。</summary>
        public string ProductCodeProfile { get; set; } = Pack6ProductCodeCatalog.Default.Name;

        /// <summary>当前档位对应前 10 位（只读推导；EnforceProductCodeProfile=false 时不参与判 NG）。</summary>
        public string ExpectedPrefix10 =>
            Pack6ProductCodeCatalog.Resolve(ProductCodeProfile).Prefix10;

        /// <summary>
        /// （已废弃回看补扫）保留配置键兼容；叠图已改临时目录，不再用本字段裁剪「标注预览」。
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

        // —— PLC 报警（欧姆龙 Fins TCP，平台 plc_config）——

        /// <summary>平台 plc_config.name，默认「六面检PLC」。</summary>
        public string PlcDeviceName { get; set; } = "六面检PLC";

        /// <summary>NG/缺张报警位；欧姆龙 Fins 如 D1996.0。</summary>
        public string PlcNgAlarmBitAddress { get; set; } = "D1996.0";

        /// <summary>本机心跳输出位（每秒翻转）；如 D1996.1。</summary>
        public string PlcHeartbeatOutBitAddress { get; set; } = "D1996.1";

        /// <summary>对方心跳输入位（读跳变）；如 D1996.2。</summary>
        public string PlcHeartbeatInBitAddress { get; set; } = "D1996.2";

        /// <summary>本机心跳翻转间隔（毫秒），默认 1000。</summary>
        public int PlcHeartbeatIntervalMs { get; set; } = 1000;

        /// <summary>对方心跳无跳变超时（毫秒），默认 5000 判断连。</summary>
        public int PlcHeartbeatTimeoutMs { get; set; } = 5000;
    }

    public enum Pack6OcrMode
    {
        None = 0,
        /// <summary>单独白标签四码（四周相机6 · Step4）。</summary>
        WhiteLabelThreeCode = 1,
        /// <summary>单独铭牌（保留枚举；南京主路径为同图双检）。</summary>
        NameplateProductSN = 2,
        /// <summary>同图：白标签 + 铭牌（四周相机2 · Step1）。</summary>
        WhiteLabelAndNameplate = 3
    }

    /// <summary>
    /// 检测通道（一检项一通道）。
    /// </summary>
    public enum Pack6DetectChannelKind
    {
        /// <summary>无视觉 DLL：仅收图齐套。</summary>
        FrameReceive = 0,
        /// <summary>四周相机6 Step4：单独白标签。</summary>
        WhiteLabel = 1,
        /// <summary>保留：单独铭牌（南京同图双检不走此通道）。</summary>
        Nameplate = 2,
        /// <summary>四周相机2 Step1：同图白标签+铭牌。</summary>
        WhiteLabelAndNameplate = 3
    }
}
