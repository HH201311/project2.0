using System.Diagnostics;
using System.Windows;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer
{
    public partial class VM_CTAutoSlicerPage
    {
        private const string OfflineCtRemoteDllHint =
            "仅 AI 标注模式下启动时会向服务器检查 NJCT.dll 更新。"
            + "云端下载接口对 IP 做了白名单限制，只有 AI 标注机可成功访问；切图机请勿开启。";

        /// <summary>AI 标注页 — 启动时是否检查 NJCT.dll 远端更新。</summary>
        public bool EnableOfflineCtRemoteDllDownload
        {
            get => _enableOfflineCtRemoteDllDownload;
            set
            {
                if (SetProperty(ref _enableOfflineCtRemoteDllDownload, value))
                {
                    SaveSetting(nameof(EnableOfflineCtRemoteDllDownload), value ? 1 : 0);
                }
            }
        }

        public string OfflineCtRemoteDllDownloadHint => OfflineCtRemoteDllHint;

        private async Task TryCheckOfflineCtDllUpdateOnStartupAsync()
        {
            if (!IsAiPipelineMode)
            {
                return;
            }

            if (!EnableOfflineCtRemoteDllDownload)
            {
                _logger.Info("[NJCT远端] 已跳过启动检查：未开启「远端下载 NJCT.dll」");
                return;
            }

            try
            {
                var check = await _offlineCtDllUpdateService
                    .CheckForUpdateAsync(_dllDownloadBaseUrl)
                    .ConfigureAwait(false);

                if (!check.IsSuccess)
                {
                    var logMsg =
                        $"[NJCT远端] 启动检查 NJCT.dll 失败：{check.Message}（服务器={_dllDownloadBaseUrl}）";
                    _logger.Error(logMsg);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendFlowLog(logMsg);
                        if (!CTAutoSlicerSettings.OfflineCtDllExistsLocally())
                        {
                            MessageShower =
                                $"NJCT.dll 远端检查失败且本地无 DLL：{check.Message}。请检查网络、IP 白名单或手动部署。";
                        }
                    });
                    return;
                }

                if (!check.UpdateAvailable)
                {
                    _logger.Info(
                        $"[NJCT远端] 启动检查：NJCT.dll 已是最新版本（{check.RemoteVersion}）");
                    return;
                }

                var remoteVersion = check.RemoteVersion ?? string.Empty;
                var localVersion = string.IsNullOrWhiteSpace(check.LocalVersion)
                    ? "（未记录）"
                    : check.LocalVersion;
                var localDllExists = CTAutoSlicerSettings.OfflineCtDllExistsLocally();

                var prompt =
                    $"检测到 NJCT.dll 有新版本。\n\n"
                    + $"远端版本：{remoteVersion}\n"
                    + $"本地记录：{localVersion}\n"
                    + $"本地文件：{(localDllExists ? "已存在" : "缺失")}\n\n"
                    + "是否立即下载？下载完成后需要重启软件。";

                var confirm = await Dispatcher.InvokeAsync(() =>
                    MessageBox.Show(
                        prompt,
                        "NJCT.dll 更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information));

                if (confirm != MessageBoxResult.Yes)
                {
                    _logger.Info("[NJCT远端] 用户取消 NJCT.dll 更新下载");
                    await Dispatcher.InvokeAsync(() =>
                        AppendFlowLog("[NJCT远端] 用户取消 NJCT.dll 更新下载"));
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                    MessageShower = $"正在下载 NJCT.dll（版本 {remoteVersion}）…");

                var download = await _offlineCtDllUpdateService
                    .DownloadAndInstallAsync(_dllDownloadBaseUrl, remoteVersion)
                    .ConfigureAwait(false);

                if (!download.IsSuccess)
                {
                    var failMsg = $"[NJCT远端] NJCT.dll 下载失败：{download.Message}";
                    _logger.Error(failMsg);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        AppendFlowLog(failMsg);
                        MessageShower = failMsg;
                    });
                    return;
                }

                _logger.Info($"[NJCT远端] NJCT.dll 下载完成，准备重启软件（版本={download.InstalledVersion}）");
                await Dispatcher.InvokeAsync(() =>
                {
                    AppendFlowLog(
                        $"[NJCT远端] NJCT.dll 已更新至 {download.InstalledVersion}，即将重启软件…");
                    var restart = MessageBox.Show(
                        "NJCT.dll 下载完成，需要重启软件后生效。是否立即重启？",
                        "NJCT.dll 更新",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (restart == MessageBoxResult.Yes)
                    {
                        RestartApplication();
                    }
                    else
                    {
                        MessageShower = "NJCT.dll 已下载，请手动重启软件后生效。";
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[NJCT远端] 启动检查 NJCT.dll 发生未处理异常：{ex.Message}", ex);
                await Dispatcher.InvokeAsync(() =>
                {
                    AppendFlowLog($"[NJCT远端] 启动检查异常：{ex.Message}");
                    MessageShower = $"NJCT.dll 远端检查异常：{ex.Message}";
                });
            }
        }

        private static void RestartApplication()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (!string.IsNullOrWhiteSpace(exePath))
            {
                Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            }

            Application.Current.Shutdown();
        }
    }
}
