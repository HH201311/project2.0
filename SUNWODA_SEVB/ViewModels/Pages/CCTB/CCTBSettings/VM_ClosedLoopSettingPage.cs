using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Masuit.Tools;
using Newtonsoft.Json;
using SqlSugar;
using SUNWODA_SEVB.Component.UserControls.Project.CCTB;
using SUNWODA_SEVB.Component.UserControls.Project.CCTB.SettingControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Services.Project.CCTB;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings
{
    [Module("VM_ClosedLoopSettingPage", "闭环设置", Order = 2, Category = "VM_CCTBSettingsPage/VM_ClosedLoopSettingPage")]
    class VM_ClosedLoopSettingPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ClosedLoopSettingPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;
        private readonly IAlgorithmService _algorithmService;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_ClosedLoopSettingPage(
            ILoggerService<VM_ClosedLoopSettingPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager,
            IAlgorithmService algorithmService
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
            _algorithmService = algorithmService;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void OnInitialize()
        {
            LoadAlgorithmModel();
            LoadGeneralSettingsModel();
            base.OnInitialize();
        }

        /// <summary>
        /// 导航完成时触发
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            LoadAlgorithmModel();
            LoadGeneralSettingsModel();
            await base.OnNavigatedToAsync(parameter);
        }

        #region 算法配置参数
        private AlgorithmModel algorithmNameModel_MDA = new(), algorithmNameModel_TDA = new(), algorithmNameModel_MDTDA = new(), algorithmNameModel_MDB = new(), algorithmNameModel_TDB = new(), algorithmNameModel_MDTDB = new();
        public AlgorithmModel AlgorithmModel_MDA { get => algorithmNameModel_MDA; set => SetProperty(ref algorithmNameModel_MDA, value); }
        public AlgorithmModel AlgorithmModel_TDA { get => algorithmNameModel_TDA; set => SetProperty(ref algorithmNameModel_TDA, value); }
        public AlgorithmModel AlgorithmModel_MDTDA { get => algorithmNameModel_MDTDA; set => SetProperty(ref algorithmNameModel_MDTDA, value); }
        public AlgorithmModel AlgorithmModel_MDB { get => algorithmNameModel_MDB; set => SetProperty(ref algorithmNameModel_MDB, value); }
        public AlgorithmModel AlgorithmModel_TDB { get => algorithmNameModel_TDB; set => SetProperty(ref algorithmNameModel_TDB, value); }
        public AlgorithmModel AlgorithmModel_MDTDB { get => algorithmNameModel_MDTDB; set => SetProperty(ref algorithmNameModel_MDTDB, value); }

        public ICommand? AlgorithmEnableChangedCommand => new RelayCommand(AlgorithmEnableChanged);
        public ICommand? AlgorithmVerifyCommand => new RelayCommand(AlgorithmVerifyAsync);

        private void AlgorithmEnableChanged(object? arg)
        {
            if (!(arg is AlgorithmSettingControl page)) return;
            var closedLoopType = page.ClosedLoopType;
            var sideType = page.SideType;

            var projectSettingName = $"AlgorithmSetting_{closedLoopType}{sideType}";
            var settingModel = (closedLoopType, sideType) switch
            {
                (EClosedLoopType.MD, ESideType.A) => AlgorithmModel_MDA,
                (EClosedLoopType.TD, ESideType.A) => AlgorithmModel_TDA,
                (EClosedLoopType.MDTD, ESideType.A) => AlgorithmModel_MDTDA,
                (EClosedLoopType.MD, ESideType.B) => AlgorithmModel_MDB,
                (EClosedLoopType.TD, ESideType.B) => AlgorithmModel_TDB,
                (EClosedLoopType.MDTD, ESideType.B) => AlgorithmModel_MDTDB,
                _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
            };
            // 若校验成功则执行更新(界面+数据库)
            var re = _projectSettingRepository.SaveAlgorithmProjectParameters(closedLoopType, sideType, settingModel);
            if (!re)
            {
                settingModel.IsEnable = !page.IsAlgorithmEnabled;
                page.IsAlgorithmEnabled = !page.IsAlgorithmEnabled;
            }
        }

        private async void AlgorithmVerifyAsync(object? arg)
        {
            if (!(arg is object[] args && args?.Length == 4)) return;
            if (!(args[0] is ESideType sideType)) return;
            if (!(args[1] is EClosedLoopType closedLoopType)) return;
            if (!(args[2] is string newAlgorithmName)) return;
            if (!(args[3] is string oldAlgorithmName)) return;

            if (string.IsNullOrEmpty(newAlgorithmName)) return;
            //if (string.Equals(newAlgorithmName, oldAlgorithmName)) return;

            var projectSettingName = $"AlgorithmSetting_{closedLoopType}{sideType}";
            var settingModel = (closedLoopType, sideType) switch
            {
                (EClosedLoopType.MD, ESideType.A) => AlgorithmModel_MDA,
                (EClosedLoopType.TD, ESideType.A) => AlgorithmModel_TDA,
                (EClosedLoopType.MDTD, ESideType.A) => AlgorithmModel_MDTDA,
                (EClosedLoopType.MD, ESideType.B) => AlgorithmModel_MDB,
                (EClosedLoopType.TD, ESideType.B) => AlgorithmModel_TDB,
                (EClosedLoopType.MDTD, ESideType.B) => AlgorithmModel_MDTDB,
                _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
            };

            var (verifyResult, verifyMessage) = await _algorithmService.VerifyAlgorithmAsync(sideType, closedLoopType, newAlgorithmName);
            // 校验算法
            if (!verifyResult)// 校验失败则弹窗提示
            {
                await RunOnUIThreadAsync(async () => await VM_CCTBMessageBoxDialog.ErrorAsync(verifyMessage, $"{sideType}面{closedLoopType}算法校验失败"));
                settingModel.AlgorithmName = string.Empty;
                settingModel.AlgorithmName = oldAlgorithmName;
                return;
            }

            settingModel.AlgorithmName = newAlgorithmName;
            var re = _projectSettingRepository.SaveAlgorithmProjectParameters(closedLoopType, sideType, settingModel);
            if (!re)
            {
                await RunOnUIThreadAsync(async () => await VM_CCTBMessageBoxDialog.ErrorAsync($"{sideType}面{closedLoopType}算法[{newAlgorithmName}]校验成功，保存数据库失败!", "提示"));
                settingModel.AlgorithmName = oldAlgorithmName;
                return;
            }

            await RunOnUIThreadAsync(async () => await VM_CCTBMessageBoxDialog.SuccessAsync($"{sideType}面{closedLoopType}算法[{newAlgorithmName}]校验成功，已保存数据库!", "提示"));
        }

        private void LoadAlgorithmModel()
        {
            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
            var sideTypes = Enum.GetValues(typeof(ESideType));
            foreach (EClosedLoopType closedLoopType in closedLoopTypes)
            {
                foreach (ESideType sideType in sideTypes)
                {
                    var settingModel = _projectSettingRepository.LoadAlgorithmProjectParameters(closedLoopType, sideType);
                    if (settingModel == null) continue;

                    var algorithmNameModel = (closedLoopType, sideType) switch
                    {
                        (EClosedLoopType.MD, ESideType.A) => AlgorithmModel_MDA,
                        (EClosedLoopType.TD, ESideType.A) => AlgorithmModel_TDA,
                        (EClosedLoopType.MDTD, ESideType.A) => AlgorithmModel_MDTDA,
                        (EClosedLoopType.MD, ESideType.B) => AlgorithmModel_MDB,
                        (EClosedLoopType.TD, ESideType.B) => AlgorithmModel_TDB,
                        (EClosedLoopType.MDTD, ESideType.B) => AlgorithmModel_MDTDB,
                        _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
                    };
                    algorithmNameModel.IsEnable = settingModel.IsEnable;
                    algorithmNameModel.AlgorithmName = settingModel.AlgorithmName;
                }
            }
        }
        #endregion

        #region 闭环参数设置
        public ICommand? ParamSettingClickCommand => new RelayCommand(ParamSettingClickAsync);

        private async void ParamSettingClickAsync(object? arg)
        {
            if (arg == null) return;
            if (!(arg is AlgorithmSettingControl page)) return;
            var closedLoopType = page.ClosedLoopType;
            var sideType = page.SideType;
            try
            {
                var settingModel = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(closedLoopType, sideType);
                if (settingModel == null) return;

                // 获取当前用户角色
                var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
                var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;

                var result = await Dialog
                    .Show(new VM_ClosedLoopSettingDialog().UIElement)
                    .Initialize<VM_ClosedLoopSettingDialog>(o =>
                    {
                        o.SideType = sideType;
                        o.ClosedLoopType = closedLoopType;
                        o.ClosedLoopSettingModel = settingModel;
                        o.UserRole = (UserRole)roleId;
                    })
                    .GetResultAsync<bool>();

                if (!result)
                {
                    _logger.Info($"用户取消 [{sideType}面{closedLoopType}闭环参数] 配置修改");
                    return;
                }

                var old = _projectSettingRepository.LoadClosedLoopSettingProjectParameters(closedLoopType, sideType);
                var isChanged = !settingModel.EqualsByJson(old);
                if (!isChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"[{sideType}面{closedLoopType}闭环参数] 配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync($"确认保存 [{sideType}面{closedLoopType}闭环参数] 配置修改？", "确认保存");

                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存 [{sideType}面{closedLoopType}闭环参数] 配置");
                    return;
                }

                var isUpdateSuccess = _projectSettingRepository.SaveClosedLoopSettingProjectParameters(closedLoopType, sideType, settingModel);
                if (isUpdateSuccess)
                {
                    _logger.Info($"[{sideType}面{closedLoopType}闭环参数] 配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"[{sideType}面{closedLoopType}闭环参数] 配置更新完成！", "提示");
                }
                else
                {
                    _logger.Error($"[{sideType}面{closedLoopType}闭环参数]  配置保存失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"[{sideType}面{closedLoopType}闭环参数] 配置更新失败！", "提示");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{sideType}面{closedLoopType}闭环参数] 配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }
        #endregion

        #region 通用设置
        public ObservableCollection<ComboboxItemModel<EWeightDataType>> WeightDataTypeSource { get; } =
            new ObservableCollection<ComboboxItemModel<EWeightDataType>>(
            Enum.GetValues(typeof(EWeightDataType)).Cast<EWeightDataType>()
            .Select(v => new ComboboxItemModel<EWeightDataType>() { ItemValue = v, DisplayName = v.ToString().Substring(0, v.ToString().Length - 2) }));

        public ObservableCollection<ComboboxItemModel<EMembraneZoneType>> EMembraneZoneTypeSource { get; } =
            new ObservableCollection<ComboboxItemModel<EMembraneZoneType>>(
            Enum.GetValues(typeof(EMembraneZoneType)).Cast<EMembraneZoneType>()
            .Select(v => new ComboboxItemModel<EMembraneZoneType>() { ItemValue = v, DisplayName = v.ToString() }));

        public ObservableCollection<ComboboxItemModel<EPartitionedType>> PartitionedTypeSource { get; } =
            new ObservableCollection<ComboboxItemModel<EPartitionedType>>(
            Enum.GetValues(typeof(EPartitionedType)).Cast<EPartitionedType>()
            .Select(v => new ComboboxItemModel<EPartitionedType>() { ItemValue = v, DisplayName = v.ToString() }));

        public ICommand? ParamSaveCommand => new RelayCommand(ParamSaveAsync);
        public ICommand? PartitionedSelectionChangedCommand => new RelayCommand(PartitionedSelectionChanged);

        private GeneralSettingsModel generalSettingsModel = new();

        public GeneralSettingsModel GeneralSettingsModel { get => generalSettingsModel; set => SetProperty(ref generalSettingsModel, value); }

        public ObservableCollection<PartitionedModel> PartitionedModels { get; set; } = [];

        private async void ParamSaveAsync()
        {
            GeneralSettingsModel.PartitionedModels.Clear();
            GeneralSettingsModel.PartitionedModels.AddRange(PartitionedModels);

            try
            {
                var old = _projectSettingRepository.LoadGeneralSettingsProjectParameters();
                var isChanged = !GeneralSettingsModel.EqualsByJson(old);
                if (!isChanged)
                {
                    await VM_CCTBMessageBoxDialog.InfoAsync($"[通用设置] 配置未修改，无需保存！", "提示");
                    return;
                }

                var confirmResult = await VM_CCTBMessageBoxDialog.AskAsync($"确认保存 [通用设置] 配置修改？", "确认保存");
                if (confirmResult != System.Windows.MessageBoxResult.OK)
                {
                    _logger.Info($"用户取消保存 [通用设置] 配置");
                    return;
                }

                var isUpdateSuccess = _projectSettingRepository.SaveGeneralSettingsProjectParameters(GeneralSettingsModel);
                if (isUpdateSuccess)
                {
                    _logger.Info($"[通用设置] 配置保存成功");
                    await VM_CCTBMessageBoxDialog.SuccessAsync($"[通用设置] 配置更新完成！", "提示");
                }
                else
                {
                    _logger.Error($"[通用设置]  配置保存失败");
                    await VM_CCTBMessageBoxDialog.ErrorAsync($"[通用设置] 配置更新失败！", "提示");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[通用设置] 配置保存异常: {ex.Message}", ex);
                await VM_CCTBMessageBoxDialog.ErrorAsync($"保存配置时发生异常：{ex.Message}", "错误");
            }
        }

        private void PartitionedSelectionChanged(object? arg)
        {
            if (!(arg is ComboboxItemModel<EPartitionedType> item)) return;

            var partitionedModels = PartitionedModel.GetPartitionedModels(GeneralSettingsModel.PartitionedType, GeneralSettingsModel.PartitionedModels);
            PartitionedModels.Clear();
            PartitionedModels.AddRange(partitionedModels);
        }

        /// <summary>
        /// 加载涂布闭环通用参数
        /// </summary>
        private void LoadGeneralSettingsModel()
        {
            var settingModel = _projectSettingRepository.LoadGeneralSettingsProjectParameters();
            if (settingModel == null) return;

            GeneralSettingsModel = settingModel;
            var partitionedModels = PartitionedModel.GetPartitionedModels(settingModel.PartitionedType, settingModel.PartitionedModels);
            RunOnUIThread(() =>
            {
                PartitionedModels.Clear();
                PartitionedModels.AddRange(partitionedModels);
            });
        }
        #endregion
    }
}
