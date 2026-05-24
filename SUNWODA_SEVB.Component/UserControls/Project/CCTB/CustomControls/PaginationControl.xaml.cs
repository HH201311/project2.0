using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    /// <summary>
    /// PaginationControl.xaml 的交互逻辑
    /// </summary>
    public partial class PaginationControl : UserControl
    {
        /// <summary>
        /// 
        /// </summary>
        public PaginationControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 首页页码
        /// </summary>
        public int FirstPageNumber { get; private set; } = 1;

        /// <summary>
        /// 末页页码
        /// </summary>
        public int LastPageNumber { get; private set; } = 1;

        /// <summary>
        /// 当前页码集合
        /// </summary>
        public ObservableCollection<PaginationItemModel> PageNumbers { get; private set; } = [];

        #region TotalCount
        /// <summary>
        /// 数据总条数
        /// </summary>
        public int TotalCount
        {
            get { return (int)GetValue(TotalCountProperty); }
            set { SetValue(TotalCountProperty, value); }
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty TotalCountProperty = DependencyProperty.Register(
            "TotalCount",
            typeof(int),
            typeof(PaginationControl),
            new PropertyMetadata(0));
        #endregion

        #region PageCount
        /// <summary>
        /// 
        /// </summary>
        public int PageCount
        {
            get { return (int)GetValue(PageCountProperty); }
            set { SetValue(PageCountProperty, value); }
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(
            "PageCount",
            typeof(int),
            typeof(PaginationControl),
            new PropertyMetadata(1, new PropertyChangedCallback(OnPageCountChanged)));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnPageCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as PaginationControl;
            if (uc == null) return;

            if (e.NewValue != null && e.NewValue is int newValue)
            {
                uc.FirstPageNumber = 1;
                uc.LastPageNumber = newValue > 0 ? newValue : 1;

                // 在这里处理 PageCount 变化后的逻辑
                uc.PageNumbers.Clear();
                var count = newValue <= 5 ? (newValue < 1 ? 1 : newValue) : (newValue < 8 ? newValue : 6);
                for (int i = 1; i <= count; i++) uc.PageNumbers.Add(new PaginationItemModel(i, i == uc.FirstPageNumber));

                // 初始时前省略号隐藏，后省略号根据总页数决定显示与否
                uc.FirstEllipsisButton.Visibility = Visibility.Collapsed;
                // 如果总页数大于等于8，显示省略号按钮，否则隐藏
                uc.LastEllipsisButton.Visibility = newValue >= 8 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region CurrentPageNumber
        /// <summary>
        /// 当前页码，从1开始
        /// </summary>
        public int CurrentPageNumber
        {
            get { return (int)GetValue(CurrentPageNumberProperty); }
            set { SetValue(CurrentPageNumberProperty, value); }
        }

        /// <summary>
        /// 当前页码，从1开始
        /// </summary>
        public static readonly DependencyProperty CurrentPageNumberProperty = DependencyProperty.Register(
            "CurrentPageNumber",
            typeof(int),
            typeof(PaginationControl),
            new PropertyMetadata(1, new PropertyChangedCallback(OnCurrentPageNumberChanged)));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnCurrentPageNumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as PaginationControl;
            if (uc != null && e.NewValue is int newValue && e.OldValue is int oldValue && newValue != oldValue)
            {
                uc.UpdatePageDisplay(newValue);
            }
        }
        #endregion

        #region JumpPageNumber
        /// <summary>
        /// 跳转的页码，从1开始
        /// </summary>
        public int JumpPageNumber
        {
            get { return (int)GetValue(JumpPageNumberProperty); }
            set { SetValue(JumpPageNumberProperty, value); }
        }

        /// <summary>
        /// 跳转的页码，从1开始
        /// </summary>
        public static readonly DependencyProperty JumpPageNumberProperty = DependencyProperty.Register(
            "JumpPageNumber",
            typeof(int),
            typeof(PaginationControl),
            new PropertyMetadata(1));
        #endregion

        #region GotoPageCommand
        /// <summary>
        /// 
        /// </summary>
        public ICommand? GotoPageCommand
        {
            get { return (ICommand?)GetValue(GotoPageCommandProperty); }
            set { SetValue(GotoPageCommandProperty, value); }
        }

        /// <summary>
        /// 
        /// </summary>
        public static readonly DependencyProperty GotoPageCommandProperty = DependencyProperty.Register(
            "GotoPageCommand",
            typeof(ICommand),
            typeof(PaginationControl),
            new PropertyMetadata(null)
        );
        #endregion

        /// <summary>
        /// 跳转到指定页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GotoPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            if (!(btn.CommandParameter is int pageNumber)) return;

            var moveCount = pageNumber - CurrentPageNumber;
            MovePageNumber(moveCount);
        }

        /// <summary>
        /// 上一页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            MovePageNumber(-1);
        }

        /// <summary>
        /// 下一页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            MovePageNumber(1);
        }

        /// <summary>
        /// 上一页集合
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FirstEllipsis_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            MovePageNumber(-5);
        }

        /// <summary>
        /// 下一页集合
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LastEllipsis_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            MovePageNumber(5);
        }

        /// <summary>
        /// 跳转到指定页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void JumpPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            var moveCount = JumpPageNumber - CurrentPageNumber;
            MovePageNumber(moveCount);
            JumpPageNumber = CurrentPageNumber;
        }

        /// <summary>
        /// 跳转到第一页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            var moveCount = FirstPageNumber - CurrentPageNumber;
            MovePageNumber(moveCount);
        }

        /// <summary>
        /// 跳转到最后一页
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;

            var moveCount = LastPageNumber - CurrentPageNumber;
            MovePageNumber(moveCount);
        }

        /// <summary>
        /// 根据移动的页数和方向更新 PageNumbers 集合
        /// </summary>
        /// <param name="moveCount">移动的页数(>0:向后移动；<0:向前移动)</param>
        private void MovePageNumber(int moveCount)
        {
            if (moveCount == 0) return;

            var newPage = CurrentPageNumber + moveCount;
            if (newPage < FirstPageNumber) newPage = FirstPageNumber;
            if (newPage > LastPageNumber) newPage = LastPageNumber;

            // 执行跳转命令
            GotoPageCommand?.Execute(newPage);

            // 设置 CurrentPageNumber，会自动触发 OnCurrentPageNumberChanged → UpdatePageDisplay
            CurrentPageNumber = newPage;
        }

        /// <summary>
        /// 根据当前页码重新计算并刷新页码按钮显示范围
        /// </summary>
        private void UpdatePageDisplay(int currentPage)
        {
            if (PageCount <= 0 || PageNumbers == null) return;

            // 边界保护
            if (currentPage < FirstPageNumber) currentPage = FirstPageNumber;
            if (currentPage > LastPageNumber) currentPage = LastPageNumber;

            // 计算显示范围
            int newFirst, newLast;
            if (PageCount < 8)
            {
                // 总页数少于8页时，全部显示
                newFirst = FirstPageNumber;
                newLast = LastPageNumber;
            }
            else
            {
                // 总页数>=8时，固定显示6个页码按钮（或5个中间页）
                if (currentPage <= FirstPageNumber + 3)
                {
                    newFirst = FirstPageNumber;
                    newLast = FirstPageNumber + 5;
                }
                else if (currentPage >= LastPageNumber - 2)
                {
                    newLast = LastPageNumber;
                    newFirst = LastPageNumber - 5;
                }
                else
                {
                    newFirst = currentPage - 2;
                    newLast = currentPage + 2;
                }
            }

            // 重建页码集合
            PageNumbers.Clear();
            for (int i = newFirst; i <= newLast; i++)
            {
                PageNumbers.Add(new PaginationItemModel(i, i == currentPage));
            }

            // 更新省略号可见性
            if (FirstEllipsisButton != null)
                FirstEllipsisButton.Visibility = (newFirst > FirstPageNumber) ? Visibility.Visible : Visibility.Collapsed;
            if (LastEllipsisButton != null)
                LastEllipsisButton.Visibility = (newLast < LastPageNumber) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
