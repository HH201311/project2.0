using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT — 通过 SysCenter 检查并下载 NJCT.dll。
    /// </summary>
    public sealed class OfflineCtDllUpdateService : IOfflineCtDllUpdateService
    {
        private readonly ILoggerService<OfflineCtDllUpdateService> _logger;
        private readonly IWebApiClient _webApiClient;
        private readonly CTAutoSlicerLocalConfigService _localConfigService;

        public OfflineCtDllUpdateService(
            ILoggerService<OfflineCtDllUpdateService> logger,
            IWebApiClient webApiClient,
            CTAutoSlicerLocalConfigService localConfigService)
        {
            _logger = logger;
            _webApiClient = webApiClient;
            _localConfigService = localConfigService;
        }

        public string? GetLocalCachedVersion() => _localConfigService.LoadOfflineCtDllCachedVersion();

        public async Task<OfflineCtDllUpdateCheckResult> CheckForUpdateAsync(
            string dllDownloadBaseUrl,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = NormalizeBaseUrl(dllDownloadBaseUrl);
            var pythonCode = CTAutoSlicerSettings.OfflineCtDllPythonCode;
            var localVersion = GetLocalCachedVersion() ?? string.Empty;
            var localDllExists = CTAutoSlicerSettings.OfflineCtDllExistsLocally();

            _logger.Info(
                $"[NJCT远端] 开始检查 NJCT.dll 更新：baseUrl={baseUrl}，pythonCode={pythonCode}，"
                + $"本地版本记录={localVersion}，本地文件存在={localDllExists}");

            try
            {
                var versionResponse = await _webApiClient
                    .GetFileVersionAsync(baseUrl, pythonCode)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (versionResponse is not { Success: true } || string.IsNullOrWhiteSpace(versionResponse.Version))
                {
                    var failMsg = versionResponse?.Message ?? "版本接口返回为空";
                    _logger.Error(
                        $"[NJCT远端] 查询 NJCT.dll 版本失败：{failMsg}（baseUrl={baseUrl}，pythonCode={pythonCode}）");
                    return new OfflineCtDllUpdateCheckResult
                    {
                        IsSuccess = false,
                        LocalVersion = localVersion,
                        Message = failMsg,
                    };
                }

                var remoteVersion = versionResponse.Version.Trim();
                var updateAvailable = !string.Equals(remoteVersion, localVersion, StringComparison.OrdinalIgnoreCase)
                    || !localDllExists;

                _logger.Info(
                    $"[NJCT远端] 版本比对完成：远端={remoteVersion}，本地记录={localVersion}，"
                    + $"本地文件存在={localDllExists}，需要更新={updateAvailable}");

                return new OfflineCtDllUpdateCheckResult
                {
                    IsSuccess = true,
                    UpdateAvailable = updateAvailable,
                    RemoteVersion = remoteVersion,
                    LocalVersion = localVersion,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"[NJCT远端] 检查 NJCT.dll 版本发生网络或系统异常：{ex.Message}（baseUrl={baseUrl}，pythonCode={pythonCode}）",
                    ex);
                return new OfflineCtDllUpdateCheckResult
                {
                    IsSuccess = false,
                    LocalVersion = localVersion,
                    Message = ex.Message,
                };
            }
        }

        public async Task<OfflineCtDllDownloadResult> DownloadAndInstallAsync(
            string dllDownloadBaseUrl,
            string remoteVersion,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = NormalizeBaseUrl(dllDownloadBaseUrl);
            var appKey = CTAutoSlicerSettings.OfflineCtDllAppKey;
            var pythonCode = CTAutoSlicerSettings.OfflineCtDllPythonCode;
            var executeType = CTAutoSlicerSettings.OfflineCtDllExecuteType;

            _logger.Info(
                $"[NJCT远端] 开始下载 NJCT.dll：baseUrl={baseUrl}，pythonCode={pythonCode}，"
                + $"appKey={appKey}，executeType={executeType}，目标版本={remoteVersion}");

            try
            {
                var xmlPublicKey = RasHelper.ToXmlPublicKey(CTAutoSlicerSettings.OfflineCtDllPublicKey);
                var signature = RasHelper.RSAEncrypt(xmlPublicKey, appKey);
                if (string.IsNullOrEmpty(signature))
                {
                    const string msg = "RSA 签名生成失败，无法请求 NJCT.dll 下载";
                    _logger.Error($"[NJCT远端] {msg}（appKey={appKey}）");
                    return new OfflineCtDllDownloadResult { IsSuccess = false, Message = msg };
                }

                var response = await _webApiClient
                    .DownloadDllAsync(baseUrl, appKey, signature, pythonCode, executeType)
                    .ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();

                if (!response.IsSuccess || response.DataStr is not { Length: > 0 } bytes)
                {
                    var failMsg = response.Message ?? "下载接口未返回文件内容";
                    _logger.Error(
                        $"[NJCT远端] 下载 NJCT.dll 失败：{failMsg}（baseUrl={baseUrl}，pythonCode={pythonCode}）");
                    return new OfflineCtDllDownloadResult { IsSuccess = false, Message = failMsg };
                }

                if (bytes.Length < 2 || bytes[0] != 0x4D || bytes[1] != 0x5A)
                {
                    const string msg = "下载内容不是有效 PE DLL（缺少 MZ 头）";
                    _logger.Error($"[NJCT远端] {msg}（baseUrl={baseUrl}，pythonCode={pythonCode}，字节数={bytes.Length}）");
                    return new OfflineCtDllDownloadResult { IsSuccess = false, Message = msg };
                }

                await WriteDllToBothLocationsAsync(bytes, cancellationToken).ConfigureAwait(false);
                _localConfigService.SaveOfflineCtDllCachedVersion(remoteVersion);

                _logger.Info(
                    $"[NJCT远端] NJCT.dll 下载并落盘成功：版本={remoteVersion}，"
                    + $"Dlls={CTAutoSlicerSettings.GetOfflineCtDllPath()}，"
                    + $"exe同级={CTAutoSlicerSettings.GetOfflineCtDllExeRootPath()}，字节数={bytes.Length}");

                return new OfflineCtDllDownloadResult
                {
                    IsSuccess = true,
                    InstalledVersion = remoteVersion,
                    Message = "下载成功",
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"[NJCT远端] 下载 NJCT.dll 发生网络或系统异常：{ex.Message}（baseUrl={baseUrl}，pythonCode={pythonCode}）",
                    ex);
                return new OfflineCtDllDownloadResult { IsSuccess = false, Message = ex.Message };
            }
        }

        private static async Task WriteDllToBothLocationsAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            var dllsPath = CTAutoSlicerSettings.GetOfflineCtDllPath();
            var exeRootPath = CTAutoSlicerSettings.GetOfflineCtDllExeRootPath();

            var dllsDir = Path.GetDirectoryName(dllsPath);
            if (!string.IsNullOrEmpty(dllsDir))
            {
                Directory.CreateDirectory(dllsDir);
            }

            await File.WriteAllBytesAsync(dllsPath, bytes, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(exeRootPath, bytes, cancellationToken).ConfigureAwait(false);
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

            return url.EndsWith('/') ? url : url + "/";
        }
    }
}
