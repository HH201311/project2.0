using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Socket;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// Socket 服务管理器实现
    /// 注意：原有 SocketServer 代码完全不变，此类负责实例化和管理多个 SocketServer
    /// </summary>
    public sealed class SocketServerManager : ISocketServerManager
    {
        private readonly ILoggerService<SocketServerManager> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly Func<ISocketServer> _serverFactory;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly ConcurrentDictionary<int, ISocketServer> _servers = new();
        private bool _disposed;

        public SocketServerManager(ILoggerService<SocketServerManager> logger,
            IProjectSettingRepository projectSettingRepository,
            Func<ISocketServer> serverFactory)
        {
            _projectSettingRepository = projectSettingRepository ?? throw new ArgumentNullException(nameof(projectSettingRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serverFactory = serverFactory ?? throw new ArgumentNullException(nameof(serverFactory));
        }

        public IReadOnlyList<int> ListeningPorts => _servers.Keys.ToList();
        public int TotalConnectedClientCount => _servers.Values.Sum(s => s.ConnectedClientCount);

        /// <summary>
        /// 启动指定端口
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="port"></param>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> StartAsync(string moduleName, int port = 0, string? ip = null, CancellationToken cancellationToken = default)
        {
            if (_servers.ContainsKey(port))
            {
                _logger.Info($"端口 {port} 已在监听中");
                return false;
            }

            try
            {
                // 初始化服务端数据库配置
                InitializeSocketOptions(moduleName);

                var server = _serverFactory();
                server.IsBuiltInHeartbeatEnabled = false;
                server.IsBuiltInAutoReplyEnabled = false;

                var result = await server.StartAsync(port, moduleName, ip, cancellationToken);
                if (!result)
                {
                    await server.DisposeAsync();
                    return false;
                }

                if (_servers.TryAdd(port, server))
                {
                    _logger.Info($"端口 {port} 已启动并纳入管理，当前监听端口: {string.Join(", ", ListeningPorts)}");
                    return true;
                }

                // 并发冲突，回滚
                await server.StopAsync();
                await server.DisposeAsync();
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"启动端口 {port} 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 停止指定端口
        /// </summary>
        public async Task<bool> StopPortAsync(int port)
        {
            if (!_servers.TryRemove(port, out var server))
            {
                _logger.Warn($"端口 {port} 未在运行中");
                return true;
            }

            try
            {
                await server.DisposeAsync();
                _logger.Info($"端口 {port} 已停止，剩余监听端口: {string.Join(", ", ListeningPorts)}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"停止端口 {port} 异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 停止所有端口
        /// </summary>
        public async Task StopAllAsync()
        {
            if (_servers.IsEmpty) return;

            _logger.Info("管理器正在停止所有端口...");

            var disposeTasks = _servers.Values.Select(async server =>
            {
                try { await server.DisposeAsync(); } catch { }
            });

            await Task.WhenAll(disposeTasks);
            _servers.Clear();

            _logger.Info("所有端口已停止");
        }

        /// <summary>
        /// 根据端口获取 SocketServer 实例
        /// </summary>
        public ISocketServer? GetSocketServerByPort(int port)
        {
            if (_servers.TryGetValue(port, out var server))
            {
                return server;
            }
            return null;
        }

        /// <summary>
        /// 初始化moduleName对应的服务器配置
        /// </summary>
        /// <param name="moduleName"></param>
        private void InitializeSocketOptions(string moduleName)
        {
            var paramName = "Port";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, "8080", "int", 2)
                    {
                        Remark = "Soket端口号"
                    }
                );
            }
            paramName = "MaxClients";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, "10", "int", 2)
                    {
                        Remark = "最大客户端连接数"
                    }
                );
            }
            paramName = "ReceiveBufferSize";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, "8192", "int", 2)
                    {
                        Remark = "接受缓冲区大小"
                    }
                );
            }
            paramName = "Backlog";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, "100", "int", 2)
                    {
                        Remark = "连接对接长度"
                    }
                );
            }
            paramName = "ReceiveTimeoutMs";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, "0", "int", 2)
                    {
                        Remark = "接收超时(毫秒)，0为无限"
                    }
                );
            }
            paramName = "Protocol";
            var protocol = new ProtocolOptions() { FramingMode = FramingMode.None };
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(protocol, JsonOptions), "string", 2)
                    {
                        Remark = "消息解析规则"
                    }
                );
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await StopAllAsync();
        }
    }
}