using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;

namespace SUNWODA_SEVB.Services
{
    public class ModuleManager : IModuleManager
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<ModuleManager> _logger;
        private readonly IWorkSpaceProjectRepository _workSpaceProjectRepository;
        private readonly List<ModuleInfo> _modules = new List<ModuleInfo>();

        // 使用线程安全的字典缓存
        private readonly ConcurrentDictionary<string, object> _viewModelCache =
            new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, Type> _viewTypeCache =
            new ConcurrentDictionary<string, Type>();
        private readonly ConcurrentDictionary<string, WeakReference> _weakViewCache =
            new ConcurrentDictionary<string, WeakReference>();

        public event EventHandler<ModuleRegisteredEventArgs>? ModuleRegistered;

        public IReadOnlyList<ModuleInfo> Modules => _modules.AsReadOnly();

        public ModuleManager(
            IServiceProvider serviceProvider,
            ILoggerService<ModuleManager> logger,
            IWorkSpaceProjectRepository workSpaceProjectRepository
        )
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workSpaceProjectRepository = workSpaceProjectRepository;
        }

        public void AutoRegisterModules()
        {
            _logger?.Info("开始自动注册业务模块");

            var assemblies = AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(a => a.FullName?.StartsWith(this.GetType().Namespace?.Split('.')[0] ?? "SUNWODA_SEVB") ?? false)
                .ToList();

            // 禁止 Parallel：SqlSugar 同步查询共用连接时并发 Open 会报
            // 「Cannot Open when State is Connecting」，导致整程序集模块注册失败、侧边栏空白。
            foreach (var assembly in assemblies)
            {
                RegisterModulesFromAssembly(assembly);
            }

            // 按Order排序
            lock (_modules)
            {
                _modules.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            OrganizeModuleHierarchy();

            _logger?.Info($"业务模块注册完成，总业务模块数: {_modules.Count}");
        }

        private void RegisterModulesFromAssembly(Assembly assembly)
        {
            try
            {
                _logger?.Debug($"扫描程序集: {assembly.FullName}");

                var viewModelTypes = GetLoadableTypes(assembly)
                    .Where(t =>
                        t.IsClass
                        && !t.IsAbstract
                        && t.IsSubclassOf(typeof(ViewModelBase))
                        && t.GetCustomAttribute<ModuleAttribute>() != null
                    )
                    .ToList();

                foreach (var vmType in viewModelTypes)
                {
                    try
                    {
                        var moduleAttr = vmType.GetCustomAttribute<ModuleAttribute>();
                        var viewType = FindViewForViewModel(vmType, assembly);
                        var isEnabled = _workSpaceProjectRepository.GetIsEnabled(vmType.Name);

                        if (isEnabled)
                        {
                            if (viewType != null && moduleAttr != null)
                            {
                                var moduleInfo = new ModuleInfo
                                {
                                    Name = moduleAttr.Name,
                                    DisplayName = moduleAttr.DisplayName,
                                    Icon = moduleAttr.Icon,
                                    Order = moduleAttr.Order,
                                    Category = moduleAttr.Category,
                                    Type = moduleAttr.Type,
                                    RequiredPermissions = moduleAttr.RequiredPermissions,
                                    ViewType = viewType,
                                    ViewModelType = vmType,
                                };

                                lock (_modules)
                                {
                                    RegisterModule(moduleInfo);
                                }

                                _logger?.Info(
                                    $"注册业务模块: {moduleInfo.Name} ({moduleInfo.DisplayName})"
                                );
                            }
                            else
                            {
                                _logger?.Warn($"没有找到 ViewModel 对应的 View: {vmType.Name}", true);
                            }
                        }
                    }
                    catch (Exception moduleEx)
                    {
                        // 单模块失败不得拖垮整程序集（否则侧边栏大量缺失）
                        _logger?.Error(
                            $"注册业务模块失败，已跳过: {vmType.Name}",
                            moduleEx,
                            true
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"无法从 {assembly.FullName} 中注册业务模块", ex, true);
            }
        }

        /// <summary>
        /// 安全枚举类型：缺依赖（如 OpenCvSharp）时 GetTypes 会抛 ReflectionTypeLoadException，
        /// 若直接放弃则侧边栏模块数为 0；此处保留已成功加载的类型。
        /// </summary>
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }

        public void AutoRegisterModulesByName(params string[] moduleNames)
        {
            _logger?.Info("开始自动注册业务模块");

            var assemblies = AppDomain
                .CurrentDomain.GetAssemblies()
                .Where(a => a.FullName?.StartsWith(this.GetType().Namespace?.Split('.')[0] ?? "SUNWODA_SEVB") ?? false)
                .ToList();

            // 同 AutoRegisterModules：禁止并行扫库注册
            foreach (var assembly in assemblies)
            {
                RegisterModulesFromAssemblyWithNames(assembly, moduleNames);
            }

            // 按Order排序
            lock (_modules)
            {
                _modules.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            OrganizeModuleHierarchy();

            _logger?.Info($"业务模块注册完成，总业务模块数: {_modules.Count}");
        }

        private void RegisterModulesFromAssemblyWithNames(Assembly assembly, params string[] moduleNames)
        {
            try
            {
                _logger?.Debug($"扫描程序集: {assembly.FullName}");

                var viewModelTypes = GetLoadableTypes(assembly)
                    .Where(t =>
                        t.IsClass
                        && !t.IsAbstract
                        && t.IsSubclassOf(typeof(ViewModelBase))
                        && t.GetCustomAttribute<ModuleAttribute>() != null
                    )
                    .ToList();

                foreach (var vmType in viewModelTypes)
                {
                    var moduleAttr = vmType.GetCustomAttribute<ModuleAttribute>();
                    var viewType = FindViewForViewModel(vmType, assembly);
                    var isContains = moduleNames.Contains(vmType.Name);
            
                    if (isContains)
                    {
                        if (viewType != null && moduleAttr != null)
                        {
                            var moduleInfo = new ModuleInfo
                            {
                                Name = moduleAttr.Name,
                                DisplayName = moduleAttr.DisplayName,
                                Icon = moduleAttr.Icon,
                                Order = moduleAttr.Order,
                                Category = moduleAttr.Category,
                                Type = moduleAttr.Type,
                                RequiredPermissions = moduleAttr.RequiredPermissions,
                                ViewType = viewType,
                                ViewModelType = vmType,
                            };

                            lock (_modules)
                            {
                                RegisterModule(moduleInfo);
                            }

                            _logger?.Info(
                                $"注册业务模块: {moduleInfo.Name} ({moduleInfo.DisplayName})"
                            );
                        }
                        else
                        {
                            _logger?.Warn($"没有找到 ViewModel 对应的 View: {vmType.Name}", true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"无法从 {assembly.FullName} 中注册业务模块", ex, true);
            }
        }

        private Type? FindViewForViewModel(Type viewModelType, Assembly assembly)
        {
            Type? viewType = null;
            if (_viewTypeCache.TryGetValue(viewModelType.Name, out viewType))
            {
                return viewType;
            }

            // 约定：ViewModel名称去掉"VM_"前缀就是View名称
            var viewName = viewModelType.Name.Replace("VM_", "");
            var viewNamespace = viewModelType.Namespace!.Replace(".ViewModels", ".Views");
            var fullViewName = $"{viewNamespace}.{viewName}";

            viewType =
                assembly.GetType(fullViewName)
                ?? GetLoadableTypes(assembly).FirstOrDefault(t => t.Name == viewName);

            if (viewType != null)
            {
                _viewTypeCache[viewModelType.Name] = viewType;
            }

            return viewType;
        }

        private void OrganizeModuleHierarchy()
        {
            // 组织多级导航结构
            var categorizedModules = _modules
                .Where(m => !string.IsNullOrEmpty(m.Category))
                .ToList();

            foreach (var module in categorizedModules)
            {
                var categoryParts = module.Category!.Split('/');
                if (categoryParts.Length > 1)
                {
                    var parentName = categoryParts[0];
                    var parent = _modules.FirstOrDefault(m => m.Name == parentName);
                    if (parent != null && !parent.SubModules.Contains(module))
                    {
                        parent.SubModules.Add(module);
                    }
                }
            }
        }

        public void RegisterModule(ModuleInfo module)
        {
            if (_modules.Any(m => m.Name == module.Name))
            {
                _logger?.Warn($"业务模块 {module.Name} 已经注册", true);
                return;
            }

            _modules.Add(module);

            // 异步触发事件，避免阻塞
            Task.Run(() =>
            {
                ModuleRegistered?.Invoke(this, new ModuleRegisteredEventArgs { Module = module });
            });
        }

        public IReadOnlyList<ModuleInfo> GetModulesByType(ModuleType type)
        {
            return _modules.Where(m => m.Type == type).ToList().AsReadOnly();
        }

        public ModuleInfo? GetModule(string moduleName)
        {
            return _modules.FirstOrDefault(m => m.Name == moduleName);
        }

        public object? GetViewModel(ModuleInfo? module)
        {
            if (module == null || module?.Name == null || module?.ViewModelType == null)
                return null;

            return _viewModelCache.GetOrAdd(
                module.Name,
                key =>
                {
                    try
                    {
                        // 优先使用依赖注入创建ViewModel
                        var viewModel =
                            _serviceProvider.GetService(module.ViewModelType)
                            ?? ActivatorUtilities.CreateInstance(
                                _serviceProvider,
                                module.ViewModelType
                            );

                        // 异步初始化
                        if (viewModel is ViewModelBase vmBase)
                        {
                            vmBase?.OnInitialize();
                        }

                        _logger?.Debug($"创建业务模块的ViewModel实例: {module.Name}");
                        return viewModel;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"不能创建业务模块的ViewModel: {module.Name}", ex, true);
                        throw;
                    }
                }
            );
        }

        public object? GetViewModel(string moduleName)
        {
            var module = GetModule(moduleName);
            return GetViewModel(module);
        }

        public Page? GetView(ModuleInfo? module)
        {
            if (module == null || module.ViewType == null)
                return null;

            try
            {
                // 先检查弱引用缓存
                if (module.Name != null && _weakViewCache.TryGetValue(module.Name, out var weakRef))
                {
                    if (weakRef.IsAlive && weakRef.Target is Page cachedPage)
                    {
                        _logger?.Debug($"从缓存获取View: {module.Name}");
                        return cachedPage;
                    }
                    else
                    {
                        // 清理失效的弱引用
                        _weakViewCache.TryRemove(module.Name, out _);
                    }
                }

                // 创建新的View实例
                var view =
                    (
                        _serviceProvider.GetService(module.ViewType)
                        ?? ActivatorUtilities.CreateInstance(_serviceProvider, module.ViewType)
                    ) as Page;

                if (view == null)
                {
                    _logger?.Error($"View类型 {module.ViewType.Name} 不是 Page", true);
                    throw new InvalidOperationException(
                        $"View 必须继承 Page: {module.ViewType.Name}"
                    );
                }

                // 设置DataContext
                if (view.DataContext is null)
                {
                    view.DataContext = GetViewModel(module);
                }

                // 设置Page的一些默认属性
                if (string.IsNullOrEmpty(view.Title))
                {
                    view.Title = module.DisplayName;
                }

                // 对于重要的模块，使用弱引用缓存
                if (module.Name != null && ShouldCacheView(module))
                {
                    _weakViewCache[module.Name!] = new WeakReference(view);
                }

                _logger?.Debug($"为业务模块创建 View 实例: {module.Name}");
                return view;
            }
            catch (Exception ex)
            {
                _logger?.Error($"业务模块不能创建 View: {module.Name}", ex, true);
                throw;
            }
        }

        private bool ShouldCacheView(ModuleInfo module)
        {
            // 只缓存重要的模块
            return module.Type == ModuleType.Dashboard
                || module.Type == ModuleType.Settings
                || module.Type == ModuleType.UserCenter
                || module.Order < 2;
        }

        public Page? GetView(string moduleName)
        {
            var module = GetModule(moduleName);
            return GetView(module);
        }

        public Page? GetViewFromService(string moduleName)
        {
            var module = GetModule(moduleName);
            if (module == null || module.ViewType == null)
                return null;
            // 获取服务中View实例
            var view = (_serviceProvider.GetService(module.ViewType)) as Page;
            return view;
        }

        public void ClearViewModelCache()
        {
            // 清理前调用清理方法
            Parallel.ForEach(
                _viewModelCache.Values.OfType<ViewModelBase>(),
                vm =>
                {
                    try
                    {
                        vm.OnCleanup();
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"清理ViewModel时出错: {vm.GetType().Name}", ex);
                    }
                }
            );

            _viewModelCache.Clear();
            _weakViewCache.Clear();

            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _logger?.Info("ViewModel缓存清除");
        }
    }
}
