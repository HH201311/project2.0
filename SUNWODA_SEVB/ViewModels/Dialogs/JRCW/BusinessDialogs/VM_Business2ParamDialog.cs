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
        private string? verifyBatteryCode;
        private int selectedTabIndex;

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserRole UserRole { get => userRole; set => SetProperty(ref userRole, value); }

        /// <summary>
        /// 参数模型
        /// </summary>
        public Business2ParamModel ItemSource { get => itemSource; set => SetProperty(ref itemSource, value); }

        /// <summary>
        /// 完美电芯页码编号
        /// </summary>
        public int SelectedTabIndex { get => selectedTabIndex; set => SetProperty(ref selectedTabIndex, value); }

        /// <summary>
        /// 校验的完美电芯条码
        /// 备注：根据卷针号自动匹配
        /// </summary>
        public string? VerifyBatteryCode { get => verifyBatteryCode; set => SetProperty(ref verifyBatteryCode, value); }

        /// <summary>
        /// 完美电芯条码校验命令
        /// </summary>
        public ICommand? VerifyBatteryCodeCommand { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand? CloseCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });

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
