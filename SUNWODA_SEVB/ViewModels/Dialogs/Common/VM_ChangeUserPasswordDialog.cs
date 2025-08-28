using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;

namespace SUNWODA_SEVB.ViewModels.Dialogs.Common
{
    public class VM_ChangeUserPasswordDialog : DialogViewModelBase, IDialogResultable<string?>
    {
        private readonly Brush red = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
        private readonly Brush gray = new SolidColorBrush(Color.FromArgb(255, 224, 224, 224));
        private string reconfirmOldPassword = "";
        private string newPassword = "";
        private string againReconfirmNewPassword = "";
        private bool isReconfirmOldPassword;
        private bool isAgainReconfirmNewPassword;
        private string? reconfirmOldPasswordErrorMessage;
        private string? againReconfirmNewPasswordErrorMessage;
        private Brush reconfirmOldPasswordBrush = new SolidColorBrush(
            Color.FromArgb(255, 224, 224, 224)
        );
        private Brush againReconfirmNewPasswordBrush = new SolidColorBrush(
            Color.FromArgb(255, 224, 224, 224)
        );

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
                IsAgainReconfirmNewPassword = (value == AgainReconfirmNewPassword && value != "");
                if (IsAgainReconfirmNewPassword)
                {
                    AgainReconfirmNewPasswordErrorMessage = "";
                    AgainReconfirmNewPasswordBrush = gray;
                }
                else
                {
                    AgainReconfirmNewPasswordErrorMessage = "新设置密码两次不一致或密码为空";
                    AgainReconfirmNewPasswordBrush = red;
                }
                SetProperty(ref newPassword, value);
            }
        }
        public string AgainReconfirmNewPassword
        {
            get => againReconfirmNewPassword;
            set
            {
                IsAgainReconfirmNewPassword = (value == NewPassword && NewPassword != "");
                if (IsAgainReconfirmNewPassword)
                {
                    AgainReconfirmNewPasswordErrorMessage = "";
                    AgainReconfirmNewPasswordBrush = gray;
                }
                else
                {
                    AgainReconfirmNewPasswordErrorMessage = "新设置密码两次不一致或密码为空";
                    AgainReconfirmNewPasswordBrush = red;
                }
                SetProperty(ref againReconfirmNewPassword, value);
            }
        }
        public bool IsReconfirmOldPassword
        {
            get => isReconfirmOldPassword;
            set => SetProperty(ref isReconfirmOldPassword, value);
        }
        public bool IsAgainReconfirmNewPassword
        {
            get => isAgainReconfirmNewPassword;
            set => SetProperty(ref isAgainReconfirmNewPassword, value);
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

        public Brush ReconfirmOldPasswordBrush
        {
            get => reconfirmOldPasswordBrush;
            set => SetProperty(ref reconfirmOldPasswordBrush, value);
        }

        public Brush AgainReconfirmNewPasswordBrush
        {
            get => againReconfirmNewPasswordBrush;
            set => SetProperty(ref againReconfirmNewPasswordBrush, value);
        }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = null;
                CloseAction?.Invoke();
            });

        public ICommand ReconfirmCommand => new RelayCommand(ReconfirmModifyPassword);

        private void ReconfirmModifyPassword()
        {
            IsReconfirmOldPassword = BCrypt.Net.BCrypt.Verify(ReconfirmOldPassword, OldPassword);
            if (IsReconfirmOldPassword)
            {
                ReconfirmOldPasswordErrorMessage = "";
                ReconfirmOldPasswordBrush = gray;
                IsAgainReconfirmNewPassword = (
                    AgainReconfirmNewPassword == NewPassword && NewPassword != ""
                );
                if (IsAgainReconfirmNewPassword)
                {
                    AgainReconfirmNewPasswordErrorMessage = "";
                    AgainReconfirmNewPasswordBrush = gray;
                    Result = BCrypt.Net.BCrypt.HashPassword(NewPassword);
                    CloseAction?.Invoke();
                }
                else
                {
                    AgainReconfirmNewPasswordErrorMessage = "新设置密码两次不一致或密码为空";
                    AgainReconfirmNewPasswordBrush = red;
                }
            }
            else
            {
                ReconfirmOldPasswordErrorMessage = "原密码不正确";
                ReconfirmOldPasswordBrush = red;
            }
        }
    }
}
