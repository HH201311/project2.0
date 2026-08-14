using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB
{
    /// <summary>
    /// 通讯状态监控与自动重连后台服务
    /// </summary>
    public class CommunicationMonitorService : BackgroundService
    {
        private readonly ILoggerService<CommunicationMonitorService> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly ICommunicationService _communicationService;

        public CommunicationMonitorService(
            ILoggerService<CommunicationMonitorService> logger,
            IGlobalSettingRepository globalSettingRepository,
            ICommunicationService communicationService)
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _communicationService = communicationService;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            try
            {
                // 等待数据库初始化完成
                await App.DatabaseInitTask;
            }
            catch (Exception ex)
            {
                // 初始化失败
                _logger.Error($"数据库初始化失败,已退出通讯监控服务！", ex);
                return;
            }

            // 如果不是CCTB项目，则退出
            var appStartMainWindow = _globalSettingRepository.GetSettingValue("AppStartMainWindow");
            if (appStartMainWindow != "CCTBMainWindow") return;

            _logger.Info("通讯监控服务已启动");

            // 并行运行两个独立循环
            var t1 = RefreshDeviceStatusMonitorLoopAsync(ct);
            var t2 = CheckAndReconnectMonitorLoopAsync(ct);
            await Task.WhenAll(t1, t2);

            _logger.Info("通讯监控服务已停止");
        }

        public override async Task StopAsync(CancellationToken ct)
        {
            _logger.Info("正在停止通讯监控服务...");
            await _communicationService.StopAllAsync(ct);
            await base.StopAsync(ct);
        }

        /// <summary>
        /// 设备重连
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task CheckAndReconnectMonitorLoopAsync(CancellationToken ct)
        {
            // 首次启动时初始化所有通讯
            try
            {
                await _communicationService.StartAllAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.Error("通讯服务首次启动失败", ex);
            }

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5000));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await _communicationService.CheckAndReconnectAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error("通讯设备重连检查异常", ex);
                }
            }
        }

        /// <summary>
        /// 设备状态监听
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task RefreshDeviceStatusMonitorLoopAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    await _communicationService.RefreshDeviceStatusAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error("通讯设备状态检查异常", ex);
                }
            }
        }
    }
}