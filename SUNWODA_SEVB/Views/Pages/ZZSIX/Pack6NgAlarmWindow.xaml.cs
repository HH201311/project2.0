using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    /// <summary>
    /// Pack 六面检缺陷报警：扫码与产品 SN 一致才可关闭；禁止手动关窗。
    /// </summary>
    public partial class Pack6NgAlarmWindow : Window
    {
        /// <summary>已知缺陷标题前缀，用于把空格拼接的多条拆开并换行展示。</summary>
        private static readonly Regex DefectTitleSplitRegex = new(
            @"(?=(?:产品码与文件夹不一致|二维码与文件夹不一致|三码不一致|三码校验失败|两白标签产品码不一致|两白标签二维码不一致|长度不符|前十位不符|缺张超时|视觉检测异常|视觉算法异常))",
            RegexOptions.Compiled);

        private bool _allowClose;
        private string _expectedSn = string.Empty;

        public Pack6NgAlarmWindow()
        {
            InitializeComponent();
        }

        /// <summary>绑定展示内容；时间取弹窗瞬间并固定。</summary>
        public void Bind(string productSn, string defectText, DateTime alarmTime)
        {
            _expectedSn = (productSn ?? string.Empty).Trim();
            ProductSnText.Text = string.IsNullOrEmpty(_expectedSn) ? "-" : _expectedSn;
            DefectTypeText.Text = FormatDefectDisplay(defectText);
            AlarmTimeText.Text = alarmTime.ToString("yyyy-MM-dd HH:mm:ss");
            Title = "Pack六面检测缺陷报警";
        }

        /// <summary>
        /// 缺陷标题与码值分行；多条缺陷分行；保证可读且配合限高滚动区。
        /// </summary>
        internal static string FormatDefectDisplay(string? defectText)
        {
            if (string.IsNullOrWhiteSpace(defectText))
                return "检测NG";

            var raw = defectText.Trim().Replace("\r\n", "\n").Replace('\r', '\n');
            var chunks = SplitDefectChunks(raw);
            var lines = new List<string>();
            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk))
                    continue;
                FormatOneDefect(chunk.Trim(), lines);
            }

            return lines.Count == 0 ? "检测NG" : string.Join(Environment.NewLine, lines);
        }

        private static List<string> SplitDefectChunks(string raw)
        {
            // 优先按换行；兼容历史空格拼接
            if (raw.Contains('\n'))
            {
                return raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }

            var parts = DefectTitleSplitRegex.Split(raw)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
            return parts.Count > 0 ? parts : [raw];
        }

        private static void FormatOneDefect(string chunk, List<string> lines)
        {
            // 产品码与文件夹不一致:文件夹=a,产品码=b
            if (TrySplitTitleBody(chunk, "产品码与文件夹不一致", out var snVsFolder))
            {
                lines.Add("产品码与文件夹不一致");
                AppendKeyValues(snVsFolder, lines);
                return;
            }

            if (TrySplitTitleBody(chunk, "二维码与文件夹不一致", out var qrVsFolder))
            {
                lines.Add("二维码与文件夹不一致");
                AppendKeyValues(qrVsFolder, lines);
                return;
            }

            // 三码不一致:文件夹=a,白标签=b,铭牌=c,二维码=d（兼容旧文案）
            if (TrySplitTitleBody(chunk, "三码不一致", out var threeBody))
            {
                lines.Add("三码不一致");
                AppendKeyValues(threeBody, lines);
                return;
            }

            if (TrySplitTitleBody(chunk, "三码校验失败", out var failBody))
            {
                lines.Add("三码校验失败");
                if (!string.IsNullOrWhiteSpace(failBody))
                    lines.Add(failBody.Trim());
                return;
            }

            // 两白标签产品码不一致:A≠B
            if (TrySplitTitleBody(chunk, "两白标签产品码不一致", out var wlSnBody))
            {
                lines.Add("两白标签产品码不一致");
                AppendNotEqualPair(wlSnBody, lines);
                return;
            }

            if (TrySplitTitleBody(chunk, "两白标签二维码不一致", out var wlQrBody))
            {
                lines.Add("两白标签二维码不一致");
                AppendNotEqualPair(wlQrBody, lines);
                return;
            }

            if (TrySplitTitleBody(chunk, "长度不符", out var lenBody))
            {
                lines.Add("长度不符");
                AppendKeyValues(lenBody, lines);
                return;
            }

            if (TrySplitTitleBody(chunk, "前十位不符", out var prefixBody))
            {
                lines.Add("前十位不符");
                AppendKeyValues(prefixBody, lines);
                return;
            }

            // 缺张等：按中文分号再拆
            if (chunk.Contains('；') || chunk.Contains(';'))
            {
                foreach (var part in chunk.Split(['；', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    lines.Add(part);
                return;
            }

            lines.Add(chunk);
        }

        private static bool TrySplitTitleBody(string chunk, string title, out string body)
        {
            body = string.Empty;
            if (chunk.Equals(title, StringComparison.Ordinal))
                return true;
            if (chunk.StartsWith(title + ":", StringComparison.Ordinal) ||
                chunk.StartsWith(title + "：", StringComparison.Ordinal))
            {
                body = chunk[(title.Length + 1)..];
                return true;
            }

            return false;
        }

        private static void AppendKeyValues(string body, List<string> lines)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;
            foreach (var kv in body.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (kv.Length > 0)
                    lines.Add(kv);
            }
        }

        private static void AppendNotEqualPair(string body, List<string> lines)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;
            var idx = body.IndexOf('≠');
            if (idx < 0)
            {
                lines.Add(body.Trim());
                return;
            }

            var left = body[..idx].Trim();
            var right = body[(idx + 1)..].Trim();
            if (left.Length > 0)
                lines.Add(left);
            lines.Add("≠");
            if (right.Length > 0)
                lines.Add(right);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ScanInputBox.Focus();
            Keyboard.Focus(ScanInputBox);
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
                e.Cancel = true;
        }

        private void OnScanTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
            TryConfirmByScan();

        private void OnScanPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                TryConfirmByScan();
            }
        }

        private void TryConfirmByScan()
        {
            if (string.IsNullOrEmpty(_expectedSn))
                return;

            var input = (ScanInputBox.Text ?? string.Empty).Trim();
            if (!string.Equals(input, _expectedSn, StringComparison.OrdinalIgnoreCase))
                return;

            _allowClose = true;
            Close();
        }

        /// <summary>页面卸载时强制关窗（仍会触发 Closed → PLC 计数）。</summary>
        public void ForceCloseForPageUnload()
        {
            _allowClose = true;
            Close();
        }

        /// <summary>兼容旧调用（已废弃列表模式）。</summary>
        public void UpdateNgList(IReadOnlyList<string> productCodes)
        {
            if (productCodes is null || productCodes.Count == 0)
                return;
            Bind(productCodes[^1], "检测NG", DateTime.Now);
        }
    }
}
