using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Models.Project.JROH.MisBusinessModels;

namespace SUNWODA_SEVB.ViewModels.Dialogs.JROH.BusinessDialogs
{
    /// <summary>
    /// 极耳错位闭环参数配置对话框 ViewModel
    /// </summary>
    public class VM_MisBusinessParamDialog : DialogViewModelBase, IDialogResultable<bool>
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
        private MisBusinessParamModel itemSource = new();

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 参数模型
        /// </summary>
        public MisBusinessParamModel ItemSource { get => itemSource; set => SetProperty(ref itemSource, value); }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });

        /// <summary>
        /// 保存
        /// </summary>
        public ICommand? ConfirmCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand? CancleCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });
    }
}
