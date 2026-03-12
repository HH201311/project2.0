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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
            base.OnInitialize();
        }

        protected override async Task OnInitializeAsync()
        {
            await InitializeArithmeticModelAsync();
            await base.OnInitializeAsync();
        }

        protected override async Task OnNavigatedToAsync(object? parameter)
        {
            await RunOnUIThreadAsync(async () => await InitializeArithmeticModelAsync());
            //await InitializeArithmeticModelAsync();
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
        public ICommand? ParamSettingClickCommand => new RelayCommand(ParamSettingClick);

        private void ArithmeticEnableChanged(object? arg)
        {
            if (!(arg is object[] args && args?.Length == 4)) return;
            if (!(args[0] is ESideType sideType)) return;
            if (!(args[1] is EClosedLoopType closedLoopType)) return;
            if (!(args[2] is bool isCheck)) return;
            if (!(args[3] is Action<bool> action)) return;

            // -TODO  算法校验

            // 若校验成功则执行更新(界面+数据库)
            if (true)
            {
                var paramName = $"ArithmeticEnable_{closedLoopType}{sideType}";
                _ = RefreshParamrersAsync(paramName, isCheck);
                action.Invoke(isCheck);
            }
        }

        private void ArithmeticVerify(object? arg)
        {
            if (!(arg is object[] args && args?.Length == 4)) return;
            if (!(args[0] is ESideType sideType)) return;
            if (!(args[1] is EClosedLoopType closedLoopType)) return;
            if (!(args[2] is string arithmeticName)) return;
            if (!(args[3] is Action<string> action)) return;

            // 校验算法
            if (false)// 校验失败则弹窗提示
            {
                MessageBox.Show($"{sideType}面{closedLoopType}算法[{arithmeticName}]校验失败:[]!");
                return;
            }

            var paramName = $"ArithmeticName_{closedLoopType}{sideType}";
            _ = RefreshParamrersAsync(paramName, arithmeticName);

            // 若校验成功则执行更新(界面+数据库)
            action.Invoke(arithmeticName);
        }

        private void ParamSettingClick(object? arg)
        {
            if (arg == null) return;
            if (!(arg is ArithmeticSettingControl page)) return;

        }

        /// <summary>
        /// 保存到数据库
        /// </summary>
        /// <returns></returns>
        private async Task RefreshParamrersAsync(string paramName, object value)
        {
            var paramrer = _projectSettingRepository.GetByNameAndVM(paramName, CCTBCore.BelongToVM);
            if (paramrer == null) return;
            paramrer.Value = value?.ToString() ?? "";
            await _projectSettingRepository.UpdateAsync(paramrer);
        }

        private void InitializeArithmeticProjectSettings()
        {
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_MDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_MDA", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_TDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_TDA", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_MDTDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_MDTDA", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_MDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_MDB", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_TDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_TDB", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_MDTDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticName_MDTDB", CCTBCore.BelongToVM, "", "string", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }

            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_MDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_MDA", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_TDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_TDA", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_MDTDA" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_MDTDA", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_MDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_MDB", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_TDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_TDB", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
            if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticEnable_MDTDB" && model.BelongToVM == CCTBCore.BelongToVM))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel("ArithmeticEnable_MDTDB", CCTBCore.BelongToVM, "false", "bool", 2)
                    {
                        Remark = "涂布闭环算法设置"
                    }
                );
            }
        }

        private async Task InitializeArithmeticModelAsync()
        {
            ArithmeticModel_MDA.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_MDA", CCTBCore.BelongToVM)?.Value ?? "";
            ArithmeticModel_TDA.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_TDA", CCTBCore.BelongToVM)?.Value ?? "";
            ArithmeticModel_MDTDA.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_MDTDA", CCTBCore.BelongToVM)?.Value ?? "";
            ArithmeticModel_MDB.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_MDB", CCTBCore.BelongToVM)?.Value ?? "";
            ArithmeticModel_TDB.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_TDB", CCTBCore.BelongToVM)?.Value ?? "";
            ArithmeticModel_MDTDB.ArithmeticName = _projectSettingRepository.GetByNameAndVM("ArithmeticName_MDTDB", CCTBCore.BelongToVM)?.Value ?? "";
            var arithmeticEnable = false;
            ArithmeticModel_MDA.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_MDA", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
            ArithmeticModel_TDA.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_TDA", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
            ArithmeticModel_MDTDA.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_MDTDA", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
            ArithmeticModel_MDB.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_MDB", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
            ArithmeticModel_TDB.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_TDB", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
            ArithmeticModel_MDTDB.IsEnable = bool.TryParse(_projectSettingRepository.GetByNameAndVM("ArithmeticEnable_MDTDB", CCTBCore.BelongToVM)?.Value ?? "", out arithmeticEnable) ? arithmeticEnable : false;
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

        private void ParamSave()
        {

        }

        private GeneralSettingsModel generalSettingsModel = new();
        public GeneralSettingsModel GeneralSettingsModel { get => generalSettingsModel; set => SetProperty(ref generalSettingsModel, value); }

        private void InitializeGeneralProjectSettings()
        {
            //if (!_projectSettingRepository.Exists(model => model.Name == "ArithmeticName_MDA" && model.BelongToVM == CCTBCore.BelongToVM))
            //{
            //    _projectSettingRepository.Add(
            //        new ProjectSettingModel("ArithmeticName_MDA", CCTBCore.BelongToVM, "", "string", 2)
            //        {
            //            Remark = "涂布闭环通用设置"
            //        }
            //    );
            //}
        }


        #endregion

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
