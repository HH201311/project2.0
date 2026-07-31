using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business1Models;

namespace SUNWODA_SEVB.ViewModels.Dialogs.JRCW.BusinessDialogs
{
    public class VM_Business1ParamDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        /// <summary>
        /// 结果
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// 关闭操作
        /// </summary>
        public Action? CloseAction { get; set; }

        private UserRole userRole;
        private Business1ParamModel itemSource = new();

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 参数模型
        /// </summary>
        public Business1ParamModel ItemSource { get => itemSource; set => SetProperty(ref itemSource, value); }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });

        /// <summary>
        /// 
        /// </summary>
        public ICommand? ConfirmCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });

        /// <summary>
        /// 
        /// </summary>
        public ICommand? CancleCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });
    }
}
