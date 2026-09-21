namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>条码校验结果（含可贴图左上角的 NG 文案）。</summary>
    public sealed class Pack6BarcodeRuleResult
    {
        public bool IsOk => Failures.Count == 0;
        public List<string> Failures { get; } = [];
        /// <summary>预览图左上角 NG 提示；空表示无需叠字。</summary>
        public string NgBanner { get; set; } = string.Empty;
    }

    /// <summary>
    /// PACK 编码校验：先一致（文件夹 / 白标签产品码 / 铭牌 / 二维码），再长度 24 + 前 10 位档位前缀。
    /// </summary>
    public static class Pack6BarcodeRuleValidator
    {
        /// <summary>
        /// 对当前 OCR 帧校验。
        /// siblingProductSn / siblingQr：同 Pack 另一路已检结果；未齐套时传空。
        /// </summary>
        public static Pack6BarcodeRuleResult Validate(
            string? productSn,
            string? folderSn,
            string? siblingProductSn,
            string? qrData,
            string? siblingQrData,
            Pack6OcrMode currentMode,
            Pack6Config config)
        {
            var result = new Pack6BarcodeRuleResult();
            var sn = Norm(productSn);
            var folder = Norm(folderSn);
            var sibling = Norm(siblingProductSn);
            var qr = Norm(qrData);
            var siblingQr = Norm(siblingQrData);

            // 白标签帧：本帧二维码；铭牌帧：用已存的白标签二维码
            var effectiveQr = currentMode == Pack6OcrMode.WhiteLabelThreeCode ? qr : siblingQr;

            var three = ValidateThreeCode(folder, sn, sibling, effectiveQr, currentMode);
            if (!three.IsOk)
            {
                foreach (var f in three.Failures)
                    result.Failures.Add(f);
                result.NgBanner = three.NgBanner;
                return result;
            }

            var rule = ValidatePrefixRule(sn, config);
            foreach (var f in rule.Failures)
                result.Failures.Add(f);
            result.NgBanner = rule.NgBanner;
            return result;
        }

        public static Pack6BarcodeRuleResult ValidateThreeCode(
            string folder,
            string currentSn,
            string siblingSn,
            string qrData,
            Pack6OcrMode currentMode)
        {
            var result = new Pack6BarcodeRuleResult();
            if (string.IsNullOrEmpty(currentSn))
            {
                result.Failures.Add("三码校验失败:当前OCR产品码为空");
                result.NgBanner = "NG 三码校验\n当前OCR为空";
                return result;
            }

            if (currentMode == Pack6OcrMode.WhiteLabelThreeCode && string.IsNullOrEmpty(qrData))
            {
                result.Failures.Add("三码校验失败:二维码为空");
                result.NgBanner = "NG 三码校验\n二维码为空";
                return result;
            }

            var white = currentMode == Pack6OcrMode.WhiteLabelThreeCode ? currentSn : siblingSn;
            var nameplate = currentMode == Pack6OcrMode.NameplateProductSN ? currentSn : siblingSn;

            // 所有非空方必须相等（文件夹 / 白标签产品码 / 铭牌 / 二维码）
            var parties = new List<(string Name, string Value)>();
            if (!string.IsNullOrEmpty(folder))
                parties.Add(("文件夹", folder));
            parties.Add(("产品码", currentSn));
            if (!string.IsNullOrEmpty(siblingSn))
                parties.Add(("对照路", siblingSn));
            if (!string.IsNullOrEmpty(qrData))
                parties.Add(("二维码", qrData));

            var mismatch = parties.Any(p => !string.Equals(p.Value, currentSn, StringComparison.Ordinal));
            if (!mismatch)
                return result;

            var folderText = string.IsNullOrEmpty(folder) ? "(无)" : folder;
            var whiteText = string.IsNullOrEmpty(white) ? "(未检)" : white;
            var nameText = string.IsNullOrEmpty(nameplate) ? "(未检)" : nameplate;
            var qrText = string.IsNullOrEmpty(qrData) ? "(无/未检)" : qrData;

            result.Failures.Add(
                $"三码不一致:文件夹={folderText},白标签={whiteText},铭牌={nameText},二维码={qrText}");
            result.NgBanner =
                $"NG 三码不一致\n文件夹={folderText}\n白标签={whiteText}\n铭牌={nameText}\n二维码={qrText}";
            return result;
        }

        public static Pack6BarcodeRuleResult ValidatePrefixRule(string sn, Pack6Config config)
        {
            var result = new Pack6BarcodeRuleResult();
            var profile = Pack6ProductCodeCatalog.Resolve(config.ProductCodeProfile);
            var expectLen = config.ProductSNExactLength > 0 ? config.ProductSNExactLength : 24;
            var expectPrefix = profile.Prefix10;

            if (sn.Length != expectLen)
            {
                result.Failures.Add($"长度不符:实际={sn.Length},要求={expectLen}");
                result.NgBanner =
                    $"NG 长度不符\n要求={expectLen}\n扫出={sn}\n长度={sn.Length}";
                return result;
            }

            var actualPrefix = sn.Length >= Pack6ProductCodeCatalog.PrefixLength
                ? sn[..Pack6ProductCodeCatalog.PrefixLength]
                : sn;
            if (!string.Equals(actualPrefix, expectPrefix, StringComparison.Ordinal))
            {
                result.Failures.Add(
                    $"前十位不符:实际={actualPrefix},要求={expectPrefix}({profile.Name})");
                result.NgBanner =
                    $"NG 前十位不符\n要求={expectPrefix}\n扫出={sn}";
            }

            return result;
        }

        private static string Norm(string? s) =>
            (s ?? string.Empty).Trim().ToUpperInvariant();
    }
}
