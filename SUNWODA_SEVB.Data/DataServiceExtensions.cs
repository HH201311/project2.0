using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlSugar;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Data.Mappings;
using SUNWODA_SEVB.Data.Repositories;

namespace SUNWODA_SEVB.Data
{
    /// <summary>
    /// 数据层服务注册扩展
    /// </summary>

    public static class DataServiceExtensions
    {
        private static readonly object _initLock = new object();
        private static bool _isInitialized = false;
        private static readonly SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);

        public static IServiceCollection AddDataServices(this IServiceCollection services)
        {
            // 配置Mapster
            MapsterConfig.Configure();

            // 注册SqlSugar
            services.AddSingleton<ISqlSugarClient>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                //var logger = provider.GetService<ILoggerService<ISqlSugarClient>>();
                // 验证连接字符串
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("数据库连接字符串未配置或为空");
                }

                // 添加连接池参数
                if (!connectionString.Contains("Pooling="))
                {
                    connectionString += ";Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Lifetime=300;";
                }

                var db = new SqlSugarScope(
                    new ConnectionConfig()
                    {
                        ConnectionString = connectionString,
                        DbType = DbType.MySql,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute,
                        MoreSettings = new ConnMoreSettings()
                        {
                            IsAutoRemoveDataCache = true,
                            IsWithNoLockQuery = false,
                            // 设置命令超时时间
                            SqlServerCodeFirstNvarchar = false,
                            DefaultCacheDurationInSeconds = 10
                        },
                       
                        ConfigureExternalServices = new ConfigureExternalServices
                        {
                            // 配置实体服务
                            EntityService = (c, p) =>
                            {
                                // 统一设置主键为自增
                                if (p.IsPrimarykey && p.PropertyInfo.PropertyType == typeof(int))
                                {
                                    p.IsIdentity = true;
                                }

                                // 设置时间字段默认值
                                if (p.PropertyInfo.PropertyType == typeof(DateTime))
                                {
                                    if (p.PropertyName.Equals("CreateTime", StringComparison.OrdinalIgnoreCase) ||
                                        p.PropertyName.Equals("CreatedTime", StringComparison.OrdinalIgnoreCase))
                                    {
                                        p.DefaultValue = "CURRENT_TIMESTAMP";
                                    }
                                    else if (p.PropertyName.Equals("UpdateTime", StringComparison.OrdinalIgnoreCase) ||
                                             p.PropertyName.Equals("UpdatedTime", StringComparison.OrdinalIgnoreCase))
                                    {
                                        p.DefaultValue = "CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP";
                                    }
                                }
                            },
                        },
                    },
                    db =>
                    {
                        ConfigureSqlAop(db);
                    });

                return db;
            });

            // 注册数据库初始化服务（作为单例，确保只初始化一次）
            services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();

            // 注册原DatabaseService（保留备份/恢复功能）
            services.AddSingleton<IDatabaseService, DatabaseService>();

            // 注册工作单元
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // 注册通用仓储
            services.AddScoped(typeof(IRepository<>), typeof(BaseRepository<>));

            // 注册特定仓储
            RegisterRepositories(services);

            return services;
        }

        /// <summary>
        /// 注册所有仓储
        /// </summary>
        private static void RegisterRepositories(IServiceCollection services)
        {
            // 基础仓储
            services.AddScoped<IAppLogRepository, AppLogRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IGlobalSettingRepository, GlobalSettingRepository>();
            services.AddScoped<IUsersRepository, UsersRepository>();

            // PLC相关仓储
            services.AddScoped<IPLCConfigRepository, PLCConfigRepository>();
            services.AddScoped<IPLCRWConfigRepository, PLCRWConfigRepository>();
            services.AddScoped<IPLCAddressConfigRepository, PLCAddressConfigRepository>();

            // 项目配置仓储
            services.AddScoped<IWorkSpaceProjectRepository, WorkSpaceProjectRepository>();
            services.AddScoped<IProjectSettingRepository, ProjectSettingRepository>();

            // MES相关仓储
            services.AddScoped<IMesInterfaceLogRepository, MesInterfaceLogRepository>();
            services.AddScoped<IMESSettingRepository, MESSettingRepository>();

            // Web相关仓储
            services.AddScoped<IWebInterfaceLogRepository, WebInterfaceLogRepository>();

            // 数据统计相关仓储
            services.AddScoped<IDetectionStatisticsRepository, DetectionStatisticsRepository>();
        }
        /// <summary>
        /// 配置SQL日志记录
        /// </summary>
        private static void ConfigureSqlAop(SqlSugarClient db)
        {
            // SQL执行前事件 - 优化日志记录
            db.Aop.OnLogExecuting = (sql, pars) =>
            {
                // 防止初始化时的日志循环
                if (sql.ToUpper().Contains("APP_LOGS") && !_isInitialized)
                {
                    return;
                }

                var tableName = ExtractTableName(sql);

                // 过滤掉系统表查询
                if (IsSystemQuery(sql, tableName))
                {
                    return;
                }

                // 只记录业务表操作
                if (!string.IsNullOrEmpty(tableName) && IsBusinessTable(tableName))
                {
                    var operation = GetSqlOperation(sql);
                    Console.WriteLine($"[SQL] {operation} 表[{tableName}]");
                }
             };

            // SQL执行后事件 - 只在慢查询时记录
            db.Aop.OnLogExecuted = (sql, pars) =>
            {
                var executionTime = db.Ado.SqlExecutionTime.TotalMilliseconds;

                if (executionTime > 1000) // 超过1秒的慢查询
                {
                    var tableName = ExtractTableName(sql);
                    var operation = GetSqlOperation(sql);
                    Console.WriteLine($"[慢查询] {operation} 表[{tableName}] - 执行时间: {executionTime:F2}ms");
                }
            };

            // SQL出错事件
            db.Aop.OnError = (exp) =>
            {
                // 不记录表不存在的错误（初始化时会出现）
                if (!exp.Message.Contains("doesn't exist"))
                {
                    Console.WriteLine($"[SQL错误] {exp.Message}");
                }
            };
        }

        /// <summary>
        /// 初始化数据库（在应用启动时调用）
        /// </summary>
        public static async Task<bool> InitializeDatabaseAsync(this IServiceProvider serviceProvider)
        {
            // 确保只初始化一次
            if (_isInitialized)
            {
                return true;
            }

            // 使用信号量防止并发初始化
            await _connectionSemaphore.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    return true;
                }

                var initializer = serviceProvider.GetRequiredService<IDatabaseInitializer>();
                var result = await Task.Run(() => initializer.Initialize());

                if (result)
                {
                    _isInitialized = true;
                    Console.WriteLine("[数据库] 初始化成功");
                }
                else
                {
                    Console.WriteLine("[数据库] 初始化失败");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[数据库] 初始化异常: {ex.Message}");
                return false;
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        #region SQL分析辅助方法
        /// <summary>
        /// 从SQL语句中提取表名
        /// </summary>
        private static string ExtractTableName(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return string.Empty;

            sql = sql.ToUpper();

            var patterns = new[]
            {
                @"FROM\s+`?(\w+)`?",
                @"INSERT\s+INTO\s+`?(\w+)`?",
                @"UPDATE\s+`?(\w+)`?",
                @"DELETE\s+FROM\s+`?(\w+)`?",
                @"ALTER\s+TABLE\s+`?(\w+)`?",
                @"CREATE\s+TABLE\s+`?(\w+)`?",
                @"DROP\s+TABLE\s+`?(\w+)`?",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(sql, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            if (sql.Contains("INFORMATION_SCHEMA"))
                return "INFORMATION_SCHEMA";

            return "未知表";
        }

        /// <summary>
        /// 判断是否为系统查询
        /// </summary>
        private static bool IsSystemQuery(string sql, string tableName)
        {
            if (string.IsNullOrEmpty(sql))
                return false;

            sql = sql.ToUpper();

            // 系统表查询
            var systemKeywords = new[]
            {
                "INFORMATION_SCHEMA", "PERFORMANCE_SCHEMA",
                "MYSQL.", "SYS.", "SHOW TABLES",
                "SHOW COLUMNS", "DESCRIBE ", "EXPLAIN "
            };

            return systemKeywords.Any(keyword => sql.Contains(keyword));
          
        }

        /// <summary>
        /// 判断是否为业务表
        /// </summary>
        private static bool IsBusinessTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
                return false;

            // 定义业务表列表
            var businessTables = new[]
            {
                "APP_LOGS",
                "MES_INTERFACE_LOGS",
                "WEB_INTERFACE_LOGS",
                "USERS",
                "DEVICE",
                "PLC_CONFIG",
                "PLC_ADDRESS_CONFIG",
                "PLC_RW_CONFIG",
                "GLOBAL_SETTING",
                "PROJECT_SETTING",
                "WORKSPACE_PROJECT",
                "MES_SETTING",
                "DETECTION_STATISTICS"
            };

            return businessTables.Contains(tableName.ToUpper());
        }

        /// <summary>
        /// 获取SQL操作类型
        /// </summary>
        private static string GetSqlOperation(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return "UNKNOWN";

            sql = sql.ToUpper().Trim();

            var operations = new Dictionary<string, string>
            {
                { "SELECT", "查询" },
                { "INSERT", "插入" },
                { "UPDATE", "更新" },
                { "DELETE", "删除" },
                { "CREATE", "创建" },
                { "ALTER", "修改" },
                { "DROP", "删除" }
            };

            foreach (var op in operations)
            {
                if (sql.StartsWith(op.Key))
                    return op.Value;
            }

            return "操作";
        }
        #endregion
    }
}
