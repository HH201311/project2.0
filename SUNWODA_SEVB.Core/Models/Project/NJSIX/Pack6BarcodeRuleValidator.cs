namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
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
    /// PACK 编码校验：以<strong>文件夹码</strong>为基准；
    /// 本帧产品码 / 白标签二维码各自只与文件夹比对（谁错谁 NG），再校验长度 24；
    /// 前 10 位档位仅当 <see cref="Pack6Config.EnforceProductCodeProfile"/> 为 true；
    /// 白标签另比：序列码第 3 位 == 产品码第 16 位（下标 2 与 15）；
    /// 白标签版本号 == 配置期望值（默认 M06）。
    /// </summary>
    public static class Pack6BarcodeRuleValidator
    {
        /// <summary>序列码取位（从 1 数第 3 位 → 下标 2）。</summary>
        public const int PartSnCrossCheckOrdinal = 3;

        /// <summary>产品码取位（从 1 数第 16 位 → 下标 15）。</summary>
        public const int ProductSnCrossCheckOrdinal = 16;

        /// <summary>
        /// 对当前 OCR 帧校验（仅与文件夹比对，不做跨图互比）。
        /// </summary>
        public static Pack6BarcodeRuleResult Validate(
            string? productSn,
            string? folderSn,
            string? qrData,
            Pack6OcrMode currentMode,
            Pack6Config config,
            string? partSn = null,
            string? version = null,
            string? nameplateProductSn = null)
        {
            var result = new Pack6BarcodeRuleResult();
            var sn = Norm(productSn);
            var folder = Norm(folderSn);
            var qr = Norm(qrData);
            var part = Norm(partSn);
            var ver = Norm(version);
            var nameplate = Norm(nameplateProductSn);

            // 同图双检：白标签规则 + 铭牌与文件夹比对
            if (currentMode == Pack6OcrMode.WhiteLabelAndNameplate)
            {
                var wl = ValidateCoreWhiteLabel(sn, folder, qr, part, ver, config);
                if (!wl.IsOk)
                    return wl;
                var np = ValidateAgainstFolder(folder, nameplate, qrData: string.Empty, Pack6OcrMode.NameplateProductSN);
                if (!np.IsOk)
                    return np;
                var prefix = ValidatePrefixRule(folder, config);
                return prefix.IsOk ? result : prefix;
            }

            var three = ValidateAgainstFolder(folder, sn, qr, currentMode);
            if (!three.IsOk)
            {
                foreach (var f in three.Failures)
                    result.Failures.Add(f);
                result.NgBanner = three.NgBanner;
                return result;
            }

            var rule = ValidatePrefixRule(folder, config);
            if (!rule.IsOk)
            {
                foreach (var f in rule.Failures)
                    result.Failures.Add(f);
                result.NgBanner = rule.NgBanner;
                return result;
            }

            if (currentMode == Pack6OcrMode.WhiteLabelThreeCode)
            {
                var cross = ValidatePartProductCrossDigit(part, sn);
                if (!cross.IsOk)
                {
                    foreach (var f in cross.Failures)
                        result.Failures.Add(f);
                    result.NgBanner = cross.NgBanner;
                    return result;
                }

                var verRule = ValidateWhiteLabelVersion(ver, config.ExpectedWhiteLabelVersion);
                if (!verRule.IsOk)
                {
                    foreach (var f in verRule.Failures)
                        result.Failures.Add(f);
                    result.NgBanner = verRule.NgBanner;
                    return result;
                }
            }

            return result;
        }

        private static Pack6BarcodeRuleResult ValidateCoreWhiteLabel(
            string sn,
            string folder,
            string qr,
            string part,
            string ver,
            Pack6Config config)
        {
            var folderCheck = ValidateAgainstFolder(folder, sn, qr, Pack6OcrMode.WhiteLabelThreeCode);
            if (!folderCheck.IsOk)
                return folderCheck;

            var prefix = ValidatePrefixRule(folder, config);
            if (!prefix.IsOk)
                return prefix;

            var cross = ValidatePartProductCrossDigit(part, sn);
            if (!cross.IsOk)
                return cross;

            return ValidateWhiteLabelVersion(ver, config.ExpectedWhiteLabelVersion);
        }

        /// <summary>
        /// 兼容旧签名：sibling 参数忽略（统一改以文件夹为准）。
        /// </summary>
        [Obsolete("请改用无 sibling 的 Validate 重载；sibling 已不再参与比对。")]
        public static Pack6BarcodeRuleResult Validate(
            string? productSn,
            string? folderSn,
            string? siblingProductSn,
            string? qrData,
            string? siblingQrData,
            Pack6OcrMode currentMode,
            Pack6Config config) =>
            Validate(productSn, folderSn, qrData, currentMode, config);

        /// <summary>本帧产品码、白标签二维码分别只与文件夹比对。</summary>
        public static Pack6BarcodeRuleResult ValidateAgainstFolder(
            string folder,
            string currentSn,
            string qrData,
            Pack6OcrMode currentMode)
        {
            var result = new Pack6BarcodeRuleResult();

            if (string.IsNullOrEmpty(folder))
            {
                result.Failures.Add("三码校验失败:文件夹码为空");
                result.NgBanner = "NG 三码校验\n文件夹码为空";
                return result;
            }

            if (string.IsNullOrEmpty(currentSn))
            {
                result.Failures.Add("三码校验失败:当前OCR产品码为空");
                result.NgBanner = "NG 三码校验\n当前OCR为空";
                return result;
            }

            if (!string.Equals(currentSn, folder, StringComparison.Ordinal))
            {
                result.Failures.Add($"产品码与文件夹不一致:文件夹={folder},产品码={currentSn}");
                result.NgBanner = $"NG 产品码≠文件夹\n文件夹={folder}\n产品码={currentSn}";
                return result;
            }

            // 白标签：二维码也必须与文件夹一致
            if (currentMode is Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.WhiteLabelAndNameplate)
            {
                if (string.IsNullOrEmpty(qrData))
                {
                    result.Failures.Add("三码校验失败:二维码为空");
                    result.NgBanner = "NG 三码校验\n二维码为空";
                    return result;
                }

                if (!string.Equals(qrData, folder, StringComparison.Ordinal))
                {
                    result.Failures.Add($"二维码与文件夹不一致:文件夹={folder},二维码={qrData}");
                    result.NgBanner = $"NG 二维码≠文件夹\n文件夹={folder}\n二维码={qrData}";
                    return result;
                }
            }

            return result;
        }

        /// <summary>兼容旧名：内部改为仅与文件夹比对。</summary>
        [Obsolete("请改用 ValidateAgainstFolder。")]
        public static Pack6BarcodeRuleResult ValidateThreeCode(
            string folder,
            string currentSn,
            string siblingSn,
            string qrData,
            Pack6OcrMode currentMode) =>
            ValidateAgainstFolder(folder, currentSn, qrData, currentMode);

        public static Pack6BarcodeRuleResult ValidatePrefixRule(string sn, Pack6Config config)
        {
            var result = new Pack6BarcodeRuleResult();
            var expectLen = config.ProductSNExactLength > 0 ? config.ProductSNExactLength : 24;

            if (sn.Length != expectLen)
            {
                result.Failures.Add($"长度不符:实际={sn.Length},要求={expectLen}");
                result.NgBanner =
                    $"NG 长度不符\n要求={expectLen}\n扫出={sn}\n长度={sn.Length}";
                return result;
            }

            // 南京暂不核产品档位前缀
            if (!config.EnforceProductCodeProfile)
                return result;

            var profile = Pack6ProductCodeCatalog.Resolve(config.ProductCodeProfile);
            var expectPrefix = profile.Prefix10;
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

        /// <summary>
        /// 白标签交叉位：序列码第 <see cref="PartSnCrossCheckOrdinal"/> 位
        /// 须等于产品码第 <see cref="ProductSnCrossCheckOrdinal"/> 位（下标分别为 -1）。
        /// </summary>
        public static Pack6BarcodeRuleResult ValidatePartProductCrossDigit(string partSn, string productSn)
        {
            var result = new Pack6BarcodeRuleResult();
            var partIdx = PartSnCrossCheckOrdinal - 1;
            var prodIdx = ProductSnCrossCheckOrdinal - 1;

            if (string.IsNullOrEmpty(partSn) || partSn.Length <= partIdx)
            {
                result.Failures.Add(
                    $"三码校验失败:序列码不足{PartSnCrossCheckOrdinal}位(实际={partSn?.Length ?? 0})");
                result.NgBanner =
                    $"NG 序列码位数不足\n要求≥{PartSnCrossCheckOrdinal}\n序列码={partSn}";
                return result;
            }

            if (string.IsNullOrEmpty(productSn) || productSn.Length <= prodIdx)
            {
                result.Failures.Add(
                    $"三码校验失败:产品码不足{ProductSnCrossCheckOrdinal}位(实际={productSn?.Length ?? 0})");
                result.NgBanner =
                    $"NG 产品码位数不足\n要求≥{ProductSnCrossCheckOrdinal}\n产品码={productSn}";
                return result;
            }

            var a = partSn[partIdx];
            var b = productSn[prodIdx];
            if (a != b)
            {
                result.Failures.Add(
                    $"序列码第{PartSnCrossCheckOrdinal}位≠产品码第{ProductSnCrossCheckOrdinal}位:序列={a},产品={b}");
                result.NgBanner =
                    $"NG 交叉位不符\n序列码第{PartSnCrossCheckOrdinal}位={a}\n产品码第{ProductSnCrossCheckOrdinal}位={b}";
            }

            return result;
        }

        /// <summary>
        /// 白标签数模版本号：扫出 == 配置期望（如 M00）。期望为空则跳过。
        /// </summary>
        public static Pack6BarcodeRuleResult ValidateWhiteLabelVersion(string actual, string? expected)
        {
            var result = new Pack6BarcodeRuleResult();
            var expect = Norm(expected);
            if (string.IsNullOrEmpty(expect))
                return result;

            if (string.IsNullOrEmpty(actual))
            {
                result.Failures.Add("三码校验失败:版本号为空");
                result.NgBanner = $"NG 版本号为空\n要求={expect}";
                return result;
            }

            if (!string.Equals(actual, expect, StringComparison.Ordinal))
            {
                result.Failures.Add($"版本号不符:实际={actual},要求={expect}");
                result.NgBanner = $"NG 版本号不符\n要求={expect}\n扫出={actual}";
            }

            return result;
        }

        private static string Norm(string? s) =>
            (s ?? string.Empty).Trim().ToUpperInvariant();
    }
}
