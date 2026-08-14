using AngleSharp.Dom;
using HandyControl.Tools.Extension;
using Masuit.Tools;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.RecordModels;
using SUNWODA_SEVB.Tool.Extension;
using SUNWODA_SEVB.Tool.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB.RecodDialogs
{
    public class VM_AdjustmentRecordDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        private const string header = $"SpecialFile.{LoggerExtension.AdjustmentLog}.";
        private IAppLogRepository _appLogRepository;

        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() =>
        {
            ILoggerService.OnLoggerReportGlobalEvent -= OnLoggerReportGlobal;
            Result = false;
            CloseAction?.Invoke();
        });


        /// <summary>
        /// 展示信息
        /// </summary>
        public ICommand? ShowMessageCommand => new RelayCommand(OnShowMessageAsync);

        /// <summary>
        /// 记录
        /// </summary>
        public ObservableCollection<AdjustmentRecordModel> Records { get; set; } = new();

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="paramOptions"></param>
        public VM_AdjustmentRecordDialog(IAppLogRepository appLogRepository)
        {
            _appLogRepository = appLogRepository;
            InitializeRecords();

            ILoggerService.OnLoggerReportGlobalEvent -= OnLoggerReportGlobal;
            ILoggerService.OnLoggerReportGlobalEvent += OnLoggerReportGlobal;
        }

        /// <summary>
        /// 当点击信息时触发
        /// </summary>
        /// <param name="arg"></param>
        private async void OnShowMessageAsync(object? arg)
        {
            if (!(arg is string message)) return;
            if (string.IsNullOrEmpty(message)) return;

            await VM_CCTBMessageBoxDialog.InfoAsync(message, "控制记录");
        }


        /// <summary>
        /// 初始化
        /// </summary>
        private void InitializeRecords()
        {
            var recordInfo = _appLogRepository.GetAdjustmentRecordsPaged(1, 20);
            if (recordInfo.Total <= 0) return;
            RunOnUIThread(() => Records.Clear());
            foreach (var item in recordInfo.Items)
            {
                if (!(item.Logger?.StartsWith(header) ?? false)) continue;

                var record = AdjustmentRecordModel.ConvertToRecord(item);
                AddRecord(record);
            }
        }

        /// <summary>
        /// log 全局记录时触发
        /// </summary>
        /// <param name="logModel"></param>
        private void OnLoggerReportGlobal(AppLogModel logModel)
        {
            if (!(logModel.Logger?.StartsWith(header) ?? false)) return;

            var record = AdjustmentRecordModel.ConvertToRecord(logModel);
            AddRecord(record);
        }

        private int lastedRecordId = 0;
        /// <summary>
        /// 新增记录
        /// </summary>
        /// <param name="record"></param>
        private void AddRecord(AdjustmentRecordModel record)
        {
            record.ID = lastedRecordId++;
            RunOnUIThread(() =>
            {
                Records.Add(record);
                if (Records.Count > 20) Records.RemoveAt(0);
            });
        }
    }
}