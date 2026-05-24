using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB.UserOperation
{
    public class VM_UserAddDialog : DialogViewModelBase, IDialogResultable<UserManagementModel?>
    {
        private int _currentUserRoleID;
        private string? _account;
        private string? _accountErrorMessage;

        public int CurrentUserRoleID
        {
            get => _currentUserRoleID;
            set
            {
                SetProperty(ref _currentUserRoleID, value);
                UserRoleSource?.Clear();
                foreach (UserRole ur in Enum.GetValues(typeof(UserRole)))
                {
                    if ((int)ur < _currentUserRoleID)
                    {
                        UserRoleSource?.Add(OperationUtil.RoleIDToString((int)ur));
                    }
                }
            }
        }

        public string? Account
        {
            get => _account;
            set
            {
                var isValidAccount = OperationUtil.IsValidAccount(value);
                AccountErrorMessage = isValidAccount ? "" : "账号仅可包含数字及大小写字母";
                if (Result != null) Result.Account = value;
                SetProperty(ref _account, value);
            }
        }

        public string? AccountErrorMessage { get => _accountErrorMessage; set => SetProperty(ref _accountErrorMessage, value); }

        private UserManagementModel? _result = new UserManagementModel();
        public UserManagementModel? Result { get => _result; set => SetProperty(ref _result, value); }

        /// <summary>
        /// 可选择的角色列表
        /// </summary>
        public ObservableCollection<string>? UserRoleSource { get; set; } = [];

        public Action? CloseAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(AddUser);

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        /// <summary>
        /// 添加用户账号
        /// </summary>
        private void AddUser()
        {
            if (Result != null)
            {
                if (Result.Account != null && Result.Name != null && Result.Role != null)
                {
                    Result.CreatedDateTime = DateTime.Now;
                    CloseAction?.Invoke();
                }
                else
                {
                    Growl.WarningGlobal("请完整填写表单！");
                }
            }
            else
            {
                CloseAction?.Invoke();
            }
        }
    }
}
