using System.IO;
using System.Runtime.InteropServices;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;
using NJX13Pack;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// NJSIX 视觉引擎：对接 <c>NJX13Pack.AI</c>（白标签三码 / 铭牌产品编码）。
    /// 非 OCR 工位在真实模式下记为跳过（OK，无缺陷）；模拟模式仍走文件名 NG 注入。
    /// </summary>
    public sealed class Pack6NjtwVisionEngine : INJSIXPack6VisionEngine
    {
        private readonly ILoggerService<Pack6NjtwVisionEngine> _logger;
        private readonly object _aiLock = new();
        private Pack6Config? _config;
        private AI? _ai;
        private bool _warmedUp;
        private static bool _nativeSearchPathConfigured;

        public Pack6NjtwVisionEngine(ILoggerService<Pack6NjtwVisionEngine> logger)
        {
            _logger = logger;
        }

        public Task InitializeAsync(Pack6Config config, CancellationToken ct = default)
        {
            _config = config;
            Pack6ResultOverlayHelper.EnsureAndPrune(config);
            if (config.UseSimulation)
            {
                _logger.Info("Pack6 视觉：模拟模式（不加载 NJX13Pack）", false);
                return Task.CompletedTask;
            }

            EnsureHalconNativeRuntime();

            lock (_aiLock)
            {
                _ai ??= new AI();
            }

            _logger.Info("Pack6 视觉：已创建 NJX13Pack.AI（白标签/铭牌 OCR）", false);

            if (config.WarmupOnInit && !_warmedUp)
            {
                try
                {
                    Warmup();
                    _warmedUp = true;
                    _logger.Info("Pack6 视觉：OCR 预热完成", false);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Pack6 OCR 预热失败（首次真图仍会加载模型）: {ex.Message}", false);
                }
            }

            return Task.CompletedTask;
        }

        public Task<Pack6FrameResult> DetectAsync(Pack6IngressItem item, CancellationToken ct = default)
        {
            var config = _config ?? Pack6DefaultConfig.Create();
            if (config.UseSimulation || item.FilePath.StartsWith("sim://", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Simulate(item, config));

            var mode = ResolveOcrMode(item, config);
            if (mode == Pack6OcrMode.None)
            {
                return Task.FromResult(new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = item.ProductCode ?? "UNKNOWN",
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = true,
                    StationName = MapStation(item.QueueName, item.CameraName),
                    EngineMessage = "非OCR工位，本资料仅覆盖白标签/铭牌，已跳过",
                    OcrMode = Pack6OcrMode.None
                });
            }

            if (!File.Exists(item.FilePath))
            {
                return Task.FromResult(Fail(item, mode, "图像文件不存在", algorithmError: true, config));
            }

            try
            {
                EnsureHalconNativeRuntime();
                return Task.FromResult(mode switch
                {
                    Pack6OcrMode.WhiteLabelAndNameplate => DetectCombinedWhiteLabelAndNameplate(item, config),
                    Pack6OcrMode.WhiteLabelThreeCode => DetectWhiteLabel(item, config),
                    Pack6OcrMode.NameplateProductSN => DetectNameplate(item, config),
                    _ => Fail(item, mode, "未知OCR模式", algorithmError: true, config)
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"NJX13Pack 调用异常: {ex.Message}", false);
                return Task.FromResult(Fail(item, mode, $"异常: {ex.Message}", algorithmError: true, config));
            }
        }

        public void Shutdown()
        {
            lock (_aiLock)
            {
                _ai = null;
            }
            _config = null;
            _warmedUp = false;
        }

        private Pack6FrameResult DetectWhiteLabel(Pack6IngressItem item, Pack6Config config)
        {
            HObject? image = null;
            HObject? qrRegion = null;
            HObject? partRegion = null;
            HObject? productRegion = null;
            HObject? versionRegion = null;
            try
            {
                HOperatorSet.ReadImage(out image, item.FilePath);
                EnsureAi();
                string err;
                string qrData;
                string partSN;
                string productSN;
                string version;
                lock (_aiLock)
                {
                    err = _ai!.DecodeThreeCodesByOCR(
                        image,
                        config.ProductSNRule ?? string.Empty,
                        out qrRegion,
                        out partRegion,
                        out productRegion,
                        out versionRegion,
                        out qrData,
                        out partSN,
                        out productSN,
                        out version);
                }

                var defects = new List<string>();
                // 视觉 DLL 返回的任意非空 err 一律归「视觉检测异常」（非上位机业务校验）
                var dllError = !string.IsNullOrWhiteSpace(err);
                if (dllError)
                    defects.Add(Pack6ResultOverlayHelper.AlgorithmErrorBanner);

                if (config.PartSNMinLength > 0 &&
                    (string.IsNullOrWhiteSpace(partSN) || partSN.Length < config.PartSNMinLength))
                    defects.Add($"零部件序列码长度不符(实际={partSN?.Length ?? 0},最小={config.PartSNMinLength})");

                var boxes = BuildBoxes(
                    (qrRegion, TrimLabel(qrData), unchecked((int)0xFF00FF66), Pack6OverlayLabelPlacement.Above, 0f),
                    (partRegion, TrimLabel(partSN), unchecked((int)0xFFFFFF00), Pack6OverlayLabelPlacement.Right, 0f),
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below, 0f),
                    // 版本号略下移，减少与绿字/框上沿重叠
                    (versionRegion, TrimLabel(version), unchecked((int)0xFFFF8C00), Pack6OverlayLabelPlacement.Above, 14f));

                var frameOk = defects.Count == 0;
                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, boxes,
                    algorithmError: dllError,
                    detailMessage: dllError ? err : null,
                    ngBanner: frameOk || dllError ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk);

                var code = FirstNonEmpty(productSN, item.ProductCode, qrData, "UNKNOWN");
                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = code,
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = frameOk,
                    Defects = defects,
                    StationName = "三码校验",
                    OcrMode = Pack6OcrMode.WhiteLabelThreeCode,
                    QrData = qrData ?? string.Empty,
                    PartSN = partSN ?? string.Empty,
                    ProductSN = productSN ?? string.Empty,
                    Version = version ?? string.Empty,
                    EngineMessage = string.IsNullOrEmpty(err) ? "OK" : err,
                    PreviewImagePath = preview ?? string.Empty,
                    NgBanner = dllError
                        ? Pack6ResultOverlayHelper.AlgorithmErrorBanner
                        : (frameOk ? string.Empty : string.Join("；", defects.Take(3))),
                    OverlayBoxes = boxes,
                    IsVisionAlgorithmError = dllError
                };
            }
            finally
            {
                qrRegion?.Dispose();
                partRegion?.Dispose();
                productRegion?.Dispose();
                versionRegion?.Dispose();
                image?.Dispose();
            }
        }

        private Pack6FrameResult DetectNameplate(Pack6IngressItem item, Pack6Config config)
        {
            HObject? image = null;
            HObject? productRegion = null;
            try
            {
                HOperatorSet.ReadImage(out image, item.FilePath);
                EnsureAi();
                string err;
                string productSN;
                lock (_aiLock)
                {
                    err = _ai!.DecodeNameplateProductSNByOCR(
                        image,
                        config.ProductSNRule ?? string.Empty,
                        out productRegion,
                        out productSN);
                }

                var defects = new List<string>();
                // 视觉 DLL 返回的任意非空 err 一律归「视觉检测异常」
                var dllError = !string.IsNullOrWhiteSpace(err);
                if (dllError)
                    defects.Add(Pack6ResultOverlayHelper.AlgorithmErrorBanner);

                var boxes = BuildBoxes(
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below, 0f));

                var frameOk = defects.Count == 0;
                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, boxes,
                    algorithmError: dllError,
                    detailMessage: dllError ? err : null,
                    ngBanner: frameOk || dllError ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk);

                var code = FirstNonEmpty(productSN, item.ProductCode, "UNKNOWN");
                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = code,
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = frameOk,
                    Defects = defects,
                    StationName = "三码校验",
                    OcrMode = Pack6OcrMode.NameplateProductSN,
                    ProductSN = productSN ?? string.Empty,
                    EngineMessage = string.IsNullOrEmpty(err) ? "OK" : err,
                    PreviewImagePath = preview ?? string.Empty,
                    NgBanner = dllError
                        ? Pack6ResultOverlayHelper.AlgorithmErrorBanner
                        : (frameOk ? string.Empty : string.Join("；", defects.Take(3))),
                    OverlayBoxes = boxes,
                    IsVisionAlgorithmError = dllError
                };
            }
            finally
            {
                productRegion?.Dispose();
                image?.Dispose();
            }
        }

        /// <summary>同图双检：白标签四码 + 铭牌产品码（四周相机2 · Step1）。</summary>
        private Pack6FrameResult DetectCombinedWhiteLabelAndNameplate(Pack6IngressItem item, Pack6Config config)
        {
            HObject? image = null;
            HObject? qrRegion = null;
            HObject? partRegion = null;
            HObject? productRegion = null;
            HObject? versionRegion = null;
            HObject? nameplateRegion = null;
            try
            {
                HOperatorSet.ReadImage(out image, item.FilePath);
                EnsureAi();
                string errWl;
                string errNp;
                string qrData;
                string partSN;
                string productSN;
                string version;
                string nameplateSN;
                lock (_aiLock)
                {
                    errWl = _ai!.DecodeThreeCodesByOCR(
                        image,
                        config.ProductSNRule ?? string.Empty,
                        out qrRegion,
                        out partRegion,
                        out productRegion,
                        out versionRegion,
                        out qrData,
                        out partSN,
                        out productSN,
                        out version);
                    errNp = _ai!.DecodeNameplateProductSNByOCR(
                        image,
                        config.ProductSNRule ?? string.Empty,
                        out nameplateRegion,
                        out nameplateSN);
                }

                var defects = new List<string>();
                var dllError = !string.IsNullOrWhiteSpace(errWl) || !string.IsNullOrWhiteSpace(errNp);
                if (dllError)
                    defects.Add(Pack6ResultOverlayHelper.AlgorithmErrorBanner);

                if (config.PartSNMinLength > 0 &&
                    (string.IsNullOrWhiteSpace(partSN) || partSN.Length < config.PartSNMinLength))
                    defects.Add($"零部件序列码长度不符(实际={partSN?.Length ?? 0},最小={config.PartSNMinLength})");

                var boxes = BuildBoxes(
                    (qrRegion, TrimLabel(qrData), unchecked((int)0xFF00FF66), Pack6OverlayLabelPlacement.Above, 0f),
                    (partRegion, TrimLabel(partSN), unchecked((int)0xFFFFFF00), Pack6OverlayLabelPlacement.Right, 0f),
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below, 0f),
                    // 橙字 M06 略下移；紫字铭牌码略上移，减少同图叠字冲突
                    (versionRegion, TrimLabel(version), unchecked((int)0xFFFF8C00), Pack6OverlayLabelPlacement.Above, 14f),
                    (nameplateRegion, TrimLabel(nameplateSN), unchecked((int)0xFFFF66FF), Pack6OverlayLabelPlacement.Right, -22f));

                var frameOk = defects.Count == 0;
                var engMsg = dllError
                    ? string.Join("；", new[] { errWl, errNp }.Where(s => !string.IsNullOrWhiteSpace(s)))
                    : "OK";
                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, boxes,
                    algorithmError: dllError,
                    detailMessage: dllError ? engMsg : null,
                    ngBanner: frameOk || dllError ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk);

                var code = FirstNonEmpty(productSN, nameplateSN, item.ProductCode, qrData, "UNKNOWN");
                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = code,
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = frameOk,
                    Defects = defects,
                    StationName = "三码校验",
                    OcrMode = Pack6OcrMode.WhiteLabelAndNameplate,
                    QrData = qrData ?? string.Empty,
                    PartSN = partSN ?? string.Empty,
                    ProductSN = productSN ?? string.Empty,
                    NameplateProductSN = nameplateSN ?? string.Empty,
                    Version = version ?? string.Empty,
                    EngineMessage = engMsg,
                    PreviewImagePath = preview ?? string.Empty,
                    NgBanner = dllError
                        ? Pack6ResultOverlayHelper.AlgorithmErrorBanner
                        : (frameOk ? string.Empty : string.Join("；", defects.Take(3))),
                    OverlayBoxes = boxes,
                    IsVisionAlgorithmError = dllError
                };
            }
            finally
            {
                qrRegion?.Dispose();
                partRegion?.Dispose();
                productRegion?.Dispose();
                versionRegion?.Dispose();
                nameplateRegion?.Dispose();
                image?.Dispose();
            }
        }

        private void EnsureAi()
        {
            lock (_aiLock)
            {
                _ai ??= new AI();
            }
        }

        private void Warmup()
        {
            // 空图预热可能失败；用极小占位：若失败仅记日志。资料要求首次调用加载模型。
            EnsureAi();
            HOperatorSet.GenEmptyObj(out HObject empty);
            try
            {
                lock (_aiLock)
                {
                    _ = _ai!.DecodeNameplateProductSNByOCR(
                        empty,
                        _config?.ProductSNRule ?? string.Empty,
                        out var region,
                        out _);
                    region?.Dispose();
                }
            }
            finally
            {
                empty.Dispose();
            }
        }

        private static Pack6FrameResult Simulate(Pack6IngressItem item, Pack6Config config)
        {
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            var forceNg = name.Contains("NG", StringComparison.OrdinalIgnoreCase)
                          || name.Contains("缺陷", StringComparison.OrdinalIgnoreCase);
            var defects = forceNg
                ? new List<string> { $"{MapStation(item.QueueName, item.CameraName)}模拟NG" }
                : new List<string>();

            var mode = ResolveOcrMode(item, config);
            var sn = item.ProductCode ?? "UNKNOWN";
            return new Pack6FrameResult
            {
                FilePath = item.FilePath,
                QueueName = item.QueueName,
                ProductCode = sn,
                CameraName = item.CameraName,
                ShotIndex = item.ShotIndex,
                FolderSn = sn,
                IsOk = defects.Count == 0,
                Defects = defects,
                StationName = mode == Pack6OcrMode.None
                    ? MapStation(item.QueueName, item.CameraName)
                    : "三码校验",
                FromSimulation = true,
                OcrMode = mode,
                ProductSN = mode == Pack6OcrMode.None ? string.Empty : sn,
                NameplateProductSN = mode == Pack6OcrMode.WhiteLabelAndNameplate ? sn : string.Empty,
                QrData = mode is Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.WhiteLabelAndNameplate ? sn : string.Empty,
                PartSN = mode is Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.WhiteLabelAndNameplate ? "1234567890" : string.Empty,
                Version = mode is Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.WhiteLabelAndNameplate
                    ? (string.IsNullOrWhiteSpace(config.ExpectedWhiteLabelVersion)
                        ? "M06"
                        : config.ExpectedWhiteLabelVersion.Trim().ToUpperInvariant())
                    : string.Empty,
                EngineMessage = "模拟"
            };
        }

        private static Pack6FrameResult Fail(
            Pack6IngressItem item,
            Pack6OcrMode mode,
            string message,
            bool algorithmError = false,
            Pack6Config? config = null)
        {
            string preview = string.Empty;
            if (algorithmError && config is not null && File.Exists(item.FilePath))
            {
                preview = Pack6ResultOverlayHelper.TryWrite(
                    config,
                    item,
                    item.FilePath,
                    Array.Empty<Pack6OverlayBox>(),
                    algorithmError: true,
                    detailMessage: message) ?? string.Empty;
            }

            return new Pack6FrameResult
            {
                FilePath = item.FilePath,
                QueueName = item.QueueName,
                ProductCode = item.ProductCode ?? "UNKNOWN",
                CameraName = item.CameraName,
                ShotIndex = item.ShotIndex,
                FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                IsOk = false,
                Defects = [algorithmError ? Pack6ResultOverlayHelper.AlgorithmErrorBanner : message],
                StationName = mode == Pack6OcrMode.None
                    ? MapStation(item.QueueName, item.CameraName)
                    : "三码校验",
                OcrMode = mode,
                EngineMessage = message,
                PreviewImagePath = preview,
                IsVisionAlgorithmError = algorithmError
            };
        }

        private static IReadOnlyList<Pack6OverlayBox> BuildBoxes(
            params (HObject? Region, string Label, int Argb, Pack6OverlayLabelPlacement Placement, float OffsetY)[] items)
        {
            var list = new List<Pack6OverlayBox>(items.Length);
            foreach (var (region, label, argb, placement, offsetY) in items)
            {
                if (!TryGetRect(region, out var x, out var y, out var w, out var h))
                    continue;
                list.Add(new Pack6OverlayBox
                {
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    Label = label,
                    Argb = argb,
                    LabelPlacement = placement,
                    LabelOffsetY = offsetY
                });
            }
            return list;
        }

        private static bool TryGetRect(HObject? region, out int x, out int y, out int w, out int h)
        {
            x = y = w = h = 0;
            if (region is null)
                return false;
            try
            {
                if (!region.IsInitialized())
                    return false;
                HOperatorSet.SmallestRectangle1(region, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                if (row1.Length < 1)
                    return false;
                y = (int)Math.Round(row1.D);
                x = (int)Math.Round(col1.D);
                var y2 = (int)Math.Round(row2.D);
                var x2 = (int)Math.Round(col2.D);
                w = Math.Max(1, x2 - x + 1);
                h = Math.Max(1, y2 - y + 1);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string TrimLabel(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "(空)";
            var t = text.Trim();
            return t.Length <= 48 ? t : t[..45] + "…";
        }

        internal static Pack6OcrMode ResolveOcrMode(Pack6IngressItem item, Pack6Config config)
        {
            ResolveCameraShot(item, out var cam, out var shot);

            // 南京：四周相机2 · Step1 = 白标签+铭牌同图；四周相机6 · Step4 = 单独白标签
            if (string.Equals(cam, "四周相机2", StringComparison.Ordinal) && shot == 1)
                return Pack6OcrMode.WhiteLabelAndNameplate;
            if (string.Equals(cam, "四周相机6", StringComparison.Ordinal) && shot == 4)
                return Pack6OcrMode.WhiteLabelThreeCode;

            // 标准十相机命名已解析但非码检工位
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            if (Pack6CameraCatalog.TryGetByCameraName(cam, out _))
                return Pack6OcrMode.None;

            // 非标准命名才用关键词兜底
            var probe = BuildKeywordProbe(name, item.ProductCode);
            if (Hit(probe, config.WhiteLabelKeywords) && Hit(probe, config.NameplateKeywords))
                return Pack6OcrMode.WhiteLabelAndNameplate;
            if (Hit(probe, config.WhiteLabelKeywords))
                return Pack6OcrMode.WhiteLabelThreeCode;
            if (Hit(probe, config.NameplateKeywords))
                return Pack6OcrMode.NameplateProductSN;

            return Pack6OcrMode.None;
        }

        /// <summary>同图双检机位（四周相机2 · Step1）。</summary>
        internal static bool IsCombinedWhiteLabelAndNameplate(Pack6IngressItem item)
        {
            ResolveCameraShot(item, out var cam, out var shot);
            return string.Equals(cam, "四周相机2", StringComparison.Ordinal) && shot == 1;
        }

        /// <summary>单独白标签机位（四周相机6 · Step4）。</summary>
        internal static bool IsSoloWhiteLabel(Pack6IngressItem item)
        {
            ResolveCameraShot(item, out var cam, out var shot);
            return string.Equals(cam, "四周相机6", StringComparison.Ordinal) && shot == 4;
        }

        private static void ResolveCameraShot(Pack6IngressItem item, out string cam, out int shot)
        {
            cam = item.CameraName ?? string.Empty;
            shot = item.ShotIndex;
            // 入队时已按盘角色解析；此处以 Ingress 字段为准
        }

        /// <summary>关键词匹配用片段。</summary>
        private static string BuildKeywordProbe(string fileName, string? productCode)
        {
            if (!string.IsNullOrWhiteSpace(productCode) &&
                fileName.StartsWith(productCode, StringComparison.OrdinalIgnoreCase) &&
                fileName.Length > productCode.Length)
                return fileName[productCode.Length..];

            return fileName;
        }

        private static bool Hit(string fileName, string keywordsCsv)
        {
            if (string.IsNullOrWhiteSpace(keywordsCsv) || string.IsNullOrWhiteSpace(fileName))
                return false;
            return keywordsCsv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => k.Length >= 2)
                .Any(k => fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        internal static string MapStation(string queueName, string? cameraName = null)
        {
            if (!string.IsNullOrWhiteSpace(cameraName) &&
                Pack6CameraCatalog.TryGetByCameraName(cameraName, out var byCam))
                return byCam.DisplayTitle;
            if (Pack6CameraCatalog.ByQueueName.TryGetValue(queueName, out var byQ))
                return byQ.DisplayTitle;
            return queueName;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return "UNKNOWN";
        }

        /// <summary>
        /// Halcon 原生库在 {程序目录}\Dlls\。.NET 8 下仅 SetDllDirectory 常不够，
        /// 必须用完整路径 LoadLibrary 预加载，否则报 0x8007007E。
        /// </summary>
        private void EnsureHalconNativeRuntime()
        {
            if (_nativeSearchPathConfigured)
                return;

            var dllsDir = Path.Combine(AppContext.BaseDirectory, "Dlls");
            var halconInDlls = Path.Combine(dllsDir, "halcon.dll");
            var halconBesideExe = Path.Combine(AppContext.BaseDirectory, "halcon.dll");
            var halconPath = File.Exists(halconInDlls)
                ? halconInDlls
                : (File.Exists(halconBesideExe) ? halconBesideExe : null);

            if (halconPath is null)
            {
                throw new FileNotFoundException(
                    "未找到 halcon.dll。请把完整程序里的 Dlls 目录拷到 exe 同级（含 halcon.dll、libiomp5md.dll、license.dat）。仅覆盖 SUNWODA_SEVB.dll 不够。");
            }

            var nativeDir = Path.GetDirectoryName(halconPath)!;

            // 1) 搜索路径
            if (!SetDllDirectory(nativeDir))
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Warn($"SetDllDirectory(\"{nativeDir}\") 失败，Win32={err}", false);
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!pathEnv.Split(Path.PathSeparator)
                    .Any(p => string.Equals(p, nativeDir, StringComparison.OrdinalIgnoreCase)))
            {
                Environment.SetEnvironmentVariable(
                    "PATH",
                    nativeDir + Path.PathSeparator + pathEnv);
            }

            // 2) 按依赖顺序完整路径预加载（.NET 8 P/Invoke 关键）
            //    halcon.dll 必须成功；其余缺失仅告警。
            string? halconLoadError = null;
            foreach (var name in new[]
                     {
                         "libiomp5md.dll",
                         "halconc.dll",
                         "halcon.dll",
                         "halconcpp.dll",
                         "hcanvas.dll"
                     })
            {
                var full = Path.Combine(nativeDir, name);
                if (!File.Exists(full))
                {
                    if (name.Equals("halcon.dll", StringComparison.OrdinalIgnoreCase))
                        halconLoadError = $"文件不存在: {full}";
                    continue;
                }

                var handle = LoadLibrary(full);
                if (handle == IntPtr.Zero)
                {
                    var err = Marshal.GetLastWin32Error();
                    var msg = $"预加载失败 {name}，Win32={err}";
                    if (name.Equals("halcon.dll", StringComparison.OrdinalIgnoreCase))
                        halconLoadError = msg + "（检查 VC++ x64 运行库 / Dlls 是否齐全）";
                    else
                        _logger.Warn(msg, false);
                }
                else
                {
                    _logger.Info($"已预加载 {full}", false);
                }
            }

            if (halconLoadError is not null)
            {
                throw new DllNotFoundException(
                    $"无法加载 Halcon 原生库（{halconLoadError}）。请确认 exe 同级存在完整 Dlls\\（含 halcon.dll、libiomp5md.dll），勿只覆盖 SUNWODA_SEVB.dll。");
            }

            EnsureHalconLicenseEnvironment(nativeDir);
            try
            {
                // 关闭 Halcon 临时/全局内存缓存，避免长时间 OCR 进程常驻内存只涨
                HOperatorSet.SetSystem("temporary_mem_cache", "false");
                HOperatorSet.SetSystem("global_mem_cache", "idle");
            }
            catch (Exception ex)
            {
                _logger.Warn($"设置 Halcon 内存策略失败: {ex.Message}", false);
            }

            _nativeSearchPathConfigured = true;
            _logger.Info($"Pack6 Halcon 原生运行时就绪：{nativeDir}", false);
        }

        private void EnsureHalconLicenseEnvironment(string dllsDir)
        {
            var existing = Environment.GetEnvironmentVariable("HALCON_LICENSE_FILE");
            if (!string.IsNullOrWhiteSpace(existing) && File.Exists(existing))
                return;

            var candidates = new[]
            {
                Path.Combine(dllsDir, "license.dat"),
                Path.Combine(dllsDir, "license", "license.dat"),
                Path.Combine(dllsDir, "license", "license_22.11.dat"),
            };
            var licensePath = candidates.FirstOrDefault(File.Exists);
            if (licensePath is null)
            {
                _logger.Warn($"未在 {dllsDir} 找到 Halcon license.dat，OCR 可能因无许可失败", false);
                return;
            }

            Environment.SetEnvironmentVariable("HALCON_LICENSE_FILE", licensePath);
            _logger.Info($"Pack6 已设置 HALCON_LICENSE_FILE={licensePath}", false);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}
