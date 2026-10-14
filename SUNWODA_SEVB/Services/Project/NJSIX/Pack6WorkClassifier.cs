using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// 一检项一通道：收图齐套 / 同图双检(四周2·Step1) / 单独白标签(四周6·Step4)。
    /// </summary>
    internal static class Pack6WorkClassifier
    {
        public static IReadOnlyList<Pack6DetectChannelKind> AllChannels { get; } =
        [
            Pack6DetectChannelKind.FrameReceive,
            Pack6DetectChannelKind.WhiteLabelAndNameplate,
            Pack6DetectChannelKind.WhiteLabel
        ];

        public static Pack6DetectChannelKind ResolveChannel(Pack6IngressItem item, Pack6Config config)
        {
            var mode = Pack6NjtwVisionEngine.ResolveOcrMode(item, config);
            return mode switch
            {
                Pack6OcrMode.WhiteLabelAndNameplate => Pack6DetectChannelKind.WhiteLabelAndNameplate,
                Pack6OcrMode.WhiteLabelThreeCode => Pack6DetectChannelKind.WhiteLabel,
                Pack6OcrMode.NameplateProductSN => Pack6DetectChannelKind.Nameplate,
                _ => Pack6DetectChannelKind.FrameReceive
            };
        }

        public static string ChannelDisplayName(Pack6DetectChannelKind kind) => kind switch
        {
            Pack6DetectChannelKind.FrameReceive => "收图齐套",
            Pack6DetectChannelKind.WhiteLabelAndNameplate => "三码校验(同图双检)",
            Pack6DetectChannelKind.WhiteLabel => "三码校验(单独白标签)",
            Pack6DetectChannelKind.Nameplate => "三码校验(铭牌)",
            _ => kind.ToString()
        };

        public static string ResolveProductKey(Pack6IngressItem item)
        {
            var key = item.ProductCode;
            if (string.IsNullOrWhiteSpace(key))
                key = Pack6FileNameParser.ResolveFolderSn(item.FilePath);
            if (string.IsNullOrWhiteSpace(key) &&
                Pack6FileNameParser.TryParseIngress(item.FilePath, GuessRole(item), out var p))
                key = p.ProductSn;
            if (string.IsNullOrWhiteSpace(key))
                key = System.IO.Path.GetFileName(item.FilePath) ?? "UNKNOWN";
            return key.Trim().ToUpperInvariant();
        }

        private static string GuessRole(Pack6IngressItem item)
        {
            if (Pack6CameraCatalog.TryGetByCameraName(item.CameraName, out var def))
                return def.DiskHint;
            return "Side";
        }
    }
}
