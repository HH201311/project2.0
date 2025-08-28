using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.ViewModels.Dialogs.Common;

namespace SUNWODA_SEVB.ViewModels.Pages.Common
{
    [Module("UserCenter", "用户", Type = ModuleType.UserCenter)]
    public class VM_UserCenter : ViewModelBase
    {
        private readonly ILoggerService<VM_UserCenter> _logger;
        private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly IUsersRepository _usersRepository;
        private bool _isLoggedIn;
        private string? _userAccount;
        private string _password = "";
        private string? _userName;
        private string? _userLogoText;
        private string? _roleDisplay;
        private Brush? _roleBrush;
        private DateTime? _createdTime;
        private DateTime? _lastLoginTime;
        private bool _canManageUsers;
        private bool _isSuperAdmin;
        private int _roleID;
        private UserManagementModel? _selectedUser;
        private bool _selectInfoTab;
        private bool _showPassword;

        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set => SetProperty(ref _isLoggedIn, value);
        }

        public string? UserAccount
        {
            get => _userAccount;
            set => SetProperty(ref _userAccount, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string? UserName
        {
            get => _userName;
            set
            {
                UserLogoText = value?.Substring(0, 1);
                SetProperty(ref _userName, value);
            }
        }

        public string? UserLogoText
        {
            get => _userLogoText;
            set => SetProperty(ref _userLogoText, value);
        }

        public string? RoleDisplay
        {
            get => _roleDisplay;
            set => SetProperty(ref _roleDisplay, value);
        }

        public Brush? RoleBrush
        {
            get => _roleBrush;
            set => SetProperty(ref _roleBrush, value);
        }

        public DateTime? CreatedTime
        {
            get => _createdTime;
            set => SetProperty(ref _createdTime, value);
        }

        public DateTime? LastLoginTime
        {
            get => _lastLoginTime;
            set => SetProperty(ref _lastLoginTime, value);
        }

        public bool CanManageUsers
        {
            get => _canManageUsers;
            set => SetProperty(ref _canManageUsers, value);
        }
        public bool IsSuperAdmin
        {
            get => _isSuperAdmin;
            set => SetProperty(ref _isSuperAdmin, value);
        }

        public int RoleID
        {
            get => _roleID;
            set => SetProperty(ref _roleID, value);
        }

        public UserManagementModel? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        public bool SelectInfoTab
        {
            get => _selectInfoTab;
            set => SetProperty(ref _selectInfoTab, value);
        }

        public bool ShowPassword
        {
            get => _showPassword;
            set => SetProperty(ref _showPassword, value);
        }

        public ObservableCollection<UserManagementModel> UserList { get; set; } =
            new ObservableCollection<UserManagementModel>();

        public ObservableCollection<string> CurrentPermissions { get; set; } =
            new ObservableCollection<string>();

        public ObservableCollection<FunctionPermission> PermissionMatrix { get; set; } =
            new ObservableCollection<FunctionPermission>();

        public UserManagementModel? AddUserModel { get; set; }

        public UserManagementModel? EditUserModel { get; set; }

        public ICommand? LoginCommand { get; }
        public ICommand? GuestModeCommand { get; }
        public ICommand? LogoutCommand { get; }
        public ICommand? AddUserCommand { get; }
        public ICommand? RefreshUsersCommand { get; }
        public ICommand? ResetPasswordCommand { get; }
        public ICommand? DeleteUserCommand { get; }
        public ICommand? EditUserCommand { get; }
        public ICommand? ChangePasswordCommand { get; }

        public VM_UserCenter(
            ILoggerService<VM_UserCenter> logger,
            IGlobalSettingRepository globalSettingRepository,
            IUsersRepository usersRepository
        )
        {
            _logger = logger;
            _globalSettingRepository = globalSettingRepository;
            _usersRepository = usersRepository;

            LoginCommand = new RelayCommand(CommonLogin);
            GuestModeCommand = new RelayCommand(GuestLogin);
            LogoutCommand = new RelayCommand(Logout);
            AddUserCommand = new RelayCommand(AddUser);
            RefreshUsersCommand = new RelayCommand(RefreshUsersAsync);
            ResetPasswordCommand = new RelayCommand(ResetSelectedUserPasswordAsync);
            DeleteUserCommand = new RelayCommand(DeleteSelectedUserAsync);
            EditUserCommand = new RelayCommand(EditSelectedUser);
            ChangePasswordCommand = new RelayCommand(ChangeCurrentUserPassword);
        }

        private void CommonLogin()
        {
            LoginHandlingAsync(UserAccount, Password);
        }

        private void GuestLogin()
        {
            UserAccount = "guest";
            Password = "";
            LoginHandlingAsync(UserAccount, Password);
        }

        private async void LoginHandlingAsync(string? userAccount, string? password)
        {
            (IsLoggedIn, UsersModel? user) = await LoginAsync(userAccount, password);
            if (IsLoggedIn)
            {
                UserName = user!.UserName;
                RoleID = user.RoleId;
                RoleDisplay = RoleIDToString(user.RoleId);
                RoleBrush = GetBrushByRoleID(user.RoleId);
                CanManageUsers = CanManageUsersByRoleID(user.RoleId);
                IsSuperAdmin = IsSuperAdminByRoleID(user.RoleId);
                CreatedTime = user.CreatedTime;
                LastLoginTime = user.LastLoginTime = DateTime.Now;
                SelectInfoTab = true;
                ShowPassword = false;
                RefreshUsersAsync();
                await _usersRepository.UpdateAsync(user);
                await _globalSettingRepository.UpdateSettingValueAsync(
                    "CurrentUserAccount",
                    user.UserAccount
                );
                _logger.Info($"{UserAccount} 在 {LastLoginTime:yyyy-MM-dd HH:mm:ss} 登录成功");
                Growl.SuccessGlobal("登录成功");
            }
            else
            {
                _logger.Warn($"账号: {UserAccount}, 账号或密码错误", true);
                Growl.WarningGlobal("账号或密码错误");
            }
        }

        private async Task<(bool, UsersModel?)> LoginAsync(string? userAccount, string? password)
        {
            if (userAccount == null)
                return (false, null);

            var user = await _usersRepository.GetByUserAccountAsync(userAccount);
            if (user == null)
                return (false, user);

            if (BCrypt.Net.BCrypt.Verify(password, user.Password))
                return (true, user);
            return (false, user);
        }

        private void Logout()
        {
            LogoutHandlingAsync();
        }

        private async void LogoutHandlingAsync()
        {
            _logger.Info($"{UserAccount} 在 {DateTime.Now:yyyy-MM-dd HH:mm:ss} 退出登录");
            UserAccount = null;
            Password = "";
            UserName = null;
            RoleID = 0;
            RoleDisplay = null;
            RoleBrush = null;
            CreatedTime = null;
            LastLoginTime = null;
            CanManageUsers = false;
            IsSuperAdmin = false;
            AddUserModel = null;
            EditUserModel = null;
            UserList.Clear();
            await _globalSettingRepository.UpdateSettingValueAsync("CurrentUserAccount", "guest");
            IsLoggedIn = false;
            Growl.SuccessGlobal("退出登录");
        }

        private void AddUser()
        {
            AddUserHandlingAsync();
        }

        private async void AddUserHandlingAsync()
        {
            await Dialog
                .Show(new VM_AddUserDialog().UIElement)
                .Initialize<VM_AddUserDialog>(vm => vm.CurrentUserRoleID = RoleID)
                .GetResultAsync<UserManagementModel?>()
                .ContinueWith(um => AddUserModel = um.Result);

            if (AddUserModel != null)
            {
                if ((await _usersRepository.GetByUserAccountAsync(AddUserModel.Account!)) == null)
                {
                    var isAddSuccess = await _usersRepository.AddAsync(
                        new UsersModel()
                        {
                            UserAccount = AddUserModel.Account!,
                            UserName = AddUserModel.Name!,
                            Password = BCrypt.Net.BCrypt.HashPassword("123456"),
                            RoleId = StringToRoleID(AddUserModel.Role!),
                            CreatedTime = AddUserModel.CreatedDateTime,
                        }
                    );
                    if (isAddSuccess)
                    {
                        _logger.Info($"添加 {AddUserModel.Account} 账号成功！");
                        Growl.SuccessGlobal($"添加 {AddUserModel.Account} 账号成功！");
                        RefreshUsersAsync();
                    }
                    else
                    {
                        _logger.Warn("添加用户失败！", true);
                        Growl.WarningGlobal("添加用户失败！");
                    }
                }
                else
                {
                    _logger.Warn($"{AddUserModel.Account} 账号已存在！", true);
                    Growl.WarningGlobal($"{AddUserModel.Account} 账号已存在！");
                }
            }
        }

        private async void RefreshUsersAsync()
        {
            UserList.Clear();
            if (CanManageUsers)
            {
                (
                    await _usersRepository.GetListAsync(model =>
                        model.RoleId < RoleID
                        && model.UserAccount != "guest"
                        && model.UserAccount != "engineer"
                        && model.UserAccount != "admin"
                        && model.UserAccount != "sadmin"
                        && model.UserAccount != UserAccount
                    )
                )?.ForEach(item =>
                    UserList.Add(
                        new UserManagementModel()
                        {
                            Account = item.UserAccount,
                            Name = item.UserName,
                            Role = RoleIDToString(item.RoleId),
                            RoleColorBrush = GetBrushByRoleID(item.RoleId),
                            CreatedDateTime = item.CreatedTime,
                            LastLoginDateTime = item.LastLoginTime,
                        }
                    )
                );
            }
        }

        private async void ResetSelectedUserPasswordAsync()
        {
            if (SelectedUser != null)
            {
                var user = await _usersRepository.GetByUserAccountAsync(SelectedUser.Account!);
                if (user != null)
                {
                    user.Password = BCrypt.Net.BCrypt.HashPassword("123456");
                    if (await _usersRepository.UpdateAsync(user))
                    {
                        _logger.Warn($"{SelectedUser.Account} 账号重置密码成功！", true);
                        Growl.SuccessGlobal($"{SelectedUser.Account} 账号重置密码成功！");
                        return;
                    }
                }
            }
            _logger.Warn($"{SelectedUser?.Account} 账号重置密码失败！", true);
            Growl.WarningGlobal($"{SelectedUser?.Account} 账号重置密码失败！");
        }

        private async void DeleteSelectedUserAsync()
        {
            if (SelectedUser != null)
            {
                var user = await _usersRepository.GetByUserAccountAsync(SelectedUser.Account!);
                if (user != null)
                {
                    if (await _usersRepository.DeleteAsync(user))
                    {
                        _logger.Warn($"{SelectedUser.Account} 账号删除！", true);
                        Growl.WarningGlobal($"{SelectedUser.Account} 账号删除！");
                        RefreshUsersAsync();
                        return;
                    }
                }
            }
            _logger.Warn($"{SelectedUser?.Account} 账号删除失败！", true);
            Growl.WarningGlobal($"{SelectedUser?.Account} 账号删除失败！");
        }

        private void EditSelectedUser()
        {
            EditUserHandlingAsync();
        }

        private async void EditUserHandlingAsync()
        {
            if (SelectedUser != null)
            {
                await Dialog
                    .Show(new VM_EditUserDialog().UIElement)
                    .Initialize<VM_EditUserDialog>(vm =>
                    {
                        vm.CurrentUserRoleID = RoleID;
                        vm.Result!.Account = SelectedUser.Account;
                        vm.Result!.Name = SelectedUser.Name;
                        vm.Result.Role = SelectedUser.Role;
                    })
                    .GetResultAsync<UserManagementModel?>()
                    .ContinueWith(um => EditUserModel = um.Result);

                if (EditUserModel != null)
                {
                    var user = await _usersRepository.GetByUserAccountAsync(SelectedUser.Account!);
                    if (user != null)
                    {
                        user.UserName = EditUserModel.Name!;
                        user.RoleId = StringToRoleID(EditUserModel.Role!);
                        if (await _usersRepository.UpdateAsync(user))
                        {
                            _logger.Warn($"修改 {user.UserAccount} 账号信息成功！");
                            Growl.SuccessGlobal($"修改 {user.UserAccount} 账号信息成功！");
                            RefreshUsersAsync();
                            return;
                        }
                        else
                        {
                            _logger.Warn($"修改 {SelectedUser?.Account} 账号信息失败！", true);
                            Growl.WarningGlobal($"修改 {SelectedUser?.Account} 账号信息失败！");
                        }
                    }
                    else
                    {
                        _logger.Warn($"{SelectedUser?.Account} 账号未找到！", true);
                        Growl.WarningGlobal($"{SelectedUser?.Account} 账号未找到！");
                    }
                }
            }
        }

        private void ChangeCurrentUserPassword()
        {
            if (UserAccount != null)
                ChangeUserPasswordHandlingAsync(UserAccount);
        }

        private async void ChangeUserPasswordHandlingAsync(string userAccount)
        {
            var user = await _usersRepository.GetByUserAccountAsync(userAccount);
            if (user != null)
            {
                string? newPassword = null;
                await Dialog
                    .Show(new VM_ChangeUserPasswordDialog().UIElement)
                    .Initialize<VM_ChangeUserPasswordDialog>(vm =>
                    {
                        vm.OldPassword = user.Password;
                    })
                    .GetResultAsync<string?>()
                    .ContinueWith(um => newPassword = um.Result);
                if (newPassword != null)
                {
                    user.Password = newPassword;
                    if (await _usersRepository.UpdateAsync(user))
                    {
                        _logger.Warn($"修改 {user.UserAccount} 账号密码成功！");
                        Growl.SuccessGlobal($"修改 {user.UserAccount} 账号密码成功！");
                        RefreshUsersAsync();
                        return;
                    }
                    else
                    {
                        _logger.Warn($"修改 {user.UserAccount} 账号密码失败！", true);
                        Growl.WarningGlobal($"修改 {user.UserAccount} 账号密码失败！");
                    }
                }
            }
            else
            {
                _logger.Warn($"{userAccount} 账号未找到！", true);
                Growl.WarningGlobal($"{userAccount} 账号未找到！");
            }
        }

        private string RoleIDToString(int id)
        {
            return id switch
            {
                (int)UserRole.Guest => "访客",
                (int)UserRole.Engineer => "工程师",
                (int)UserRole.Admin => "管理员",
                (int)UserRole.SuperAdmin => "超级管理员",
                _ => "访客",
            };
        }

        private int StringToRoleID(string role)
        {
            return role switch
            {
                "访客" => (int)UserRole.Guest,
                "工程师" => (int)UserRole.Engineer,
                "管理员" => (int)UserRole.Admin,
                "超级管理员" => (int)UserRole.SuperAdmin,
                _ => (int)UserRole.Guest,
            };
        }

        private Brush GetBrushByRoleID(int id)
        {
            return id switch
            {
                (int)UserRole.Guest => new SolidColorBrush(Color.FromArgb(255, 158, 158, 158)),
                (int)UserRole.Engineer => new SolidColorBrush(Color.FromArgb(255, 76, 175, 80)),
                (int)UserRole.Admin => new SolidColorBrush(Color.FromArgb(255, 33, 150, 243)),
                (int)UserRole.SuperAdmin => new SolidColorBrush(Color.FromArgb(255, 233, 30, 99)),
                _ => new SolidColorBrush(Color.FromArgb(255, 158, 158, 158)),
            };
        }

        private bool CanManageUsersByRoleID(int id)
        {
            return id switch
            {
                (int)UserRole.Guest => false,
                (int)UserRole.Engineer => false,
                (int)UserRole.Admin => true,
                (int)UserRole.SuperAdmin => true,
                _ => false,
            };
        }

        private bool IsSuperAdminByRoleID(int id)
        {
            return id switch
            {
                (int)UserRole.Guest => false,
                (int)UserRole.Engineer => false,
                (int)UserRole.Admin => false,
                (int)UserRole.SuperAdmin => true,
                _ => false,
            };
        }
    }
}
