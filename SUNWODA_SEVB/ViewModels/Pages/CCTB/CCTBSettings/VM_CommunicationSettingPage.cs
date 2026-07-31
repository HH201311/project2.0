using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.CommunicationOptions;
using SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.FunctionSetting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings
{
    [Module("VM_CommunicationSettingPage", "通讯设置", Order = 2, Category = "VM_CCTBSettingsPage/VM_CommunicationSettingPage")]
    class VM_CommunicationSettingPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CommunicationSettingPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly ISocketOptionsLoader _socketOptionsLoader;
        private readonly ICustomMqttOptionsLoader _customMqttOptionsLoader;
        private readonly ICommunicationService _communicationService;

        private string? controlPageName;
        public string? ControlPageName { get => controlPageName; set => SetProperty(ref controlPageName, value); }

        /// <summary>
        /// 界面导航按钮
        /// </summary>
        public ICommand? ControlPageNavigateCommand => new RelayCommand(OnControlPageNavigateCommand);

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// PLC参数保存按钮
        /// </summary>
        public ICommand? ParamPLCSaveCommand => new RelayCommand(OnParamPLCSaveAsync);

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand => new RelayCommand(OnParamCancleAsync);

        /// <summary>
        /// Socket 客户端配置命令
        /// </summary>
        public ICommand? SocketClientConfigCommand => new RelayCommand(OnSocketClientConfigAsync);

        /// <summary>
        /// Socket 服务端配置命令
        /// </summary>
        public ICommand? SocketServerConfigCommand => new RelayCommand(OnSocketServerConfigAsync);

        /// <summary>
        /// Mqtt 客户端配置命令
        /// </summary>
        public ICommand? MqttClientConfigCommand => new RelayCommand(OnMqttClientConfigAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_CommunicationSettingPage(
            ILoggerService<VM_CommunicationSettingPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            ISocketOptionsLoader socketOptionsLoader,
            ICustomMqttOptionsLoader customMqttOptionsLoader,
            ICommunicationService communicationService)
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _socketOptionsLoader = socketOptionsLoader;
            _customMqttOptionsLoader = customMqttOptionsLoader;
            _communicationService = communicationService;
        }

        /// <summary>
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadCommunicationSettingModel();
            NavigateToDefault();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            ControlPageName = "WMCommunicationControl";
        }

        /// <summary>
        /// 通讯设置界面切换时触发
        /// </summary>
        private void OnControlPageNavigateCommand()
        {
            LoadCommunicationSettingModel();
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var old = _projectSettingRepository.LoadCommunicationSettingProjectParameters();
            var isEquals = CommunicationModel.EqualsByJson(old);
            if (isEquals)
            {
                await VM_CCTBMessageBoxDialog.InfoAsync("当前配置未发生修改！", "提示");
                return;
            }

            var re = _projectSettingRepository.SaveCommunicationSettingProjectParameters(CommunicationModel);
            if (re) await VM_CCTBMessageBoxDialog.SuccessAsync("保存成功！相关设备将自动更新配置或连接！", "保存提示");
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        /// <summary>
        /// 当PLC参数保存时触发
        /// </summary>
        private async void OnParamPLCSaveAsync()
        {
            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            var isExists = model != null;
            if (!isExists)
            {
                model = new PLCConfigModel();
                model.Name = CCTBCore.PLCName;
                model.BrandSpecificationProtocal = "Omron_Fins_TCP";
                model.CycleReadTime = 500;
                model.CycleWriteTime = 500;
                model.IsEnable = true;
            }

            model!.IP = CommunicationModel.CMPLCIP ?? "192.168.0.1";
            model!.Port = CommunicationModel.CMPLCPort;
            model!.BrandSpecificationProtocal = CommunicationModel.CMBrandSpecificationProtocal ?? string.Empty;
            model!.CycleReadTime = CommunicationModel.CMCycleReadTime;
            model!.CycleWriteTime = CommunicationModel.CMCycleWriteTime;
            var re = isExists ? _plcConfigRepository.Update(model) : _plcConfigRepository.Add(model);
            if (re) await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！请重启软件以更新配置！", "保存提示");
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        /// <summary>
        /// 当参数保存取消时触发
        /// </summary>
        private async void OnParamCancleAsync()
        {
            LoadCommunicationSettingModel();
        }

        #region 通讯设置参数
        private CommunicationSettingModel communicationModel = new();

        /// <summary>
        /// 通讯设置参数模型
        /// </summary>
        public CommunicationSettingModel CommunicationModel { get => communicationModel; set => SetProperty(ref communicationModel, value); }

        /// <summary>
        /// 加载通讯设置参数
        /// </summary>
        /// <returns></returns>
        private void LoadCommunicationSettingModel()
        {
            var settingModel = _projectSettingRepository.LoadCommunicationSettingProjectParameters();
            if (settingModel == null) return;

            CommunicationModel = settingModel;
            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();

            if (model == null) return;

            CommunicationModel.CMPLCIP = model.IP;
            CommunicationModel.CMPLCPort = model.Port;
            CommunicationModel.CMBrandSpecificationProtocal = model.BrandSpecificationProtocal;
            CommunicationModel.CMCycleReadTime = model.CycleReadTime;
            CommunicationModel.CMCycleWriteTime = model.CycleWriteTime;
            CommunicationModel.CMIsEnable = model.IsEnable;
        }
        #endregion

        #region 
        /// <summary>
        /// 当 Socket 客户端配置时触发
        /// </summary>
        /// <param name="parameter"></param>
        private async void OnSocketClientConfigAsync(object? parameter)
        {
            try
            {
                if (!(parameter is string deviceName)) return;
                var moduleName = ICommunicationService.ModuleToConfigGroupMap[deviceName];

                var options = await _socketOptionsLoader.LoadClientOptionsAsync(moduleName);
                var result = await Dialog.Show(new VM_SocketClientConfigDialog().UIElement)
                            .Initialize<VM_SocketClientConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                                o.ProtocolOptions = options.Protocol;
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.Info($"用户取消 Socket 客户端[{moduleName}]配置修改");
                    return;
                }
                var old = await _socketOptionsLoader.LoadClientOptionsAsync(moduleName);
                var isServerOptionsChanged = !options.EqualsByJson(old);
                var isProtocolChanged = !options.Protocol.EqualsByJson(old.Protocol);

                if (!isServerOptionsChanged && !isProtocolChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"Socket 客户端[{moduleName}]配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync(
                    $"确认保存 Socket 客户端[{moduleName}]配置修改？\n\n" +
                    $"服务器配置: {(isServerOptionsChanged ? "已修改" : "未修改")}\n" +
                    $"协议配置: {(isProtocolChanged ? "已修改" : "未修改")}", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存Socket客户端[{moduleName}]配置");
                    return;
                }

                bool serverUpdateSuccess = true, protocolUpdateSuccess = true;
                var updateErrors = new List<string>();

                if (isServerOptionsChanged)
                {
                    serverUpdateSuccess = await _socketOptionsLoader.UpdateClientOptionsAsync(moduleName, options);
                    if (!serverUpdateSuccess) updateErrors.Add("服务器配置更新失败");
                }

                if (isProtocolChanged)
                {
                    protocolUpdateSuccess = await _socketOptionsLoader.UpdateProtocolOptionsAsync(moduleName, options.Protocol);
                    if (!protocolUpdateSuccess) updateErrors.Add("协议配置更新失败");
                }

                if (updateErrors.Count == 0)
                {
                    _logger.Info($"Socket 客户端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Socket 客户端[{moduleName}]配置更新完成！", "提示");
                }
                else if (updateErrors.Count == 1 && !serverUpdateSuccess && !protocolUpdateSuccess)
                {
                    _logger.Error($"Socket 客户端[{moduleName}]配置保存完全失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Socket 客户端[{moduleName}]配置更新失败！\n原因：{string.Join("、", updateErrors)}", "提示");
                }
                else
                {
                    _logger.Warn($"Socket 客户端[{moduleName}]配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"Socket 客户端[{moduleName}]配置部分更新成功！\n失败项：{string.Join("、", updateErrors)}", "提示");
                }

                _communicationService.NotifyModulesChanged(deviceName);
            }
            catch (Exception ex)
            {
                _logger.Error($"Socket 客户端配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 当 Socket 服务端配置时触发
        /// </summary>
        /// <param name="parameter"></param>
        private async void OnSocketServerConfigAsync(object? parameter)
        {
            try
            {
                if (!(parameter is string deviceName)) return;
                var moduleName = ICommunicationService.ModuleToConfigGroupMap[deviceName];

                var options = await _socketOptionsLoader.LoadServerOptionsAsync(moduleName);
                var result = await Dialog
                            .Show(new VM_SocketServerConfigDialog().UIElement)
                            .Initialize<VM_SocketServerConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                                o.ProtocolOptions = options.Protocol;
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.Info($"用户取消 Socket 服务端[{moduleName}]配置修改");
                    return;
                }
                var old = await _socketOptionsLoader.LoadServerOptionsAsync(moduleName);
                var isServerOptionsChanged = !options.EqualsByJson(old);
                var isProtocolChanged = !options.Protocol.EqualsByJson(old.Protocol);

                if (!isServerOptionsChanged && !isProtocolChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"Socket 服务端[{moduleName}]配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync(
                    $"确认保存 Socket 服务端[{moduleName}]配置修改？\n\n" +
                    $"服务器配置: {(isServerOptionsChanged ? "已修改" : "未修改")}\n" +
                    $"协议配置: {(isProtocolChanged ? "已修改" : "未修改")}", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存Socket服务端[{moduleName}]配置");
                    return;
                }

                bool serverUpdateSuccess = true, protocolUpdateSuccess = true;
                var updateErrors = new List<string>();

                if (isServerOptionsChanged)
                {
                    serverUpdateSuccess = await _socketOptionsLoader.UpdateServerOptionsAsync(moduleName, options);
                    if (!serverUpdateSuccess) updateErrors.Add("服务器配置更新失败");
                }

                if (isProtocolChanged)
                {
                    protocolUpdateSuccess = await _socketOptionsLoader.UpdateProtocolOptionsAsync(moduleName, options.Protocol);
                    if (!protocolUpdateSuccess) updateErrors.Add("协议配置更新失败");
                }

                if (updateErrors.Count == 0)
                {
                    _logger.Info($"Socket 服务端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Socket 服务端[{moduleName}]配置更新完成！", "提示");
                }
                else if (updateErrors.Count == 1 && !serverUpdateSuccess && !protocolUpdateSuccess)
                {
                    _logger.Error($"Socket 服务端[{moduleName}]配置保存完全失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Socket 服务端[{moduleName}]配置更新失败！\n原因：{string.Join("、", updateErrors)}", "提示");
                }
                else
                {
                    _logger.Warn($"Socket 服务端[{moduleName}]配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"Socket 服务端[{moduleName}]配置部分更新成功！\n失败项：{string.Join("、", updateErrors)}", "提示");
                }

                if (deviceName == ICommunicationService.OPMMSocketClientDeviceNames)
                {
                    _communicationService.NotifyModulesChanged(ICommunicationService.OPMMPLCSocketClientDeviceName);
                    _communicationService.NotifyModulesChanged(ICommunicationService.OPMMCCDSocketClientDeviceName);
                    _communicationService.NotifyModulesChanged(ICommunicationService.OPMMWeightSocketClientDeviceName);
                }
                else _communicationService.NotifyModulesChanged(deviceName);
            }
            catch (Exception ex)
            {
                _logger.Error($"Socket 服务端配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 当 Mqtt 客户端配置时触发
        /// </summary>
        /// <param name="parameter"></param>
        private async void OnMqttClientConfigAsync(object? parameter)
        {
            try
            {
                if (!(parameter is string deviceName)) return;
                var moduleName = ICommunicationService.ModuleToConfigGroupMap[deviceName];

                var options = await _customMqttOptionsLoader.LoadMqttClientOptionsAsync(moduleName);

                var result = await Dialog.Show(new VM_MqttClientConfigDialog().UIElement)
                            .Initialize<VM_MqttClientConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.Info($"用户取消 Mqtt 客户端[{moduleName}]配置修改");
                    return;
                }

                var old = await _customMqttOptionsLoader.LoadMqttClientOptionsAsync(moduleName);
                var isChanged = !options.EqualsByJson(old);
                if (!isChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"Mqtt 客户端[{moduleName}]配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync($"确认保存 Mqtt 客户端[{moduleName}]配置修改？", "确认保存");

                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存 Mqtt 客户端[{moduleName}]配置");
                    return;
                }

                var isUpdateSuccess = await _customMqttOptionsLoader.UpdateMqttClientOptionsAsync(moduleName, options);

                if (isUpdateSuccess)
                {
                    _logger.Info($"Mqtt 客户端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Mqtt 客户端[{moduleName}]配置更新完成！", "提示");
                }
                else
                {
                    _logger.Error($"Mqtt 客户端[{moduleName}]配置保存失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Mqtt 客户端[{moduleName}]配置更新失败！", "提示");
                }

                _communicationService.NotifyModulesChanged(deviceName);
            }
            catch (Exception ex)
            {
                _logger.Error($"Mqtt 客户端配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }
        #endregion
    }
}
