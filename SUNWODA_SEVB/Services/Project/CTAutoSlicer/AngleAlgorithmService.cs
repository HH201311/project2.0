using System.IO;
using System.Reflection;
using HalconDotNet;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public class AngleAlgorithmService : IAngleAlgorithmService
    {
        private const string PublicKey =
            "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCGHlQzjOW6tTY0H9JjuorN9QYftH8WMWf7LA+bH3yGXCFEn2e+IMAjS3/LHtQTNBXro80+Gdzvv/UzzB2bsXUHLv7EUhoxxJa308N99P3AS5AzhiTRx5AZ3yMjNyAgkAXQ51vqi6q0pfoxt5/I3/KaCtz4wkm3hSg4g1JYPylj5QIDAQAB";

        private readonly ILoggerService<AngleAlgorithmService> _logger;
        private readonly IWebApiClient _webApiClient;
        private object? _algorithmInstance;
        private MethodInfo? _angleMeasureMethod;

        public AngleAlgorithmService(ILoggerService<AngleAlgorithmService> logger, IWebApiClient webApiClient)
        {
            _logger = logger;
            _webApiClient = webApiClient;
        }

        public bool IsInitialized => _algorithmInstance != null && _angleMeasureMethod != null;

        public async Task InitializeAsync(string dllDownloadBaseUrl, CancellationToken cancellationToken = default)
        {
            var assembly = await LoadAssemblyAsync(dllDownloadBaseUrl, cancellationToken).ConfigureAwait(false);
            if (assembly == null)
            {
                throw new InvalidOperationException("角度识别算法 DLL 加载失败");
            }

            var algorithmType = assembly.GetType("NJSuanzi.Class1")
                ?? throw new InvalidOperationException("未找到算法类型 NJSuanzi.Class1");

            _algorithmInstance = Activator.CreateInstance(algorithmType)
                ?? throw new InvalidOperationException("无法创建算法实例");

            _angleMeasureMethod = algorithmType.GetMethod("Secondary_positioning")
                ?? throw new InvalidOperationException("未找到算法方法 Secondary_positioning");

            _logger.Info("CT 角度识别算法初始化成功");
        }

        public (double deg, string direction) MeasureAngle(string measurementType, string imagePath)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("角度识别算法尚未初始化");
            }

            HTuple deliveryMeasurement = measurementType;
            HTuple imagePathTuple = imagePath;
            HTuple degStr = null;
            HTuple directionStr = null;
            var parameters = new object?[] { deliveryMeasurement, imagePathTuple, degStr, directionStr };

            try
            {
                _angleMeasureMethod!.Invoke(_algorithmInstance, parameters);
                var deg = ((HTuple)parameters[2]!).S.ToDouble();
                var direction = ((HTuple)parameters[3]!).S;
                return (deg, direction);
            }
            finally
            {
                deliveryMeasurement?.Dispose();
                imagePathTuple?.Dispose();
                ((HTuple?)parameters[2])?.Dispose();
                ((HTuple?)parameters[3])?.Dispose();
            }
        }

        private async Task<Assembly?> LoadAssemblyAsync(string dllDownloadBaseUrl, CancellationToken cancellationToken)
        {
            try
            {
                var dllPath = CTAutoSlicerSettings.GetAngleAlgorithmDllPath();
                if (File.Exists(dllPath))
                {
                    _logger.Info($"加载本地角度算法 DLL：{dllPath}");
                    await using var fileStream = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new BinaryReader(fileStream);
                    var bytes = reader.ReadBytes((int)fileStream.Length);
                    return Assembly.Load(bytes);
                }

                _logger.Info("本地角度算法 DLL 不存在，开始远程下载");
                var xmlPublicKey = RasHelper.ToXmlPublicKey(PublicKey);
                var signature = RasHelper.RSAEncrypt(xmlPublicKey, CTAutoSlicerSettings.AppKey);
                if (string.IsNullOrEmpty(signature))
                {
                    throw new InvalidOperationException("RSA 签名生成失败");
                }

                var baseUrl = NormalizeBaseUrl(dllDownloadBaseUrl);
                var response = await _webApiClient
                    .DownloadDllAsync(baseUrl, CTAutoSlicerSettings.AppKey, signature, CTAutoSlicerSettings.PythonCode, "PRED")
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (!response.IsSuccess || response.DataStr == null || response.DataStr.Length == 0)
                {
                    throw new InvalidOperationException($"远程下载 DLL 失败：{response.Message}");
                }

                if (response.DataStr.Length < 2 || response.DataStr[0] != 0x4D || response.DataStr[1] != 0x5A)
                {
                    throw new InvalidOperationException("下载的文件不是有效 DLL");
                }

                return Assembly.Load(response.DataStr);
            }
            catch (Exception ex)
            {
                _logger.Error("加载角度算法 DLL 异常", ex);
                return null;
            }
        }

        private static string NormalizeBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return CTAutoSlicerSettings.DefaultDllDownloadBaseUrl;
            }

            url = url.Trim();
            if (url.Contains("sysCenter/interfaceApi/downloadFile", StringComparison.OrdinalIgnoreCase))
            {
                var index = url.IndexOf("sysCenter", StringComparison.OrdinalIgnoreCase);
                url = url[..index];
            }

            return url.EndsWith('/') ? url : $"{url}/";
        }
    }
}
