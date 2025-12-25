using System;
using System.Collections.Generic;
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
using ScottPlot;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// MmTrendPlot.xaml 的交互逻辑
    /// </summary>
    public partial class MmTrendPlot : UserControl
    {
        public MmTrendPlot()
        {
            InitializeComponent();
            Loaded += (_, __) => Render();
        }

        #region Dependency Properties

        public List<double>? XValues
        {
            get => (List<double>?)GetValue(XValuesProperty);
            set => SetValue(XValuesProperty, value);
        }
        public static readonly DependencyProperty XValuesProperty = DependencyProperty.Register(
            nameof(XValues),
            typeof(List<double>),
            typeof(MmTrendPlot),
            new PropertyMetadata(null, OnDataChanged)
        );

        public List<double>? YValues
        {
            get => (List<double>?)GetValue(YValuesProperty);
            set => SetValue(YValuesProperty, value);
        }
        public static readonly DependencyProperty YValuesProperty = DependencyProperty.Register(
            nameof(YValues),
            typeof(List<double>),
            typeof(MmTrendPlot),
            new PropertyMetadata(null, OnDataChanged)
        );

        public double Baseline
        {
            get => (double)GetValue(BaselineProperty);
            set => SetValue(BaselineProperty, value);
        }
        public static readonly DependencyProperty BaselineProperty = DependencyProperty.Register(
            nameof(Baseline),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(0.0, OnStyleChanged)
        );

        public double WarnLow
        {
            get => (double)GetValue(WarnLowProperty);
            set => SetValue(WarnLowProperty, value);
        }
        public static readonly DependencyProperty WarnLowProperty = DependencyProperty.Register(
            nameof(WarnLow),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(-0.2, OnStyleChanged)
        );

        public double WarnHigh
        {
            get => (double)GetValue(WarnHighProperty);
            set => SetValue(WarnHighProperty, value);
        }
        public static readonly DependencyProperty WarnHighProperty = DependencyProperty.Register(
            nameof(WarnHigh),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(0.2, OnStyleChanged)
        );

        public double SpecLow
        {
            get => (double)GetValue(SpecLowProperty);
            set => SetValue(SpecLowProperty, value);
        }
        public static readonly DependencyProperty SpecLowProperty = DependencyProperty.Register(
            nameof(SpecLow),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(-0.4, OnStyleChanged)
        );

        public double SpecHigh
        {
            get => (double)GetValue(SpecHighProperty);
            set => SetValue(SpecHighProperty, value);
        }
        public static readonly DependencyProperty SpecHighProperty = DependencyProperty.Register(
            nameof(SpecHigh),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(0.4, OnStyleChanged)
        );

        public bool ShowLegend
        {
            get => (bool)GetValue(ShowLegendProperty);
            set => SetValue(ShowLegendProperty, value);
        }
        public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(
            nameof(ShowLegend),
            typeof(bool),
            typeof(MmTrendPlot),
            new PropertyMetadata(true, OnStyleChanged)
        );

        public bool ShowFills
        {
            get => (bool)GetValue(ShowFillsProperty);
            set => SetValue(ShowFillsProperty, value);
        }
        public static readonly DependencyProperty ShowFillsProperty = DependencyProperty.Register(
            nameof(ShowFills),
            typeof(bool),
            typeof(MmTrendPlot),
            new PropertyMetadata(true, OnStyleChanged)
        );

        public bool AutoFitOnDataChange
        {
            get => (bool)GetValue(AutoFitOnDataChangeProperty);
            set => SetValue(AutoFitOnDataChangeProperty, value);
        }
        public static readonly DependencyProperty AutoFitOnDataChangeProperty =
            DependencyProperty.Register(
                nameof(AutoFitOnDataChange),
                typeof(bool),
                typeof(MmTrendPlot),
                new PropertyMetadata(true, OnDataChanged)
            );

        public double LineWidth
        {
            get => (double)GetValue(LineWidthProperty);
            set => SetValue(LineWidthProperty, value);
        }
        public static readonly DependencyProperty LineWidthProperty = DependencyProperty.Register(
            nameof(LineWidth),
            typeof(double),
            typeof(MmTrendPlot),
            new PropertyMetadata(2.0, OnStyleChanged)
        );



        public double LabelFontSize
        {
            get { return (double)GetValue(LabelFontSizeProperty); }
            set { SetValue(LabelFontSizeProperty, value); }
        }

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register("LabelFontSize", typeof(double), typeof(MmTrendPlot), new PropertyMetadata(16.0, OnStyleChanged));

        #endregion

        private static void OnDataChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        ) => (d as MmTrendPlot)?.Render();

        private static void OnStyleChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        ) => (d as MmTrendPlot)?.Render();

        // === 渲染主流程 ===
        private void Render()
        {
            if (!IsLoaded || PART_Plot?.Plot is null)
                return;

            // 不允许使用上下文菜单、缩放/拖动
            PART_Plot.Menu?.Clear();
            PART_Plot.UserInputProcessor.RightClickDragZoom(false);
            PART_Plot.UserInputProcessor.LeftClickDragPan(false);
            PART_Plot.UserInputProcessor.Disable();

            var plt = PART_Plot.Plot;
            plt.Clear();

            // 隐藏轴与网格
            plt.Axes.Bottom.IsVisible = false;
            plt.Axes.Top.IsVisible = false;
            plt.Axes.Left.IsVisible = true;
            plt.Axes.Left.FrameLineStyle.Color = ScottPlot.Colors.Transparent;
            plt.Axes.Left.TickLabelStyle.IsVisible = false;
            plt.Axes.Left.RemoveTickGenerator();
            plt.Axes.Right.IsVisible = false;
            plt.Grid.IsVisible = false;

            // 数据校验
            if (YValues is not { Count: > 1 })
            {
                //PART_Plot.Refresh();
                return;
            }

            // 构造 X
            double[] xs;
            if (XValues is { Count: > 1 } && XValues.Count == YValues.Count)
                xs = XValues.Select(v => (double)v).ToArray();
            else
                xs = Enumerable.Range(0, YValues.Count).Select(i => (double)i).ToArray();

            double[] ys = YValues.Select(v => (double)v).ToArray();

            // 曲线（分色）
            AddColoredLine(plt, xs, ys);

            // 限线与背景
            AddLimitLines(plt);
            if (ShowFills)
                AddFills(plt);


            
            // Legend
            plt.Legend.IsVisible = ShowLegend;
            plt.Legend.Alignment = ScottPlot.Alignment.UpperRight;

            if (AutoFitOnDataChange)
                plt.Axes.AutoScale(true, false);

            plt.Axes.SetLimitsY(SpecLow + (SpecLow - Baseline) * 0.25, SpecHigh + (SpecHigh - Baseline) * 0.25);

            PART_Plot.Refresh();
        }

        #region 绘制细节（分色与限线）
        private enum BandColor
        {
            Green,
            Yellow,
            Red,
        }

        private BandColor GetBand(double y)
        {
            if (y >= WarnLow && y <= WarnHigh)
                return BandColor.Green;
            if (y >= SpecLow && y <= SpecHigh)
                return BandColor.Yellow;
            return BandColor.Red;
        }

        private static double? IntersectXWithHorizontal(
            double x0,
            double y0,
            double x1,
            double y1,
            double yLine
        )
        {
            if ((y0 < yLine && y1 < yLine) || (y0 > yLine && y1 > yLine))
                return null;
            if (Math.Abs(y1 - y0) < 1e-12)
                return null;
            double t = (yLine - y0) / (y1 - y0);
            if (t <= 0 || t >= 1)
                return null;
            return x0 + t * (x1 - x0);
        }

        private static List<(double xa, double ya, double xb, double yb)> SplitByLimits(
            double x0,
            double y0,
            double x1,
            double y1,
            double[] yLines
        )
        {
            var cuts = new List<(double x, double y)>();
            foreach (double yl in yLines)
            {
                var xi = IntersectXWithHorizontal(x0, y0, x1, y1, yl);
                if (xi is double xInt)
                    cuts.Add((xInt, yl));
            }
            cuts.Add((x0, y0));
            cuts.Add((x1, y1));
            cuts = cuts.Distinct().OrderBy(p => p.x).ToList();

            var segs = new List<(double, double, double, double)>();
            for (int i = 0; i < cuts.Count - 1; i++)
                segs.Add((cuts[i].x, cuts[i].y, cuts[i + 1].x, cuts[i + 1].y));
            return segs;
        }

        private void AddColoredLine(Plot plot, double[] xs, double[] ys)
        {
            var lines = new[] { WarnLow, WarnHigh, SpecLow, SpecHigh }
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            var greenRuns = new List<(List<double> X, List<double> Y)>();
            var yellowRuns = new List<(List<double> X, List<double> Y)>();
            var redRuns = new List<(List<double> X, List<double> Y)>();

            List<(List<double> X, List<double> Y)> GetRuns(BandColor c) =>
                c switch
                {
                    BandColor.Green => greenRuns,
                    BandColor.Yellow => yellowRuns,
                    _ => redRuns,
                };

            void Append(List<(List<double> X, List<double> Y)> runs, double x, double y)
            {
                if (runs.Count == 0 || runs.Last().X.Count == 0)
                    runs.Add((new List<double>(), new List<double>()));
                runs.Last().X.Add(x);
                runs.Last().Y.Add(y);
            }

            void StartNew(BandColor c, double x, double y)
            {
                var r = GetRuns(c);
                r.Add((new List<double>(), new List<double>()));
                r.Last().X.Add(x);
                r.Last().Y.Add(y);
            }

            BandColor? lastColor = null;

            for (int i = 0; i < xs.Length - 1; i++)
            {
                var segs = SplitByLimits(xs[i], ys[i], xs[i + 1], ys[i + 1], lines);

                foreach (var (xa, ya, xb, yb) in segs)
                {
                    double xm = 0.5 * (xa + xb);
                    double ym = 0.5 * (ya + yb);
                    var color = GetBand(ym);

                    if (lastColor is null || lastColor != color)
                        StartNew(color, xa, ya);

                    Append(GetRuns(color), xb, yb);
                    lastColor = color;
                }
            }

            void PlotRuns(
                List<(List<double> X, List<double> Y)> runs,
                ScottPlot.Color color,
                string label
            )
            {
                foreach (var (X, Y) in runs)
                {
                    if (X.Count < 2)
                        continue;
                    var sc = plot.Add.ScatterLine(X.ToArray(), Y.ToArray());
                    sc.Color = color;
                    sc.LineWidth = (float)LineWidth;
                    sc.LegendText = label;
                }
            }

            PlotRuns(greenRuns, ScottPlot.Color.FromHtml("#29CC7A"), "OK（警告内）");
            PlotRuns(yellowRuns, ScottPlot.Color.FromHtml("#FFBB33"), "接近超差（警告↔规格）");
            PlotRuns(redRuns, ScottPlot.Color.FromHtml("#FF3333"), "超出规格");
        }

        private void AddLimitLines(Plot plot)
        {
            var baseLine = plot.Add.HorizontalLine(Baseline, 1.0f, ScottPlot.Color.FromHtml("#29CC7A"));
            baseLine.Text = $"{Baseline:F2}";
            baseLine.LabelFontSize = (float)LabelFontSize;
            baseLine.LabelRotation = 0;
            baseLine.LabelAlignment = Alignment.MiddleLeft;
            baseLine.LabelOffsetX = 15;

            var wl = plot.Add.HorizontalLine(
                WarnLow,
                1.0f,
                ScottPlot.Color.FromHtml("#FFBB33"),
                LinePattern.Dashed
            );
            wl.Text = $"{WarnLow:F2}";
            wl.LabelFontSize = (float)LabelFontSize;
            wl.LabelRotation = 0;
            wl.LabelAlignment = Alignment.MiddleLeft;
            wl.LabelOffsetX = 15;

            var wh = plot.Add.HorizontalLine(
                WarnHigh,
                1.0f,
                ScottPlot.Color.FromHtml("#FFBB33"),
                LinePattern.Dashed
            );
            wh.Text = $"{WarnHigh:F2}";
            wh.LabelFontSize = (float)LabelFontSize;
            wh.LabelRotation = 0;
            wh.LabelAlignment = Alignment.MiddleLeft;
            wh.LabelOffsetX = 15;

            var sl = plot.Add.HorizontalLine(
                SpecLow,
                1.0f,
                ScottPlot.Color.FromHtml("#FF3333"),
                LinePattern.Dashed
            );
            sl.Text = $"{SpecLow:F2}";
            sl.LabelFontSize = (float)LabelFontSize;
            sl.LabelRotation = 0;
            sl.LabelAlignment = Alignment.MiddleLeft;
            sl.LabelOffsetX = 15;

            var sh = plot.Add.HorizontalLine(
                SpecHigh,
                1.0f,
                ScottPlot.Color.FromHtml("#FF3333"),
                LinePattern.Dashed
            );
            sh.Text = $"{SpecHigh:F2}";
            sh.LabelFontSize = (float)LabelFontSize;
            sh.LabelRotation = 0;
            sh.LabelAlignment = Alignment.MiddleLeft;
            sh.LabelOffsetX = 15;
        }

        private void AddFills(Plot plot)
        {
            var greenFill = plot.Add.HorizontalSpan(
                WarnLow,
                WarnHigh,
                ScottPlot.Colors.SeaGreen.WithOpacity(30)
            );

            var yellowTop = plot.Add.HorizontalSpan(
                WarnHigh,
                SpecHigh,
                ScottPlot.Colors.GoldenRod.WithOpacity(20)
            );

            var yellowBot = plot.Add.HorizontalSpan(
                SpecLow,
                WarnLow,
                ScottPlot.Colors.GoldenRod.WithOpacity(20)
            );
        }
        #endregion
    }
}
