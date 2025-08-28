using HandyControl.Controls;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Dialogs.Common
{
    public class VM_EditUserDialog : DialogViewModelBase, IDialogResultable<UserManagementModel?>
    {
        private int _currentUserRoleID;
        public int CurrentUserRoleID
        {
            get => _currentUserRoleID;
            set
            {
                SetProperty(ref _currentUserRoleID, value);
                CanEditUserRoleList?.Clear();
                foreach (UserRole ur in Enum.GetValues(typeof(UserRole)))
                {
                    if ((int)ur < _currentUserRoleID)
                    {
                        CanEditUserRoleList?.Add(RoleIDToString((int)ur));
                    }
                }
            }
        }

        private UserManagementModel? _result = new UserManagementModel();
        public UserManagementModel? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public ObservableCollection<string>? CanEditUserRoleList { get; set; } =
            new ObservableCollection<string>();
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = null;
                CloseAction?.Invoke();
            });

        public ICommand EditUserCommand => new RelayCommand(EditUser);

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
    }
}
