using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.CCTB.Business
{
    /// <summary>
    /// 业务服务工厂
    /// </summary>
    public class BusinessServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<EBusinessType, IBusinessService> _services;

        public BusinessServiceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _services = new Dictionary<EBusinessType, IBusinessService>();
        }

        /// <summary>
        /// 注册业务服务
        /// </summary>
        public void RegisterService<T>() where T : IBusinessService
        {
            var service = (IBusinessService?)_serviceProvider.GetService(typeof(T));
            if (service != null)
            {
                var businessType = service.BusinessType;
                _services[businessType] = service;
            }
        }

        /// <summary>
        /// 获取业务服务
        /// </summary>
        public IBusinessService GetService(EBusinessType businessType)
        {
            if (_services.TryGetValue(businessType, out var service))
            {
                return service;
            }
            throw new KeyNotFoundException($"未找到业务类型 {businessType} 的服务");
        }

        /// <summary>
        /// 获取所有业务服务
        /// </summary>
        public IEnumerable<IBusinessService> GetAllServices()
        {
            return _services.Values;
        }

        /// <summary>
        /// 停止所有业务
        /// </summary>
        public async Task StopAllAsync()
        {
            var tasks = _services.Values.Select(s => s.StopAsync());
            await Task.WhenAll(tasks);
        }
    }
}