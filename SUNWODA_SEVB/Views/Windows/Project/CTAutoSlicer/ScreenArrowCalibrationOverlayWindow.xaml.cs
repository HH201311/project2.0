using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏直线/箭头标定：按住左键从起点拖到终点，箭头表示拖拽方向与距离；Esc/右键取消。
    /// </summary>
    public partial class ScreenArrowCalibrationOverlayWindow : Window
    {
        private readonly DispatcherTimer _moveTimer;
        private bool _dragging;
        private bool _confirmed;
        private int _startScreenX;
        private int _startScreenY;
        private int _endScreenX;
        private int _endScreenY;

        public int StartX { get; private set; }
        public int StartY { get; private set; }
        public int EndX { get; private set; }
        public int EndY { get; private set; }

        public ScreenArrowCalibrationOverlayWindow(string? hintText = null)
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
            PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
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

            _dragging = true;
            _startScreenX = pt.X;
            _startScreenY = pt.Y;
            _endScreenX = pt.X;
            _endScreenY = pt.Y;
            CaptureMouse();
            e.Handled = true;
            UpdateArrowVisual();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }

            if (GetCursorPos(out var pt))
            {
                _endScreenX = pt.X;
                _endScreenY = pt.Y;
            }

            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;

            var dx = _endScreenX - _startScreenX;
            var dy = _endScreenY - _startScreenY;
            if (Math.Abs(dx) < 3 && Math.Abs(dy) < 3)
            {
                // 位移过小视为未完成，保留遮罩让用户重拖
                ArrowShaft.Visibility = Visibility.Collapsed;
                ArrowHead.Visibility = Visibility.Collapsed;
                StartDot.Visibility = Visibility.Collapsed;
                CoordText.Text = "位移过小，请重新按住拖拽一段距离";
                return;
            }

            _confirmed = true;
            StartX = _startScreenX;
            StartY = _startScreenY;
            EndX = _endScreenX;
            EndY = _endScreenY;
            DialogResult = true;
            Close();
        }

        private void CancelAndClose()
        {
            if (_confirmed)
            {
                return;
            }

            if (_dragging)
            {
                _dragging = false;
                if (IsMouseCaptured)
                {
                    ReleaseMouseCapture();
                }
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

            if (_dragging)
            {
                _endScreenX = pt.X;
                _endScreenY = pt.Y;
                UpdateArrowVisual();
            }
            else
            {
                CoordText.Text = $"光标 ({pt.X}, {pt.Y}) — 按住左键拖出箭头";
                PositionCoordPanel(pt.X, pt.Y);
            }
        }

        private void UpdateArrowVisual()
        {
            var startLocal = PointFromScreen(new Point(_startScreenX, _startScreenY));
            var endLocal = PointFromScreen(new Point(_endScreenX, _endScreenY));

            StartDot.Visibility = Visibility.Visible;
            Canvas.SetLeft(StartDot, startLocal.X - 5);
            Canvas.SetTop(StartDot, startLocal.Y - 5);

            ArrowShaft.Visibility = Visibility.Visible;
            ArrowShaft.X1 = startLocal.X;
            ArrowShaft.Y1 = startLocal.Y;
            ArrowShaft.X2 = endLocal.X;
            ArrowShaft.Y2 = endLocal.Y;

            UpdateArrowHead(startLocal, endLocal);

            var dx = _endScreenX - _startScreenX;
            var dy = _endScreenY - _startScreenY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            CoordText.Text =
                $"({_startScreenX},{_startScreenY}) → ({_endScreenX},{_endScreenY})  Δ=({dx},{dy})  L={len:F0}px";
            PositionCoordPanel(_endScreenX, _endScreenY);
        }

        private void UpdateArrowHead(Point start, Point end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1)
            {
                ArrowHead.Visibility = Visibility.Collapsed;
                return;
            }

            // 箭头尖端朝终点，翼长约 14px
            const double headLen = 14;
            const double headWidth = 8;
            var ux = dx / len;
            var uy = dy / len;
            var baseX = end.X - ux * headLen;
            var baseY = end.Y - uy * headLen;
            var px = -uy;
            var py = ux;

            ArrowHead.Visibility = Visibility.Visible;
            ArrowHead.Points = new PointCollection
            {
                end,
                new Point(baseX + px * headWidth, baseY + py * headWidth),
                new Point(baseX - px * headWidth, baseY - py * headWidth),
            };

            // 箭杆缩短，避免盖住箭头
            ArrowShaft.X2 = baseX;
            ArrowShaft.Y2 = baseY;
        }

        private void PositionCoordPanel(int screenX, int screenY)
        {
            var local = PointFromScreen(new Point(screenX, screenY));
            var w = ActualWidth > 0 ? ActualWidth : Width;
            var h = ActualHeight > 0 ? ActualHeight : Height;
            var panelW = CoordPanel.ActualWidth > 0 ? CoordPanel.ActualWidth : 280;
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

            Canvas.SetLeft(CoordPanel, Math.Max(8, left));
            Canvas.SetTop(CoordPanel, Math.Max(8, top));
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
