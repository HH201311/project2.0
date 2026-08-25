using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// 业务服务管理器
    /// </summary>
    public class BusinessServiceManager
    {
        private readonly ILoggerService<BusinessServiceManager> _logger;
        private readonly BusinessServiceFactory _factory;
        private readonly Dictionary<EBusinessType, Task> _runningTasks;

        public BusinessServiceManager(
            BusinessServiceFactory factory,
            ILoggerService<BusinessServiceManager> logger)
        {
            _logger = logger;
            _factory = factory;
            _runningTasks = new Dictionary<EBusinessType, Task>();

            // 在管理器构造时完成注册
            _factory.RegisterService<BackBusinessService>();
            _factory.RegisterService<MDBusinessService>();
            _factory.RegisterService<TDBusinessService>();
            _factory.RegisterService<MDTDBusinessService>();
        }

        /// <summary>
        /// 启动指定业务
        /// </summary>
        public async Task StartBusinessAsync(EBusinessType businessType)
        {
            if (_runningTasks.ContainsKey(businessType))
            {
                _logger.Warn($"业务 {businessType} 已在运行中");
                return;
            }

            var service = _factory.GetService(businessType);
            var task = Task.Run(async () =>
            {
                try
                {
                    await service.StartAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error($"业务 {businessType} 运行异常", ex);
                }
                finally
                {
                    _runningTasks.Remove(businessType);
                }
            });

            _runningTasks[businessType] = task;
            _logger.Info($"业务 {businessType} 已启动");
        }

        /// <summary>
        /// 停止指定业务
        /// </summary>
        public async Task StopBusinessAsync(EBusinessType businessType)
        {
            var service = _factory.GetService(businessType);
            await service.StopAsync();

            if (_runningTasks.TryGetValue(businessType, out var task))
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    _logger.Error($"停止业务 {businessType} 时发生异常", ex);
                }
                _runningTasks.Remove(businessType);
            }

            _logger.Info($"业务 {businessType} 已停止");
        }

        /// <summary>
        /// 获取运行状态
        /// </summary>
        public IEnumerable<(EBusinessType BusinessType, string BusinessName, bool IsRunning)> GetStatus()
        {
            var services = _factory.GetAllServices();
            return services.Select(s => (s.BusinessType, s.BusinessName, s.IsRunning));
        }
    }
}