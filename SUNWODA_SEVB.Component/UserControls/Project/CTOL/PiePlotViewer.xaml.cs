using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ScottPlot;

namespace SUNWODA_SEVB.Component.UserControls.Project.CTOL
{
    /// <summary>
    /// PiePlotViewer.xaml 的交互逻辑
    /// </summary>
    public partial class PiePlotViewer : UserControl
    {
        // 关键：保存 legend panel，避免重复 ShowLegend() 叠加
        private IPanel? _legendPanel;

        // 你可以按需调整这个阈值：>=10 走“底部+横向换行”，否则走“右侧竖向”
        private const int LegendWrapThreshold = 10;

        // ✅ 控件太小时不显示图例：按需调整阈值
        private const double LegendMinWidth = 100;
        private const double LegendMinHeight = 100;

        public List<double> Values
        {
            get { return (List<double>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }

        public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
            "Values",
            typeof(List<double>),
            typeof(PiePlotViewer),
            new PropertyMetadata(null, OnDataChanged)
        );

        public List<string> Labels
        {
            get { return (List<string>)GetValue(LabelsProperty); }
            set { SetValue(LabelsProperty, value); }
        }

        public static readonly DependencyProperty LabelsProperty = DependencyProperty.Register(
            "Labels",
            typeof(List<string>),
            typeof(PiePlotViewer),
            new PropertyMetadata(null, OnDataChanged)
        );

        public string BackgroundColorHex
        {
            get { return (string)GetValue(BackgroundColorHexProperty); }
            set { SetValue(BackgroundColorHexProperty, value); }
        }

        public static readonly DependencyProperty BackgroundColorHexProperty =
            DependencyProperty.Register(
                "BackgroundColorHex",
                typeof(string),
                typeof(PiePlotViewer),
                new PropertyMetadata("#FFFFFF", OnBackgroundColorHexChanged)
            );

        public PiePlotViewer()
        {
            InitializeComponent();

            // 不允许使用上下文菜单、缩放/拖动
            PART_Plot.Menu?.Clear();
            PART_Plot.UserInputProcessor.RightClickDragZoom(false);
            PART_Plot.UserInputProcessor.LeftClickDragPan(false);
            PART_Plot.UserInputProcessor.Disable();

            PART_Plot.SizeChanged += (_, __) =>
            {
                PART_Plot.Plot.Axes.AutoScale();
                PART_Plot.Refresh();
            };
        }

        private static void OnDataChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        ) => (d as PiePlotViewer)?.Render();

        private static void OnBackgroundColorHexChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        ) => (d as PiePlotViewer)?.Render();

        private void Render()
        {
            if (PART_Plot?.Plot is null || Values is null || Labels is null)
                return;

            if (Values.Count != Labels.Count)
                return;

            var plt = PART_Plot.Plot;
            plt.Clear();

            // 隐藏轴与网格
            plt.Axes.Frameless();
            plt.HideGrid();
            plt.FigureBackground = new BackgroundStyle()
            {
                Color = ScottPlot.Color.FromHex(BackgroundColorHex),
            };

            var pie = plt.Add.Pie(Values);

            // 为每个参数设置不同标签和图例
            double total = pie.Slices.Select(x => x.Value).Sum();
            for (int i = 0; i < pie.Slices.Count; i++)
            {
                pie.Slices[i].LabelFontSize = 20;
                pie.Slices[i].LegendText = $"{Labels[i]} " + $"({pie.Slices[i].Value / total:p1})";
            }

            ConfigureLegend(plt, Labels.Count);

            plt.Font.Automatic();
            plt.Axes.AutoScale();

            PART_Plot.Refresh();
        }

        private void ConfigureLegend(Plot plt, int itemCount)
        {
            // 移除旧 legend，避免叠加
            RemoveLegendIfExists(plt);

            if (itemCount >= LegendWrapThreshold)
            {
                // 多项：放到底部 + 横向 + 换行（更不容易挤压饼图）
                _legendPanel = plt.ShowLegend(Edge.Bottom);
                plt.Legend.Orientation = ScottPlot.Orientation.Horizontal;
            }
            else
            {
                // 少项：放到右侧竖向，阅读更直观
                _legendPanel = plt.ShowLegend(Edge.Right);
                plt.Legend.Orientation = ScottPlot.Orientation.Vertical;
            }

            // 你可以按需要微调图例字体（避免过长时太挤）
            plt.Legend.FontSize = 12;

            plt.Legend.OutlineWidth = 0;
            plt.Legend.ShadowColor = Colors.Transparent;
            plt.Legend.ShadowOffset = new(0, 0);
        }

        private void RemoveLegendIfExists(Plot plt)
        {
            if (_legendPanel is not null)
            {
                plt.Axes.Remove(_legendPanel);
                _legendPanel = null;
            }
        }
    }
}
