using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    /// <summary>
    /// 反射调用 NJCT.NJSwdCT（ModelInit / ModelAction / ModelAction_origin）。
    /// DLL 与 Halcon/ONNX 原生依赖位于输出目录 Dlls\。
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

        public OfflineCtInferResult InferEnhance(string imagePath, int blackHeight, string? resultImagePath = null) =>
            Infer(ModelActionMethodName, "enhance", imagePath, blackHeight, resultImagePath);

        public OfflineCtInferResult InferOrigin(string imagePath, int blackHeight, string? resultImagePath = null) =>
            Infer(ModelActionOriginMethodName, "origin", imagePath, blackHeight, resultImagePath);

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
            string? resultImagePath)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("AI 标注输入图像不存在", imagePath);
            }

            EnsureNativeDllSearchPath();

            var method = methodName == ModelActionOriginMethodName
                ? _modelActionOriginMethod!
                : _modelActionMethod!;

            // ImgPath, Models, BlackHeight, ResultImg, AllPointRegion, AllRegionRow, AllRegionCol, AllPrintfValue, RunInfo
            object?[] parameters =
            [
                imagePath,
                _modelHandle,
                blackHeight,
                null,
                null,
                null,
                null,
                null,
                null,
            ];

            HObject? resultImg = null;
            List<HObject>? pointRegions = null;
            try
            {
                method.Invoke(_algorithmInstance, parameters);

                resultImg = parameters[3] as HObject;
                pointRegions = parameters[4] as List<HObject>;
                var regionRows = ExtractHTupleList(parameters[5]);
                var regionCols = ExtractHTupleList(parameters[6]);
                var printfValues = ExtractHTupleList(parameters[7]);
                var runInfo = parameters[8] as string ?? string.Empty;

                string? writtenPath = null;
                if (!string.IsNullOrWhiteSpace(resultImagePath) && resultImg is not null)
                {
                    writtenPath = WriteResultImage(resultImg, resultImagePath);
                }

                if (!string.IsNullOrEmpty(runInfo))
                {
                    _logger.Warn($"离线 CT AI 推理 RunInfo（{modeTag}）：{runInfo}");
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
                DisposeHObjectList(pointRegions);
                resultImg?.Dispose();
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

            _nativeSearchPathConfigured = true;
            _logger.Info($"已配置离线 CT AI 原生 DLL 搜索目录：{dllsDir}");
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

            try
            {
                return ht.DArr;
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

        private static void DisposeHObjectList(List<HObject>? list)
        {
            if (list is null)
            {
                return;
            }

            foreach (var obj in list)
            {
                try
                {
                    obj?.Dispose();
                }
                catch
                {
                    // 忽略释放异常，避免掩盖主异常
                }
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
