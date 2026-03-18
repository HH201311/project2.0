using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Masuit.Tools;
using Newtonsoft.Json;
using SqlSugar;
using SUNWODA_SEVB.Component.UserControls.Project.CCTB;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
using SUNWODA_SEVB.Tool.Extension;
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
    [Module("VM_ClosedLoopSettingPage", "闭环设置", Order = 0, Category = "VM_CCTBSettingsPage/VM_ClosedLoopSettingPage")]
    class VM_ClosedLoopSettingPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ClosedLoopSettingPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_ClosedLoopSettingPage(
            ILoggerService<VM_ClosedLoopSettingPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        public override void OnInitialize()
        {
            // 初始化导航
            InitializeArithmeticProjectSettings();
            InitializeClosedLoopProjectSettings();
            InitializeGeneralProjectSettings();

            base.OnInitialize();
        }

        protected override async Task OnInitializeAsync()
        {
            await LoadArithmeticModelAsync();
            await LoadGeneralSettingsModelAsync();
            await base.OnInitializeAsync();
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            await LoadArithmeticModelAsync();
            await LoadGeneralSettingsModelAsync();
            await base.OnNavigatedToAsync(parameter);
        }

        #region 算法配置参数
        private ArithmeticModel arithmeticModel_MDA = new(), arithmeticModel_TDA = new(), arithmeticModel_MDTDA = new(), arithmeticModel_MDB = new(), arithmeticModel_TDB = new(), arithmeticModel_MDTDB = new();
        public ArithmeticModel ArithmeticModel_MDA { get => arithmeticModel_MDA; set => SetProperty(ref arithmeticModel_MDA, value); }
        public ArithmeticModel ArithmeticModel_TDA { get => arithmeticModel_TDA; set => SetProperty(ref arithmeticModel_TDA, value); }
        public ArithmeticModel ArithmeticModel_MDTDA { get => arithmeticModel_MDTDA; set => SetProperty(ref arithmeticModel_MDTDA, value); }
        public ArithmeticModel ArithmeticModel_MDB { get => arithmeticModel_MDB; set => SetProperty(ref arithmeticModel_MDB, value); }
        public ArithmeticModel ArithmeticModel_TDB { get => arithmeticModel_TDB; set => SetProperty(ref arithmeticModel_TDB, value); }
        public ArithmeticModel ArithmeticModel_MDTDB { get => arithmeticModel_MDTDB; set => SetProperty(ref arithmeticModel_MDTDB, value); }

        public ICommand? ArithmeticEnableChangedCommand => new RelayCommand(ArithmeticEnableChanged);
        public ICommand? ArithmeticVerifyCommand => new RelayCommand(ArithmeticVerify);

        private async void ArithmeticEnableChanged(object? arg)
        {
            if (!(arg is ArithmeticSettingControl page)) return;
            var closedLoopType = page.ClosedLoopType;
            var sideType = page.SideType;

            var projectSettingName = $"ArithmeticSetting_{closedLoopType}{sideType}";
            var settingModel = (closedLoopType, sideType) switch
            {
                (EClosedLoopType.MD, ESideType.A) => ArithmeticModel_MDA,
                (EClosedLoopType.TD, ESideType.A) => ArithmeticModel_TDA,
                (EClosedLoopType.MDTD, ESideType.A) => ArithmeticModel_MDTDA,
                (EClosedLoopType.MD, ESideType.B) => ArithmeticModel_MDB,
                (EClosedLoopType.TD, ESideType.B) => ArithmeticModel_TDB,
                (EClosedLoopType.MDTD, ESideType.B) => ArithmeticModel_MDTDB,
                _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
            };
            // 若校验成功则执行更新(界面+数据库)
            var re = await RefreshJsonParamrersAsync(projectSettingName, settingModel);
            if (!re)
            {
                settingModel.IsEnable = !page.IsArithmeticEnabled;
            }
        }

        private async void ArithmeticVerify(object? arg)
        {
            if (!(arg is object[] args && args?.Length == 4)) return;
            if (!(args[0] is ESideType sideType)) return;
            if (!(args[1] is EClosedLoopType closedLoopType)) return;
            if (!(args[2] is string newArithmeticName)) return;
            if (!(args[3] is string oldArithmeticName)) return;

            if (string.IsNullOrEmpty(newArithmeticName)) return; ;
            if (string.Equals(newArithmeticName, oldArithmeticName)) return;

            var projectSettingName = $"ArithmeticSetting_{closedLoopType}{sideType}";
            var settingModel = (closedLoopType, sideType) switch
            {
                (EClosedLoopType.MD, ESideType.A) => ArithmeticModel_MDA,
                (EClosedLoopType.TD, ESideType.A) => ArithmeticModel_TDA,
                (EClosedLoopType.MDTD, ESideType.A) => ArithmeticModel_MDTDA,
                (EClosedLoopType.MD, ESideType.B) => ArithmeticModel_MDB,
                (EClosedLoopType.TD, ESideType.B) => ArithmeticModel_TDB,
                (EClosedLoopType.MDTD, ESideType.B) => ArithmeticModel_MDTDB,
                _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
            };

            // 校验算法
            if (false)// 校验失败则弹窗提示
            {
                MessageBox.Show($"{sideType}面{closedLoopType}算法[{newArithmeticName}]校验失败:[]!");
                settingModel.ArithmeticName = oldArithmeticName;
                return;
            }

            settingModel.ArithmeticName = newArithmeticName;
            var re = await RefreshArithmeticParamrersAsync(projectSettingName, settingModel);
            if (!re)
            {
                MessageBox.Show($"{sideType}面{closedLoopType}算法[{newArithmeticName}]保存数据库失败!");
                settingModel.ArithmeticName = oldArithmeticName;
                return;
            }
        }

        private async Task<bool> RefreshArithmeticParamrersAsync(string paramName, ArithmeticModel value)
        {
            return await RefreshJsonParamrersAsync(paramName, value);
        }

        private void InitializeArithmeticProjectSettings()
        {
            var model = new ArithmeticModel();
            model.IsEnable = false;
            model.ArithmeticName = "DY-MD-L1-1-N-S-314Ah";

            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
            var sideTypes = Enum.GetValues(typeof(ESideType));
            foreach (EClosedLoopType closedLoopType in closedLoopTypes)
            {
                foreach (ESideType sideType in sideTypes)
                {
                    var projectSettingName = $"ArithmeticSetting_{closedLoopType}{sideType}";
                    if (!_projectSettingRepository.Exists(model => model.Name == projectSettingName && model.BelongToVM == CCTBCore.BelongToVM))
                    {
                        _projectSettingRepository.Add(
                            new ProjectSettingModel(projectSettingName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                            {
                                Remark = "涂布闭环算法设置"
                            }
                        );
                    }
                }
            }
        }

        private async Task LoadArithmeticModelAsync()
        {
            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
            var sideTypes = Enum.GetValues(typeof(ESideType));
            foreach (EClosedLoopType closedLoopType in closedLoopTypes)
            {
                foreach (ESideType sideType in sideTypes)
                {
                    var projectSettingName = $"ArithmeticSetting_{closedLoopType}{sideType}";
                    var projectSetting = _projectSettingRepository.GetByNameAndVM(projectSettingName, CCTBCore.BelongToVM)?.Value ?? "";
                    var settingModel = JsonConvert.DeserializeObject<ArithmeticModel>(projectSetting);
                    if (settingModel == null) continue;

                    var arithmeticModel = (closedLoopType, sideType) switch
                    {
                        (EClosedLoopType.MD, ESideType.A) => ArithmeticModel_MDA,
                        (EClosedLoopType.TD, ESideType.A) => ArithmeticModel_TDA,
                        (EClosedLoopType.MDTD, ESideType.A) => ArithmeticModel_MDTDA,
                        (EClosedLoopType.MD, ESideType.B) => ArithmeticModel_MDB,
                        (EClosedLoopType.TD, ESideType.B) => ArithmeticModel_TDB,
                        (EClosedLoopType.MDTD, ESideType.B) => ArithmeticModel_MDTDB,
                        _ => throw new ArgumentException($"未找到匹配的组合: {closedLoopType}, {sideType}")
                    };
                    arithmeticModel.IsEnable = settingModel.IsEnable;
                    arithmeticModel.ArithmeticName = settingModel.ArithmeticName;
                }
            }
        }
        #endregion

        #region 闭环参数设置
        public ICommand? ParamSettingClickCommand => new RelayCommand(ParamSettingClick);

        private async void ParamSettingClick(object? arg)
        {
            if (arg == null) return;
            if (!(arg is ArithmeticSettingControl page)) return;
            var closedLoopType = page.ClosedLoopType;
            var sideType = page.SideType;
            var projectSettingName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            var settingModel = LoadClosedLoopSettingModel(closedLoopType, sideType);
            if (settingModel == null) return;

            var result = await Dialog
                .Show(new VM_ClosedLoopSettingDialog().UIElement)
                .Initialize<VM_ClosedLoopSettingDialog>(o =>
                {
                    o.SideType = sideType;
                    o.ClosedLoopType = closedLoopType;
                    o.ClosedLoopSettingModel = settingModel;
                })
                .GetResultAsync<bool>();

            if (result)// 保存数据库
            {
                var re = await RefreshClosedLoopParamrersAsync(projectSettingName, settingModel);
            }
        }

        private async Task<bool> RefreshClosedLoopParamrersAsync(string paramName, ClosedLoopSettingModel value)
        {
            return await RefreshJsonParamrersAsync(paramName, value);
        }

        private void InitializeClosedLoopProjectSettings()
        {
            var model = new ClosedLoopSettingModel();

            var closedLoopTypes = Enum.GetValues(typeof(EClosedLoopType));
            var sideTypes = Enum.GetValues(typeof(ESideType));
            foreach (EClosedLoopType closedLoopType in closedLoopTypes)
            {
                foreach (ESideType sideType in sideTypes)
                {
                    var projectSettingName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
                    if (!_projectSettingRepository.Exists(model => model.Name == projectSettingName && model.BelongToVM == CCTBCore.BelongToVM))
                    {
                        _projectSettingRepository.Add(
                            new ProjectSettingModel(projectSettingName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                            {
                                Remark = "涂布闭环参数设置"
                            }
                        );
                    }
                }
            }
        }

        private ClosedLoopSettingModel? LoadClosedLoopSettingModel(EClosedLoopType closedLoopType, ESideType sideType)
        {
            var projectSettingName = $"ClosedLoopSetting_{closedLoopType}{sideType}";
            var projectSetting = _projectSettingRepository.GetByNameAndVM(projectSettingName, CCTBCore.BelongToVM)?.Value ?? "";
            var settingModel = JsonConvert.DeserializeObject<ClosedLoopSettingModel>(projectSetting);
            return settingModel;
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

        public ICommand? ParamSaveCommand => new RelayCommand(ParamSave);
        public ICommand? PartitionedSelectionChangedCommand => new RelayCommand(PartitionedSelectionChanged);

        private GeneralSettingsModel generalSettingsModel = new();

        public GeneralSettingsModel GeneralSettingsModel { get => generalSettingsModel; set => SetProperty(ref generalSettingsModel, value); }

        public ObservableCollection<PartitionedModel> PartitionedModels { get; set; } = [];

        private void ParamSave()
        {
            _ = RefreshGeneralParamrersAsync();
        }

        private void PartitionedSelectionChanged(object? arg)
        {
            if (!(arg is ComboboxItemModel<EPartitionedType> item)) return;

            var partitionedModels = PartitionedModel.GetPartitionedModels(GeneralSettingsModel.PartitionedType, GeneralSettingsModel.PartitionedModels);
            PartitionedModels.Clear();
            PartitionedModels.AddRange(partitionedModels);
        }

        private async Task<bool> RefreshGeneralParamrersAsync()
        {
            var projectSettingName = "GeneralSettings";
            GeneralSettingsModel.PartitionedModels.Clear();
            GeneralSettingsModel.PartitionedModels.AddRange(PartitionedModels);
            return await RefreshJsonParamrersAsync(projectSettingName, GeneralSettingsModel);
        }

        private void InitializeGeneralProjectSettings()
        {
            var model = new GeneralSettingsModel();
            model.PartitionedType = EPartitionedType.一分膜区;
            model.PartitionedModels.AddRange(PartitionedModel.GetPartitionedModels(model.PartitionedType));

            var projectSettingName = "GeneralSettings";
            if (!_projectSettingRepository.Exists(model => model.Name == projectSettingName && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(projectSettingName, CCTBCore.BelongToVM, JsonConvert.SerializeObject(model), "string", 2)
                    {
                        Remark = "涂布闭环通用设置"
                    }
                );
            }
        }

        private async Task LoadGeneralSettingsModelAsync()
        {
            var projectSettingName = "GeneralSettings";
            var projectSetting = _projectSettingRepository.GetByNameAndVM(projectSettingName, CCTBCore.BelongToVM)?.Value ?? "";
            var settingModel = JsonConvert.DeserializeObject<GeneralSettingsModel>(projectSetting);
            if (settingModel == null) return;

            GeneralSettingsModel = settingModel;
            var partitionedModels = PartitionedModel.GetPartitionedModels(settingModel.PartitionedType, settingModel.PartitionedModels);
            PartitionedModels.Clear();
            PartitionedModels.AddRange(partitionedModels);
        }
        #endregion

        /// <summary>
        /// 保存到数据库
        /// </summary>
        /// <returns></returns>
        private async Task<bool> RefreshJsonParamrersAsync(string paramName, object value)
        {
            var paramrer = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramrer == null) return false;
            paramrer.Value = JsonConvert.SerializeObject(value);
            return await _projectSettingRepository.UpdateAsync(paramrer);
        }

        #region
        public class ComboboxItemModel<T> : ModelBase where T : Enum
        {
            private T? itemValue;
            private string? displayName;

            public T? ItemValue { get => itemValue; set => SetProperty(ref itemValue, value); }
            public string? DisplayName { get => displayName; set => SetProperty(ref displayName, value); }
        }
        #endregion
    }
}
