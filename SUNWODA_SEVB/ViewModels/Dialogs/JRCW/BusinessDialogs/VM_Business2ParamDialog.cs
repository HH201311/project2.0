using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.JRCW.Business2Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SUNWODA_SEVB.ViewModels.Dialogs.JRCW.BusinessDialogs
{
    public class VM_Business2ParamDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        /// <summary>
        /// 结果
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// 关闭操作
        /// </summary>
        public Action? CloseAction { get; set; }

        /// <summary>
        /// 通讯模式数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<ENeedleType>> NeedleTypeSource { get; } =
               new ObservableCollection<ComboboxItemModel<ENeedleType>>(Enum.GetValues<ENeedleType>().Select(v =>
               new ComboboxItemModel<ENeedleType> { ItemValue = v, DisplayName = v.ToString() }));

        private UserRole userRole;
        private Business2ParamModel itemSource = new();

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 参数模型
        /// </summary>
        public Business2ParamModel ItemSource { get => itemSource; set => SetProperty(ref itemSource, value); }

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
