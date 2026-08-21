using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏点位标定：左键确认屏幕物理像素坐标，右键/Esc 取消。
    /// </summary>
    public partial class ScreenPointPickerOverlayWindow : Window
    {
        private readonly DispatcherTimer _moveTimer;
        private bool _confirmed;

        public int SelectedX { get; private set; }
        public int SelectedY { get; private set; }

        public ScreenPointPickerOverlayWindow(string? hintText = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(hintText))
            {
                HintTextBlock.Text = hintText;
            }

            _moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _moveTimer.Tick += (_, _) => UpdateFromCursor();
            Loaded += OnLoaded;
            Closed += OnClosed;
            KeyDown += OnKeyDown;
            PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
            PreviewMouseRightButtonDown += (_, e) =>
            {
                e.Handled = true;
                CancelAndClose();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Activate();
            Focus();
            Keyboard.Focus(this);
            _moveTimer.Start();
            UpdateFromCursor();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _moveTimer.Stop();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelAndClose();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!GetCursorPos(out var pt))
            {
                return;
            }

            _confirmed = true;
            SelectedX = pt.X;
            SelectedY = pt.Y;
            DialogResult = true;
            Close();
        }

        private void CancelAndClose()
        {
            if (_confirmed)
            {
                return;
            }

            DialogResult = false;
            Close();
        }

        private void UpdateFromCursor()
        {
            if (!GetCursorPos(out var pt))
            {
                return;
            }

            CoordText.Text = $"({pt.X}, {pt.Y})";

            var local = PointFromScreen(new Point(pt.X, pt.Y));
            var w = ActualWidth > 0 ? ActualWidth : Width;
            var h = ActualHeight > 0 ? ActualHeight : Height;

            CrossH.X1 = 0;
            CrossH.Y1 = local.Y;
            CrossH.X2 = w;
            CrossH.Y2 = local.Y;

            CrossV.X1 = local.X;
            CrossV.Y1 = 0;
            CrossV.X2 = local.X;
            CrossV.Y2 = h;

            var panelW = CoordPanel.ActualWidth > 0 ? CoordPanel.ActualWidth : 120;
            var panelH = CoordPanel.ActualHeight > 0 ? CoordPanel.ActualHeight : 28;
            var left = local.X + 18;
            var top = local.Y + 18;
            if (left + panelW > w - 8)
            {
                left = local.X - panelW - 18;
            }

            if (top + panelH > h - 8)
            {
                top = local.Y - panelH - 18;
            }

            System.Windows.Controls.Canvas.SetLeft(CoordPanel, Math.Max(8, left));
            System.Windows.Controls.Canvas.SetTop(CoordPanel, Math.Max(8, top));
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }
}
