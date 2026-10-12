using System.IO;
using System.Runtime.InteropServices;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;
using ZZTWPack;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 枣庄 TW 视觉引擎：对接 <c>ZZTWPack.AI</c>（白标签三码 / 铭牌产品编码）。
    /// 非 OCR 工位在真实模式下记为跳过（OK，无缺陷）；模拟模式仍走文件名 NG 注入。
    /// </summary>
    public sealed class Pack6ZztwVisionEngine : IZZSIXPack6VisionEngine
    {
        private readonly ILoggerService<Pack6ZztwVisionEngine> _logger;
        private readonly object _aiLock = new();
        private Pack6Config? _config;
        private AI? _ai;
        private bool _warmedUp;
        private static bool _nativeSearchPathConfigured;

        public Pack6ZztwVisionEngine(ILoggerService<Pack6ZztwVisionEngine> logger)
        {
            _logger = logger;
        }

        public Task InitializeAsync(Pack6Config config, CancellationToken ct = default)
        {
            _config = config;
            Pack6ResultOverlayHelper.EnsureAndPrune(config);
            if (config.UseSimulation)
            {
                _logger.Info("Pack6 视觉：模拟模式（不加载 ZZTWPack）", false);
                return Task.CompletedTask;
            }

            EnsureHalconNativeRuntime();

            lock (_aiLock)
            {
                _ai ??= new AI();
            }

            _logger.Info("Pack6 视觉：已创建 ZZTWPack.AI（白标签/铭牌 OCR）", false);

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
                return Task.FromResult(mode == Pack6OcrMode.WhiteLabelThreeCode
                    ? DetectWhiteLabel(item, config)
                    : DetectNameplate(item, config));
            }
            catch (Exception ex)
            {
                _logger.Warn($"ZZTWPack 调用异常: {ex.Message}", false);
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
                    (qrRegion, TrimLabel(qrData), unchecked((int)0xFF00FF66), Pack6OverlayLabelPlacement.Above),
                    (partRegion, TrimLabel(partSN), unchecked((int)0xFFFFFF00), Pack6OverlayLabelPlacement.Right),
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below),
                    (versionRegion, TrimLabel(version), unchecked((int)0xFFFF8C00), Pack6OverlayLabelPlacement.Above));

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
                    (productRegion, TrimLabel(productSN), unchecked((int)0xFF00E5FF), Pack6OverlayLabelPlacement.Below));

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
            params (HObject? Region, string Label, int Argb, Pack6OverlayLabelPlacement Placement)[] items)
        {
            var list = new List<Pack6OverlayBox>(items.Length);
            foreach (var (region, label, argb, placement) in items)
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
                    LabelPlacement = placement
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
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;

            // 1) 标准文件名优先按「相机+张序」判定。
            //    旧逻辑先扫关键词，会导致 SN 中含 F3/F1 等短串时，整包所有图都误进铭牌/白标签 OCR，
            //    同一产品刷出上千条相同 #3111 NG。
            var cam = item.CameraName;
            var shot = item.ShotIndex;
            var parsed = Pack6FileNameParser.TryParseFileName(name, out var p);
            if (parsed)
            {
                cam = p.CameraName;
                shot = p.ShotIndex;
            }

            if (string.Equals(cam, "前", StringComparison.Ordinal))
            {
                if (shot == 1)
                    return Pack6OcrMode.WhiteLabelThreeCode;
                if (shot == 3)
                    return Pack6OcrMode.NameplateProductSN;
                // 前-2 等：标准命名明确不是码检工位
                if (parsed)
                    return Pack6OcrMode.None;
            }
            else if (string.Equals(cam, "长左2", StringComparison.Ordinal))
            {
                // 长左2-2：第二张白标签（与前1同算法）
                if (shot == 2)
                    return Pack6OcrMode.WhiteLabelThreeCode;
                if (parsed)
                    return Pack6OcrMode.None;
            }
            else if (parsed)
            {
                // 其它标准相机名：跳过 OCR，禁止再用全文件名关键词（SN 易误伤）
                return Pack6OcrMode.None;
            }

            // 2) 仅非标准命名才用关键词；探测串去掉 SN，只留相机/张序附近片段
            var probe = BuildKeywordProbe(name, item.ProductCode);
            if (Hit(probe, config.WhiteLabelKeywords))
                return Pack6OcrMode.WhiteLabelThreeCode;
            if (Hit(probe, config.NameplateKeywords))
                return Pack6OcrMode.NameplateProductSN;

            return Pack6OcrMode.None;
        }

        /// <summary>是否长左2-2 白标签机位（与前1同 OCR，统计统一为三码校验）。</summary>
        internal static bool IsLongLeft2WhiteLabel(Pack6IngressItem item)
        {
            ResolveCameraShot(item, out var cam, out var shot);
            return string.Equals(cam, "长左2", StringComparison.Ordinal) && shot == 2;
        }

        private static void ResolveCameraShot(Pack6IngressItem item, out string cam, out int shot)
        {
            cam = item.CameraName ?? string.Empty;
            shot = item.ShotIndex;
            var name = Path.GetFileName(item.FilePath) ?? string.Empty;
            if (Pack6FileNameParser.TryParseFileName(name, out var p))
            {
                cam = p.CameraName;
                shot = p.ShotIndex;
            }
        }

        /// <summary>关键词匹配用片段：优先 "-相机-张序-OK|NG_"，避免扫到 SN。</summary>
        private static string BuildKeywordProbe(string fileName, string? productCode)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                fileName,
                @"-[^-]+-\d+-(?:OK|NG)_",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Value;

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
                .Where(k => k.Length >= 2) // 拒绝单字符；F3/F1 等短码易撞 SN
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
