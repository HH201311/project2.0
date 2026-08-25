using AngleSharp.Dom;
using Google.Protobuf.WellKnownTypes;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Socket;
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
using static SUNWODA_SEVB.ViewModels.Dialogs.CCTB.VM_TBDataConfigDialog;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB.CommunicationOptions
{
    public class VM_SocketClientConfigDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        /// <summary>
        /// 关闭
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(() => { Result = true; CloseAction?.Invoke(); });

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(() => { Result = false; CloseAction?.Invoke(); });

        /// <summary>
        /// 恢复默认设置：通讯配置
        /// </summary>
        public ICommand? ResetOptionCommand { get; set; }

        /// <summary>
        /// 恢复默认设置：协议配置
        /// </summary>
        public ICommand? ResetProtocolCommand { get; set; }

        /// <summary>
        /// 通讯配置
        /// </summary>
        public SocketClientOptions? CommunicationOptions { get; set; }

        /// <summary>
        /// 数据接收协议配置
        /// </summary>
        public ProtocolOptions? ProtocolOptions { get; set; }

        /// <summary>
        /// 分包模型选项数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<FramingMode>> FramingModeSource { get; } =
               new ObservableCollection<ComboboxItemModel<FramingMode>>(FramingMode.GetValues<FramingMode>().Select(v =>
               new ComboboxItemModel<FramingMode> { ItemValue = v, DisplayName = v.ToString() }));
    }
}
