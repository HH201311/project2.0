using LibVLCSharp.Shared;
using SUNWODA_SEVB.Tool.Helper.CCTBHelper;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls
{
    /// <summary>
    /// MicroChartControl.xaml 的交互逻辑
    /// </summary>
    public partial class MicroChartControl : UserControl
    {
        public MicroChartControl()
        {
            InitializeComponent();

            // 初始化坐标轴
            XAxisPainter.Update(0, 10, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            YAxisPainter.Update(0, 300, 50);
        }

        #region 事件
        private void ChartGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawChart();
        }
        #endregion

        #region Title
        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title",
            typeof(string),
            typeof(MicroChartControl),
            new PropertyMetadata("", new PropertyChangedCallback(OnTitleChanged))
        );

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as MicroChartControl;
            if (uc != null)
            {
                uc.TitleTextBlock.Text = uc.Title;
            }
        }
        #endregion

        #region ItemSource
        public IEnumerable<double> ItemSource
        {
            get { return (IEnumerable<double>)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(IEnumerable<double>),
            typeof(MicroChartControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as MicroChartControl;
            if (uc != null && uc.ItemSource != null)
            {
                // A. 防止内存泄漏：取消订阅旧集合的事件
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= uc.OnItemSourcePropertyChanged;
                }

                // B. 订阅新集合的事件
                if (e.NewValue is INotifyCollectionChanged newCollection)
                {
                    newCollection.CollectionChanged += uc.OnItemSourcePropertyChanged;
                }

                // C. 数据源变了，立即重绘一次
                uc.DrawChart();
            }
        }

        private void OnItemSourcePropertyChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 当数据增加、删除、重置时，重新绘制
            DrawChart();
        }
        #endregion

        private void DrawChart()
        {
            // 基础校验
            if (ChartGrid.ActualWidth == 0 || ChartGrid.ActualHeight == 0 || ItemSource == null)
                return;

            var datas = ItemSource.ToList();
            if (datas.Count < 2)
            {
                ChartPath.Data = null;
                FillPath.Data = null;
                return; // 点太少画不出来
            }

            var titleH = TitleTextBlock.ActualHeight;
            var min = datas.Min();
            var max = datas.Max();
            var range = (max - min) <= 0 ? 1 : (max - min);
            var topMargin = Math.Max(range / 5, titleH);
            var yMin = min - range / 5;
            var yMax = max + range / 5;
            var yRange = Math.Max(range, yMax - yMin);

            var count = datas.Count();
            var xSetp = count < 10 ? 1 : count / 10;
            var xLabels = new List<string>();
            for (int i = 0; i <= count; i++)
            {
                var label = i % xSetp == 0 ? i.ToString() : "";
                xLabels.Add(label);
            }

            XAxisPainter.Update(xLabels.ToArray());
            YAxisPainter.Update(yMin, yMax, yRange / 6);
            DrawSubLine(datas, yMin, yRange);
            DrawLine(datas, yMin, yRange);
        }

        #region 虚线
        private void DrawSubLine(List<double> datas, double yMin, double yRange)
        {
            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            #region 绘制虚线
            var value = datas.Average();
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var y = GraphicHelper.Normalize(value, yMin, yRange, h);
                ctx.BeginFigure(new Point(0, y), false, false);
                ctx.LineTo(new Point(w, y), true, true);
            }
            geometry.Freeze(); // 冻结以提升性能
            SubPath.Data = geometry;
            #endregion
        }
        #endregion

        #region 折线
        private void DrawLine(List<double> datas, double yMin, double yRange)
        {
            var w = ChartGrid.ActualWidth;
            var h = ChartGrid.ActualHeight;

            if (yRange == 0) yRange = 1;
            #region 绘制折线
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var stepX = w / (datas.Count - 1); // X轴步长
                // 移动到起点
                ctx.BeginFigure(new Point(0, GraphicHelper.Normalize(datas[0], yMin, yRange, h)), true, false);
                // 连接后续点
                for (int i = 1; i < datas.Count; i++)
                {
                    var x = i * stepX;
                    var y = GraphicHelper.Normalize(datas[i], yMin, yRange, h);
                    ctx.LineTo(new Point(x, y), true, true);
                }
            }
            geometry.Freeze(); // 冻结以提升性能
            ChartPath.Data = geometry;
            #endregion

            #region 绘制折线填充
            // 如果需要填充下方的半透明色，需要闭合路径
            var fillGeo = geometry.Clone();
            using (var ctx = fillGeo.Open())
            {
                // 接着上面的路径画
                // 1. 移动到右下角
                ctx.BeginFigure(new Point(w, h), true, true);
                // 2. 连到左下角
                ctx.LineTo(new Point(0, h), false, false);
                // 3. 连到第一个数据点
                ctx.LineTo(new Point(0, GraphicHelper.Normalize(datas[0], yMin, yRange, h)), false, false);

                // 4. 重画曲线部分 (或者复制上面的逻辑)
                var stepX = w / (datas.Count - 1);
                for (int i = 1; i < datas.Count; i++)
                {
                    ctx.LineTo(new Point(i * stepX, GraphicHelper.Normalize(datas[i], yMin, yRange, h)), true, true);
                }
                // 5. 连回右下角 (自动闭合)
            }
            fillGeo.Freeze();
            FillPath.Data = fillGeo;
            #endregion
        }
        #endregion
    }
}
