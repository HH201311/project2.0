using Castle.Core.Logging;
using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.JECW;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.JRCW;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Services.Project.JRCW.Business
{
    /// <summary>
    /// 业务服务基类
    /// </summary>
    public abstract class BusinessServiceBase<T> : IBusinessService where T : BusinessServiceBase<T>
    {
        private readonly ILoggerService<T> _logger;
        private readonly IPLCService _plcService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger"></param>
        protected BusinessServiceBase(ILoggerService<T> logger,
            IPLCService plcService)
        {
            _logger = logger;
            _plcService = plcService;
        }

        protected CancellationTokenSource? _cts;
        protected readonly object _lockObj = new object();

        /// <summary>
        /// 业务类型
        /// </summary>
        public abstract EBusinessType BusinessType { get; }

        /// <summary>
        /// 业务名称
        /// </summary>
        public virtual string BusinessName => GetBusinessName(BusinessType);

        /// <summary>
        /// 运行状态
        /// </summary>
        public bool IsRunning { get; protected set; }

        /// <summary>
        /// 初始化业务相关资源
        /// </summary>
        protected abstract Task InitializeAsync(CancellationToken token);

        /// <summary>
        /// 执行业务逻辑
        /// </summary>
        protected abstract Task ExecuteAsync(CancellationToken token);

        /// <summary>
        /// 清理业务资源
        /// </summary>
        protected abstract Task CleanupAsync();

        /// <summary>
        /// 开始业务
        /// </summary>
        public virtual async Task StartAsync(CancellationToken token = default)
        {
            lock (_lockObj)
            {
                if (IsRunning)
                {
                    throw new InvalidOperationException($"业务 {BusinessType} 已在运行中");
                }
                IsRunning = true;
                _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            }

            try
            {
                await InitializeAsync(_cts.Token);
                await ExecuteAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
                _logger.Warn($"业务 {BusinessType}:{BusinessName} 已取消运行！");
            }
            catch (Exception ex)
            {
                // 记录异常
                _logger.Error($"业务 {BusinessType}:{BusinessName} 运行异常[{ex.Message}]");
                throw;
            }
            finally
            {
                await CleanupAsync();
                IsRunning = false;
            }
        }

        /// <summary>
        /// 停止业务
        /// </summary>
        public virtual async Task StopAsync(CancellationToken token = default)
        {
            if (!IsRunning) return;

            _cts?.Cancel();

            // 等待业务完全停止
            while (IsRunning && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
            }

            await CleanupAsync();
        }

        /// <summary>
        /// 重启业务
        /// </summary>
        public virtual async Task RestartAsync(CancellationToken token = default)
        {
            await StopAsync(token);
            await Task.Delay(1000, token); // 等待资源释放
            await StartAsync(token);
        }

        /// <summary>
        /// 获取业务名称
        /// </summary>
        /// <param name="businessType"></param>
        /// <returns></returns>
        protected virtual string GetBusinessName(EBusinessType businessType)
        {
            return businessType switch
            {
                EBusinessType.BusinessType1 => "义乌C05",
                EBusinessType.BusinessType2 => "德阳C01",
                EBusinessType.BusinessType3 => "德阳C04",
                _ => "Unknow"
            };
        }

        #region 私有方法
        #region PLC读写操作
        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected string? GetPLCAddressValue(string? parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName)?.MonitorValue;
            return value?.ToString();
        }

        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected PLCRWAddress? GetPLCAddressModel(string parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName);
            return value;
        }

        /// <summary>
        /// PLC写值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected async Task<OperateResult> WritePLCAsync(string address, string type, dynamic value)
        {
            if (!(_plcService.GetPLCByName(JRCWCore.PLCName) is PLCDevice plc))
                throw new ArgumentException($"未查询到已连接的PLC设备[{JRCWCore.PLCName}]！", nameof(JRCWCore.PLCName));

            var result = await plc.WriteAsync(address, type, value);
            _logger.Debug($"PLC执行写入:地址[{address}]类型[{type}]值[{value}]，写入结果[{result.Message}]");
            return result;
        }
        #endregion

        #region 弹窗
        /// <summary>
        /// 弹窗
        /// </summary>
        /// <param name="messageText"></param>
        /// <param name="caption"></param>
        /// <param name="countdownTime"></param>
        /// <returns></returns>
        protected async Task<bool> MessageBoxShowDialog(string messageText, string caption, int countdownTime = 5)
        {
            return await UIThreadHelper.RunOnUIThreadAsync(async () =>
            {
                var tcs = new TaskCompletionSource<bool>();
                try
                {
                    var mb = new VM_JRCWMessageBoxDialog();
                    var re = await Dialog.Show(mb.UIElement, "DialogMainWindow")
                        .Initialize<VM_JRCWMessageBoxDialog>(vm =>
                        {
                            vm.MessageText = messageText;
                            vm.Caption = caption;
                            vm.CountdownTime = countdownTime;
                            vm.CountdownTimeStart();
                        }).GetResultAsync<bool>();
                    tcs.SetResult(re);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
                return await tcs.Task;
            });
        }
        #endregion

        #region 根据特性将对象转换为字典/列表/字符串等
        /// <summary>
        /// 多个obj转换为字典列表
        /// </summary>
        /// <param name="objs"></param>
        /// <returns></returns>
        protected static List<Dictionary<string, string>> ToDictionalsByDataColumn<TData>(params TData[] objs) where TData : class
        {
            if (objs == null || objs.Length <= 0) return new List<Dictionary<string, string>>();

            var lis = new List<Dictionary<string, string>>();
            foreach (var obj in objs)
            {
                var dic = new Dictionary<string, string>();
                ToDictionalByDataColumn(obj, ref dic);
                lis.add(dic);
            }

            return lis;
        }

        /// <summary>
        /// obj转换为字典
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="dic"></param>
        protected static void ToDictionalByDataColumn<T>(T? obj, ref Dictionary<string, string> dic) where T : class
        {
            if (obj == null) return;
            if (dic == null) dic = new Dictionary<string, string>();

            var properties = typeof(T).GetProperties();

            foreach (var property in properties)
            {
                var cdAttr = property.GetCustomAttribute<DataColumnAttribute>();
                if (cdAttr == null || cdAttr.IsIgnore) continue;

                var key = string.IsNullOrEmpty(cdAttr.ColumnName) ? property.Name : cdAttr.ColumnName;
                var value = property.GetValue(obj);
                if (value == null)
                {
                    ProcessDictionary(ref dic, key, string.Empty);
                    continue;
                }
                if (value is string vStr)
                {
                    ProcessDictionary(ref dic, key, vStr);
                    continue;
                }
                if (value is DateTime vDateTime)
                {
                    var v = vDateTime.ToString(cdAttr.DataTimeFormat);
                    ProcessDictionary(ref dic, key, v);
                    continue;
                }
                if (value is IEnumerable vs)// 可完善:深层递归
                {
                    var index = 0;
                    foreach (var v in vs)
                    {
                        index++;
                        var cnf = cdAttr.ColumnNameFormat;
                        var newKey = string.IsNullOrEmpty(cnf) ? key : string.Format(cnf, index, key);
                        ProcessDictionary(ref dic, newKey, v.ToString());
                    }
                    continue;
                }
                ProcessDictionary(ref dic, key, value.ToString());
                // 可完善:其他类型
            }
        }

        /// <summary>
        /// 字典插入
        /// </summary>
        /// <param name="dic"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private static void ProcessDictionary(ref Dictionary<string, string> dic, string key, string? value)
        {
            if (dic == null) return;
            var count = dic.Keys.Count(o => o == key);
            var newKey = key;
            while (dic.ContainsKey(newKey)) newKey = $"{key}_{count++}";

            dic[newKey] = value ?? string.Empty;
        }
        #endregion
        #endregion
    }
}
