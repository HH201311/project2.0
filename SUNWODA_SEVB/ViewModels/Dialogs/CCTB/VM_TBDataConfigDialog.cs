using AngleSharp.Dom;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
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

namespace SUNWODA_SEVB.ViewModels.Dialogs.CCTB
{
    public class VM_TBDataConfigDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        public bool Result { get; set; } = false;
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = false;
                CloseAction?.Invoke();
            });

        public List<EParamOption> ParamOptions { get; private set; }

        /// <summary>
        /// 确认
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(Confirm);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="paramOptions"></param>
        public VM_TBDataConfigDialog(List<EParamOption> paramOptions)
        {
            ParamOptions = paramOptions;
            InitilizeParamOptionModels(paramOptions);
        }


        /// <summary>
        /// 绑定到界面
        /// </summary>
        public ObservableCollection<CheckedItemModel<EParamOption>> ParamOptionModels { get; set; } = [];

        /// <summary>
        /// 确认
        /// </summary>
        private void Confirm()
        {
            var selectedParamOptions = ParamOptionModels.Where(o => o.IsChecked).Select(o => o.Item);
            ParamOptions.Clear();
            ParamOptions.AddRange(selectedParamOptions);
            ParamOptions.Sort();
            Result = true;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 取消
        /// </summary>
        public ICommand CancleCommand => new RelayCommand(Cancle);

        private void Cancle()
        {
            Result = false;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 初始化界面控件
        /// </summary>
        /// <param name="paramOptions"></param>
        /// <returns></returns>
        private void InitilizeParamOptionModels(List<EParamOption> paramOptions)
        {
            var eParamOptions = Enum.GetValues(typeof(EParamOption));
            foreach (EParamOption eParamOption in eParamOptions)
            {
                var itmes = new CheckedItemModel<EParamOption>();
                itmes.Item = eParamOption;
                itmes.DisplayName = eParamOption.ToString();
                var isChecked = (paramOptions != null && paramOptions.Contains(eParamOption));
                itmes.IsChecked = isChecked;
                ParamOptionModels.Add(itmes);
            }
        }
    }
}
