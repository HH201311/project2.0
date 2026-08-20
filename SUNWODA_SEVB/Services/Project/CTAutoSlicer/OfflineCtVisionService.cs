using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    /// <summary>
    /// 反射调用 NJCT.NJSwdCT（ModelInit / ModelAction / ModelAction_origin）。
    /// DLL 与 Halcon/ONNX 原生依赖位于输出目录 Dlls\。
    /// 每次推理后按视觉约定释放：ResultImg、List&lt;HObject&gt;（AllPointRegion）、List&lt;HTuple&gt; 各索引。
    /// </summary>
    public sealed class OfflineCtVisionService : ICTOfflineVisionService, IDisposable
    {
         

        private const string AlgorithmTypeName = "NJCT.NJSwdCT";
        private const string ModelInitMethodName = "ModelInit";
        private const string ModelActionMethodName = "ModelAction";
        private const string ModelActionOriginMethodName = "ModelAction_origin";

        private readonly ILoggerService<OfflineCtVisionService> _logger;
        private object? _algorithmInstance;
        private MethodInfo? _modelInitMethod;
        private MethodInfo? _modelActionMethod;
        private MethodInfo? _modelActionOriginMethod;
        private object? _modelHandle;
        private string? _initializedModelPath;
        private bool? _initializedCuda;
        private static bool _nativeSearchPathConfigured;
        private bool _disposed;

        public OfflineCtVisionService(ILoggerService<OfflineCtVisionService> logger)
        {
            _logger = logger;
        }

        public bool IsInitialized =>
            _algorithmInstance != null
            && _modelInitMethod != null
            && _modelActionMethod != null
            && _modelActionOriginMethod != null
            && _modelHandle != null;

        public void Initialize(string modelPath, bool isRunCuda = true)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new ArgumentException("模型路径不能为空", nameof(modelPath));
            }

            var fullModelPath = Path.GetFullPath(modelPath.Trim());
            if (!File.Exists(fullModelPath))
            {
                throw new FileNotFoundException($"未找到 ONNX 模型：{fullModelPath}", fullModelPath);
            }

            // 同一模型 + CUDA 配置已初始化则复用
            if (IsInitialized
                && string.Equals(_initializedModelPath, fullModelPath, StringComparison.OrdinalIgnoreCase)
                && _initializedCuda == isRunCuda)
            {
                return;
            }

            EnsureNativeDllSearchPath();
            EnsureAlgorithmLoaded();

            object?[] parameters = [fullModelPath, null, null, isRunCuda];
            _modelInitMethod!.Invoke(_algorithmInstance, parameters);

            var exInfo = parameters[2] as string ?? string.Empty;
            var handle = parameters[1];
            if (handle is null)
            {
                DisposeModel();
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(exInfo)
                        ? "ModelInit 失败：模型句柄为 null"
                        : $"ModelInit 失败：{exInfo}");
            }

            // 换模型时释放旧句柄
            if (_modelHandle is IDisposable oldDisposable
                && !ReferenceEquals(oldDisposable, handle as IDisposable))
            {
                try
                {
                    oldDisposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"释放旧模型句柄异常：{ex.Message}");
                }
            }

            _modelHandle = handle;
            _initializedModelPath = fullModelPath;
            _initializedCuda = isRunCuda;
            _logger.Info(
                $"离线 CT AI 模型初始化成功：{fullModelPath}，CUDA={isRunCuda}"
                + (string.IsNullOrEmpty(exInfo) ? string.Empty : $"，ExInfo={exInfo}"));
        }

        public OfflineCtInferResult InferEnhance(
            string imagePath,
            int blackHeight,
            string? resultImagePath = null,
            double pixelSizeUm = 1.0,
            int bottomInfoBlackHeight = 0,
            OfflineCtLRangeCheckOptions? rangeCheck = null,
            bool applyHostOverlay = true) =>
            Infer(
                ModelActionMethodName,
                "enhance",
                imagePath,
                blackHeight,
                resultImagePath,
                pixelSizeUm,
                bottomInfoBlackHeight,
                rangeCheck,
                applyHostOverlay);

        public OfflineCtInferResult InferOrigin(
            string imagePath,
            int blackHeight,
            string? resultImagePath = null,
            double pixelSizeUm = 1.0,
            int bottomInfoBlackHeight = 0,
            OfflineCtLRangeCheckOptions? rangeCheck = null,
            bool applyHostOverlay = true) =>
            Infer(
                ModelActionOriginMethodName,
                "origin",
                imagePath,
                blackHeight,
                resultImagePath,
                pixelSizeUm,
                bottomInfoBlackHeight,
                rangeCheck,
                applyHostOverlay);

        public void DisposeModel()
        {
            if (_modelHandle is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"释放模型句柄异常：{ex.Message}");
                }
            }

            _modelHandle = null;
            _initializedModelPath = null;
            _initializedCuda = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeModel();
            _disposed = true;
        }

        private OfflineCtInferResult Infer(
            string methodName,
            string modeTag,
            string imagePath,
            int blackHeight,
            string? resultImagePath,
            double pixelSizeUm,
            int bottomInfoBlackHeight,
            OfflineCtLRangeCheckOptions? rangeCheck,
            bool applyHostOverlay)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("AI 标注输入图像不存在", imagePath);
            }

            EnsureNativeDllSearchPath();
            EnsureNjCtRuntimePrerequisites();

            var method = methodName == ModelActionOriginMethodName
                ? _modelActionOriginMethod!
                : _modelActionMethod!;

            // OpenCvSharp.ImRead 无法可靠处理含非 ASCII（如中文）的路径，先转存到纯 ASCII 临时文件
            string invokeImagePath = imagePath;
            string? stagedAsciiPath = null;
            try
            {
                stagedAsciiPath = StageImageToAsciiPathIfNeeded(imagePath);
                if (stagedAsciiPath != null)
                {
                    invokeImagePath = stagedAsciiPath;
                    _logger.Info($"离线 CT AI 输入路径含非 ASCII，已转存：{imagePath} -> {invokeImagePath}");
                }

                // ImgPath, Models, BlackHeight, ResultImg, AllPointRegion, AllRegionRow, AllRegionCol, AllPrintfValue, RunInfo
                object?[] parameters =
                [
                    invokeImagePath,
                    _modelHandle,
                    blackHeight,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                ];

                // 视觉侧约定：ResultImg / List<HObject> / List<HTuple> 均须手动 Dispose，否则原生内存泄漏
                HObject? resultImg = null;
                object? rawPointRegions = null;
                object? rawRegionRows = null;
                object? rawRegionCols = null;
                object? rawPrintfValues = null;
                try
                {
                    method.Invoke(_algorithmInstance, parameters);

                    resultImg = parameters[3] as HObject;
                    rawPointRegions = parameters[4];
                    rawRegionRows = parameters[5];
                    rawRegionCols = parameters[6];
                    rawPrintfValues = parameters[7];
                    var pointRegions = ExtractHObjectList(rawPointRegions);
                    var regionRows = ExtractHTupleList(rawRegionRows);
                    var regionCols = ExtractHTupleList(rawRegionCols);
                    var printfValues = ExtractHTupleList(rawPrintfValues);
                    var runInfo = parameters[8] as string ?? string.Empty;

                    // NJCT 内部 catch 会把任意异常吞成该文案；自动检测后给出具体原因
                    if (string.Equals(runInfo, "当前图像暂不支持检测", StringComparison.Ordinal))
                    {
                        runInfo = OfflineCtImageDiagnostics.BuildUnsupportedRunInfo(
                            invokeImagePath,
                            imagePath);
                        _logger.Warn($"离线 CT AI 推理失败（{modeTag}）：{runInfo}");
                    }
                    else if (!string.IsNullOrEmpty(runInfo))
                    {
                        _logger.Warn($"离线 CT AI 推理 RunInfo（{modeTag}）：{runInfo}");
                    }

                    string? writtenPath = null;
                    var failed = !string.IsNullOrEmpty(runInfo);
                    if (failed)
                    {
                        // 失败不向产品输出目录落盘任何图（含算法裁剪排查图）；仅日志
                        _logger.Warn(
                            $"检测失败，不落盘结果图（{modeTag}）：RunInfo={runInfo}，输入={invokeImagePath}");
                    }
                    else if (!string.IsNullOrWhiteSpace(resultImagePath) && resultImg is not null)
                    {
                        if (!applyHostOverlay)
                        {
                            // DLL 直调：仅落盘算法返回的 ResultImg，不做上位机叠加
                            writtenPath = WriteResultImage(resultImg, resultImagePath);
                            _logger.Info(
                                $"离线 CT AI 裸结果图已落盘（DLL 直调 {modeTag}，BlackHeight={blackHeight}）：{writtenPath}");
                        }
                        else
                        {
                            // 成功：叠 AllPointRegion + L1~L5 线 + PrintfValue 后落盘（主流程最终展示图）
                            try
                            {
                                writtenPath = OfflineCtResultOverlayHelper.WriteAnnotatedResultImage(
                                    resultImg,
                                    pointRegions,
                                    regionRows,
                                    regionCols,
                                    printfValues,
                                    resultImagePath,
                                    pixelSizeUm > 0 ? pixelSizeUm : 1.0,
                                    bottomInfoBlackHeight,
                                    rangeCheck);
                                _logger.Info(
                                    $"离线 CT AI 标注结果图已叠加抓点/尺寸"
                                    + (bottomInfoBlackHeight > 0
                                        ? $"（底部遮罩 {bottomInfoBlackHeight}px）"
                                        : string.Empty)
                                    + $"：{writtenPath}");
                            }
                            catch (Exception overlayEx)
                            {
                                _logger.Warn(
                                    $"叠加抓点/尺寸失败，回退写裸 ResultImg：{overlayEx.Message}");
                                writtenPath = WriteResultImage(resultImg, resultImagePath);
                            }
                        }
                    }

                    // 出图后立刻把尺寸汇总记入内存，二次标注左栏可直接显示
                    if (!string.IsNullOrWhiteSpace(writtenPath) && printfValues.Count > 0)
                    {
                        var summary = OfflineCtAnnotationHelper.FormatAiDimensionSummaryText(
                            printfValues,
                            pixelSizeUm > 0 ? pixelSizeUm : 1.0);
                        OfflineCtAnnotationHelper.RememberAiDimensionSummary(writtenPath, summary);
                    }

                    return new OfflineCtInferResult
                    {
                        ModeTag = modeTag,
                        ResultImagePath = writtenPath,
                        RunInfo = runInfo,
                        RegionRows = regionRows,
                        RegionCols = regionCols,
                        PrintfValues = printfValues,
                    };
                }
                finally
                {
                    // 与 CTNCLX / 视觉文档一致：先释放 List 内各对象，再释放 ResultImg
                    DisposeHObjectList(rawPointRegions);
                    DisposeHTupleList(rawRegionRows);
                    DisposeHTupleList(rawRegionCols);
                    DisposeHTupleList(rawPrintfValues);
                    try
                    {
                        resultImg?.Dispose();
                    }
                    catch
                    {
                        // 忽略释放异常，避免掩盖主异常
                    }
                }
            }
            finally
            {
                TryDeleteFile(stagedAsciiPath);
            }
        }

        private void EnsureAlgorithmLoaded()
        {
            if (_algorithmInstance != null
                && _modelInitMethod != null
                && _modelActionMethod != null
                && _modelActionOriginMethod != null)
            {
                return;
            }

            var dllPath = CTAutoSlicerSettings.GetOfflineCtDllPath();
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException(
                    $"未找到离线 CT AI DLL：{dllPath}。请将 NJCT.dll / CF.dll 放到程序目录 Dlls\\。",
                    dllPath);
            }

            _logger.Info($"加载离线 CT AI DLL：{dllPath}");
            var assembly = Assembly.LoadFrom(dllPath);
            var algorithmType = assembly.GetType(AlgorithmTypeName)
                ?? throw new InvalidOperationException(
                    $"未找到算法类型 {AlgorithmTypeName}。"
                    + $"DLL 内可用类型：{string.Join(", ", assembly.GetTypes().Select(t => t.FullName))}");

            _algorithmInstance = Activator.CreateInstance(algorithmType)
                ?? throw new InvalidOperationException($"无法创建算法实例 {algorithmType.FullName}");

            _modelInitMethod = algorithmType.GetMethod(ModelInitMethodName)
                ?? throw new InvalidOperationException($"未找到方法 {ModelInitMethodName}");
            _modelActionMethod = algorithmType.GetMethod(ModelActionMethodName)
                ?? throw new InvalidOperationException($"未找到方法 {ModelActionMethodName}");
            _modelActionOriginMethod = algorithmType.GetMethod(ModelActionOriginMethodName)
                ?? throw new InvalidOperationException($"未找到方法 {ModelActionOriginMethodName}");

            _logger.Info("离线 CT AI 算法类型加载成功");
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("离线 CT AI 尚未初始化，请先调用 Initialize()");
            }
        }

        private void EnsureNativeDllSearchPath()
        {
            if (_nativeSearchPathConfigured)
            {
                return;
            }

            var dllsDir = CTAutoSlicerSettings.GetNativeDllsDirectory();
            if (!Directory.Exists(dllsDir))
            {
                throw new DirectoryNotFoundException(
                    $"未找到原生 DLL 目录：{dllsDir}。请确认 NJCT/CF/halcon 等已复制到输出目录 Dlls\\。");
            }

            if (!SetDllDirectory(dllsDir))
            {
                var err = Marshal.GetLastWin32Error();
                _logger.Warn($"SetDllDirectory(\"{dllsDir}\") 失败，Win32={err}，改用 PATH 前置");
            }

            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!pathEnv.Split(Path.PathSeparator)
                    .Any(p => string.Equals(p, dllsDir, StringComparison.OrdinalIgnoreCase)))
            {
                Environment.SetEnvironmentVariable(
                    "PATH",
                    dllsDir + Path.PathSeparator + pathEnv);
            }

            // Halcon 默认在进程 cwd 找 license.dat；主程序 cwd 通常是 exe 目录而非 Dlls，
            // 必须显式指定，否则 ReadImage 报 #2036，最终被 NJCT 吞成「暂不支持检测」。
            EnsureHalconLicenseEnvironment(dllsDir);

            _nativeSearchPathConfigured = true;
            _logger.Info($"已配置离线 CT AI 原生 DLL 搜索目录：{dllsDir}");
        }

        /// <summary>
        /// NJCT.ConvertMat32FC1ToHObject 会硬编码写 <c>D:\TEST.png</c> 再 Halcon ReadImage。
        /// D 盘空间不足或 Halcon 许可找不到时，异常会被吞成「当前图像暂不支持检测」。
        /// </summary>
        private void EnsureNjCtRuntimePrerequisites()
        {
            const long minFreeBytes = 50L * 1024 * 1024; // 50 MB，足够写中间图
            try
            {
                var drive = new DriveInfo("D");
                if (!drive.IsReady)
                {
                    throw new IOException(
                        "离线 CT AI 需要可写的 D: 盘（NJCT 硬编码写入 D:\\TEST.png），当前 D: 不可用。");
                }

                if (drive.AvailableFreeSpace < minFreeBytes)
                {
                    throw new IOException(
                        $"离线 CT AI 需要向 D:\\TEST.png 写中间结果，当前 D: 仅剩 "
                        + $"{drive.AvailableFreeSpace / 1024.0 / 1024.0:F1} MB（建议至少 50 MB）。请清理 D: 后重试。");
                }
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn($"检查 D: 可用空间失败：{ex.Message}");
            }
        }

        private void EnsureHalconLicenseEnvironment(string dllsDir)
        {
            var existing = Environment.GetEnvironmentVariable("HALCON_LICENSE_FILE");
            if (!string.IsNullOrWhiteSpace(existing) && File.Exists(existing))
            {
                return;
            }

            var candidates = new[]
            {
                Path.Combine(dllsDir, "license.dat"),
                Path.Combine(dllsDir, "license", "license.dat"),
                Path.Combine(dllsDir, "license", "license_22.11.dat"),
            };

            var licensePath = candidates.FirstOrDefault(File.Exists);
            if (licensePath is null)
            {
                _logger.Warn(
                    $"未在 {dllsDir} 找到 Halcon license.dat；若随后出现「暂不支持检测」，请检查 Halcon 许可。");
                return;
            }

            Environment.SetEnvironmentVariable("HALCON_LICENSE_FILE", licensePath);
            _logger.Info($"已设置 HALCON_LICENSE_FILE={licensePath}");
        }

        /// <returns>若需要转存则返回临时 ASCII 路径；否则 null（直接用原路径）。</returns>
        private static string? StageImageToAsciiPathIfNeeded(string imagePath)
        {
            var fullPath = Path.GetFullPath(imagePath);
            if (IsAsciiPath(fullPath))
            {
                return null;
            }

            var ext = Path.GetExtension(fullPath);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".png";
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "OfflineCtAscii");
            Directory.CreateDirectory(tempDir);
            var staged = Path.Combine(tempDir, $"img_{Guid.NewGuid():N}{ext}");
            File.Copy(fullPath, staged, overwrite: true);
            return staged;
        }

        private static bool IsAsciiPath(string path)
        {
            foreach (var ch in path)
            {
                if (ch > 127)
                {
                    return false;
                }
            }

            return true;
        }

        private static void TryDeleteFile(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 临时文件清理失败不影响主流程
            }
        }

        private static string WriteResultImage(HObject resultImg, string resultImagePath)
        {
            var dir = Path.GetDirectoryName(resultImagePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var ext = Path.GetExtension(resultImagePath);
            var pathWithoutExt = string.IsNullOrEmpty(ext)
                ? resultImagePath
                : resultImagePath[..^ext.Length];

            // Halcon WriteImage：jpeg 质量写法与 CTNCLX 一致
            var format = ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpeg 90";
            var writePath = format.StartsWith("jpeg", StringComparison.OrdinalIgnoreCase)
                ? pathWithoutExt + ".jpg"
                : pathWithoutExt + (string.IsNullOrEmpty(ext) ? ".png" : ext);

            // WriteImage 的文件名参数通常不含扩展名时由 format 决定；此处传无扩展名路径更稳妥
            var writeArg = Path.Combine(
                Path.GetDirectoryName(writePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(writePath));
            HOperatorSet.WriteImage(resultImg, format, 0, writeArg);
            return writePath;
        }

        private static IReadOnlyList<HObject>? ExtractHObjectList(object? raw)
        {
            if (raw is null)
            {
                return null;
            }

            if (raw is List<HObject> typed)
            {
                return typed;
            }

            if (raw is not System.Collections.IEnumerable enumerable)
            {
                return null;
            }

            // 反射出参偶发非 List<HObject>（跨程序集/装箱），逐项挑出 HObject 供叠加绘制
            var list = new List<HObject>();
            foreach (var item in enumerable)
            {
                if (item is HObject ho)
                {
                    list.Add(ho);
                }
            }

            return list.Count > 0 ? list : null;
        }

        private static IReadOnlyList<double[]> ExtractHTupleList(object? raw)
        {
            if (raw is null)
            {
                return Array.Empty<double[]>();
            }

            if (raw is not System.Collections.IEnumerable enumerable)
            {
                return Array.Empty<double[]>();
            }

            var list = new List<double[]>();
            foreach (var item in enumerable)
            {
                list.Add(ExtractHTupleValues(item));
            }

            return list;
        }

        private static double[] ExtractHTupleValues(object? item)
        {
            if (item is null)
            {
                return Array.Empty<double>();
            }

            if (item is HTuple ht)
            {
                return HTupleToDoubles(ht);
            }

            // 反射兜底：Length + 索引器 / DArr
            var type = item.GetType();
            var lengthProp = type.GetProperty("Length");
            if (lengthProp?.GetValue(item) is int length && length >= 0)
            {
                var result = new double[length];
                var indexer = type.GetProperty("Item", [typeof(int)])
                    ?? type.GetProperty("Item");
                for (var i = 0; i < length; i++)
                {
                    var v = indexer?.GetValue(item, [i]) ?? type.GetMethod("get_Item")?.Invoke(item, [i]);
                    result[i] = Convert.ToDouble(v);
                }

                return result;
            }

            var dArrProp = type.GetProperty("DArr");
            if (dArrProp?.GetValue(item) is double[] dArr)
            {
                return dArr;
            }

            return Array.Empty<double>();
        }

        private static double[] HTupleToDoubles(HTuple ht)
        {
            if (ht.Length <= 0)
            {
                return Array.Empty<double>();
            }

            // 必须拷贝：随后会 Dispose 原 HTuple，不能持有其内部缓冲引用
            try
            {
                var dArr = ht.DArr;
                var copy = new double[dArr.Length];
                Array.Copy(dArr, copy, dArr.Length);
                return copy;
            }
            catch
            {
                var result = new double[ht.Length];
                for (var i = 0; i < ht.Length; i++)
                {
                    result[i] = ht[i].D;
                }

                return result;
            }
        }

        /// <summary>
        /// 释放 DLL 出参 <c>List&lt;HObject&gt;</c> 各索引对象（视觉文档强制要求，否则原生内存泄漏）。
        /// </summary>
        private static void DisposeHObjectList(object? raw)
        {
            if (raw is null)
            {
                return;
            }

            if (raw is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    try
                    {
                        if (list[i] is HObject ho)
                        {
                            ho.Dispose();
                        }
                        else if (list[i] is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch
                    {
                        // 忽略单元素释放异常
                    }
                }

                try
                {
                    list.Clear();
                }
                catch
                {
                    // 部分只读集合不支持 Clear
                }

                return;
            }

            if (raw is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    try
                    {
                        if (item is HObject ho)
                        {
                            ho.Dispose();
                        }
                        else if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch
                    {
                        // 忽略单元素释放异常
                    }
                }
            }
        }

        /// <summary>
        /// 释放 DLL 出参 <c>List&lt;HTuple&gt;</c>（与 CTNCLX 一致；拷贝为 double[] 后即可释放原生句柄）。
        /// </summary>
        private static void DisposeHTupleList(object? raw)
        {
            if (raw is null)
            {
                return;
            }

            if (raw is System.Collections.IList list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    try
                    {
                        if (list[i] is HTuple ht)
                        {
                            ht.Dispose();
                        }
                        else if (list[i] is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch
                    {
                        // 忽略单元素释放异常
                    }
                }

                try
                {
                    list.Clear();
                }
                catch
                {
                    // 部分只读集合不支持 Clear
                }

                return;
            }

            if (raw is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    try
                    {
                        if (item is HTuple ht)
                        {
                            ht.Dispose();
                        }
                        else if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch
                    {
                        // 忽略单元素释放异常
                    }
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
