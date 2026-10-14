using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Dialogs.NCLCCT
{
    /// <summary>连续检测双监控目录选择结果。</summary>
    public sealed class NclcctMonitorPathsResult
    {
        public string? Path1 { get; set; }
        public string? Path2 { get; set; }
    }

    public class VM_NCLCCTMonitorPathSelectDialog
        : DialogViewModelBase,
            IDialogResultable<NclcctMonitorPathsResult?>
    {
        private NclcctMonitorPathsResult? _result;
        private string? _monitorPath1;
        private string? _monitorPath2;

        public NclcctMonitorPathsResult? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }

        public string? MonitorPath1
        {
            get => _monitorPath1;
            set => SetProperty(ref _monitorPath1, value);
        }

        public string? MonitorPath2
        {
            get => _monitorPath2;
            set => SetProperty(ref _monitorPath2, value);
        }

        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = null;
                CloseAction?.Invoke();
            });

        public ICommand AffirmMonitorPathCommand => new RelayCommand(AffirmMonitorPath);

        public ICommand ChangeMonitorPath1Command =>
            new RelayCommand(() => ChangeMonitorPath(isPath1: true));

        public ICommand ChangeMonitorPath2Command =>
            new RelayCommand(() => ChangeMonitorPath(isPath1: false));

        private void AffirmMonitorPath()
        {
            if (
                string.IsNullOrWhiteSpace(MonitorPath1)
                || string.IsNullOrWhiteSpace(MonitorPath2)
                || !Directory.Exists(MonitorPath1)
                || !Directory.Exists(MonitorPath2)
            )
            {
                Growl.WarningGlobal("请分别选择左环、右环两个有效的监听文件夹！");
                return;
            }

            Result = new NclcctMonitorPathsResult
            {
                Path1 = MonitorPath1,
                Path2 = MonitorPath2,
            };
            CloseAction?.Invoke();
        }

        private void ChangeMonitorPath(bool isPath1)
        {
            var current = isPath1 ? MonitorPath1 : MonitorPath2;
            var folderDialog = new OpenFolderDialog
            {
                Title = isPath1 ? "左环监听路径选择" : "右环监听路径选择",
                InitialDirectory = string.IsNullOrWhiteSpace(current) ? "C:\\" : current,
                ValidateNames = true,
                Multiselect = false,
            };
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                if (isPath1)
                    MonitorPath1 = folderDialog.FolderName;
                else
                    MonitorPath2 = folderDialog.FolderName;
            }
        }
    }
}
