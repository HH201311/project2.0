using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Common.Services
{
    /// <summary>
    /// 全局服务定位器
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        /// <summary>
        /// 初始化服务定位器
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T GetService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceLocator未初始化，请先调用Initialize方法");

            var service = _serviceProvider?.GetService(typeof(T)) as T;
            return service ?? throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册");
        }

        /// <summary>
        /// 获取必需服务（如果不存在则抛出异常）
        /// </summary>
        public static T GetRequiredService<T>() where T : class
        {
            if (_serviceProvider == null)
                throw new InvalidOperationException("ServiceLocator未初始化，请先调用Initialize方法");

            var service = _serviceProvider.GetService(typeof(T)) as T;
            return service ?? throw new InvalidOperationException($"服务 {typeof(T).Name} 未注册");
        }
    }
}
