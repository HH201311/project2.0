using Castle.Core.Logging;
using Dm.util;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using HslCommunication;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.JROH;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.JROH;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.JROH.Core;
using SUNWODA_SEVB.Core.Models.Project.JROH.MCBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.OHBusinessModels;
using SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels;
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
using static SUNWODA_SEVB.Core.Interfaces.Project.JROH.IBusinessService;

namespace SUNWODA_SEVB.Services.Project.JROH.Business
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

        private UserRole userRole;

        /// <summary>
        /// 当前的锅炉账号权限
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 业务类型
        /// </summary>
        public abstract EBusinessType BusinessType { get; }

        /// <summary>
        /// 业务名称
        /// </summary>
        public virtual string BusinessName => GetBusinessName(BusinessType);

        /// <summary>
        /// 调机记录通知事件
        /// </summary>
        public event MessageNotifationHandler? MessageNotifation;

        /// <summary>
        /// 调机记录通知
        /// </summary>
        /// <param name="record"></param>
        protected virtual void OnAdjustmentRecordReceived(Dictionary<string, string> record)
        {
            RaiseMessageNotifation(this, record);
        }

        /// <summary>
        /// 执行消息通知委托
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void RaiseMessageNotifation(object? sender, object e)
        {
            MessageNotifation?.Invoke(sender, e);
        }

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
                EBusinessType.PoleWidth => _projectSettingRepository.LoadPWBusinessParamProjectParameters() as TConfig,
                EBusinessType.CoatedOH => _projectSettingRepository.LoadOHBusinessParamProjectParameters() as TConfig,
                EBusinessType.MultiCorrect => _projectSettingRepository.LoadMCBusinessParamProjectParameters() as TConfig,
                EBusinessType.Mismatch => _projectSettingRepository.LoadMisBusinessParamProjectParameters() as TConfig,
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
                EBusinessType.PoleWidth => _projectSettingRepository.SavePWBusinessParamProjectParameters(config as PWBusinessParamModel),
                EBusinessType.CoatedOH => _projectSettingRepository.SaveOHBusinessParamProjectParameters(config as OHBusinessParamModel),
                EBusinessType.MultiCorrect => _projectSettingRepository.SaveMCBusinessParamProjectParameters(config as MCBusinessParamModel),
                EBusinessType.Mismatch => _projectSettingRepository.SaveMisBusinessParamProjectParameters(config as MisBusinessParamModel),
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
                EBusinessType.PoleWidth => "极片宽度闭环",
                EBusinessType.CoatedOH => "包覆OH闭环",
                EBusinessType.MultiCorrect => "多级联动闭环",
                EBusinessType.Mismatch => "极耳错位闭环",
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
        protected TValue? GetPLCAddressValue<TValue>(string? parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName)?.MonitorValue;
            if (value is TValue typedValue) return typedValue;
            else return default(TValue);
        }
        /// <summary>
        /// PLC读值
        /// </summary>
        /// <param name="parameterName"></param>
        /// <returns></returns>
        protected string GetPLCAddressValue(string? parameterName)
        {
            var value = _plcService.RWAddresses.Values.FirstOrDefault(o => o.ParameterName == parameterName)?.MonitorValue;
            return value?.ToString() ?? string.Empty;
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

            if (!(_plcService.GetPLCByName(JROHCore.PLCName) is PLCDevice plc))
                throw new ArgumentException($"未查询到已连接的PLC设备[{JROHCore.PLCName}]！", nameof(JROHCore.PLCName));

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
        #endregion
        #endregion
    }
}
