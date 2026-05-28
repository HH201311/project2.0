using NLog;
using NLog.Config;
using NLog.Targets;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.Logging
{
    /// <summary>
    /// 使用 NLog 实现的日志管理服务
    /// </summary>
    public class NLogManagementService : ILogManagementService
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        public string GetLogFilePath(DateTime date)
        {
            var config = LogManager.Configuration;
            if (config == null) return string.Empty;

            var fileTarget = config.AllTargets
                                  .OfType<FileTarget>()
                                  .FirstOrDefault();

            if (fileTarget == null) return string.Empty;

            try
            {
                var logEventInfo = new LogEventInfo
                {
                    TimeStamp = date,
                    Level = LogLevel.Info,
                    LoggerName = "LogManagementService"
                };

                string renderedFileName = fileTarget.FileName.Render(logEventInfo);

                if (!Path.IsPathRooted(renderedFileName))
                {
                    renderedFileName = Path.GetFullPath(renderedFileName);
                }

                return renderedFileName;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "获取日志文件路径失败");
                return string.Empty;
            }
        }
        public void CleanupOldLogs(int daysToKeep = 90)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var parentDir = Directory.GetParent(baseDir)?.FullName;

                if (string.IsNullOrEmpty(parentDir))
                {
                    Logger.Warn("无法获取父目录，跳过日志清理");
                    return;
                }

                var logDir = Path.Combine(parentDir, "logs");

                if (!Directory.Exists(logDir))
                {
                    Logger.Info($"日志目录不存在: {logDir}");
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var directories = Directory.GetDirectories(logDir);

                Logger.Info($"开始清理{daysToKeep}天前的日志，截止日期: {cutoffDate:yyyy-MM-dd}");

                int deletedCount = 0;
                int keptCount = 0;
                DateTime? earliestKeptDate = null;
                DateTime? latestKeptDate = null;


                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    if (DateTime.TryParseExact(dirName, "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out var dirDate))
                    {
                        if (dirDate < cutoffDate)
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                                deletedCount++;
                                Logger.Info($"已删除过期日志目录: {dir} (日期: {dirDate:yyyy-MM-dd})");
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, $"删除过期日志目录失败: {dir}");
                            }
                        }
                        else
                        {
                            keptCount++;

                            // 更新最早和最晚保留日期
                            if (earliestKeptDate == null || dirDate < earliestKeptDate)
                            {
                                earliestKeptDate = dirDate;
                            }
                            if (latestKeptDate == null || dirDate > latestKeptDate)
                            {
                                latestKeptDate = dirDate;
                            }
                        }
                    }
                    else
                    {
                        Logger.Debug($"跳过非日期格式的目录: {dir}");
                    }
                }
                if (keptCount > 0)
                {
                    Logger.Info($"保留 {keptCount} 天日志目录 (从 {earliestKeptDate:yyyy-MM-dd} 到 {latestKeptDate:yyyy-MM-dd})");
                }
                Logger.Info($"日志清理完成，删除了 {deletedCount} 个过期目录");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "清理过期日志时发生错误");
            }
        }

        /// <summary>
        /// 获取日志文件列表
        /// </summary>
        /// <param name="logDirectory">日志目录路径（可选）</param>
        /// <returns>日志文件路径集合</returns>
        public IEnumerable<string> GetLogFiles(string logDirectory = null!)
        {
            var logFiles = new List<string>();

            try
            {
                // 如果没有指定目录，尝试从 NLog 配置中获取
                if (string.IsNullOrEmpty(logDirectory))
                {
                    var config = LogManager.Configuration;
                    if (config != null)
                    {
                        var fileTarget = config.AllTargets
                                              .OfType<FileTarget>()
                                              .FirstOrDefault();

                        if (fileTarget != null)
                        {
                            var logEventInfo = new LogEventInfo
                            {
                                TimeStamp = DateTime.Now,
                                Level = LogLevel.Info,
                                LoggerName = "LogManagementService"
                            };

                            string renderedFileName = fileTarget.FileName.Render(logEventInfo);
                            logDirectory = Path.GetDirectoryName(renderedFileName) ??
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                        }
                    }
                }

                // 如果还是没有目录，使用默认目录
                if (string.IsNullOrEmpty(logDirectory))
                {
                    logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                }

                if (Directory.Exists(logDirectory))
                {
                    // 搜索所有日志文件（通常是 .log, .txt 等）
                    var patterns = new[] { "*.log", "*.txt" };
                    foreach (var pattern in patterns)
                    {
                        var files = Directory.GetFiles(logDirectory, pattern, SearchOption.AllDirectories);
                        logFiles.AddRange(files);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "获取日志文件列表时发生错误");
            }

            return logFiles.OrderByDescending(f => File.GetLastWriteTime(f));
        }

        /// <summary>
        /// 获取当前日志配置信息
        /// </summary>
        public LogConfiguration GetConfiguration()
        {
            var configuration = new LogConfiguration();

            try
            {
                var nlogConfig = LogManager.Configuration;
                if (nlogConfig != null)
                {
                    // 获取最低日志级别
                    var rules = nlogConfig.LoggingRules;
                    if (rules.Any())
                    {
                        var minLevel = rules.SelectMany(r => r.Levels).OrderBy(l => l.Ordinal).FirstOrDefault();
                        if (minLevel != null)
                        {
                            configuration.MinimumLevel = ConvertNLogLevelToLogLevel(minLevel);
                        }
                    }

                    // 获取文件目标配置
                    var fileTarget = nlogConfig.AllTargets.OfType<FileTarget>().FirstOrDefault();
                    if (fileTarget != null)
                    {
                        configuration.EnableFileOutput = true;

                        // 获取日志目录
                        var logEventInfo = new LogEventInfo
                        {
                            TimeStamp = DateTime.Now,
                            Level = LogLevel.Info,
                            LoggerName = "LogManagementService"
                        };
                        string renderedFileName = fileTarget.FileName.Render(logEventInfo);
                        configuration.LogDirectory = Path.GetDirectoryName(renderedFileName) ??
                                                   Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

                        // 获取文件大小限制
                        if (fileTarget.ArchiveAboveSize > 0)
                        {
                            configuration.MaxFileSizeInMB = (int)(fileTarget.ArchiveAboveSize / (1024 * 1024));
                        }

                        // 获取备份文件数量
                        configuration.MaxBackupFiles = fileTarget.MaxArchiveFiles;
                    }

                    // 检查是否有控制台输出
                    var consoleTarget = nlogConfig.AllTargets.OfType<ConsoleTarget>().FirstOrDefault();
                    configuration.EnableConsoleOutput = consoleTarget != null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "获取日志配置时发生错误");
            }

            return configuration;
        }

        /// <summary>
        /// 更新日志配置
        /// </summary>
        public void UpdateConfiguration(LogConfiguration configuration)
        {
            try
            {
                var nlogConfig = LogManager.Configuration ?? new LoggingConfiguration();

                // 清除现有规则
                nlogConfig.LoggingRules.Clear();

                // 设置文件输出
                if (configuration.EnableFileOutput)
                {
                    var fileTarget = nlogConfig.AllTargets.OfType<FileTarget>().FirstOrDefault() ?? new FileTarget("file");

                    // 更新文件目标配置
                    fileTarget.FileName = Path.Combine(configuration.LogDirectory ?? "logs", "${date:format=yyyy-MM-dd}", "${processname}.log");
                    fileTarget.ArchiveAboveSize = configuration.MaxFileSizeInMB * 1024 * 1024;
                    fileTarget.MaxArchiveFiles = configuration.MaxBackupFiles;
                    fileTarget.Layout = "${longdate} ${level:uppercase=true} ${logger} ${Message} ${exception:format=ToString}";
                    fileTarget.ArchiveEvery = FileArchivePeriod.Day;


                    if (!nlogConfig.AllTargets.Contains(fileTarget))
                    {
                        nlogConfig.AddTarget(fileTarget);
                    }

                    // 添加文件规则
                    var fileRule = new LoggingRule("*", ConvertLogLevelToNLogLevel(configuration.MinimumLevel), fileTarget);
                    nlogConfig.LoggingRules.Add(fileRule);
                }

                // 设置控制台输出
                if (configuration.EnableConsoleOutput)
                {
                    var consoleTarget = nlogConfig.AllTargets.OfType<ConsoleTarget>().FirstOrDefault() ?? new ConsoleTarget("console");
                    consoleTarget.Layout = "${longdate} ${level:uppercase=true} ${logger} ${Message} ${exception:format=ToString}";

                    if (!nlogConfig.AllTargets.Contains(consoleTarget))
                    {
                        nlogConfig.AddTarget(consoleTarget);
                    }

                    // 添加控制台规则
                    var consoleRule = new LoggingRule("*", ConvertLogLevelToNLogLevel(configuration.MinimumLevel), consoleTarget);
                    nlogConfig.LoggingRules.Add(consoleRule);
                }

                // 应用新配置
                LogManager.Configuration = nlogConfig;
                Logger.Info("日志配置已更新");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "更新日志配置时发生错误");
                throw;
            }
        }

        /// <summary>
        /// 将 NLog 日志级别转换为 LogLevel
        /// </summary>
        private CoreLogLevel ConvertNLogLevelToLogLevel(LogLevel nlogLevel)
        {
            if (nlogLevel == LogLevel.Trace) return CoreLogLevel.Trace;
            if (nlogLevel == LogLevel.Debug) return CoreLogLevel.Debug;
            if (nlogLevel == LogLevel.Info) return CoreLogLevel.Info;
            if (nlogLevel == LogLevel.Warn) return CoreLogLevel.Warn;
            if (nlogLevel == LogLevel.Error) return CoreLogLevel.Error;
            if (nlogLevel == LogLevel.Fatal) return CoreLogLevel.Fatal;
            return CoreLogLevel.Info;
        }

        /// <summary>
        /// 将 LogLevel 转换为 NLog 日志级别
        /// </summary>
        private LogLevel ConvertLogLevelToNLogLevel(CoreLogLevel logLevel)
        {
            switch (logLevel)
            {
                case CoreLogLevel.Trace: return LogLevel.Trace;
                case CoreLogLevel.Debug: return LogLevel.Debug;
                case CoreLogLevel.Info: return LogLevel.Info;
                case CoreLogLevel.Warn: return LogLevel.Warn;
                case CoreLogLevel.Error: return LogLevel.Error;
                case CoreLogLevel.Fatal: return LogLevel.Fatal;
                default: return LogLevel.Info;
            }
        }


        public void Flush()
        {
            LogManager.Flush();
        }
       
        /// <summary>
        /// 关闭日志系统，释放资源
        /// </summary>
        public void Shutdown()
        {
            try
            {
                LogManager.Flush();
                LogManager.Shutdown();
                Logger.Info("日志系统已关闭");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭日志系统时发生错误: {ex.Message}");
            }
        }

        public void Reconfigure()
        {
            LogManager.Configuration = LogManager.Configuration;
        }

    }
}
