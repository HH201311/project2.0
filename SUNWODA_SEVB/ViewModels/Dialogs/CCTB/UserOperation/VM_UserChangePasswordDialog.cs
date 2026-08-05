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
    public class VM_UserChangePasswordDialog : DialogViewModelBase, IDialogResultable<string?>
    {
        private string reconfirmOldPassword = "";
        private string newPassword = "";
        private string againReconfirmNewPassword = "";
        private string? reconfirmOldPasswordErrorMessage;
        private string? againReconfirmNewPasswordErrorMessage;

        public string? Result { get; set; }
        public Action? CloseAction { get; set; }

        public string? OldPassword { get; set; }
        public string ReconfirmOldPassword
        {
            get => reconfirmOldPassword;
            set => SetProperty(ref reconfirmOldPassword, value);
        }
        public string NewPassword
        {
            get => newPassword;
            set
            {
                var isAgainReconfirmNewPassword = (value == AgainReconfirmNewPassword && value != "");
                AgainReconfirmNewPasswordErrorMessage = isAgainReconfirmNewPassword ? "" : "新设置密码两次不一致或密码为空";
                SetProperty(ref newPassword, value);
            }
        }
        public string AgainReconfirmNewPassword
        {
            get => againReconfirmNewPassword;
            set
            {
                var isAgainReconfirmNewPassword = (value == NewPassword && NewPassword != "");
                AgainReconfirmNewPasswordErrorMessage = isAgainReconfirmNewPassword ? "" : "新设置密码两次不一致或密码为空";
                SetProperty(ref againReconfirmNewPassword, value);
            }
        }

        public string? ReconfirmOldPasswordErrorMessage
        {
            get => reconfirmOldPasswordErrorMessage;
            set => SetProperty(ref reconfirmOldPasswordErrorMessage, value);
        }

        public string? AgainReconfirmNewPasswordErrorMessage
        {
            get => againReconfirmNewPasswordErrorMessage;
            set => SetProperty(ref againReconfirmNewPasswordErrorMessage, value);
        }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(ConfirmModifyPassword);

        /// <summary>
        /// 确认
        /// </summary>
        private void ConfirmModifyPassword()
        {
            var isReconfirmOldPassword = BCrypt.Net.BCrypt.Verify(ReconfirmOldPassword, OldPassword);
            if (isReconfirmOldPassword)
            {
                ReconfirmOldPasswordErrorMessage = "";
                var isAgainReconfirmNewPassword = (AgainReconfirmNewPassword == NewPassword && NewPassword != "");
                if (isAgainReconfirmNewPassword)
                {
                    AgainReconfirmNewPasswordErrorMessage = "";
                    Result = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                    CloseAction?.Invoke();
                }
                else
                {
                    AgainReconfirmNewPasswordErrorMessage = "新设置密码两次不一致或密码为空";
                }
            }
            else
            {
                ReconfirmOldPasswordErrorMessage = "原密码不正确";
            }
        }

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });
    }
}
