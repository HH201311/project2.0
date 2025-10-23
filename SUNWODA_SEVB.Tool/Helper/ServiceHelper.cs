using System.Diagnostics;
using System.ServiceProcess;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class ServiceHelper
    {
        public static bool IsServiceInstalled(string serviceName)
        {
            foreach (var s in ServiceController.GetServices())
            {
                if (s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static void StartService(string serviceName)
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
            }
        }

        public static void StopService(string serviceName)
        {
            using var sc = new ServiceController(serviceName);
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
            }
        }

        public static void InstallService(string exePath, string serviceName)
        {
            RunScCommand($"create \"{serviceName}\" binPath= \"{exePath}\" start= auto");
        }

        public static void UninstallService(string serviceName)
        {
            RunScCommand($"delete \"{serviceName}\"");
        }

        /// <summary>
        /// 设置服务描述
        /// </summary>
        public static void SetServiceDescription()
        {
            RunScCommand($"description WatchdogService \"🛡️ 应用程序看门狗服务 v1.0.0\"");
        }

        private static void RunScCommand(string args)
        {
            var psi = new ProcessStartInfo("sc.exe", args)
            {
                Verb = "runas", // 提升为管理员
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi)?.WaitForExit();
        }
    }
}
