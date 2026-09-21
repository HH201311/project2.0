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
using System.ComponentModel;
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
    public class VM_CheckListDialog : DialogViewModelBase, IDialogResultable<bool>
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
        /// 选择项
        /// </summary>
        public ICommand ItemCheckedCommand => new RelayCommand(OnItemChecked);

        /// <summary>
        /// 移除项
        /// </summary>
        public ICommand RemoveItemCommand => new RelayCommand(OnRemoveItem);

        /// <summary>
        /// 查询
        /// </summary>
        public ICommand InquireCommand => new RelayCommand(OnInquire);

        private string inquireText = string.Empty;
        private int selectedCount;

        private ObservableCollection<CheckedItemModel<AddressItemModel>> filteredItemSource = [];
        private ObservableCollection<CheckedItemModel<AddressItemModel>> itemSource = [];

        /// <summary>
        /// 窗口标题
        /// </summary>
        public string DialogTitle { get; set; } = "PLC采集参数配置";

        /// <summary>
        /// 查询文本
        /// </summary>
        public string InquireText { get => inquireText; set => SetProperty(ref inquireText, value); }

        /// <summary>
        /// 已选中的项数量（用于界面显示）
        /// </summary>
        public int SelectedCount { get => selectedCount; set => SetProperty(ref selectedCount, value); }

        /// <summary>
        /// 数据源-左侧
        /// </summary>
        public ObservableCollection<CheckedItemModel<AddressItemModel>> FilteredItemSource { get => filteredItemSource; set => SetProperty(ref filteredItemSource, value); }

        /// <summary>
        /// 完整数据源（所有项，右侧列表使用）
        /// </summary>
        public ObservableCollection<CheckedItemModel<AddressItemModel>> ItemSource
        {
            get => itemSource;
            set
            {
                if (SetProperty(ref itemSource, value))
                {
                    // 当完整数据源变化时，过滤集合默认显示全部
                    FilteredItemSource = new ObservableCollection<CheckedItemModel<AddressItemModel>>(value ?? []);
                    SelectedCount = value?.Count(o => o.IsChecked) ?? 0;
                }
            }
        }

        /// <summary>
        /// 当移除项时触发
        /// </summary>
        /// <param name="arg"></param>
        private void OnItemChecked(object? arg)
        {
            if (arg is not CheckedItemModel<AddressItemModel> item) return;
            SelectedCount = ItemSource?.Count(o => o.IsChecked) ?? 0;
        }

        /// <summary>
        /// 当移除项时触发
        /// </summary>
        /// <param name="arg"></param>
        private void OnRemoveItem(object? arg)
        {
            if (arg is not CheckedItemModel<AddressItemModel> item) return;
            item.IsChecked = false;
            SelectedCount = ItemSource?.Count(o => o.IsChecked) ?? 0;
        }

        /// <summary>
        /// 当查询时触发
        /// </summary>
        /// <param name="arg"></param>
        private void OnInquire(object? arg)
        {
            if (arg is not string condition) return;

            // 根据条件过滤 ItemSource，注意使用原始对象引用，不能 new 新对象
            var filtered = string.IsNullOrWhiteSpace(condition)
                ? ItemSource.ToList()
                : ItemSource.Where(item => (item.DisplayName?.Contains(condition, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();

            // 更新过滤集合
            FilteredItemSource = new ObservableCollection<CheckedItemModel<AddressItemModel>>(filtered);
        }
    }
}
