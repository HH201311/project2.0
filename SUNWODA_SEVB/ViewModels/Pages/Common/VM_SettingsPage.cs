using HandyControl.Controls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Pages.Common
{
    [Module("SettingsPage", "设置", Type = ModuleType.Settings)]
    public class VM_SettingsPage : ViewModelBase
    {
        private readonly ILoggerService<VM_SettingsPage> _logger;
        private readonly IWorkSpaceProjectRepository _workSpaceProjectRepository;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly AssemblyBuilder _assemblyBuilder;
        private readonly ModuleBuilder _moduleBuilder;
        private object? _globalSettingsExtraModelObject;
        private List<GlobalSettingModel>? _globalSettings;
        private List<ProjectSettingModel>? _projectSettings;
        private object? _projectSettingsExtraModelObject;
        private string? _selectedVM;
        private bool isSettingsLoading = true;

        public ObservableCollection<string> VMNames { get; set; } =
            new ObservableCollection<string>();
        public string? SelectedVM
        {
            get => _selectedVM;
            set
            {
                if (HasUnsavedProjectChanges())
                {
                    var result = HandyControl.Controls.MessageBox.Show(
                        $"{_selectedVM} 项目设置有未保存的更改，是否保存？",
                        "确认保存",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        SaveProjectAsync();
                    }
                }
                SetProperty(ref _selectedVM, value);
                _ = LoadProjectSettingsAsync();
            }
        }

        public bool IsSettingsLoading
        {
            get => isSettingsLoading;
            set => SetProperty(ref isSettingsLoading, value);
        }

        public object? GlobalSettingsExtraModelObject
        {
            get => _globalSettingsExtraModelObject;
            set => SetProperty(ref _globalSettingsExtraModelObject, value);
        }

        public object? ProjectSettingsExtraModelObject
        {
            get => _projectSettingsExtraModelObject;
            set => SetProperty(ref _projectSettingsExtraModelObject, value);
        }

        public ICommand? SaveGlobalCommand { get; }
        public ICommand? SaveProjectCommand { get; }

        public VM_SettingsPage(
            ILoggerService<VM_SettingsPage> logger,
            IWorkSpaceProjectRepository workSpaceProjectRepository,
            IGlobalSettingRepository globalSettingRepository,
            IProjectSettingRepository projectSettingRepository,
            IUsersRepository usersRepository
        )
        {
            _logger = logger;
            _workSpaceProjectRepository = workSpaceProjectRepository;
            _globalSettingRepository = globalSettingRepository;
            _projectSettingRepository = projectSettingRepository;
            _usersRepository = usersRepository;

            SaveGlobalCommand = new RelayCommand(SaveGlobalAsync);
            SaveProjectCommand = new RelayCommand(SaveProjectAsync);

            var assemblyName = new AssemblyName("DynamicSettingAssembly");
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                assemblyName,
                AssemblyBuilderAccess.Run
            );
            _moduleBuilder = _assemblyBuilder.DefineDynamicModule("DynamicExtraModel");
        }

        public override async void OnInitialize()
        {
            var enabledProjects = (await _workSpaceProjectRepository.GetAllAsync()).FindAll(model =>
                model.IsEnabled
            );
            foreach (var enabledProject in enabledProjects)
            {
                VMNames.Add(enabledProject.VMClassName);
            }
            var defaultProject = await _globalSettingRepository.GetSettingValueAsync(
                "DefaultProject"
            );
            if (VMNames.Contains(defaultProject))
            {
                _selectedVM = defaultProject;
            }
            else
            {
                _selectedVM = VMNames.FirstOrDefault();
            }
            OnPropertyChanged(nameof(SelectedVM));
            await InitializeSettingsAsync();
            base.OnInitialize();
        }

        public override async void OnNavigatedFrom()
        {
            await HandleUnsavedChangesAsync();
            ClearAllSettings();
            IsSettingsLoading = true;
            GC.Collect(2, GCCollectionMode.Forced);
            GC.WaitForPendingFinalizers();
            base.OnNavigatedFrom();
        }

        public override async void OnNavigatedTo(object? parameter)
        {
            await LoadGlobalSettingsAsync();
            await LoadProjectSettingsAsync();
            base.OnNavigatedTo(parameter);
            await Task.Delay(500);
            IsSettingsLoading = false;
        }

        private async Task HandleUnsavedChangesAsync()
        {
            var hasUnsavedGlobalChanges = HasUnsavedGlobalChanges();
            var hasUnsavedProjectChanges = HasUnsavedProjectChanges();
            if (hasUnsavedGlobalChanges || hasUnsavedProjectChanges)
            {
                var changeDetails = BuildChangeDetails(
                    hasUnsavedGlobalChanges,
                    hasUnsavedProjectChanges
                );
                var result = HandyControl.Controls.MessageBox.Show(
                    $"检测到以下未保存的更改：\n{changeDetails}\n\n是否保存？",
                    "确认保存",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );
                if (result == MessageBoxResult.Yes)
                {
                    var savedItems = new List<string>();
                    var errors = new List<string>();
                    if (hasUnsavedGlobalChanges)
                    {
                        try
                        {
                            await SaveGlobalSettingsAsync();
                            savedItems.Add("全局设置");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"保存全局设置失败", ex, true);
                            Growl.ErrorGlobal("保存全局设置失败");
                        }
                    }
                    if (hasUnsavedProjectChanges)
                    {
                        try
                        {
                            await SaveProjectSettingsAsync();
                            savedItems.Add($"{_selectedVM} 项目设置");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"保存 {_selectedVM} 项目设置失败", ex, true);
                            Growl.ErrorGlobal($"保存 {_selectedVM} 项目设置失败");
                        }
                    }
                    ShowSaveResults(savedItems, errors);
                }
            }
        }

        private string BuildChangeDetails(bool hasGlobalChanges, bool hasProjectChanges)
        {
            var details = new List<string>();

            if (hasGlobalChanges)
                details.Add("• 全局设置");

            if (hasProjectChanges)
                details.Add($"• {_selectedVM} 项目设置");

            return string.Join("\n", details);
        }

        private void ShowSaveResults(List<string> savedItems, List<string> errors)
        {
            if (errors.Any())
            {
                var errorMessage = $"以下项目设置保存失败：\n{string.Join("\n", errors)}";

                if (savedItems.Any())
                {
                    errorMessage =
                        $"部分设置保存成功：\n{string.Join("、", savedItems)}\n\n{errorMessage}";
                }

                _logger.Error(errorMessage, true);
                Growl.ErrorGlobal(errorMessage);
            }
            else if (savedItems.Any())
            {
                var successMessage = $"{string.Join("、", savedItems)} 保存成功！";
                Growl.SuccessGlobal(successMessage);
            }
        }

        private void ClearAllSettings()
        {
            _globalSettings?.Clear();
            _projectSettings?.Clear();
            GlobalSettingsExtraModelObject = null;
            ProjectSettingsExtraModelObject = null;
        }

        private void AddAutoProperty(
            TypeBuilder typeBuilder,
            string propertyName,
            Type propertyType,
            string description
        )
        {
            // 创建字段
            var fieldBuilder = typeBuilder.DefineField(
                "_" + propertyName,
                propertyType,
                FieldAttributes.Private
            );

            // 创建属性
            var propertyBuilder = typeBuilder.DefineProperty(
                propertyName,
                PropertyAttributes.HasDefault,
                propertyType,
                null
            );

            // 创建 getter 方法
            var getterMethod = typeBuilder.DefineMethod(
                "get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType,
                Type.EmptyTypes
            );

            var getIL = getterMethod.GetILGenerator();
            getIL.Emit(OpCodes.Ldarg_0);
            getIL.Emit(OpCodes.Ldfld, fieldBuilder);
            getIL.Emit(OpCodes.Ret);

            // 创建 setter 方法
            var setterMethod = typeBuilder.DefineMethod(
                "set_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                new Type[] { propertyType }
            );

            var setIL = setterMethod.GetILGenerator();
            setIL.Emit(OpCodes.Ldarg_0);
            setIL.Emit(OpCodes.Ldarg_1);
            setIL.Emit(OpCodes.Stfld, fieldBuilder);
            setIL.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getterMethod);
            propertyBuilder.SetSetMethod(setterMethod);

            // 添加Description特性
            var attrCtor = typeof(DescriptionAttribute).GetConstructor(new[] { typeof(string) });
            if (attrCtor != null)
            {
                var attrBuilder = new CustomAttributeBuilder(
                    attrCtor,
                    new object[] { description }
                );
                propertyBuilder.SetCustomAttribute(attrBuilder);
            }
        }

        private async Task InitializeSettingsAsync()
        {
            await LoadGlobalSettingsAsync();
            await LoadProjectSettingsAsync();
        }

        private async void SaveGlobalAsync()
        {
            try
            {
                var hasModifySave = await SaveGlobalSettingsAsync();
                if (hasModifySave)
                {
                    // 显示成功消息
                    Growl.SuccessGlobal("全局设置保存成功！");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"全局设置保存失败", ex, true);
                Growl.ErrorGlobal($"全局设置保存失败：{ex.Message}");
            }
        }

        private async void SaveProjectAsync()
        {
            try
            {
                var hasModifySave = await SaveProjectSettingsAsync();
                if (hasModifySave)
                {
                    // 显示成功消息
                    Growl.SuccessGlobal($"{_selectedVM} 项目设置保存成功！");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"{_selectedVM} 项目设置保存失败", ex, true);
                Growl.ErrorGlobal($"{_selectedVM} 项目设置保存失败：{ex.Message}");
            }
        }

        public async Task LoadGlobalSettingsAsync()
        {
            //_globalSettings = await _globalSettingRepository.GetAllAsync();
            var currentUser = (UsersModel)(
                await _usersRepository.GetByUserAccountAsync(
                    _globalSettingRepository.GetSettingValue("CurrentUserAccount")
                )
            );
            _globalSettings = await _globalSettingRepository.GetListAsync(model =>
                model.RoleRank <= currentUser.RoleId
            );
            var golbalSettingsExtraModelType = _moduleBuilder.GetType(
                $"GlobalSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (golbalSettingsExtraModelType == null)
            {
                TypeBuilder typeBuilder = _moduleBuilder.DefineType(
                    $"GlobalSettingsExtraModelByRole{currentUser.RoleId}",
                    TypeAttributes.Public | TypeAttributes.Class
                );

                typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

                // 收集属性信息
                foreach (var setting in _globalSettings)
                {
                    // 使用Remark作为描述，如果有单位则添加到描述中
                    var description = setting.Remark ?? "";
                    if (!string.IsNullOrEmpty(setting.Unit))
                    {
                        description += $" (单位: {setting.Unit})";
                    }
                    AddAutoProperty(typeBuilder, setting.Name, setting.Type.ToType(), description);
                }

                golbalSettingsExtraModelType = typeBuilder.CreateType();
            }

            // 创建实例
            var golbalSettingsExtraModel = Activator.CreateInstance(golbalSettingsExtraModelType);

            // 给动态属性赋值
            foreach (var setting in _globalSettings)
            {
                var prop = golbalSettingsExtraModelType.GetProperty(setting.Name);
                if (prop != null)
                {
                    prop.SetValue(
                        golbalSettingsExtraModel,
                        DataTypeConverter.StringToValue(setting.Type, setting.Value)
                    );
                }
            }
            GlobalSettingsExtraModelObject = golbalSettingsExtraModel;
        }

        public async Task<bool> SaveGlobalSettingsAsync()
        {
            if (_globalSettings == null || GlobalSettingsExtraModelObject == null)
                return false;
            var currentUser = (UsersModel)(
                await _usersRepository.GetByUserAccountAsync(
                    _globalSettingRepository.GetSettingValue("CurrentUserAccount")
                )
            );
            var golbalSettingsExtraModelType = _moduleBuilder.GetType(
                $"GlobalSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (golbalSettingsExtraModelType != null)
            {
                var modifySettings = new List<GlobalSettingModel>();
                foreach (var setting in _globalSettings)
                {
                    var prop = golbalSettingsExtraModelType.GetProperty(setting.Name);
                    if (prop != null)
                    {
                        var newValue = prop.GetValue(GlobalSettingsExtraModelObject);                    
                        if (AreDifferent(setting.Type, setting.Value, newValue))
                        {
                            var newString = NormalizeToString(setting.Type, newValue);
                            _logger.Info($"全局变量 {setting.Name} 修改值 [{setting.Value}] --> [{newString}]");
                            setting.Value = newString;
                            modifySettings.Add(setting);
                        }
                    }
                }
                if (modifySettings.Count != 0)
                {
                    // 批量更新到数据库
                    await _globalSettingRepository.UpdateRangeAsync(modifySettings);
                    return true;
                }
            }
            return false;
        }

        public bool HasUnsavedGlobalChanges()
        {
            if (_globalSettings == null || GlobalSettingsExtraModelObject == null)
                return false;
            var currentUser = _usersRepository.GetByUserAccount(
                _globalSettingRepository.GetSettingValue("CurrentUserAccount")
            );
            var golbalSettingsExtraModelType = _moduleBuilder.GetType(
                $"GlobalSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (golbalSettingsExtraModelType != null)
            {
                var modifySettings = new List<GlobalSettingModel>();
                foreach (var setting in _globalSettings)
                {
                    var prop = golbalSettingsExtraModelType.GetProperty(setting.Name);
                    if (prop != null)
                    {
                        var newValue = prop.GetValue(GlobalSettingsExtraModelObject);
                        if (AreDifferent(setting.Type, setting.Value, newValue))
                        {
                            modifySettings.Add(setting);
                        }
                    }
                }
                if (modifySettings.Count != 0)
                {
                    return true;
                }
            }
            return false;
        }

        public async Task LoadProjectSettingsAsync()
        {
            if (_selectedVM == null)
                return;
            var currentUser = (UsersModel)(
                await _usersRepository.GetByUserAccountAsync(
                    _globalSettingRepository.GetSettingValue("CurrentUserAccount")
                )
            );
            _projectSettings = (await _projectSettingRepository.GetByVMNameAsync(_selectedVM))
                ?.Where(model => model.RoleRank <= currentUser.RoleId)
                .ToList();
            if (_projectSettings == null)
                return;
            var projectSettingsExtraModelType = _moduleBuilder.GetType(
                $"{_selectedVM}ProjectSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (projectSettingsExtraModelType == null)
            {
                TypeBuilder typeBuilder = _moduleBuilder.DefineType(
                    $"{_selectedVM}ProjectSettingsExtraModelByRole{currentUser.RoleId}",
                    TypeAttributes.Public | TypeAttributes.Class
                );

                typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

                // 收集属性信息
                foreach (var setting in _projectSettings)
                {
                    // 使用Remark作为描述，如果有单位则添加到描述中
                    var description = setting.Remark ?? "";
                    if (!string.IsNullOrEmpty(setting.Unit))
                    {
                        description += $" (单位: {setting.Unit})";
                    }
                    AddAutoProperty(typeBuilder, setting.Name, setting.Type.ToType(), description);
                }

                projectSettingsExtraModelType = typeBuilder.CreateType();
            }

            // 创建实例
            var projectSettingsExtraModel = Activator.CreateInstance(projectSettingsExtraModelType);

            // 给动态属性赋值
            foreach (var setting in _projectSettings)
            {
                var prop = projectSettingsExtraModelType.GetProperty(setting.Name);
                if (prop != null)
                {
                    prop.SetValue(
                        projectSettingsExtraModel,
                        DataTypeConverter.StringToValue(setting.Type, setting.Value)
                    );
                }
            }
            ProjectSettingsExtraModelObject = projectSettingsExtraModel;
        }

        public async Task<bool> SaveProjectSettingsAsync()
        {
            if (_selectedVM == null)
                return false;
            if (_projectSettings == null || ProjectSettingsExtraModelObject == null)
                return false;
            var currentUser = (UsersModel)(
                await _usersRepository.GetByUserAccountAsync(
                    _globalSettingRepository.GetSettingValue("CurrentUserAccount")
                )
            );
            var projectSettingsExtraModelType = _moduleBuilder.GetType(
                $"{_selectedVM}ProjectSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (projectSettingsExtraModelType != null)
            {
                var modifySettings = new List<ProjectSettingModel>();
                foreach (var setting in _projectSettings)
                {
                    var prop = projectSettingsExtraModelType.GetProperty(setting.Name);
                    if (prop != null)
                    {
                        var newValue = prop.GetValue(ProjectSettingsExtraModelObject);
                        if (AreDifferent(setting.Type, setting.Value, newValue))
                        {
                            var newString = NormalizeToString(setting.Type, newValue);
                            _logger.Info($"{setting.BelongToVM} 项目变量 {setting.Name} 修改值 [{setting.Value}] --> [{newString}]");
                            setting.Value = newString;
                            modifySettings.Add(setting);
                        }
                    }
                }
                if (modifySettings.Count != 0)
                {
                    // 批量更新到数据库
                    await _projectSettingRepository.UpdateRangeAsync(modifySettings);
                    return true;
                }
            }
            return false;
        }

        public bool HasUnsavedProjectChanges()
        {
            if (_selectedVM == null)
                return false;
            if (_projectSettings == null || ProjectSettingsExtraModelObject == null)
                return false;
            var currentUser = _usersRepository.GetByUserAccount(
                _globalSettingRepository.GetSettingValue("CurrentUserAccount")
            );
            var projectSettingsExtraModelType = _moduleBuilder.GetType(
                $"{_selectedVM}ProjectSettingsExtraModelByRole{currentUser.RoleId}"
            );
            if (projectSettingsExtraModelType != null)
            {
                var modifySettings = new List<ProjectSettingModel>();
                foreach (var setting in _projectSettings)
                {
                    var prop = projectSettingsExtraModelType.GetProperty(setting.Name);
                    if (prop != null)
                    {
                        var newValue = prop.GetValue(ProjectSettingsExtraModelObject);
                        if (AreDifferent(setting.Type, setting.Value, newValue))
                        {
                            modifySettings.Add(setting);
                        }
                    }
                }
                if (modifySettings.Count != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AreDifferent(string typeName, string? oldValue, object? newValue)
        {
            return !DataTypeConverter.AreEqual(typeName, oldValue, newValue);
        }

        private static string NormalizeToString(string typeName, object? value)
        {
            return DataTypeConverter.ValueToString(typeName, value);
        }

    }
}
