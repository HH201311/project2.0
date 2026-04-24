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

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB
{
    public class VM_ClosedLoopSettingDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        private ESideType sideType;
        private EClosedLoopType closedLoopType;
        private UserRole? userRole;
        private ClosedLoopSettingModel? closedLoopSettingModel;

        public ESideType SideType { get => sideType; set => SetProperty(ref sideType, value); }
        public EClosedLoopType ClosedLoopType { get => closedLoopType; set => SetProperty(ref closedLoopType, value); }
        public UserRole? UserRole { get => userRole; set => SetProperty(ref userRole, value); }
        public ClosedLoopSettingModel? ClosedLoopSettingModel { get => closedLoopSettingModel; set => SetProperty(ref closedLoopSettingModel, value); }

        public ICommand SelectBootUpTypeCommand => new RelayCommand(SelectBootUpType);

        private void SelectBootUpType(object? arg)
        {
            if (!(arg is EBootUpType bootUpType)) return;
            if (ClosedLoopSettingModel == null) return;
            ClosedLoopSettingModel.BootUp_EBootUpType = bootUpType;
        }

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
            Result = false;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 确认
        /// </summary>
        private void Confirm()
        {
            Result = true;
            CloseAction?.Invoke();
        }

        private void Cancle()
        {
            Result = false;
            CloseAction?.Invoke();
        }
    }
}
