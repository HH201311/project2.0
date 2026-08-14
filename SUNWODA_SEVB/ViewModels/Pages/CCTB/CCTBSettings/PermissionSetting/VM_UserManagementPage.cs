using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Masuit.Tools;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Events;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB;
using SUNWODA_SEVB.ViewModels.Dialogs.CCTB.UserOperation;
using SUNWODA_SEVB.ViewModels.Dialogs.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace SUNWODA_SEVB.ViewModels.Pages.CCTB.CCTBSettings.PermissionSetting
{
    [Module("VM_UserManagementPage", "用户管理", Order = 3, Category = "VM_CCTBSettingsPage/VM_PermissionSettingPage/VM_UserManagementPage")]
    class VM_UserManagementPage : ViewModelBase
    {
        private readonly ILoggerService<VM_UserManagementPage> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IWebApiClient _webApiClient;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IModuleManager _moduleManager;

        public ICommand? UserAddCommand => new RelayCommand(OnUserAddAsync);
        public ICommand? UserDeleteInBatchesCommand => new RelayCommand(OnUserDeleteInBatchesAsync);
        public ICommand? UserSelectAllCommand => new RelayCommand(OnUserSelectAllAsync);
        public ICommand? UserResetPasswordCommand => new RelayCommand(OnUserResetPasswordAsync);
        public ICommand? UserModifyCommand => new RelayCommand(OnUserModifyAsync);
        public ICommand? UserDeleteCommand => new RelayCommand(OnUserDeleteAsync);

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_UserManagementPage(
            ILoggerService<VM_UserManagementPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository,
            IPLCConfigRepository plcConfigRepository,
            IWebApiClient webApiClient,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;
            _plcConfigRepository = plcConfigRepository;
            _webApiClient = webApiClient;
            _projectSettingRepository = projectSettingRepository;
            _moduleManager = moduleManager;
        }

        /// <summary>
        /// 导航结束时触发
        /// </summary>
        /// <param name="parameter"></param>
        public override void OnNavigatedTo(object? parameter)
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            UserStateChangedEvent.OnUserStateChanged += OnUserStateChanged;
            LoadRoleAccounts();
            base.OnNavigatedTo(parameter);
        }

        /// <summary>
        /// 导航离开前
        /// </summary>
        public override void OnNavigatedFrom()
        {
            UserStateChangedEvent.OnUserStateChanged -= OnUserStateChanged;
            base.OnNavigatedFrom();
        }

        /// <summary>
        /// 更新用户显示信息
        /// </summary>
        /// <param name="userAccount"></param>
        private void OnUserStateChanged(string userAccount)
        {
            LoadRoleAccounts(userAccount);
        }

        /// <summary>
        /// 当新增用户时触发
        /// </summary>
        private async void OnUserAddAsync()
        {
            _logger.UserInfo($"用户点击了 用户管理/新增用户 按钮!");
            // 获取当前用户角色
            var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;
            var userModel = new UserManagementModel();
            await Dialog
            .Show(new VM_UserAddDialog().UIElement)
            .Initialize<VM_UserAddDialog>(vm => vm.CurrentUserRoleID = roleId)
            .GetResultAsync<UserManagementModel?>()
            .ContinueWith(um => userModel = um.Result);

            if (userModel != null)
            {
                if ((await _usersRepository.GetByUserAccountAsync(userModel.Account!)) == null)
                {
                    var isAddSuccess = await _usersRepository.AddAsync(
                        new UsersModel()
                        {
                            UserAccount = userModel.Account!,
                            UserName = userModel.Name!,
                            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                            RoleId = CCTBCore.StringToRoleID(userModel.Role!),
                            CreatedTime = userModel.CreatedDateTime,
                        }
                    );
                    if (isAddSuccess)
                    {
                        _logger.UserInfo($"用户新增了 {userModel.Role} 账号 [{userModel.Name}:{userModel.Account}]！");
                        Growl.SuccessGlobal($"用户新增了 {userModel.Role} 账号 [{userModel.Name}:{userModel.Account}]！");
                        LoadRoleAccounts();
                    }
                    else
                    {
                        _logger.Warn("添加用户失败！", true);
                        Growl.WarningGlobal("添加用户失败！");
                    }
                }
                else
                {
                    _logger.Warn($"{userModel.Account} 账号已存在！", true);
                    Growl.WarningGlobal($"{userModel.Account} 账号已存在！");
                }
            }
        }

        /// <summary>
        /// 当批量删除时触发
        /// </summary>
        private async void OnUserDeleteInBatchesAsync()
        {
            _logger.UserInfo($"用户点击了 用户管理/批量删除 按钮!");
            var selectedUsers = UserAccounts.Where(u => u.IsChecked).ToList();
            if (selectedUsers == null || selectedUsers.Count <= 0)
            {
                await VM_CCTBMessageBoxDialog.ShowAsync($"请勾选需要删除的用户！", "删除提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = await VM_CCTBMessageBoxDialog.ShowAsync($"删除 {selectedUsers.Count} 个用户?", "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (result != MessageBoxResult.OK) return;

            int successCount = 0, failCount = 0;
            foreach (var selectedUser in selectedUsers)
            {
                var user = await _usersRepository.GetByUserAccountAsync(selectedUser.Account!);
                if (user != null)
                {
                    if (await _usersRepository.DeleteAsync(user))
                    {
                        successCount++;
                        continue;
                    }
                }
                failCount++;
            }
            LoadRoleAccounts();
            _logger.UserInfo($"用户执行了用户账号的批量删除操作:[{successCount} 个账号删除成功！{failCount} 个账号删除失败]", true);
            Growl.WarningGlobal($"{successCount} 个账号删除成功！{failCount} 个账号删除失败！");
        }

        /// <summary>
        /// 当全选时触发
        /// </summary>
        private async void OnUserSelectAllAsync()
        {
            if (UserAccounts.Count <= 0) return;
            var isSelectAll = UserAccounts.All(o => o.IsChecked);
            var result = isSelectAll ?
                await VM_CCTBMessageBoxDialog.ShowAsync($"是否取消全选？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) :
                await VM_CCTBMessageBoxDialog.ShowAsync($"是否全选？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            UserAccounts.ForEach(o => o.IsChecked = !isSelectAll);
        }

        /// <summary>
        /// 当重置密码时触发
        /// </summary>
        private async void OnUserResetPasswordAsync(object? arg)
        {
            if (!(arg is UserManagementModel selectedUser)) return;
            if (selectedUser == null) return;

            _logger.UserInfo($"用户点击了用户[账号:{selectedUser.Account}] 重置密码 按钮!");
            // 获取当前用户角色
            var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;

            var newPassword = await Dialog
                    .Show(new VM_UserResetPasswordDialog().UIElement)
                    .Initialize<VM_UserResetPasswordDialog>(vm =>
                    {
                        vm.UserModel = selectedUser;
                    })
                    .GetResultAsync<string?>();

            if (string.IsNullOrEmpty(newPassword)) return;

            var user = await _usersRepository.GetByUserAccountAsync(selectedUser.Account!);
            if (user != null)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
                if (await _usersRepository.UpdateAsync(user))
                {
                    _logger.UserInfo($"用户重置了账号[{selectedUser.Account}]密码！");
                    Growl.SuccessGlobal($"用户重置了账号[{selectedUser.Account}]密码！");
                    LoadRoleAccounts();
                    return;
                }
                else
                {
                    _logger.Warn($"重置 {selectedUser?.Account} 账号密码失败！", true);
                    Growl.WarningGlobal($"重置 {selectedUser?.Account} 账号密码失败！");
                }
            }
            else
            {
                _logger.Warn($"{selectedUser?.Account} 账号未找到！", true);
                Growl.WarningGlobal($"{selectedUser?.Account} 账号未找到！");
            }
        }

        /// <summary>
        /// 当用户信息修改时触发
        /// </summary>
        private async void OnUserModifyAsync(object? arg)
        {
            if (!(arg is UserManagementModel selectedUser)) return;
            if (selectedUser == null) return;

            _logger.UserInfo($"用户点击了用户[账号:{selectedUser.Account}] 修改 按钮!");
            // 获取当前用户角色
            var userAccount = _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var roleId = _usersRepository.GetByUserAccount(userAccount)?.RoleId ?? 0;

            var userModel = await Dialog
                    .Show(new VM_UserModifyDialog().UIElement)
                    .Initialize<VM_UserModifyDialog>(vm =>
                    {
                        vm.CurrentUserRoleID = roleId;
                        vm.Result!.Account = selectedUser.Account;
                        vm.Result!.Name = selectedUser.Name;
                        vm.Result.Role = selectedUser.Role;
                    })
                    .GetResultAsync<UserManagementModel?>();

            if (userModel == null) return;

            var user = await _usersRepository.GetByUserAccountAsync(selectedUser.Account!);
            if (user != null)
            {
                user.UserName = userModel.Name!;
                user.RoleId = CCTBCore.StringToRoleID(userModel.Role!);
                if (await _usersRepository.UpdateAsync(user))
                {
                    _logger.UserInfo($"用户修改了账号[{selectedUser.Account}]信息 [姓名:{selectedUser.Name} => {userModel.Name},权限:{selectedUser.Role} => {userModel.Role}]！");
                    Growl.SuccessGlobal($"用户修改了账号[{selectedUser.Account}]信息 [姓名:{selectedUser.Name} => {userModel.Name},权限:{selectedUser.Role} => {userModel.Role}]！");
                    LoadRoleAccounts();
                    return;
                }
                else
                {
                    _logger.Warn($"修改 {selectedUser?.Account} 账号信息失败！", true);
                    Growl.WarningGlobal($"修改 {selectedUser?.Account} 账号信息失败！");
                }
            }
            else
            {
                _logger.Warn($"{selectedUser?.Account} 账号未找到！", true);
                Growl.WarningGlobal($"{selectedUser?.Account} 账号未找到！");
            }
        }

        /// <summary>
        /// 当用户信息删除时触发
        /// </summary>
        private async void OnUserDeleteAsync(object? arg)
        {
            if (!(arg is UserManagementModel selectedUser)) return;
            if (selectedUser == null) return;

            _logger.UserInfo($"用户点击了用户[账号:{selectedUser.Account}] 删除 按钮!");
            var result = await VM_CCTBMessageBoxDialog.ShowAsync($"删除用户'{selectedUser.Name}({selectedUser.Account})'?", "确认删除", MessageBoxButton.OKCancel, MessageBoxImage.Question);

            if (result != MessageBoxResult.OK) return;

            var user = await _usersRepository.GetByUserAccountAsync(selectedUser.Account!);
            if (user != null)
            {
                if (await _usersRepository.DeleteAsync(user))
                {
                    _logger.UserInfo($"用户删除了用户账号{selectedUser.Account}！", true);
                    Growl.WarningGlobal($"用户删除了用户账号{selectedUser.Account}！");
                    LoadRoleAccounts();
                    return;
                }
            }
            _logger.Warn($"{selectedUser?.Account} 账号删除失败！", true);
            Growl.WarningGlobal($"{selectedUser?.Account} 账号删除失败！");
        }

        #region 用户账号管理表格
        /// <summary>
        /// 用户角色枚举数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<UserRole>> UserRoleSource { get; private set; } =
                 new ObservableCollection<ComboboxItemModel<UserRole>>(
                 Enum.GetValues(typeof(UserRole)).Cast<UserRole>()
                 .Select(v => new ComboboxItemModel<UserRole>() { ItemValue = v, DisplayName = CCTBCore.RoleIDToString((int)v) }));

        private UserRole userRole = UserRole.Guest;

        /// <summary>
        /// 角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        public ObservableCollection<UserAccountModel> UserAccounts { get; set; } = [];

        /// <summary>
        /// 加载用户账号信息
        /// </summary>
        private void LoadRoleAccounts(string? userAccount = null)
        {
            // 获取当前用户角色
            var account = userAccount ?? _globalSettingRepository.GetByName("CurrentUserAccount")?.Value ?? string.Empty;
            var roleId = _usersRepository.GetByUserAccount(account)?.RoleId ?? 0;

            UserRole = (UserRole)roleId;

            // 加载用户账号信息，过滤掉角色等级高于或等于当前用户的账号，以及系统预设账号
            UserAccounts.Clear();
            _usersRepository.GetList(model =>
               model.RoleId < roleId
               && model.UserAccount != "guest"
               && model.UserAccount != "engineer"
               && model.UserAccount != "admin"
               && model.UserAccount != "sadmin"
               && model.UserAccount != userAccount)?.ForEach(item =>
             UserAccounts.Add(new UserAccountModel()
             {
                 Account = item.UserAccount,
                 Name = item.UserName,
                 UserRole = (UserRole)item.RoleId,
                 Role = CCTBCore.RoleIDToString(item.RoleId),
                 CreatedDateTime = item.CreatedTime,
                 LastLoginDateTime = item.LastLoginTime,
             }));

            // 加载用户角色枚举数据源
            UserRoleSource.Clear();
            UserAccounts.ForEach(item =>
            UserRoleSource.Add(new ComboboxItemModel<UserRole>()
            {
                ItemValue = item.UserRole,
                DisplayName = item.Role,
            }));
        }
        #endregion
    }
}
