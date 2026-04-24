using DocumentFormat.OpenXml.Office2010.Excel;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CTNCLX
{
    public class VM_MonitorPathSelectDialog : DialogViewModelBase, IDialogResultable<string?>
    {
        private string? _result;
        public string? Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = null;
                CloseAction?.Invoke();
            });

        public ICommand AffirmMonitorPathCommand => new RelayCommand(AffirmMonitorPath);

        public ICommand ChangeMonitorPathCommand => new RelayCommand(ChangeMonitorPath);

        private void AffirmMonitorPath()
        {
            if (!string.IsNullOrEmpty(Result))
            {
                CloseAction?.Invoke();
            }
            else
            {
                Growl.WarningGlobal("请选择正确的路径！");
            }
        }

        private void ChangeMonitorPath()
        {
            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = $"监控路径选择";
            folderDialog.InitialDirectory = Result ?? "C:\\";
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                Result = folderDialog.FolderName;
            }
        }
    }
}
