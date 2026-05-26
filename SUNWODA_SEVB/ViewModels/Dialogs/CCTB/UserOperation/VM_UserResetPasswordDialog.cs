using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Python.Runtime;
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
    /// <summary>
    /// 重置密码
    /// </summary>
    public class VM_UserResetPasswordDialog : DialogViewModelBase, IDialogResultable<string?>
    {
        private UserManagementModel? _userModel;
        private string? _result = "123456";

        public UserManagementModel? UserModel { get => _userModel; set => SetProperty(ref _userModel, value); }
        public string? Result { get => _result; set => SetProperty(ref _result, value); }

        public Action? CloseAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(EditUserPassword);

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(() => { Result = null; CloseAction?.Invoke(); });

        private void EditUserPassword()
        {
            if (!string.IsNullOrEmpty(Result))
            {
                CloseAction?.Invoke();
            }
            else
            {
                Growl.WarningGlobal("请完整填写表单！");
            }
        }
    }
}
