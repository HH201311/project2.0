using Castle.Core.Logging;
using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using SUNWODA_SEVB.Core.Attributes.Project.JRCW;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.JRCW;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Core;
using SUNWODA_SEVB.Tool.Extension;
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
    public abstract class BusinessServiceBase<T> : ModelBase, IBusinessService where T : BusinessServiceBase<T>
    {
        private readonly ILoggerService<T> _logger;
        private readonly IPLCService _plcService;
        private readonly IProjectSettingRepository _projectSettingRepository;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger"></param>
        protected BusinessServiceBase(ILoggerService<T> logger,
            IPLCService plcService,
            IProjectSettingRepository projectSettingRepository)
        {
            _logger = logger;
            _plcService = plcService;
            _projectSettingRepository = projectSettingRepository;
        }

        protected CancellationTokenSource? _cts;
        protected readonly object _lockObj = new object();

        protected const string AppKey = "WINDING_JUDGE";

        protected const string PublicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCSSfwUbk24Tu4aoD3LKQb1GKHL3x3g3b24xcZQiCd5xwlNkoTgJaafm+fLwwSJpwLrw9UfAEOLqD6WepXb52bsrxU/zqtHUDbduLO+7lMY9hpm0pTrPEXpRdaclffJAnSV6BePBruLrbFJlba8/5OMgs4lMlDOqgbIyghZl4Qz7wIDAQAB";

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
        /// 显示业务参数配置对话框
        /// </summary>
        /// <returns></returns>
        public virtual Task ShowBusinessParamConfigDialogAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 显示业务配置对话框
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="dialogShow"></param>
        /// <returns></returns>
        protected async Task ShowBusinessConfigDialogAsync<TConfig>(Func<TConfig, Task<bool>> dialogShow) where TConfig : class
        {
            try
            {
                // 1. 加载原始配置
                var originalConfig = LoadBusinessParamConfig<TConfig>();
                if (originalConfig == null)
                {
                    _logger.Error($"无法加载业务配置[{BusinessType}]");
                    MessageBox.Error($"无法加载业务配置[{BusinessType}]！", "错误");
                    return;
                }

                // 2. 显示配置对话框
                var result = await dialogShow(originalConfig);
                if (!result)
                {
                    _logger.Info($"用户取消业务配置 [{BusinessType}] 修改");
                    return;
                }

                // 3. 重新加载原始配置（防止并发修改）
                var currentOriginalConfig = LoadBusinessParamConfig<TConfig>();

                // 4. 检查配置是否发生变化
                var isChanged = !originalConfig.EqualsByJson(currentOriginalConfig);
                if (!isChanged)
                {
                    MessageBox.Info($"业务配置 [{BusinessType}] 未修改，无需保存！", "提示");
                    return;
                }

                // 5. 确认保存
                var confirmResult = MessageBox.Ask($"确认保存业务配置[{BusinessType}]修改？", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存业务配置[{BusinessType}]");
                    return;
                }

                // 6. 保存配置
                var isUpdateSuccess = await UpdateConfigAsync(originalConfig);
                if (isUpdateSuccess)
                {
                    _logger.Info($"业务配置[{BusinessType}]保存成功");
                    MessageBox.Success($"业务配置[{BusinessType}]更新完成！", "提示");
                }
                else
                {
                    _logger.Error($"业务配置[{BusinessType}]保存失败");
                    MessageBox.Error($"业务配置[{BusinessType}]更新失败！", "提示");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"业务配置[{BusinessType}]保存异常: {ex.Message}", ex);
                MessageBox.Error($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 根据业务类型加载不同的业务配置
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected TConfig? LoadBusinessParamConfig<TConfig>() where TConfig : class
        {
            return BusinessType switch
            {
                EBusinessType.BusinessType1 => _projectSettingRepository.LoadBusiness1ParamProjectParameters() as TConfig,
                EBusinessType.BusinessType2 => _projectSettingRepository.LoadBusiness2ParamProjectParameters() as TConfig,
                //EBusinessType.BusinessType3 => _projectSettingRepository.LoadBusiness3ParamProjectParameters() as TConfig,
                _ => throw new ArgumentException($"未实现的业务类型 {BusinessType}"),
            };
        }

        /// <summary>
        /// 根据业务类型更新不同的业务配置
        /// </summary>
        /// <typeparam name="TConfig"></typeparam>
        /// <param name="businessType"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        private async Task<bool> UpdateConfigAsync<TConfig>(TConfig config) where TConfig : class
        {
            return BusinessType switch
            {
                EBusinessType.BusinessType1 => _projectSettingRepository.SaveBusiness1ParamProjectParameters(config as Business1ParamModel),
                EBusinessType.BusinessType2 => _projectSettingRepository.SaveBusiness2ParamProjectParameters(config as Business2ParamModel),
                //EBusinessType.BusinessType3 => _projectSettingRepository.SaveBusiness3ParamProjectParameters(config as Business3ParamModel),
                _ => false
            };
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
                EBusinessType.BusinessType2 => "德阳C04",
                EBusinessType.BusinessType3 => "德阳C01",
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
        /// <param name="parameterName"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected async Task<OperateResult> WritePLCAsync(string parameterName, dynamic value)
        {
            var plcAddress = GetPLCAddressModel(parameterName);
            if (plcAddress == null) return new OperateResult { IsSuccess = false, Message = $"未查询到[{parameterName}]PLC地址配置！" };

            return await WritePLCAsync(plcAddress?.Address, plcAddress?.Type, value);
        }

        /// <summary>
        /// PLC写值
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        protected async Task<OperateResult> WritePLCAsync(string? address, string? type, dynamic value)
        {
            if (string.IsNullOrEmpty(address)) throw new ArgumentNullException(nameof(address));
            if (string.IsNullOrEmpty(type)) throw new ArgumentNullException(nameof(type));
            if (value == null) throw new ArgumentNullException(nameof(value));

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
        protected static void ToDictionalByDataColumn<TObj>(TObj? obj, ref Dictionary<string, string> dic) where TObj : class
        {
            if (obj == null) return;
            if (dic == null) dic = new Dictionary<string, string>();

            var properties = typeof(TObj).GetProperties();

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

        #region
        /// <summary>
        /// 查找最接近的数值
        /// </summary>
        /// <param name="numbers"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static float FindClosest(IEnumerable<float> numbers, double target, int index)
        {
            if (numbers == null || !numbers.Any())
                throw new ArgumentException("The numbers collection cannot be null or empty.", nameof(numbers));

            var startIndex = Math.Max(0, index - 1);
            var endIndex = Math.Min(numbers.Count() - 1, index + 1);
            var filteredNumbers = numbers.Skip(startIndex).Take(endIndex - startIndex + 1);
            return filteredNumbers.OrderBy(n => Math.Abs(n - target)).First();
        }

        /// <summary>
        /// 将数据集合格式化为 [item1,item2,...] 的字符串。
        /// </summary>
        /// <param name="data">数据集合，可以为 null。</param>
        /// <returns>格式化的字符串，例如 "[123,123,234]"；若输入为 null 或空集合则返回 "[]"。</returns>
        public static string FormatArray<TData>(IEnumerable<TData> datas)
        {
            if (datas == null || !datas.Any()) return "[]";
            return $"[{string.Join(",", datas)}]";
        }
        #endregion
        #endregion
    }
}
