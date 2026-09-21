using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 一检项一通道：按文件名/张序路由到 FrameReceive / WhiteLabel / Nameplate（可扩展）。
    /// </summary>
    internal static class Pack6WorkClassifier
    {
        public static IReadOnlyList<Pack6DetectChannelKind> AllChannels { get; } =
        [
            Pack6DetectChannelKind.FrameReceive,
            Pack6DetectChannelKind.WhiteLabel,
            Pack6DetectChannelKind.Nameplate
        ];

        public static Pack6DetectChannelKind ResolveChannel(Pack6IngressItem item, Pack6Config config)
        {
            var mode = Pack6ZztwVisionEngine.ResolveOcrMode(item, config);
            return mode switch
            {
                Pack6OcrMode.WhiteLabelThreeCode => Pack6DetectChannelKind.WhiteLabel,
                Pack6OcrMode.NameplateProductSN => Pack6DetectChannelKind.Nameplate,
                _ => Pack6DetectChannelKind.FrameReceive
            };
        }

        public static string ChannelDisplayName(Pack6DetectChannelKind kind) => kind switch
        {
            Pack6DetectChannelKind.FrameReceive => "收图齐套",
            Pack6DetectChannelKind.WhiteLabel => "前1白标签",
            Pack6DetectChannelKind.Nameplate => "前3铭牌",
            _ => kind.ToString()
        };

        public static string ResolveProductKey(Pack6IngressItem item)
        {
            var key = item.ProductCode;
            if (string.IsNullOrWhiteSpace(key))
                key = Pack6FileNameParser.ResolveFolderSn(item.FilePath);
            if (string.IsNullOrWhiteSpace(key) &&
                Pack6FileNameParser.TryParseFileName(System.IO.Path.GetFileName(item.FilePath), out var p))
                key = p.ProductSn;
            if (string.IsNullOrWhiteSpace(key))
                key = System.IO.Path.GetFileName(item.FilePath) ?? "UNKNOWN";
            return key.Trim().ToUpperInvariant();
        }
    }
}
