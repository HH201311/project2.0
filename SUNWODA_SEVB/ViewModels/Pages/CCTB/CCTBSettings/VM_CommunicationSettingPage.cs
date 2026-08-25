using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.ProtocolModels.WMSocketClient;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Socket;
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
using TouchSocket.Sockets;

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
        private void OnControlPageNavigateCommand(object? arg)
        {
            if (!(arg is string pageName)) return;

            _logger.UserInfo($"用户导航到了 通讯设置/{pageName} 界面！");

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
            if (re)
            {
                _logger.UserInfo($"用户保存了 通讯设置/{ControlPageName} 参数修改！");
                await VM_CCTBMessageBoxDialog.SuccessAsync("保存成功！相关设备将自动更新配置或连接！", "保存提示");
            }
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
                _logger.UserInfo($"用户打开了 Socket 客户端[{moduleName}]配置窗口！");
                var result = await Dialog.Show(new VM_SocketClientConfigDialog().UIElement)
                            .Initialize<VM_SocketClientConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                                o.ProtocolOptions = options.Protocol;
                                o.ResetOptionCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is SocketClientOptions options)) return;
                                    ReserSocketOptions(ref options);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"通讯配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                                o.ResetProtocolCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is ProtocolOptions protocol)) return;
                                    ResetSocketProtocol(deviceName, ref protocol);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"协议配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.UserInfo($"用户取消 Socket 客户端[{moduleName}]配置修改");
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
                    _logger.UserInfo($"用户取消保存Socket客户端[{moduleName}]配置");
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
                    _logger.UserInfo($"Socket 客户端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Socket 客户端[{moduleName}]配置更新完成！", "提示");
                }
                else if (!serverUpdateSuccess && !protocolUpdateSuccess)
                {
                    _logger.UserError($"Socket 客户端[{moduleName}]配置保存完全失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Socket 客户端[{moduleName}]配置更新失败！\n原因：{string.Join("、", updateErrors)}", "提示");
                    _communicationService.NotifyModulesChanged(deviceName);
                }
                else
                {
                    _logger.UserWarn($"Socket 客户端[{moduleName}]配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"Socket 客户端[{moduleName}]配置部分更新成功！\n失败项：{string.Join("、", updateErrors)}", "提示");
                }
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
                _logger.UserInfo($"用户打开了 Socket 服务端[{moduleName}]配置窗口！");
                var result = await Dialog.Show(new VM_SocketServerConfigDialog().UIElement)
                            .Initialize<VM_SocketServerConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                                o.ProtocolOptions = options.Protocol;
                                o.ResetOptionCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is SocketServerOptions options)) return;
                                    ReserSocketOptions(ref options);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"通讯配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                                o.ResetProtocolCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is ProtocolOptions protocol)) return;
                                    ResetSocketProtocol(deviceName, ref protocol);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"协议配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.UserInfo($"用户取消 Socket 服务端[{moduleName}]配置修改");
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
                    _logger.UserInfo($"用户取消保存Socket服务端[{moduleName}]配置");
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
                    _logger.UserInfo($"Socket 服务端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Socket 服务端[{moduleName}]配置更新完成！", "提示");
                }
                else if (!serverUpdateSuccess && !protocolUpdateSuccess)
                {
                    _logger.UserError($"Socket 服务端[{moduleName}]配置保存完全失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Socket 服务端[{moduleName}]配置更新失败！\n原因：{string.Join("、", updateErrors)}", "提示");
                }
                else
                {
                    _logger.UserWarn($"Socket 服务端[{moduleName}]配置部分保存失败");
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
                var sideType = deviceName switch
                {
                    ICommunicationService.DHAMqttClientDeviceName => ESideType.A,
                    ICommunicationService.DHBMqttClientDeviceName => ESideType.B,
                    _ => throw new Exception($"不支持的设备：{deviceName}")
                };

                var options = await _customMqttOptionsLoader.LoadMqttClientOptionsAsync(moduleName);
                var addressModel = _projectSettingRepository.LoadMqttAddressSettingProjectParameters(sideType);
                _logger.UserInfo($"用户打开了 Mqtt 客户端[{moduleName}]配置窗口！");
                var result = await Dialog.Show(new VM_MqttClientConfigDialog().UIElement)
                            .Initialize<VM_MqttClientConfigDialog>(o =>
                            {
                                o.CommunicationOptions = options;
                                o.AddressModel = addressModel;
                                o.ResetOptionCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is CustomMqttClientOptions options)) return;
                                    ResetMqttOptions(deviceName, ref options);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"通讯配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                                o.ResetAddressCommand = new RelayCommand(async (arg) =>
                                {
                                    if (!(arg is MqttAddressModel address)) return;
                                    ResetMqttAddress(deviceName, ref address);
                                    await VM_CCTBMessageBoxDialog.SuccessAsync($"通讯配置已恢复默认配置，您可点击 保存 按钮保存您的修改！", "提示");
                                });
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.UserInfo($"用户取消 Mqtt 客户端[{moduleName}]配置修改");
                    return;
                }

                var oldOption = await _customMqttOptionsLoader.LoadMqttClientOptionsAsync(moduleName);
                var oldAddress = _projectSettingRepository.LoadMqttAddressSettingProjectParameters(sideType);
                var isOptionChanged = !options.EqualsByJson(oldOption);
                var isAddressChanged = !addressModel.EqualsByJson(oldAddress);
                if (!isOptionChanged && !isAddressChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"Mqtt 客户端[{moduleName}]配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync(
                        $"确认保存 Mqtt 客户端[{moduleName}]配置修改？\n\n" +
                        $"通讯配置: {(isOptionChanged ? "已修改" : "未修改")}\n" +
                        $"地址信息配置: {(isAddressChanged ? "已修改" : "未修改")}", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.UserInfo($"用户取消保存Mqtt客户端[{moduleName}]配置");
                    return;
                }

                bool optionUpdateSuccess = true, addressUpdateSuccess = true;
                var updateErrors = new List<string>();

                if (isOptionChanged)
                {
                    optionUpdateSuccess = await _customMqttOptionsLoader.UpdateMqttClientOptionsAsync(moduleName, options);
                    if (!optionUpdateSuccess) updateErrors.Add("客户器通讯配置更新失败");
                }

                if (isAddressChanged)
                {
                    addressUpdateSuccess = _projectSettingRepository.SaveMqttAddressSettingProjectParameters(sideType, addressModel);
                    if (!addressUpdateSuccess) updateErrors.Add("客户器地址信息配置更新失败");
                }

                if (updateErrors.Count == 0)
                {
                    _logger.UserInfo($"Mqtt 客户端[{moduleName}]配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"Mqtt 客户端[{moduleName}]配置更新完成！", "提示");
                }
                else if (!optionUpdateSuccess && !addressUpdateSuccess)
                {
                    _logger.UserError($"Mqtt 客户端[{moduleName}]配置保存完全失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"Mqtt 客户端[{moduleName}]配置更新失败！\n原因：{string.Join("、", updateErrors)}", "提示");
                }
                else
                {
                    _logger.UserWarn($"Mqtt 客户端[{moduleName}]配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"Mqtt 客户端[{moduleName}]配置部分更新成功！\n失败项：{string.Join("、", updateErrors)}", "提示");
                }

                if (optionUpdateSuccess) _communicationService.NotifyModulesChanged(deviceName);
            }
            catch (Exception ex)
            {
                _logger.Error($"Mqtt 客户端配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 重置通讯配置
        /// </summary>
        /// <param name="options"></param>
        private void ReserSocketOptions(ref SocketServerOptions options)
        {
            options.MaxClients = 5;
            options.ReceiveBufferSize = 8192;
            options.Backlog = 100;
            options.ReceiveTimeoutMilliseconds = 0;
            options.DefaultPort = 8080;
        }

        /// <summary>
        /// 重置通讯配置
        /// </summary>
        /// <param name="options"></param>
        private void ReserSocketOptions(ref SocketClientOptions options)
        {
            options.ConnectTimeoutMilliseconds = 5000;
            options.ReceiveBufferSize = 8192;
            options.ReceiveTimeout = 5000;
            options.SendTimeout = 5000;
            options.defaultIP = "localhost";
            options.defaultPort = 8080;
        }

        /// <summary>
        /// 重置协议配置
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="protocol"></param>
        private void ResetSocketProtocol(string deviceName, ref ProtocolOptions protocol)
        {
            switch (deviceName)
            {
                case ICommunicationService.WMSocketClientDeviceName:
                    {
                        protocol.FramingMode = FramingMode.StartEndDelimiter;
                        protocol.StartDelimiter = MeasurementFrame.StartMarker.ToArray();
                        protocol.EndDelimiter = MeasurementFrame.EndMarker.ToArray();
                        protocol.StripDelimiters = false;
                        break;
                    }
                case ICommunicationService.WMSocketServerDeviceName:
                case ICommunicationService.DHSocketServerDeviceName:
                case ICommunicationService.DHASocketClientDeviceName:
                case ICommunicationService.DHBSocketClientDeviceName:
                    {
                        protocol.FramingMode = FramingMode.StartEndDelimiter;
                        protocol.StartDelimiter = Encoding.UTF8.GetBytes("\u0002");
                        protocol.EndDelimiter = Encoding.UTF8.GetBytes("\u0003");
                        break;
                    }
                case ICommunicationService.CCDSocketServerDeviceName:
                    {
                        protocol.FramingMode = FramingMode.EndDelimiterOnly;
                        protocol.StartDelimiter = Encoding.UTF8.GetBytes("|||");
                        protocol.EndDelimiter = Encoding.UTF8.GetBytes("|||");
                        protocol.StripDelimiters = true;
                        break;
                    }
                default:
                    {
                        protocol.FramingMode = FramingMode.None;
                        break;
                    }
            }
        }

        /// <summary>
        /// 重置协议配置
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="options"></param>
        private void ResetMqttOptions(string deviceName, ref CustomMqttClientOptions options)
        {
            switch (deviceName)
            {
                case ICommunicationService.DHAMqttClientDeviceName:
                case ICommunicationService.DHBMqttClientDeviceName:
                    {
                        options.ClientId = "ime";
                        options.ConnectTimeout = 5000;
                        options.UserName = "ime";
                        options.Password = "123456";
                        options.KeepAlivePeriod = 10;
                        options.KeepAliveSendInterval = 10;
                        options.IpAddress = "localhost";
                        options.Port = 183;
                        options.PublishTopics = new();
                        options.SubscribeTopics = new();
                        break;
                    }
                default: break;
            }
        }

        /// <summary>
        /// 重置Mqtt地址配置
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="address"></param>
        private void ResetMqttAddress(string deviceName, ref MqttAddressModel address)
        {
            switch (deviceName)
            {
                case ICommunicationService.DHAMqttClientDeviceName:
                    {
                        address.Protocol = "fins";
                        address.PumpAddress1 = "8176";
                        address.PumpAddress2 = string.Empty;
                        address.PumpAddress3 = string.Empty;
                        address.PumpAddress4 = string.Empty;
                        address.PumpAddress5 = string.Empty;
                        address.FlagAddress = "8186";
                        break;
                    }
                case ICommunicationService.DHBMqttClientDeviceName:
                    {
                        address.Protocol = "fins";
                        address.PumpAddress1 = "8236";
                        address.PumpAddress2 = string.Empty;
                        address.PumpAddress3 = string.Empty;
                        address.PumpAddress4 = string.Empty;
                        address.PumpAddress5 = string.Empty;
                        address.FlagAddress = "8226";
                        break;
                    }
                default: break;
            }
        }
        #endregion
    }
}
