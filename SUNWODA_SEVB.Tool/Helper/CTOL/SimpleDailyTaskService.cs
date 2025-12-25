using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool
{
    public class SimpleDailyTaskService
    {
        private readonly Func<Task> _dailyTask;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10); // 每10分钟检查一次
        private DateTime _lastExecutionDate = DateTime.Now.Date; // 记录上一次执行的日期（初始为今天）
        private readonly object _lock = new object();
        private CancellationTokenSource? _cancellationTokenSource;

        public Action<string> AddLog { get; set; } = (message) => Console.WriteLine(message);

        // 构造函数接收一个待执行的任务
        public SimpleDailyTaskService(Func<Task> dailyTask)
        {
            _dailyTask = dailyTask ?? throw new ArgumentNullException(nameof(dailyTask));
        }

        // 启动后台检查线程
        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            // 启动一个后台任务
            Task.Run(async () =>
            {
                AddLog("[简易每日任务服务] 已启动，开始监控日期变化...");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // 检查是否已经过了凌晨
                        if (CheckAndUpdateDate())
                        {
                            AddLog("[简易每日任务服务] 检测到新的一天，开始执行每日任务...");
                            try
                            {
                                // 执行文件移动任务
                                await _dailyTask();
                                AddLog("[简易每日任务服务] 每日任务执行成功。");
                            }
                            catch (Exception ex)
                            {
                                AddLog($"[简易每日任务服务] 执行每日任务时发生错误: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[简易每日任务服务] 监控线程发生未知错误: {ex.Message}");
                    }

                    // 等待10分钟后再次检查
                    await Task.Delay(_checkInterval, token);
                }
                AddLog("[简易每日任务服务] 已停止。");
            }, token);
        }

        // 停止后台检查线程
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        // 检查当前日期是否已更新，并更新上次执行日期
        private bool CheckAndUpdateDate()
        {
            lock (_lock)
            {
                DateTime currentDate = DateTime.Now.Date;
                // 如果当前日期不等于上次执行日期，说明是新的一天
                if (currentDate != _lastExecutionDate)
                {
                    _lastExecutionDate = currentDate;
                    return true;
                }
                return false;
            }
        }
    }
}
