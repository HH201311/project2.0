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
    public class VM_UserLoginDialog : DialogViewModelBase, IDialogResultable<string?>
    {
        private UserManagementModel? _userModel;
        private string? _password;
        private string? _result;

        /// <summary>
        /// 用户信息
        /// </summary>
        public UserManagementModel? UserModel
        {
            get => _userModel;
            set => SetProperty(ref _userModel, value);
        }

        /// <summary>
        /// 密码
        /// </summary>
        public string? Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// 结果
        /// </summary>
        public string? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public Action? CloseAction { get; set; }

        public ICommand CloseCommand => new RelayCommand(Close);

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(Confirm);

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(Cancle);

        private void Close()
        {
            Result = string.Empty;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 确认
        /// </summary>
        private void Confirm()
        {
            if (UserModel != null)
            {
                if (!string.IsNullOrEmpty(UserModel.Account) && !string.IsNullOrEmpty(Password))
                {
                    Result = Password;
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

        private void Cancle()
        {
            Result = string.Empty;
            CloseAction?.Invoke();
        }
    }
}
