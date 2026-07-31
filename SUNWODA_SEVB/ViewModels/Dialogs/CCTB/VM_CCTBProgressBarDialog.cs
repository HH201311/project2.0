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

        private string _title = "正在处理...";
        private double _progressValue = 0;
        private string _progressText = "0%";
        private string _remainingTimeText = string.Empty;
        private bool _isIndeterminate = false; // 是否未知进度
        private bool _showCompletedButton = false;
        private bool _showCancelButton = true;

        // 用于取消后台任务
        private CancellationTokenSource? _cts;

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public double ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }
        public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }
        public string RemainingTimeText { get => _remainingTimeText; set => SetProperty(ref _remainingTimeText, value); }
        public bool IsIndeterminate { get => _isIndeterminate; set => SetProperty(ref _isIndeterminate, value); }
        public bool ShowCompletedButton { get => _showCompletedButton; set => SetProperty(ref _showCompletedButton, value); }
        public bool ShowCancelButton { get => _showCancelButton; set => SetProperty(ref _showCancelButton, value); }

        // 对外暴露的命令
        public ICommand CompletedCommand => new RelayCommand(Completed);
        public ICommand CancelExportCommand => new RelayCommand(CancelExport);

        /// <summary>
        /// 初始化弹窗并绑定后台任务
        /// </summary>
        public async Task StartExportAsync(Func<CancellationToken, Task> exportTask, bool indeterminate = false)
        {
            _cts = new CancellationTokenSource();
            IsIndeterminate = indeterminate;
            ShowCancelButton = true;
            Title = "正在导出数据...";

            try
            {
                await exportTask(_cts.Token);
                ShowCompletedButton = true;
                ShowCancelButton = false;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Title = $"导出失败";
                RemainingTimeText = ex.Message;
                IsIndeterminate = false;
            }
        }

        /// <summary>
        /// 更新进度（由业务层调用）
        /// </summary>
        public void UpdateProgress(double percentage, string? customText = null, string? timeLeft = null)
        {
            // WPF 必须在 UI 线程更新属性
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = percentage;
                ProgressText = customText ?? $"{percentage:F0}%";
                if (!string.IsNullOrEmpty(timeLeft)) RemainingTimeText = timeLeft;
            });
        }

        /// <summary>
        /// 完成
        /// </summary>
        private void Completed()
        {
            Result = true;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 取消
        /// </summary>
        private async void CancelExport()
        {
            _cts?.Cancel();
            Title = "正在取消导出...";
            RemainingTimeText = string.Empty;
            // 取消时显示转圈状态，避免误导
            IsIndeterminate = ProgressValue < 100;
            ShowCancelButton = false; // 隐藏取消按钮
            await Task.Delay(1500);
            Result = false;
            CloseAction?.Invoke();
        }
    }
}
