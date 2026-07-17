using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using MediaColor = System.Windows.Media.Color;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏取色：先截虚拟屏，再跟随鼠标显示 9×9 放大格与当前像素色（uTools 风格）。
    /// </summary>
    public partial class ScreenColorPickerOverlayWindow : Window
    {
        private const int GridSize = 9;
        private const int CellPx = 14;
        private const int PreviewOffset = 24;

        private Drawing.Bitmap? _screenCapture;
        private int _captureLeft;
        private int _captureTop;
        private readonly DispatcherTimer _moveTimer;
        private MediaColor _currentColor = Colors.Gray;
        private bool _confirmed;

        public string? SelectedHex { get; private set; }

        public ScreenColorPickerOverlayWindow()
        {
            InitializeComponent();
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

            // 先隐藏再截屏，避免取到本遮罩；随后再显示
            Opacity = 0;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                try
                {
                    _screenCapture?.Dispose();
                    _screenCapture = CaptureVirtualScreen();
                }
                catch
                {
                    _screenCapture = null;
                }

                Opacity = 1;
                Activate();
                Focus();
                Keyboard.Focus(this);
                _moveTimer.Start();
                UpdateFromCursor();
            });
        }

        private Drawing.Bitmap CaptureVirtualScreen()
        {
            GetVirtualScreenRect(out var vx, out var vy, out var vw, out var vh);
            _captureLeft = vx;
            _captureTop = vy;
            vw = Math.Max(1, vw);
            vh = Math.Max(1, vh);

            var bmp = new Drawing.Bitmap(vw, vh, Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(vx, vy, 0, 0, new Drawing.Size(vw, vh), CopyPixelOperation.SourceCopy);
            return bmp;
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _moveTimer.Stop();
            _screenCapture?.Dispose();
            _screenCapture = null;
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
            if (_screenCapture == null)
            {
                return;
            }

            _confirmed = true;
            SelectedHex = ToHex(_currentColor);
            DialogResult = true;
            Close();
        }

        private void CancelAndClose()
        {
            if (_confirmed)
            {
                return;
            }

            SelectedHex = null;
            DialogResult = false;
            Close();
        }

        private void UpdateFromCursor()
        {
            if (_screenCapture == null)
            {
                return;
            }

            if (!GetCursorPos(out var pt))
            {
                return;
            }

            var localX = pt.X - _captureLeft;
            var localY = pt.Y - _captureTop;
            localX = Math.Clamp(localX, 0, _screenCapture.Width - 1);
            localY = Math.Clamp(localY, 0, _screenCapture.Height - 1);

            var pixel = _screenCapture.GetPixel(localX, localY);
            _currentColor = MediaColor.FromRgb(pixel.R, pixel.G, pixel.B);

            ColorSwatch.Background = new SolidColorBrush(_currentColor);
            HexText.Text = ToHex(_currentColor);
            RgbText.Text = $"RGB({pixel.R}, {pixel.G}, {pixel.B})  @({pt.X},{pt.Y})";

            MagnifierImage.Source = BuildMagnifierBitmapSource(_screenCapture, localX, localY);

            // 预览面板跟鼠标，避开右下边缘
            var panelW = PreviewPanel.ActualWidth > 0 ? PreviewPanel.ActualWidth : 160;
            var panelH = PreviewPanel.ActualHeight > 0 ? PreviewPanel.ActualHeight : 200;
            var cursorInWindow = PointFromScreen(new System.Windows.Point(pt.X, pt.Y));
            var left = cursorInWindow.X + PreviewOffset;
            var top = cursorInWindow.Y + PreviewOffset;
            if (left + panelW > ActualWidth - 8)
            {
                left = cursorInWindow.X - panelW - PreviewOffset;
            }

            if (top + panelH > ActualHeight - 8)
            {
                top = cursorInWindow.Y - panelH - PreviewOffset;
            }

            left = Math.Max(8, left);
            top = Math.Max(8, top);
            System.Windows.Controls.Canvas.SetLeft(PreviewPanel, left);
            System.Windows.Controls.Canvas.SetTop(PreviewPanel, top);
        }

        private static BitmapSource BuildMagnifierBitmapSource(Drawing.Bitmap src, int cx, int cy)
        {
            var half = GridSize / 2;
            var size = GridSize * CellPx;
            using var zoom = new Drawing.Bitmap(size, size, Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(zoom))
            {
                g.Clear(Drawing.Color.Black);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                using var pen = new Drawing.Pen(Drawing.Color.FromArgb(80, 255, 255, 255));
                using var centerPen = new Drawing.Pen(Drawing.Color.Red, 2);

                for (var dy = -half; dy <= half; dy++)
                {
                    for (var dx = -half; dx <= half; dx++)
                    {
                        var sx = Math.Clamp(cx + dx, 0, src.Width - 1);
                        var sy = Math.Clamp(cy + dy, 0, src.Height - 1);
                        var c = src.GetPixel(sx, sy);
                        var cellX = (dx + half) * CellPx;
                        var cellY = (dy + half) * CellPx;
                        using var brush = new SolidBrush(c);
                        g.FillRectangle(brush, cellX, cellY, CellPx, CellPx);
                        g.DrawRectangle(pen, cellX, cellY, CellPx - 1, CellPx - 1);
                    }
                }

                var mid = half * CellPx;
                g.DrawRectangle(centerPen, mid, mid, CellPx - 1, CellPx - 1);
            }

            return ToBitmapSource(zoom);
        }

        private static BitmapSource ToBitmapSource(Drawing.Bitmap bmp)
        {
            var hBitmap = bmp.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        private static string ToHex(MediaColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static void GetVirtualScreenRect(out int x, out int y, out int w, out int h)
        {
            x = GetSystemMetrics(76); // SM_XVIRTUALSCREEN
            y = GetSystemMetrics(77); // SM_YVIRTUALSCREEN
            w = GetSystemMetrics(78); // SM_CXVIRTUALSCREEN
            h = GetSystemMetrics(79); // SM_CYVIRTUALSCREEN
            if (w <= 0 || h <= 0)
            {
                x = 0;
                y = 0;
                w = GetSystemMetrics(0);
                h = GetSystemMetrics(1);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }
}
