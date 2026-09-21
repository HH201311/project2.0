using ScottPlot;
using ScottPlot.WPF;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Views.Pages.BoxGluing
{
    /// <summary>
    /// 按出胶工位单图刷新：一口六条线（扭矩A/B、压力A/B、液位A/B）。
    /// </summary>
    internal static class BoxGluingPlotHelper
    {
        public const double DefaultXAxisSeconds = 5;

        private static readonly string ChineseFont = ResolveChineseFont();

        static BoxGluingPlotHelper()
        {
            Fonts.Default = ChineseFont;
        }

        public static void Render(WpfPlot plot, BoxGluingChartSnapshot snapshot, int station)
        {
            var plt = plot.Plot;
            plt.Clear();
            plt.Font.Set(ChineseFont);

            plt.Title($"出胶{station}");
            plt.Axes.Bottom.Label.Text = "时间 (s)";
            plt.Axes.Left.Label.Text = "过程值";
            plt.Legend.IsVisible = true;
            plt.Legend.Alignment = Alignment.UpperRight;
            plt.Legend.FontName = ChineseFont;
            plt.Axes.Title.Label.FontName = ChineseFont;
            plt.Axes.Bottom.Label.FontName = ChineseFont;
            plt.Axes.Left.Label.FontName = ChineseFont;
            plt.Axes.Bottom.TickLabelStyle.FontName = ChineseFont;
            plt.Axes.Left.TickLabelStyle.FontName = ChineseFont;

            var live = snapshot.Series
                .Where(s => s.Meter == station && s.Xs.Length > 0)
                .OrderBy(s => GroupOrder(s.Group))
                .ThenBy(s => s.Side, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var x0 = live.Count > 0 ? live.Min(s => s.Xs[0]) : 0;
            var xMax = 0.0;

            foreach (var series in live)
            {
                var xs = new double[series.Xs.Length];
                for (var i = 0; i < series.Xs.Length; i++)
                    xs[i] = Math.Max(0, series.Xs[i] - x0);

                if (xs.Length > 0)
                    xMax = Math.Max(xMax, xs[^1]);

                var scatter = plt.Add.Scatter(xs, series.Ys);
                scatter.LegendText = series.Label;
                scatter.MarkerSize = 0;
                scatter.LineWidth = 1.8f;
                scatter.Color = SeriesColor(series.Group);
                scatter.LinePattern = string.Equals(series.Side, "B", StringComparison.OrdinalIgnoreCase)
                    ? LinePattern.Dashed
                    : LinePattern.Solid;
            }

            plt.Font.Automatic();

            var xRight = Math.Max(xMax * 1.02, DefaultXAxisSeconds);
            plt.Axes.SetLimitsX(0, xRight);
            // 下限 -30：PLC 过程量可能为负；上限 100 保持固定不抖动
            plt.Axes.SetLimitsY(-30, 100);

            plot.Refresh();
        }

        private static int GroupOrder(string group) => group switch
        {
            "Torque" => 0,
            "Pressure" => 1,
            "Level" => 2,
            _ => 9
        };

        private static Color SeriesColor(string group) => group switch
        {
            "Torque" => Colors.RoyalBlue,
            "Pressure" => Colors.Orange,
            "Level" => Colors.ForestGreen,
            _ => Colors.Gray
        };

        private static string ResolveChineseFont()
        {
            try
            {
                var detected = Fonts.Detect("出胶工位扭矩压力液位");
                if (!string.IsNullOrWhiteSpace(detected))
                    return detected;
            }
            catch
            {
                // 回退微软雅黑
            }

            return "Microsoft YaHei";
        }
    }
}
