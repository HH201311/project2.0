using System.IO;
using System.Runtime.InteropServices;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.YWSIX;
using SUNWODA_SEVB.Core.Models.Project.YWSIX;
using YWX01Pack;

namespace SUNWODA_SEVB.Services.Project.YWSIX
{
    /// <summary>
    /// 义乌视觉引擎：对接 <c>YWX01Pack.AI</c>
    /// （白标签四码 / 铭牌 / 上盖 YOLO / 连接器 PIN）。
    /// </summary>
    public sealed class Pack6Ywx01VisionEngine : IYWSIXPack6VisionEngine
    {
        private readonly ILoggerService<Pack6Ywx01VisionEngine> _logger;
        private readonly object _aiLock = new();
        private Pack6Config? _config;
        private AI? _ai;
        private static bool _nativeSearchPathConfigured;

        public Pack6Ywx01VisionEngine(ILoggerService<Pack6Ywx01VisionEngine> logger)
        {
            _logger = logger;
        }

        public Task InitializeAsync(Pack6Config config, CancellationToken ct = default)
        {
            _config = config;
            Pack6ResultOverlayHelper.EnsureAndPrune(config);
            if (config.UseSimulation)
            {
                _logger.Info("YWSIX 视觉：模拟模式（不加载 YWX01Pack）", false);
                return Task.CompletedTask;
            }

            EnsureNativeRuntime();
            lock (_aiLock)
            {
                _ai ??= new AI();
            }

            _logger.Info("YWSIX 视觉：已创建 YWX01Pack.AI", false);
            return Task.CompletedTask;
        }

        public void RefreshRuntimeConfig(Pack6Config config)
        {
            if (config is null)
                return;
            _config = config;
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
                    EngineMessage = "非算法工位，仅收图齐套",
                    OcrMode = Pack6OcrMode.None
                });
            }

            if (!File.Exists(item.FilePath))
                return Task.FromResult(Fail(item, mode, "图像文件不存在", algorithmError: true, config));

            try
            {
                EnsureNativeRuntime();
                return Task.FromResult(mode switch
                {
                    Pack6OcrMode.WhiteLabelThreeCode => DetectWhiteLabel(item, config),
                    Pack6OcrMode.NameplateProductSN => DetectNameplate(item, config),
                    Pack6OcrMode.TopCover => DetectTopCover(item, config),
                    Pack6OcrMode.ConnectorPinSkew => DetectPinSkew(item, config),
                    _ => Fail(item, mode, "未知检测模式", algorithmError: true, config)
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"YWX01Pack 调用异常: {ex.Message}", false);
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
        }

        /// <summary>
        /// 后侧-1 / 右侧-2 → 白标签；后侧-3 → 铭牌；后侧-2 → PIN；
        /// 顶面 1～4 → 上盖；顶面 5/6 与其余 → 仅齐套。
        /// </summary>
        internal static Pack6OcrMode ResolveOcrMode(Pack6IngressItem item, Pack6Config config)
        {
            _ = config;
            var cam = item.CameraName ?? string.Empty;
            var shot = item.ShotIndex;

            if (string.Equals(cam, "后侧相机", StringComparison.Ordinal))
            {
                return shot switch
                {
                    1 => Pack6OcrMode.WhiteLabelThreeCode,
                    2 => Pack6OcrMode.ConnectorPinSkew,
                    3 => Pack6OcrMode.NameplateProductSN,
                    _ => Pack6OcrMode.None
                };
            }

            if (string.Equals(cam, "右侧相机", StringComparison.Ordinal) && shot == 2)
                return Pack6OcrMode.WhiteLabelThreeCode;

            if (string.Equals(cam, "顶面相机", StringComparison.Ordinal))
            {
                if (Pack6TopCoverExpectations.IsSkippedShot(shot))
                    return Pack6OcrMode.None;
                if (Pack6TopCoverExpectations.TryGet(shot, out _))
                    return Pack6OcrMode.TopCover;
            }

            return Pack6OcrMode.None;
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
                var dllError = !string.IsNullOrWhiteSpace(err);
                if (dllError)
                    defects.Add(Pack6ResultOverlayHelper.AlgorithmErrorBanner);

                var boxes = BuildBoxes(
                    (qrRegion, TrimLabel(qrData), unchecked((int)0xFF00FF66), Pack6OverlayLabelPlacement.Above, 0f),
                    (partRegion, TrimLabel(partSN), unchecked((int)0xFFFFFF00), Pack6OverlayLabelPlacement.Right, 0f),
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below, 0f),
                    (versionRegion, TrimLabel(version), unchecked((int)0xFFFF8C00), Pack6OverlayLabelPlacement.Above, 14f));

                var frameOk = defects.Count == 0;
                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, boxes,
                    algorithmError: dllError,
                    detailMessage: dllError ? err : null,
                    ngBanner: frameOk || dllError ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk);

                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = FirstNonEmpty(productSN, item.ProductCode, qrData, "UNKNOWN"),
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

                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = FirstNonEmpty(productSN, item.ProductCode, "UNKNOWN"),
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

        private Pack6FrameResult DetectTopCover(Pack6IngressItem item, Pack6Config config)
        {
            if (!Pack6TopCoverExpectations.TryGet(item.ShotIndex, out var expect))
            {
                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = item.ProductCode ?? "UNKNOWN",
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = true,
                    StationName = "上盖检测",
                    OcrMode = Pack6OcrMode.None,
                    EngineMessage = "顶面本张不检测"
                };
            }

            HObject? image = null;
            List<AI.AIResults>? results = null;
            try
            {
                HOperatorSet.ReadImage(out image, item.FilePath);
                EnsureAi();
                var modelPath = ResolveTopCoverModelPath(config);
                string err;
                lock (_aiLock)
                {
                    err = _ai!.DetectTopCover(
                        image,
                        modelPath,
                        [Pack6TopCoverExpectations.ClassFoam, Pack6TopCoverExpectations.ClassHanger],
                        out results,
                        config.TopCoverUseGpu);
                }

                var defects = new List<string>();
                var dllError = !string.IsNullOrWhiteSpace(err);
                if (dllError)
                    defects.Add(Pack6ResultOverlayHelper.AlgorithmErrorBanner);

                var hangers = 0;
                var foams = 0;
                var boxes = new List<Pack6OverlayBox>();
                if (results is not null)
                {
                    foreach (var r in results)
                    {
                        var name = r.Name ?? string.Empty;
                        if (string.Equals(name, Pack6TopCoverExpectations.ClassHanger, StringComparison.OrdinalIgnoreCase))
                            hangers++;
                        else if (string.Equals(name, Pack6TopCoverExpectations.ClassFoam, StringComparison.OrdinalIgnoreCase))
                            foams++;

                        if (TryRegionRect(r.Region, out var x, out var y, out var w, out var h))
                        {
                            var argb = string.Equals(name, Pack6TopCoverExpectations.ClassHanger, StringComparison.OrdinalIgnoreCase)
                                ? unchecked((int)0xFFFF0000)
                                : unchecked((int)0xFFFFFF00);
                            boxes.Add(new Pack6OverlayBox
                            {
                                X = x,
                                Y = y,
                                Width = w,
                                Height = h,
                                Label = $"{name}:{r.Score:F2}",
                                Argb = argb,
                                LabelPlacement = Pack6OverlayLabelPlacement.Above
                            });
                        }
                    }
                }

                if (!dllError)
                {
                    if (hangers != expect.HangerBushing)
                        defects.Add($"吊耳数量不符:期望={expect.HangerBushing},实际={hangers}");
                    if (foams != expect.SealingFoam)
                        defects.Add($"密封泡棉数量不符:期望={expect.SealingFoam},实际={foams}");
                }

                var frameOk = defects.Count == 0;
                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, boxes,
                    algorithmError: dllError,
                    detailMessage: dllError ? err : null,
                    ngBanner: frameOk || dllError ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk);

                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = item.ProductCode ?? "UNKNOWN",
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = frameOk,
                    Defects = defects,
                    StationName = "上盖检测",
                    OcrMode = Pack6OcrMode.TopCover,
                    EngineMessage = dllError
                        ? err
                        : $"OK hanger={hangers}/{expect.HangerBushing} foam={foams}/{expect.SealingFoam}",
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
                if (results is not null)
                {
                    foreach (var r in results)
                        r.Region?.Dispose();
                }
                image?.Dispose();
            }
        }

        private Pack6FrameResult DetectPinSkew(Pack6IngressItem item, Pack6Config config)
        {
            HObject? image = null;
            HTuple? angleDiff = null;
            HTuple? lCol = null;
            HTuple? lRow = null;
            HTuple? sRow = null;
            HTuple? sCol = null;
            HTuple? l1r = null, l1c = null, l2r = null, l2c = null;
            HTuple? s1r = null, s1c = null, s2r = null, s2c = null;
            try
            {
                HOperatorSet.ReadImage(out image, item.FilePath);
                EnsureAi();
                lock (_aiLock)
                {
                    _ai!.DetectConnectorPinSkew(
                        image,
                        out angleDiff, out lCol, out lRow, out sRow, out sCol,
                        out l1r, out l1c, out l2r, out l2c,
                        out s1r, out s1c, out s2r, out s2c);
                }

                double A(HTuple? t) => t is { Length: > 0 } ? t.D : 0.0;
                var angle = A(angleDiff);
                var longCol = A(lCol);
                var longRow = A(lRow);
                var shortRow = A(sRow);
                var shortCol = A(sCol);
                var long1Row = A(l1r);
                var long1Col = A(l1c);
                var long2Row = A(l2r);
                var long2Col = A(l2c);
                var short1Row = A(s1r);
                var short1Col = A(s1c);
                var short2Row = A(s2r);
                var short2Col = A(s2c);

                // 文档：失败时输出全 0
                var detectFailed =
                    Math.Abs(angle) < 1e-6 &&
                    Math.Abs(longCol) < 1e-6 &&
                    Math.Abs(longRow) < 1e-6 &&
                    Math.Abs(shortRow) < 1e-6 &&
                    Math.Abs(shortCol) < 1e-6 &&
                    Math.Abs(long1Row) < 1e-6 && Math.Abs(long1Col) < 1e-6 &&
                    Math.Abs(long2Row) < 1e-6 && Math.Abs(long2Col) < 1e-6 &&
                    Math.Abs(short1Row) < 1e-6 && Math.Abs(short1Col) < 1e-6 &&
                    Math.Abs(short2Row) < 1e-6 && Math.Abs(short2Col) < 1e-6;

                var defects = new List<string>();
                if (detectFailed)
                {
                    defects.Add("未检测到PIN针");
                }
                else
                {
                    if (angle > config.PinLongAngleMax)
                        defects.Add($"长PIN角度差超限:{angle:F2}>{config.PinLongAngleMax:F2}");
                    if (longCol > config.PinLongColDiffMax)
                        defects.Add($"长PIN列差超限:{longCol:F2}>{config.PinLongColDiffMax:F2}");
                    if (longRow < config.PinLongRowDiffMin || longRow > config.PinLongRowDiffMax)
                        defects.Add($"长PIN行差不在区间:[{config.PinLongRowDiffMin:F1},{config.PinLongRowDiffMax:F1}] 实际={longRow:F2}");
                    if (shortRow > config.PinShortRowDiffMax)
                        defects.Add($"短PIN行差超限:{shortRow:F2}>{config.PinShortRowDiffMax:F2}");
                    if (shortCol < config.PinShortColDiffMin || shortCol > config.PinShortColDiffMax)
                        defects.Add($"短PIN列差不在区间:[{config.PinShortColDiffMin:F1},{config.PinShortColDiffMax:F1}] 实际={shortCol:F2}");
                }

                var frameOk = defects.Count == 0;
                var measures =
                    $"角度差={angle:F2}° (≤{config.PinLongAngleMax:F1})\n" +
                    $"长PIN列差={longCol:F2} (≤{config.PinLongColDiffMax:F1})\n" +
                    $"长PIN行差={longRow:F2} [{config.PinLongRowDiffMin:F0},{config.PinLongRowDiffMax:F0}]\n" +
                    $"短PIN行差={shortRow:F2} (≤{config.PinShortRowDiffMax:F1})\n" +
                    $"短PIN列差={shortCol:F2} [{config.PinShortColDiffMin:F0},{config.PinShortColDiffMax:F0}]";

                // 橙=长PIN，青=短PIN；连线便于看歪斜
                const int argbLong = unchecked((int)0xFFFF6600);
                const int argbShort = unchecked((int)0xFF00E5FF);
                const int argbLineLong = unchecked((int)0xFFFFFF00);
                const int argbLineShort = unchecked((int)0xFF66FF66);
                var points = new List<Pack6OverlayPoint>();
                var lines = new List<Pack6OverlayLine>();
                if (!detectFailed)
                {
                    points.Add(new Pack6OverlayPoint { Col = (float)long1Col, Row = (float)long1Row, Label = "L1", Argb = argbLong });
                    points.Add(new Pack6OverlayPoint { Col = (float)long2Col, Row = (float)long2Row, Label = "L2", Argb = argbLong });
                    points.Add(new Pack6OverlayPoint { Col = (float)short1Col, Row = (float)short1Row, Label = "S1", Argb = argbShort });
                    points.Add(new Pack6OverlayPoint { Col = (float)short2Col, Row = (float)short2Row, Label = "S2", Argb = argbShort });
                    lines.Add(new Pack6OverlayLine
                    {
                        Col1 = (float)long1Col, Row1 = (float)long1Row,
                        Col2 = (float)long2Col, Row2 = (float)long2Row,
                        Argb = argbLineLong
                    });
                    lines.Add(new Pack6OverlayLine
                    {
                        Col1 = (float)short1Col, Row1 = (float)short1Row,
                        Col2 = (float)short2Col, Row2 = (float)short2Row,
                        Argb = argbLineShort
                    });
                }

                var preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, Array.Empty<Pack6OverlayBox>(),
                    algorithmError: false,
                    detailMessage: measures,
                    ngBanner: frameOk ? null : string.Join("；", defects.Take(3)),
                    isOk: frameOk,
                    points: points,
                    lines: lines,
                    topLeftMeasures: measures);

                return new Pack6FrameResult
                {
                    FilePath = item.FilePath,
                    QueueName = item.QueueName,
                    ProductCode = item.ProductCode ?? "UNKNOWN",
                    CameraName = item.CameraName,
                    ShotIndex = item.ShotIndex,
                    FolderSn = Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? item.ProductCode ?? string.Empty,
                    IsOk = frameOk,
                    Defects = defects,
                    StationName = "PIN歪斜",
                    OcrMode = Pack6OcrMode.ConnectorPinSkew,
                    EngineMessage = frameOk ? $"OK {measures.Replace('\n', ' ')}" : measures.Replace('\n', ' '),
                    PreviewImagePath = preview ?? string.Empty,
                    NgBanner = frameOk ? string.Empty : string.Join("；", defects.Take(3)),
                    OverlayBoxes = Array.Empty<Pack6OverlayBox>()
                };
            }
            finally
            {
                angleDiff?.Dispose();
                lCol?.Dispose();
                lRow?.Dispose();
                sRow?.Dispose();
                sCol?.Dispose();
                l1r?.Dispose();
                l1c?.Dispose();
                l2r?.Dispose();
                l2c?.Dispose();
                s1r?.Dispose();
                s1c?.Dispose();
                s2r?.Dispose();
                s2c?.Dispose();
                image?.Dispose();
            }
        }

        private Pack6FrameResult Simulate(Pack6IngressItem item, Pack6Config config)
        {
            var mode = ResolveOcrMode(item, config);
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            var forceNg = name.Contains("NG", StringComparison.OrdinalIgnoreCase)
                          || name.Contains("缺陷", StringComparison.OrdinalIgnoreCase);
            var station = mode switch
            {
                Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.NameplateProductSN => "三码校验",
                Pack6OcrMode.TopCover => "上盖检测",
                Pack6OcrMode.ConnectorPinSkew => "PIN歪斜",
                _ => MapStation(item.QueueName, item.CameraName)
            };
            var defects = forceNg ? new List<string> { $"{station}模拟NG" } : new List<string>();
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
                StationName = station,
                FromSimulation = true,
                OcrMode = mode,
                ProductSN = mode is Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.NameplateProductSN ? sn : string.Empty,
                QrData = mode == Pack6OcrMode.WhiteLabelThreeCode ? sn : string.Empty,
                PartSN = mode == Pack6OcrMode.WhiteLabelThreeCode ? "1234567890" : string.Empty,
                Version = mode == Pack6OcrMode.WhiteLabelThreeCode
                    ? (string.IsNullOrWhiteSpace(config.ExpectedWhiteLabelVersion)
                        ? "M00"
                        : config.ExpectedWhiteLabelVersion.Trim().ToUpperInvariant())
                    : string.Empty,
                EngineMessage = "模拟"
            };
        }

        private Pack6FrameResult Fail(
            Pack6IngressItem item,
            Pack6OcrMode mode,
            string message,
            bool algorithmError,
            Pack6Config config)
        {
            var station = mode switch
            {
                Pack6OcrMode.TopCover => "上盖检测",
                Pack6OcrMode.ConnectorPinSkew => "PIN歪斜",
                Pack6OcrMode.WhiteLabelThreeCode or Pack6OcrMode.NameplateProductSN => "三码校验",
                _ => MapStation(item.QueueName, item.CameraName)
            };
            string preview = string.Empty;
            if (algorithmError && File.Exists(item.FilePath))
            {
                preview = Pack6ResultOverlayHelper.TryWrite(
                    config, item, item.FilePath, Array.Empty<Pack6OverlayBox>(),
                    algorithmError: true, detailMessage: message, ngBanner: null, isOk: false) ?? string.Empty;
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
                StationName = station,
                OcrMode = mode,
                EngineMessage = message,
                PreviewImagePath = preview,
                NgBanner = algorithmError ? Pack6ResultOverlayHelper.AlgorithmErrorBanner : message,
                IsVisionAlgorithmError = algorithmError
            };
        }

        private void EnsureAi()
        {
            lock (_aiLock)
            {
                _ai ??= new AI();
            }
        }

        private static string ResolveTopCoverModelPath(Pack6Config config)
        {
            var p = config.TopCoverModelPath?.Trim();
            if (string.IsNullOrWhiteSpace(p))
                p = @"Dlls\YWSIX\YW_X01_TOP.onnx";
            if (Path.IsPathRooted(p))
                return p;
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, p));
        }

        private static List<Pack6OverlayBox> BuildBoxes(
            params (HObject? Region, string Label, int Argb, Pack6OverlayLabelPlacement Place, float OffsetY)[] items)
        {
            var list = new List<Pack6OverlayBox>();
            foreach (var (region, label, argb, place, offsetY) in items)
            {
                if (!TryRegionRect(region, out var x, out var y, out var w, out var h))
                    continue;
                list.Add(new Pack6OverlayBox
                {
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    Label = label,
                    Argb = argb,
                    LabelPlacement = place,
                    LabelOffsetY = offsetY
                });
            }
            return list;
        }

        private static bool TryRegionRect(HObject? region, out int x, out int y, out int w, out int h)
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

        private void EnsureNativeRuntime()
        {
            if (_nativeSearchPathConfigured)
                return;

            var dllsDir = Path.Combine(AppContext.BaseDirectory, "Dlls");
            var ywDir = Path.Combine(dllsDir, "YWSIX");
            var halconInDlls = Path.Combine(dllsDir, "halcon.dll");
            if (!File.Exists(halconInDlls))
            {
                throw new FileNotFoundException(
                    "未找到 Dlls\\halcon.dll。请确认完整程序目录含 Halcon 原生库。");
            }

            if (!SetDllDirectory(dllsDir))
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Warn($"SetDllDirectory(\"{dllsDir}\") 失败，Win32={err}", false);
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            void PrependPath(string dir)
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return;
                if (!pathEnv.Split(Path.PathSeparator)
                        .Any(p => string.Equals(p, dir, StringComparison.OrdinalIgnoreCase)))
                    pathEnv = dir + Path.PathSeparator + pathEnv;
            }

            PrependPath(dllsDir);
            PrependPath(ywDir);
            Environment.SetEnvironmentVariable("PATH", pathEnv);

            foreach (var name in new[]
                     {
                         "libiomp5md.dll", "halconc.dll", "halcon.dll", "halconcpp.dll", "hcanvas.dll"
                     })
            {
                var full = Path.Combine(dllsDir, name);
                if (!File.Exists(full))
                    continue;
                var handle = LoadLibrary(full);
                if (handle == IntPtr.Zero && name.Equals("halcon.dll", StringComparison.OrdinalIgnoreCase))
                {
                    throw new DllNotFoundException(
                        $"无法预加载 halcon.dll，Win32={Marshal.GetLastWin32Error()}");
                }
            }

            // YOLO26Det 与算法 DLL 同目录，便于依赖解析
            var yolo = Path.Combine(ywDir, "YOLO26Det.dll");
            if (File.Exists(yolo))
            {
                var h = LoadLibrary(yolo);
                if (h == IntPtr.Zero)
                    _logger.Warn($"预加载 YOLO26Det 失败，Win32={Marshal.GetLastWin32Error()}", false);
            }

            var pack = Path.Combine(ywDir, "YWX01Pack.dll");
            if (File.Exists(pack))
            {
                var h = LoadLibrary(pack);
                if (h == IntPtr.Zero)
                    _logger.Warn($"预加载 YWX01Pack 失败，Win32={Marshal.GetLastWin32Error()}", false);
            }

            var licenseDir = dllsDir;
            var licenseDat = Path.Combine(licenseDir, "license.dat");
            var licenseAlt = Path.Combine(licenseDir, "license", "license_22.11.dat");
            if (File.Exists(licenseDat))
                Environment.SetEnvironmentVariable("HALCONROOT", licenseDir);
            else if (File.Exists(licenseAlt))
                Environment.SetEnvironmentVariable("HALCONROOT", Path.Combine(licenseDir, "license"));

            try
            {
                HOperatorSet.SetSystem("temporary_mem_cache", "false");
                HOperatorSet.SetSystem("global_mem_cache", "idle");
            }
            catch (Exception ex)
            {
                _logger.Warn($"设置 Halcon 内存策略失败: {ex.Message}", false);
            }

            _nativeSearchPathConfigured = true;
            _logger.Info($"YWSIX 原生运行时就绪：{dllsDir} + {ywDir}", false);
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}
