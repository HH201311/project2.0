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
    public class VM_CCTBProgressBarDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        private string? title = "默认标题";
        private double progressValue;
        private bool isIndeterminate;
        private string? statusMessage;
        private bool showCancelButton = true;
        private bool isCompleted;
        private CancellationTokenSource? _cts;

        public string? Title { get => title; set => SetProperty(ref title, value); }
        public double ProgressValue { get => progressValue; set => SetProperty(ref progressValue, value); }
        public bool IsIndeterminate { get => isIndeterminate; set => SetProperty(ref isIndeterminate, value); }
        public string? StatusMessage { get => statusMessage; set => SetProperty(ref statusMessage, value); }
        public bool ShowCancelButton { get => showCancelButton; set => SetProperty(ref showCancelButton, value); }

        public Action<VM_CCTBProgressBarDialog>? ProgressAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
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
