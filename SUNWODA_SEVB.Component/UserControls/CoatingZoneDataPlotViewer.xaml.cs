using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using ScottPlot.Plottables;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// CoatingZoneDataPlotViewer.xaml 的交互逻辑
    /// </summary>
    public partial class CoatingZoneDataPlotViewer : UserControl
    {
        private List<double[]> _zones = new(); // 每个元素代表一个膜区的数值数组
        private List<Bar> _bars = new(); // ScottPlot 柱对象列表
        private List<Text> _labels = new();

        // 规格/参考线
        private double dangerMaxLimit = 3.6;
        private double dangerMinLimit = -3.6;
        private double centerValue = 0.0;
        private double warnMaxLimit = 3.0;
        private double warnMinLimit = -3.0;

        public CoatingZoneDataPlotViewer()
        {
            InitializeComponent();
        }

        public double LabelFontSize
        {
            get { return (double)GetValue(LabelFontSizeProperty); }
            set { SetValue(LabelFontSizeProperty, value); }
        }

        public static readonly DependencyProperty LabelFontSizeProperty =
            DependencyProperty.Register(
                "LabelFontSize",
                typeof(double),
                typeof(CoatingZoneDataPlotViewer),
                new PropertyMetadata(16.0, OnLabelFontSizeChanged)
            );

        private static void OnLabelFontSizeChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CoatingZoneDataPlotViewer viewer)
            {
                viewer.UpdateBar();
                viewer.ShowWeightBar();
            }
        }

        public List<double[]> FilmData
        {
            get => (List<double[]>)GetValue(FilmDataProperty);
            set => SetValue(FilmDataProperty, value);
        }

        public static readonly DependencyProperty FilmDataProperty = DependencyProperty.Register(
            nameof(FilmData),
            typeof(List<double[]>),
            typeof(CoatingZoneDataPlotViewer),
            new PropertyMetadata(null, OnFilmDataChanged)
        );

        private static void OnFilmDataChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CoatingZoneDataPlotViewer viewer)
            {
                viewer.SetFilmData();
                viewer.UpdateBar();
                viewer.ShowWeightBar();
            }
        }

        private void SetFilmData()
        {
            _zones.Clear();

            if (FilmData == null || FilmData.Count == 0)
                return;

            foreach (var arr in FilmData)
                _zones.Add(arr ?? Array.Empty<double>());
        }

        public Dictionary<string, double> RefValue
        {
            get => (Dictionary<string, double>)GetValue(RefValueProperty);
            set { SetValue(RefValueProperty, value); }
        }

        public static readonly DependencyProperty RefValueProperty = DependencyProperty.Register(
            nameof(RefValue),
            typeof(Dictionary<string, double>),
            typeof(CoatingZoneDataPlotViewer),
            new PropertyMetadata(null, OnRefValueChanged)
        );

        private static void OnRefValueChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CoatingZoneDataPlotViewer viewer)
                viewer.SetRefValue();
        }

        private void SetRefValue()
        {
            // 兼容键缺失的情况，使用旧值作为默认
            if (RefValue == null)
                return;

            if (RefValue.TryGetValue("dangerMaxLimit", out var v1))
                dangerMaxLimit = v1;
            if (RefValue.TryGetValue("dangerMinLimit", out var v2))
                dangerMinLimit = v2;
            if (RefValue.TryGetValue("centerValue", out var v3))
                centerValue = v3;
            if (RefValue.TryGetValue("warnMaxLimit", out var v4))
                warnMaxLimit = v4;
            if (RefValue.TryGetValue("warnMinLimit", out var v5))
                warnMinLimit = v5;

            // 参考线变化后需要重绘
            ShowWeightBar();
        }

        public double BaseValue
        {
            get { return (double)GetValue(BaseValueProperty); }
            set { SetValue(BaseValueProperty, value); }
        }

        public static readonly DependencyProperty BaseValueProperty = DependencyProperty.Register(
            "BaseValue",
            typeof(double),
            typeof(CoatingZoneDataPlotViewer),
            new PropertyMetadata(300.0, new PropertyChangedCallback(OnBaseValueChanged))
        );

        private static void OnBaseValueChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CoatingZoneDataPlotViewer viewer)
                viewer.ShowWeightBar();
        }

        // 区间分隔空柱数量（默认 3）
        public int SeparatorSlots
        {
            get => (int)GetValue(SeparatorSlotsProperty);
            set => SetValue(SeparatorSlotsProperty, Math.Max(0, value));
        }

        public static readonly DependencyProperty SeparatorSlotsProperty =
            DependencyProperty.Register(
                nameof(SeparatorSlots),
                typeof(int),
                typeof(CoatingZoneDataPlotViewer),
                new PropertyMetadata(3, OnSeparatorSlotsChanged)
            );

        private static void OnSeparatorSlotsChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is CoatingZoneDataPlotViewer viewer)
            {
                viewer.UpdateBar();
                viewer.ShowWeightBar();
            }
        }

        // ===== 柱状体生成 =====
        public void UpdateBar()
        {
            _bars = new List<Bar>();
            _labels = new List<Text>();
            int pos = 0;

            if (_zones.Count == 0)
                return;

            for (int zoneIndex = 0; zoneIndex < _zones.Count; zoneIndex++)
            {
                var zone = _zones[zoneIndex] ?? Array.Empty<double>();

                // 当前膜区的柱
                for (int i = 0; i < zone.Length; i++)
                {
                    _bars.Add(
                        new Bar
                        {
                            Value = zone[i],
                            Position = pos,
                            FillColor = GetBarColor(zone[i]),
                            LineWidth = 0,
                        }
                    );

                    _labels.Add(
                        new Text
                        {
                            Location = new Coordinates(pos - 0.5, zone[i]),
                            LabelRotation = -45,
                            LabelText = $"{(BaseValue + zone[i]):F2}",
                            LabelFontSize = (float)LabelFontSize,
                        }
                    );
                    pos++;
                }

                // zone 间分隔（最后一个 zone 不加）
                if (zoneIndex < _zones.Count - 1 && SeparatorSlots > 0)
                {
                    for (int s = 0; s < SeparatorSlots; s++)
                    {
                        _bars.Add(
                            new Bar
                            {
                                Value = 0.0,
                                Position = pos++,
                                FillColor = ScottPlot.Color.FromColor(
                                    System.Drawing.Color.Transparent
                                ),
                                LineWidth = 0,
                            }
                        );
                    }
                }
            }
        }

        // ===== 颜色规则 =====
        public ScottPlot.Color GetBarColor(double valueNum)
        {
            var transparent = ScottPlot.Color.FromColor(System.Drawing.Color.Transparent);
            // > dangerMax
            if (valueNum > dangerMaxLimit)
                return ScottPlot.Color.FromHtml("#FF3333");
            // (dangerMax, warnMax]
            if (valueNum <= dangerMaxLimit && valueNum > warnMaxLimit)
                return ScottPlot.Color.FromHtml("#FFBB33");
            // (warnMax, center]
            if (valueNum <= warnMaxLimit && valueNum > centerValue)
                return ScottPlot.Color.FromHtml("#29CC7A");
            // (center, warnMin]
            if (valueNum <= centerValue && valueNum > warnMinLimit)
                return ScottPlot.Color.FromHtml("#29CC7A");
            // (warnMin, dangerMin]
            if (valueNum <= warnMinLimit && valueNum > dangerMinLimit)
                return ScottPlot.Color.FromHtml("#FFBB33");
            // < dangerMin
            if (valueNum <= dangerMinLimit)
                return ScottPlot.Color.FromHtml("#FF3333");

            return transparent;
        }

        // ===== 绘制 =====
        public void ShowWeightBar()
        {
            // 不允许使用上下文菜单、缩放/拖动
            this.weightBar.Menu?.Clear();
            this.weightBar.UserInputProcessor.RightClickDragZoom(false);
            this.weightBar.UserInputProcessor.LeftClickDragPan(false);
            this.weightBar.UserInputProcessor.Disable();

            // 显示画板
            this.weightBar.Visibility = Visibility.Visible;

            // 绘图
            Plot plt = this.weightBar.Plot;
            plt.Clear();

            // 参考线
            var zeroHLine = plt.Add.HorizontalLine(
                centerValue,
                0.1f,
                ScottPlot.Color.FromHtml("#29CC7A")
            );
            zeroHLine.Text = $"{(BaseValue + centerValue):F2}";
            zeroHLine.LabelFontSize = (float)LabelFontSize;
            zeroHLine.LabelRotation = 0;
            zeroHLine.LabelBackgroundColor = zeroHLine.Color;
            zeroHLine.LineWidth = 1;
            zeroHLine.IsDraggable = false;
            zeroHLine.LabelOffsetX = 15;

            var warningMinHLine = plt.Add.HorizontalLine(
                warnMinLimit,
                0.1f,
                ScottPlot.Color.FromHtml("#FFBB33"),
                LinePattern.Dashed
            );
            warningMinHLine.Text = $"{(BaseValue + warnMinLimit):F2}";
            warningMinHLine.LabelFontSize = (float)LabelFontSize;
            warningMinHLine.LabelRotation = 0;
            warningMinHLine.LabelAlignment = Alignment.MiddleLeft;
            warningMinHLine.LabelBackgroundColor = warningMinHLine.Color;
            warningMinHLine.LineWidth = 1;
            warningMinHLine.IsDraggable = false;
            warningMinHLine.LabelOffsetX = 15;

            var warningMaxHLine = plt.Add.HorizontalLine(
                warnMaxLimit,
                0.1f,
                ScottPlot.Color.FromHtml("#FFBB33"),
                LinePattern.Dashed
            );
            warningMaxHLine.Text = $"{(BaseValue + warnMaxLimit):F2}";
            warningMaxHLine.LabelFontSize = (float)LabelFontSize;
            warningMaxHLine.LabelRotation = 0;
            warningMaxHLine.LabelAlignment = Alignment.MiddleLeft;
            warningMaxHLine.LabelBackgroundColor = warningMaxHLine.Color;
            warningMaxHLine.LineWidth = 1;
            warningMaxHLine.IsDraggable = false;
            warningMaxHLine.LabelOffsetX = 15;

            var errorMinHLine = plt.Add.HorizontalLine(
                dangerMinLimit,
                0.1f,
                ScottPlot.Color.FromHtml("#FF3333"),
                LinePattern.Dashed
            );
            errorMinHLine.Text = $"{(BaseValue + dangerMinLimit):F2}";
            errorMinHLine.LabelFontSize = (float)LabelFontSize;
            errorMinHLine.LabelRotation = 0;
            errorMinHLine.LabelAlignment = Alignment.MiddleLeft;
            errorMinHLine.LabelBackgroundColor = errorMinHLine.Color;
            errorMinHLine.LineWidth = 1;
            errorMinHLine.IsDraggable = false;
            errorMinHLine.LabelOffsetX = 15;

            var errorMaxHLine = plt.Add.HorizontalLine(
                dangerMaxLimit,
                0.1f,
                ScottPlot.Color.FromHtml("#FF3333"),
                LinePattern.Dashed
            );
            errorMaxHLine.Text = $"{(BaseValue + dangerMaxLimit):F2}";
            errorMaxHLine.LabelFontSize = (float)LabelFontSize;
            errorMaxHLine.LabelRotation = 0;
            errorMaxHLine.LabelAlignment = Alignment.MiddleLeft;
            errorMaxHLine.LabelBackgroundColor = errorMaxHLine.Color;
            errorMaxHLine.LineWidth = 1;
            errorMaxHLine.IsDraggable = false;
            errorMaxHLine.LabelOffsetX = 15;

            // 隐藏轴与网格
            plt.Axes.Bottom.IsVisible = false;
            plt.Axes.Top.IsVisible = false;
            plt.Axes.Left.IsVisible = true;
            plt.Axes.Left.FrameLineStyle.Color = Colors.Transparent;
            plt.Axes.Left.TickLabelStyle.IsVisible = false;
            plt.Axes.Left.RemoveTickGenerator();
            plt.Axes.Right.IsVisible = false;
            plt.Grid.IsVisible = false;

            if (_bars.Count > 0)
                plt.Add.Bars(_bars);
            if (_labels.Count > 0)
                foreach (var label in _labels)
                {
                    plt.Add.Plottable(label);
                }

            plt.Axes.AutoScale();

            this.weightBar.Refresh();
        }
    }
}
