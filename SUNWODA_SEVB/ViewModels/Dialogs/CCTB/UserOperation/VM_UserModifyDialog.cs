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
    public class VM_UserModifyDialog : DialogViewModelBase, IDialogResultable<UserManagementModel?>
    {
        private int _currentUserRoleID;
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
                        UserRoleSource?.Add(CCTBCore.RoleIDToString((int)ur));
                    }
                }
            }
        }

        private UserManagementModel? _result = new UserManagementModel();
        public UserManagementModel? Result { get => _result; set => SetProperty(ref _result, value); }

        public ObservableCollection<string>? UserRoleSource { get; set; } = [];

        public Action? CloseAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(EditUser);

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        private void EditUser()
        {
            if (Result != null)
            {
                if (Result.Name != null && Result.Role != null)
                {
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
