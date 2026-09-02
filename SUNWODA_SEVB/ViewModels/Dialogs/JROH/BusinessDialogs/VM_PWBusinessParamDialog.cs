using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models.Project.JROH.PWBusinessModels;

namespace SUNWODA_SEVB.ViewModels.Dialogs.JROH.BusinessDialogs
{
    public class VM_PWBusinessParamDialog : DialogViewModelBase, IDialogResultable<bool>
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
        private PWBusinessParamModel itemSource = new();

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 参数模型
        /// </summary>
        public PWBusinessParamModel ItemSource { get => itemSource; set => SetProperty(ref itemSource, value); }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });

        /// <summary>
        /// 
        /// </summary>
        public ICommand? ConfirmCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });

        /// <summary>
        /// 
        /// </summary>
        public ICommand? CancleCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });
    }
}
