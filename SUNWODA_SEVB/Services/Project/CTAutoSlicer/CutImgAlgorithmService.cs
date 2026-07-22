using System.Drawing;
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
    /// 反射调用 CTAutoCutImg.dll（类型 CT）。按接口文档使用 LoadFrom；
    /// 原生 halcon 等依赖位于输出目录 Dlls\，初始化时加入 DLL 搜索路径。
    /// </summary>
    public class CutImgAlgorithmService : ICutImgAlgorithmService
    {
        private const string AlgorithmTypeName = "CTAutoCutImg.CT";
        private const string AlgorithmTypeNameFallback = "CT";
        private const string SectionReferenceMethodName = "GetSectionReferencePoints";
        private const string TopBottomMethodName = "GetObjectTopBottomPoints";

        private readonly ILoggerService<CutImgAlgorithmService> _logger;
        private object? _algorithmInstance;
        private MethodInfo? _sectionReferenceMethod;
        private MethodInfo? _topBottomMethod;
        private static bool _nativeSearchPathConfigured;

        public CutImgAlgorithmService(ILoggerService<CutImgAlgorithmService> logger)
        {
            _logger = logger;
        }

        public bool IsInitialized =>
            _algorithmInstance != null
            && _sectionReferenceMethod != null
            && _topBottomMethod != null;

        public void Initialize()
        {
            EnsureNativeDllSearchPath();

            var dllPath = CTAutoSlicerSettings.GetCutImgAlgorithmDllPath();
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException(
                    $"未找到切图算法 DLL：{dllPath}。请将 CTAutoCutImg.dll 放到程序目录下的 Dlls 文件夹。",
                    dllPath);
            }

            _logger.Info($"加载切图算法 DLL：{dllPath}");
            var assembly = Assembly.LoadFrom(dllPath);
            var algorithmType = assembly.GetType(AlgorithmTypeName)
                ?? assembly.GetType(AlgorithmTypeNameFallback)
                ?? throw new InvalidOperationException(
                    $"未找到算法类型 {AlgorithmTypeName}（亦尝试 {AlgorithmTypeNameFallback}）。" +
                    $"DLL 内可用类型：{string.Join(", ", assembly.GetTypes().Select(t => t.FullName))}");

            _algorithmInstance = Activator.CreateInstance(algorithmType)
                ?? throw new InvalidOperationException($"无法创建算法实例 {algorithmType.FullName}");

            _sectionReferenceMethod = algorithmType.GetMethod(SectionReferenceMethodName)
                ?? throw new InvalidOperationException($"未找到算法方法 {SectionReferenceMethodName}");

            _topBottomMethod = algorithmType.GetMethod(TopBottomMethodName)
                ?? throw new InvalidOperationException($"未找到算法方法 {TopBottomMethodName}");

            _logger.Info("CT 切图算法（CTAutoCutImg）初始化成功");
        }

        public (int X, int Y)[] GetSectionReferencePoints(string imagePath)
        {
            EnsureInitialized();
            return InvokeWithImage(_sectionReferenceMethod!, imagePath, expectedMinCount: 4);
        }

        public (int X, int Y)[] GetObjectTopBottomPoints(string imagePath)
        {
            EnsureInitialized();
            return InvokeWithImage(_topBottomMethod!, imagePath, expectedMinCount: 2);
        }

        private (int X, int Y)[] InvokeWithImage(MethodInfo method, string imagePath, int expectedMinCount)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException("切图算法输入图像不存在", imagePath);
            }

            // ReadImage 会 P/Invoke 加载 halcon.dll，须先指向输出目录 Dlls\
            EnsureNativeDllSearchPath();

            HObject? image = null;
            try
            {
                HOperatorSet.ReadImage(out image, imagePath);
                object?[] parameters = [image, null];
                method.Invoke(_algorithmInstance, parameters);

                var points = ConvertToXyPoints(parameters[1]);
                if (points.Length < expectedMinCount)
                {
                    throw new InvalidOperationException(
                        $"{method.Name} 返回点数不足：期望至少 {expectedMinCount}，实际 {points.Length}");
                }

                return points;
            }
            finally
            {
                image?.Dispose();
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("切图算法尚未初始化，请先调用 Initialize()");
            }
        }

        /// <summary>
        /// 将 {程序目录}\Dlls 加入原生 DLL 搜索路径，使 P/Invoke 能加载 halcon.dll 等依赖。
        /// </summary>
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
                    $"未找到原生 DLL 目录：{dllsDir}。请确认 halcon.dll 等已复制到输出目录 Dlls\\。");
            }

            // SetDllDirectory：让 LoadLibrary("halcon") 能解析到 Dlls\halcon.dll
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
            _logger.Info($"已配置原生 DLL 搜索目录：{dllsDir}");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        /// <summary>
        /// 兼容 System.Drawing.Point[] / 具备 X,Y 属性的数组元素。
        /// </summary>
        private static (int X, int Y)[] ConvertToXyPoints(object? raw)
        {
            if (raw is Point[] drawingPoints)
            {
                return drawingPoints.Select(p => (p.X, p.Y)).ToArray();
            }

            if (raw is not Array array)
            {
                throw new InvalidOperationException(
                    $"算法输出 points 类型无效：{(raw?.GetType().FullName ?? "null")}");
            }

            var result = new (int X, int Y)[array.Length];
            for (var i = 0; i < array.Length; i++)
            {
                var item = array.GetValue(i)
                    ?? throw new InvalidOperationException($"算法输出 points[{i}] 为空");

                if (item is Point p)
                {
                    result[i] = (p.X, p.Y);
                    continue;
                }

                var xProp = item.GetType().GetProperty("X")
                    ?? throw new InvalidOperationException($"points[{i}] 缺少 X 属性：{item.GetType().FullName}");
                var yProp = item.GetType().GetProperty("Y")
                    ?? throw new InvalidOperationException($"points[{i}] 缺少 Y 属性：{item.GetType().FullName}");

                result[i] = (
                    Convert.ToInt32(xProp.GetValue(item)),
                    Convert.ToInt32(yProp.GetValue(item)));
            }

            return result;
        }
    }
}
