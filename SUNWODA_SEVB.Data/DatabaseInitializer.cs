using Microsoft.Extensions.Configuration;
using SqlSugar;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Data.Models;
using System.Reflection;

namespace SUNWODA_SEVB.Data
{
    /// <summary>
    /// 数据库初始化器实现
    /// </summary>
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly ISqlSugarClient _db;
        private readonly ILoggerService<DatabaseInitializer> _logger;
        private readonly IConfiguration _configuration;
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        public DatabaseInitializer(
            ISqlSugarClient db,
            ILoggerService<DatabaseInitializer> logger,
            IConfiguration configuration)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
        }

        public bool Initialize()
        {
            // 双重检查锁定模式
            if (_initialized)
            {
                _logger.Info("数据库已经初始化，跳过重复初始化");
                return true;
            }

            lock (_lock)
            {
                if (_initialized)
                {
                    return true;
                }

                try
                {
                    _logger.Info("========== 开始数据库初始化 ==========");

                    // 步骤1：创建数据库
                    CreateDatabase();

                    // 步骤2：创建表结构
                    CreateTables();

                    // 步骤3：初始化默认数据
                    InitializeDefaultData();

                    _initialized = true;
                    _logger.Info("========== 数据库初始化成功 ==========");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"数据库初始化失败: {ex.Message}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 创建数据库
        /// </summary>
        private void CreateDatabase()
        {
            try
            {
                _logger.Info("检查/创建数据库...");
                _db.DbMaintenance.CreateDatabase();
                _logger.Info("数据库检查/创建完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"创建数据库失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 创建表结构
        /// </summary>
        private void CreateTables()
        {
            _logger.Info("开始创建/更新表结构...");

            var modelTypes = GetAllModelTypes();
            if (!modelTypes.Any())
            {
                _logger.Warn("未找到任何数据模型类型");
                return;
            }

            // 分批创建表，避免一次性创建太多表导致超时
            const int batchSize = 5;
            for (int i = 0; i < modelTypes.Count; i += batchSize)
            {
                var batch = modelTypes.Skip(i).Take(batchSize).ToArray();
                try
                {
                    _db.CodeFirst.InitTables(batch);
                    _logger.Info($"已创建/检查 {batch.Length} 个表 (第 {i / batchSize + 1} 批)");
                }
                catch (Exception ex)
                {
                    _logger.Error($"创建表失败 (批次 {i / batchSize + 1}): {ex.Message}", ex);
                    throw;
                }
            }

            _logger.Info($"表结构创建/更新完成，共处理 {modelTypes.Count} 个表");
        }

        /// <summary>
        /// 获取所有模型类型
        /// </summary>
        private List<Type> GetAllModelTypes()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName?.Contains("SUNWODA_SEVB") ?? false);

                var modelTypes = new List<Type>();

                foreach (var assembly in assemblies)
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.Namespace == "SUNWODA_SEVB.Data.Models"
                            && t.IsClass
                            && !t.IsAbstract
                            && !t.IsInterface
                            && !t.IsGenericType);

                    modelTypes.AddRange(types);
                }

                _logger.Info($"找到 {modelTypes.Count} 个数据模型类型");
                return modelTypes;
            }
            catch (Exception ex)
            {
                _logger.Error($"获取模型类型失败: {ex.Message}", ex);
                return new List<Type>();
            }
        }

        /// <summary>
        /// 初始化默认数据
        /// </summary>
        private void InitializeDefaultData()
        {
            _logger.Info("开始初始化默认数据...");

            // 使用事务确保数据一致性
            _db.Ado.BeginTran();
            try
            {
                // 按依赖顺序初始化数据
                InitializeGlobalSettings();
                //InitializeDevices();
                //InitializePLCConfigs();
                //InitializePLCRWConfigs();
                //InitializePLCAddressConfigs();
                InitializeUsers();
                InitializeWorkSpaceProjects();
                InitializeMESSettings();// 后续启用

                _db.Ado.CommitTran();
                _logger.Info("默认数据初始化完成");
            }
            catch (Exception ex)
            {
                _db.Ado.RollbackTran();
                _logger.Error($"初始化默认数据失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 初始化全局设置
        /// </summary>
        private void InitializeGlobalSettings()
        {
            var settings = new[]
            {
                new GlobalSetting { Name = "IsCycleReadPLC", Value = "true", Type = "bool", Remark = "是否开启循环读取PLC", RoleRank = 2 },
                new GlobalSetting { Name = "IsCycleWritePLC", Value = "true", Type = "bool", Remark = "是否开启循环写入PLC", RoleRank = 2 },
                new GlobalSetting { Name = "DefaultProject", Value = "", Type = "string", Remark = "启动默认显示项目(VM)", RoleRank = 2 },
                new GlobalSetting { Name = "CurrentUserAccount", Value = "guest", Type = "string", Remark = "当前用户", RoleRank = 0b1000 },//000访客 001工程师  010管理员 100超级管理员 
                new GlobalSetting { Name = "IsMESEnabled", Value = "false", Type = "bool", Remark = "是否启用MES功能", RoleRank = 2 },
                new GlobalSetting { Name = "PLCConnectTime", Value = "5000", Type = "int", Unit = "ms", Remark = "PLC重连时间", RoleRank = 2 },
                new GlobalSetting { Name = "IsEnableWatchDogService", Value = "false", Type="bool", Remark = "是否启用看门狗服务", RoleRank = 2},
                new GlobalSetting { Name = "IsUseWeb", Value = "true", Type = "bool", Remark = "是否启用WEB服务", RoleRank = 2 },
                new GlobalSetting { Name ="isEnableHeartbeat",Value="false",Type ="bool",Remark = "是否启用web心跳",RoleRank =2 },
                new GlobalSetting { Name ="isEnableMachineErrorLog",Value="false",Type ="bool",Remark = "是否启用上位机预警飞书推送",RoleRank =2 },
            };
            foreach (var setting in settings)
            {
                if (!_db.Queryable<GlobalSetting>().Any(it => it.Name == setting.Name))
                {
                    _db.Insertable(setting).ExecuteCommand();
                }
            }
            _logger.Info("全局设置初始化完成");
        }

        /// <summary>
        /// 初始化设备
        /// </summary>
        private void InitializeDevices()
        {
            if (!_db.Queryable<Device>().Any(it => it.Name == "测试设备1"))
            {
                _db.Insertable(new Device
                {
                    Name = "测试设备1",
                    Number = "test-01",
                    BaseName = "惠州",
                    LineName = "测试线"
                }).ExecuteCommand();
            }
            _logger.Info("设备数据初始化完成");
        }

        /// <summary>
        /// 初始化PLC配置
        /// </summary>
        private void InitializePLCConfigs()
        {
            if (!_db.Queryable<PLCConfig>().Any(it => it.Name == "欧姆龙测试PLC"))
            {
                _db.Insertable(new PLCConfig
                {
                    Name = "欧姆龙测试PLC",
                    DeviceID = 1,
                    IP = "127.0.0.1",
                    Port = 9600,
                    BrandSpecificationProtocal = "Omron_Fins_TCP",
                    CycleReadTime = 500,
                    CycleWriteTime = 500,
                    IsEnable = true
                }).ExecuteCommand();
            }
            _logger.Info("PLC配置初始化完成");
        }

        /// <summary>
        /// 初始化PLC读写配置
        /// </summary>
        private void InitializePLCRWConfigs()
        {
            if (!_db.Queryable<PLCRWConfig>().Any(it => it.Name == "测试地址段1"))
            {
                _db.Insertable(new PLCRWConfig
                {
                    Name = "测试地址段1",
                    PLCID = 1,
                    AreaName = "D",
                    StartAddress = "100",
                    Length = 6,
                    RWMode = "R",
                    Cycle = 100,
                    AddressType = 1,
                    IsEnable = true
                }).ExecuteCommand();
            }
            _logger.Info("PLC读写配置初始化完成");
        }

        /// <summary>
        /// 初始化PLC地址配置
        /// </summary>
        private void InitializePLCAddressConfigs()
        {
            var configs = new[]
            {
                new PLCAddressConfig { PLCID = 1, PLCRWID = 1, CategoryID = 1, ParameterName = "测试地址1", Type = "float", Length = 1, Address = "D100", IsMonitor = true },
                new PLCAddressConfig { PLCID = 1, PLCRWID = 1, CategoryID = 1, ParameterName = "测试地址2", Type = "int", Length = 1, Address = "D102", IsMonitor = true },
                new PLCAddressConfig { PLCID = 1, PLCRWID = 1, CategoryID = 1, ParameterName = "测试地址3", Type = "float", Length = 1, Address = "D104", IsMonitor = true }
            };

            foreach (var config in configs)
            {
                if (!_db.Queryable<PLCAddressConfig>().Any(it => it.ParameterName == config.ParameterName))
                {
                    _db.Insertable(config).ExecuteCommand();
                }
            }
            _logger.Info("PLC地址配置初始化完成");
        }

        /// <summary>
        /// 初始化用户
        /// </summary>
        private void InitializeUsers()
        {
            var users = new[]
            {
                new Users { UserAccount = "guest", UserName = "访客(默认)", Password = BCrypt.Net.BCrypt.HashPassword(""), RoleId = (int)UserRole.Guest, CreatedTime = DateTime.Now },
                new Users { UserAccount = "engineer", UserName = "工程师(默认)", Password = BCrypt.Net.BCrypt.HashPassword("swd123456"), RoleId = (int)UserRole.Engineer, CreatedTime = DateTime.Now },
                new Users { UserAccount = "admin", UserName = "管理员(默认)", Password = BCrypt.Net.BCrypt.HashPassword("ime123456"), RoleId = (int)UserRole.Admin, CreatedTime = DateTime.Now },
                new Users { UserAccount = "sadmin", UserName = "超级管理员(默认)", Password = BCrypt.Net.BCrypt.HashPassword("sime123456"), RoleId = (int)UserRole.SuperAdmin, CreatedTime = DateTime.Now }
            };

            foreach (var user in users)
            {
                if (!_db.Queryable<Users>().Any(it => it.UserAccount == user.UserAccount))
                {
                    _db.Insertable(user).ExecuteCommand();
                }
            }
            _logger.Info("用户数据初始化完成");
        }

        /// <summary>
        /// 初始化工作区项目
        /// </summary>
        private void InitializeWorkSpaceProjects()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.FullName?.StartsWith("SUNWODA_SEVB") ?? false);

                int addedCount = 0;
                foreach (var assembly in assemblies)
                {
                    var viewModelTypes = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract
                            && t.IsSubclassOf(typeof(ViewModelBase))
                            && t.GetCustomAttribute<ModuleAttribute>() != null);

                    foreach (var vmType in viewModelTypes)
                    {
                        var moduleAttr = vmType.GetCustomAttribute<ModuleAttribute>();
                        if (moduleAttr != null)
                        {
                            if (!_db.Queryable<WorkSpaceProject>().Any(it => it.VMClassName == vmType.Name))
                            {
                                var isEnabled = moduleAttr.Type == ModuleType.Settings
                                    || moduleAttr.Type == ModuleType.UserCenter;

                                _db.Insertable(new WorkSpaceProject
                                {
                                    VMClassName = vmType.Name,
                                    IsEnabled = isEnabled
                                }).ExecuteCommand();

                                addedCount++;
                            }
                        }
                    }
                }

                _logger.Info($"工作区项目初始化完成，新增 {addedCount} 个项目");
            }
            catch (Exception ex)
            {
                _logger.Error($"初始化工作区项目失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 初始化MES设置
        /// </summary>
        private void InitializeMESSettings()
        {
            // 如果有MES相关的默认设置，在这里初始化
            var mesSettings = new[]
            {
                new MESSetting {ProfileName ="test",Key = "BaseUrl",Value ="http://10.178.5.6:9002",Type = "string", Remark = "MES服务器地址"},
                new MESSetting {ProfileName ="test",Key = "TimeoutSeconds",Value ="30",Type = "int", Remark = "超时时间(秒)"},
                new MESSetting { ProfileName ="test",Key = "EnableRetry",Value ="false",Type = "bool", Remark = "是否启用重试"},
                new MESSetting { ProfileName ="test",Key = "MaxRetryCount",Value ="3",Type = "int", Remark = "重试次数"},
        
            };

            foreach(var setting in mesSettings)
            {
                if (!_db.Queryable<MESSetting>().Any(it => it.ProfileName == setting.ProfileName && it.Key == setting.Key))
                {
                    _db.Insertable(setting).ExecuteCommand();
                }
            }
            _logger.Info("MES设置初始化完成");
        }
    }
}
