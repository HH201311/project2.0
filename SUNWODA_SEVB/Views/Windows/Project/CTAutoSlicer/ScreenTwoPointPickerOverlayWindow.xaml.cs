using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏两点标定：左键依次确认两个屏幕物理像素坐标，右键/Esc 取消。
    /// </summary>
    public partial class ScreenTwoPointPickerOverlayWindow : Window
    {
        private readonly DispatcherTimer _moveTimer;
        private readonly string _hintPoint1;
        private readonly string _hintPoint2;
        private bool _hasPoint1;
        private bool _confirmed;

        public int Point1X { get; private set; }
        public int Point1Y { get; private set; }
        public int Point2X { get; private set; }
        public int Point2Y { get; private set; }

        public ScreenTwoPointPickerOverlayWindow(
            string? hintPoint1 = null,
            string? hintPoint2 = null)
        {
            InitializeComponent();
            _hintPoint1 = string.IsNullOrWhiteSpace(hintPoint1)
                ? "第 1 步：左键点击第一个目标点；右键 / Esc 取消"
                : hintPoint1;
            _hintPoint2 = string.IsNullOrWhiteSpace(hintPoint2)
                ? "第 2 步：左键点击第二个目标点；右键 / Esc 取消"
                : hintPoint2;
            HintTextBlock.Text = _hintPoint1;

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

            if (!_hasPoint1)
            {
                Point1X = pt.X;
                Point1Y = pt.Y;
                _hasPoint1 = true;
                HintTextBlock.Text = _hintPoint2;
                ShowPoint1Marker(pt.X, pt.Y);
                e.Handled = true;
                return;
            }

            Point2X = pt.X;
            Point2Y = pt.Y;
            _confirmed = true;
            DialogResult = true;
            Close();
        }

        private void ShowPoint1Marker(int screenX, int screenY)
        {
            var local = PointFromScreen(new Point(screenX, screenY));
            Point1Marker.Visibility = Visibility.Visible;
            System.Windows.Controls.Canvas.SetLeft(Point1Marker, local.X - Point1Marker.Width / 2);
            System.Windows.Controls.Canvas.SetTop(Point1Marker, local.Y - Point1Marker.Height / 2);
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

            var step = _hasPoint1 ? "P2" : "P1";
            CoordText.Text = $"{step} ({pt.X}, {pt.Y})";

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

            var panelW = CoordPanel.ActualWidth > 0 ? CoordPanel.ActualWidth : 140;
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
