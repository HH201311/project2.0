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
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
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
    public class VM_MqttClientConfigDialog : DialogViewModelBase, IDialogResultable<bool>
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
        /// 恢复默认设置：地址配置
        /// </summary>
        public ICommand? ResetAddressCommand { get; set; }

        /// <summary>
        /// 通讯配置
        /// </summary>
        public CustomMqttClientOptions? CommunicationOptions { get; set; }

        /// <summary>
        /// 地址信息配置
        /// </summary>
        public MqttAddressModel? AddressModel { get; set; }

        private static readonly string[] protocols = new[] { "s7", "fins", "mc" };

        /// <summary>
        /// 通讯协议数据源
        /// </summary>
        public ObservableCollection<ComboboxItemModel<string>> ProtocolSource { get; } =
               new ObservableCollection<ComboboxItemModel<string>>(protocols.Select(v =>
               new ComboboxItemModel<string> { ItemValue = v, DisplayName = v.ToString() }));
    }
}
