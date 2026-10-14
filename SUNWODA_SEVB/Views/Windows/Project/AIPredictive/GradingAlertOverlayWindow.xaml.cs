using System.Text;
using System.Windows;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Views.Windows.Project.AIPredictive
{
    /// <summary>全屏红色遮罩：展示一级/二级预警轴，确认后关闭。</summary>
    public partial class GradingAlertOverlayWindow : Window
    {
        public bool Confirmed { get; private set; }

        public IReadOnlyList<AIPredictiveGradingAlertItem> Alerts { get; }

        public GradingAlertOverlayWindow(IReadOnlyList<AIPredictiveGradingAlertItem> alerts)
        {
            Alerts = alerts ?? Array.Empty<AIPredictiveGradingAlertItem>();
            InitializeComponent();
            DetailText.Text = BuildDetail(Alerts);
        }

        private static string BuildDetail(IReadOnlyList<AIPredictiveGradingAlertItem> alerts)
        {
            var sb = new StringBuilder();
            var i = 1;
            foreach (var a in alerts)
            {
                sb.Append(i++).Append(". [").Append(a.Level).Append("] ");
                sb.Append("工位=").Append(a.Station).Append("  ");
                sb.Append(string.IsNullOrWhiteSpace(a.Mechanism) ? a.Axis : a.Mechanism);
                sb.Append("  风险分=").Append(a.RiskScore.ToString("0.##"));
                if (!string.IsNullOrWhiteSpace(a.EvidenceType))
                    sb.Append("  证据=").Append(a.EvidenceType);
                if (!string.IsNullOrWhiteSpace(a.Advice))
                    sb.AppendLine().Append("    建议：").Append(a.Advice);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
