using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Mapster;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core.MessageModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Services.Project.CCTB.Business;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.CommunicationOptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.FunctionSetting
{
    [Module("VM_BasicFunctionsPage", "基础功能", Order = 3, Category = "VM_CCTBSettingsPage/VM_FunctionSettingPage/VM_BasicFunctionsPage")]
    class VM_BasicFunctionsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_BasicFunctionsPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCService _plcService;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly BusinessServiceManager _businessServiceManager;
        private readonly BusinessServiceFactory _businessServiceFactory;
        private string? controlPageName;
        public string? ControlPageName { get => controlPageName; set => SetProperty(ref controlPageName, value); }

        /// <summary>
        /// 界面导航按钮
        /// </summary>
        public ICommand? ControlPageNavigateCommand => new RelayCommand(OnControlPageNavigate);

        /// <summary>
        /// 保存按钮
        /// </summary>
        public ICommand? ParamSaveCommand => new RelayCommand(OnParamSaveAsync);

        /// <summary>
        /// 取消按钮
        /// </summary>
        public ICommand? ParamCancleCommand => new RelayCommand(OnParamCancle);

        /// <summary>
        /// FTP手动上传
        /// </summary>
        public ICommand? FTPUpLoadCommand => new RelayCommand(OnFTPUpLoadAsync);

        /// <summary>
        /// 膜卷号读取地址验证
        /// </summary>
        public ICommand? RollNumPathVerifyCommand => new RelayCommand(OnRollNumPathVerifyAsync);

        /// <summary>
        /// PLC采集配置修改
        /// </summary>
        public ICommand? PLCGatherConfigCommand => new RelayCommand(OnPLCGatherConfigAsync);

        /// <summary>
        /// FTP配置修改
        /// </summary>
        public ICommand? FtpConfigCommand => new RelayCommand(OnFtpConfigAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_BasicFunctionsPage(
            ILoggerService<VM_BasicFunctionsPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCService plcService,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            BusinessServiceManager businessServiceManager,
            BusinessServiceFactory businessServiceFactory
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcService = plcService;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _businessServiceManager = businessServiceManager;
            _businessServiceFactory = businessServiceFactory;
        }

        /// <summary>
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            LoadFunctionSettingModel();
            NavigateToDefault();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 导航到默认界面
        /// </summary>
        /// <returns></returns>
        private void NavigateToDefault()
        {
            ControlPageName = "WMFunctionControl";
        }

        /// <summary>
        /// 功能参数界面切换时触发
        /// </summary>
        private void OnControlPageNavigate(object? arg)
        {
            if (!(arg is string pageName)) return;

            _logger.UserInfo($"用户导航到了 基础功能/{pageName} 界面！");

            LoadFunctionSettingModel();
        }

        /// <summary>
        /// 当参数保存时触发
        /// </summary>
        private async void OnParamSaveAsync()
        {
            var re = _projectSettingRepository.SaveBasicFunctionSettingProjectParameters(FunctionModel);
            if (re)
            {
                // 参数更新通知
                CCTBCore.SettingMessageNotificationEvent?.Invoke(this, new MessageEventArgs<object>()
                {
                    Tag = "基础功能参数保存成功！",
                    Message = FunctionModel
                });

                _logger.UserInfo($"用户保存了 基础功能/{ControlPageName} 参数修改！");
                await VM_CCTBMessageBoxDialog.InfoAsync("保存成功！", "保存提示");
            }
            else await VM_CCTBMessageBoxDialog.ErrorAsync("保存失败！", "保存提示");
        }

        /// <summary>
        /// 当参数保存取消时触发
        /// </summary>
        private void OnParamCancle()
        {

        }

        #region 基础功能参数
        private BasicFunctionSettingModel functionModel = new();
        private DateTime ftpUpLoadData = DateTime.Now;

        /// <summary>
        /// 基础功能参数模型
        /// </summary>
        public BasicFunctionSettingModel FunctionModel { get => functionModel; set => SetProperty(ref functionModel, value); }

        /// <summary>
        /// FTP手动上传日期
        /// </summary>
        public DateTime FTPUpLoadData { get => ftpUpLoadData; set => SetProperty(ref ftpUpLoadData, value); }

        /// <summary>
        /// 加载基础功能参数
        /// </summary>
        /// <returns></returns>
        private void LoadFunctionSettingModel()
        {
            var settingModel = _projectSettingRepository.LoadBasicFunctionSettingProjectParameters();
            if (settingModel == null) return;

            FunctionModel = settingModel;
        }

        /// <summary>
        /// 手动上传FTP时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnFTPUpLoadAsync(object? arg)
        {
            if (!(arg is DateTime date)) return;

            try
            {
                var business = _businessServiceFactory.GetService(EBusinessType.BackBusiness) as BackBusinessService;
                if (business == null) return;
                business.SaveToFtp(date);
                _logger.UserInfo($"用户手动上传了FTP数据，日期为：{date}");
            }
            catch (Exception ex)
            {
                await VM_CCTBMessageBoxDialog.ErrorAsync(ex.Message, "用户手动上传了FTP数据异常");
            }
        }

        /// <summary>
        /// 点击验证膜卷号获取地址时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnRollNumPathVerifyAsync(object? arg)
        {
            if (!(arg is string filePath)) return;

            try
            {
                var productSn = CCTBCore.GetProductSn(filePath);
                if (string.IsNullOrEmpty(productSn)) await VM_CCTBMessageBoxDialog.WarningAsync("获取的膜卷号为空值！！", "获取膜卷号失败");
                else await VM_CCTBMessageBoxDialog.InfoAsync($"膜卷号:[{productSn}]！", "获取膜卷号成功");
            }
            catch (Exception ex)
            {
                await VM_CCTBMessageBoxDialog.ErrorAsync(ex.Message, "获取膜卷号异常");
            }
        }

        /// <summary>
        /// 修改PLC采集信息时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnPLCGatherConfigAsync(object? arg)
        {
            try
            {
                if (!(arg is string configName)) return;

                var items = LoadPLCGatherConfig(configName);
                _logger.UserInfo($"用户打开了[{configName}]PLC采集地址配置窗口！");
                var result = await Dialog.Show(new VM_CheckListDialog().UIElement)
                            .Initialize<VM_CheckListDialog>(o =>
                            {
                                o.DialogTitle = $"PLC采集参数配置-{configName}";
                                o.ItemSource = items;
                            })
                            .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.UserInfo($"用户取消[{configName}]PLC采集地址配置修改");
                    return;
                }
                var old = LoadPLCGatherConfig(configName);
                var isChanged = !items.EqualsByJson(old);

                if (!isChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"[{configName}]PLC采集地址配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync($"确认保存[{configName}]PLC采集地址配置修改？", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.UserInfo($"用户取消保存[{configName}]PLC采集地址配置");
                    return;
                }

                var isUpdateSuccess = SavePLCGatherConfig(configName, items);
                if (isUpdateSuccess)
                {
                    _logger.UserInfo($"[{configName}]PLC采集地址配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"[{configName}]PLC采集地址配置更新完成！", "提示");
                }
                else
                {
                    _logger.UserWarn($"[{configName}]PLC采集地址配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"[{configName}]PLC采集地址配置更新成功！", "提示");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"PLC采集地址配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存PLC采集地址配置时发生异常：{ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 获取PLC采集配置参数
        /// </summary>
        /// <param name="configName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private ObservableCollection<CheckedItemModel<AddressItemModel>> LoadPLCGatherConfig(string configName)
        {
            var result = new ObservableCollection<CheckedItemModel<AddressItemModel>>();
            var plcs = _plcConfigRepository.GetAll();
            if (!plcs.Exists(o => o.Name == CCTBCore.PLCName)) return result;
            var plcId = plcs.First(o => o.Name == CCTBCore.PLCName).ID;
            var plcAddresses = _plcService.RWAddresses.Values.Where(o => o.PlcId == plcId).ToList();
            var plcGatherConfig = configName switch
            {
                "A" => _projectSettingRepository.LoadAPLCGatherConfig(),
                "B" => _projectSettingRepository.LoadBPLCGatherConfig(),
                "PLC" => _projectSettingRepository.LoadPLCGatherConfig(),
                _ => throw new NotImplementedException()
            };
            foreach (var plcAddress in plcAddresses)
            {
                var addressItem = new AddressItemModel();
                addressItem.ID = plcAddress.ID;
                addressItem.Name = plcAddress.ParameterName ?? string.Empty;
                addressItem.Address = plcAddress.Address ?? string.Empty;
                var checkedItem = new CheckedItemModel<AddressItemModel>();
                checkedItem.Item = addressItem;
                checkedItem.DisplayName = plcAddress.ParameterName;
                checkedItem.IsChecked = plcGatherConfig.Contains(plcAddress.ID);
                result.Add(checkedItem);
            }

            return result;
        }

        /// <summary>
        /// 保存PLC采集配置参数
        /// </summary>
        /// <param name="configName"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private bool SavePLCGatherConfig(string configName, ObservableCollection<CheckedItemModel<AddressItemModel>> items)
        {
            var values = items.Where(o => o.IsChecked && o.Item != null).Select(o => o.Item!.ID).ToList();
            var result = configName switch
            {
                "A" => _projectSettingRepository.SaveAPLCGatherConfig(values),
                "B" => _projectSettingRepository.SaveBPLCGatherConfig(values),
                "PLC" => _projectSettingRepository.SavePLCGatherConfig(values),
                _ => throw new NotImplementedException()
            };

            // 参数更新通知
            CCTBCore.SettingMessageNotificationEvent?.Invoke(this, new MessageEventArgs<object>()
            {
                Tag = configName,
                Message = values
            });
            return result;
        }

        /// <summary>
        /// 修改FTP配置时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnFtpConfigAsync(object? arg)
        {
            try
            {
                var ftpConfig = _projectSettingRepository.LoadFtpConfigProjectParameter();
                _logger.UserInfo($"用户打开了FTP配置窗口！");
                var result = await Dialog.Show(new VM_FtpConfigDialog().UIElement)
                            .Initialize<VM_FtpConfigDialog>(o => { o.ItemSource = ftpConfig; })
                            .GetResultAsync<bool>();
                if (!result)
                {
                    _logger.UserInfo($"用户取消FTP配置修改");
                    return;
                }
                var old = _projectSettingRepository.LoadFtpConfigProjectParameter();
                var isChanged = !ftpConfig.EqualsByJson(old);
                if (!isChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"FTP配置未修改，无需保存！", "提示");
                    return;
                }
                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync($"确认保存FTP配置修改？", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.UserInfo($"用户取消保存FTP配置");
                    return;
                }
                var isUpdateSuccess = _projectSettingRepository.SaveFtpConfigProjectParameter(ftpConfig);
                if (isUpdateSuccess)
                {
                    _logger.UserInfo($"FTP配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"FTP配置更新完成！", "提示");
                }
                else
                {
                    _logger.UserWarn($"FTP配置部分保存失败");
                    await VM_CCTBMessageBoxDialog.WarningAsync($"FTP配置更新成功！", "提示");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"FTP配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存FTP配置时发生异常：{ex.Message}", "错误");
            }
        }
        #endregion
    }
}
