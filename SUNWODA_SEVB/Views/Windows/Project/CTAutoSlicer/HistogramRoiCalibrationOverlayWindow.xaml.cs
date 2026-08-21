using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Drawing = System.Drawing;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏半透明遮罩：拖拽框选屏幕 ROI，框下方 ✓ 提交 / ✕ 重选。
    /// 返回的矩形为物理屏幕像素（供 GDI CopyFromScreen / 视觉点击换算使用）。
    /// 可用于直方图、3D 视图、「上」视图等任意 ROI 标定。
    /// </summary>
    public partial class HistogramRoiCalibrationOverlayWindow : Window
    {
        private bool _isDragging;
        private Point _dragStartDip;
        private Drawing.Point _dragStartScreen;
        private Drawing.Rectangle? _pendingScreenRect;
        private Rect _pendingDipRect;

        public Drawing.Rectangle? SelectedScreenRect { get; private set; }

        /// <summary>默认：直方图 ROI 标定文案。</summary>
        public HistogramRoiCalibrationOverlayWindow()
            : this(
                windowTitle: "直方图 ROI 标定",
                hintText: "拖拽框选直方图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选直方图区域")
        {
        }

        /// <summary>
        /// 自定义提示文案的 ROI 标定遮罩（阶段四 3D / 「上」视图等复用）。
        /// </summary>
        public HistogramRoiCalibrationOverlayWindow(string windowTitle, string hintText, string confirmBarText)
        {
            InitializeComponent();
            Title = windowTitle;
            HintText.Text = hintText;
            ConfirmHintText.Text = confirmBarText;
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            MaskCanvas.MouseLeftButtonDown += OnMouseLeftButtonDown;
            MaskCanvas.MouseMove += OnMouseMove;
            MaskCanvas.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            FillFullMask();
            Activate();
            Focus();
            Keyboard.Focus(this);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SelectedScreenRect = null;
                DialogResult = false;
                Close();
            }
        }

        private void FillFullMask()
        {
            var w = ActualWidth > 0 ? ActualWidth : Width;
            var h = ActualHeight > 0 ? ActualHeight : Height;
            MaskTop.Width = w;
            MaskTop.Height = h;
            Canvas.SetLeft(MaskTop, 0);
            Canvas.SetTop(MaskTop, 0);
            MaskBottom.Width = 0;
            MaskBottom.Height = 0;
            MaskLeft.Width = 0;
            MaskLeft.Height = 0;
            MaskRight.Width = 0;
            MaskRight.Height = 0;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ActionBar.Visibility == Visibility.Visible)
            {
                return;
            }

            _isDragging = true;
            _dragStartDip = e.GetPosition(MaskCanvas);
            _dragStartScreen = GetCursorScreenPoint();
            SelectionRect.Visibility = Visibility.Visible;
            ActionBar.Visibility = Visibility.Collapsed;
            MaskCanvas.CaptureMouse();
            UpdateSelectionVisual(_dragStartDip, _dragStartDip);
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            var current = e.GetPosition(MaskCanvas);
            UpdateSelectionVisual(_dragStartDip, current);
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            MaskCanvas.ReleaseMouseCapture();

            var endDip = e.GetPosition(MaskCanvas);
            var endScreen = GetCursorScreenPoint();
            UpdateSelectionVisual(_dragStartDip, endDip);

            var screenRect = NormalizeScreenRect(_dragStartScreen, endScreen);
            if (screenRect.Width < 8 || screenRect.Height < 8)
            {
                ResetSelection();
                e.Handled = true;
                return;
            }

            _pendingScreenRect = screenRect;
            _pendingDipRect = NormalizeDipRect(_dragStartDip, endDip);
            ShowActionBarBelowSelection(_pendingDipRect);
            e.Handled = true;
        }

        private void ShowActionBarBelowSelection(Rect dipRect)
        {
            ActionBar.Visibility = Visibility.Visible;
            ActionBar.UpdateLayout();

            var barWidth = ActionBar.ActualWidth > 0 ? ActionBar.ActualWidth : 220;
            var barHeight = ActionBar.ActualHeight > 0 ? ActionBar.ActualHeight : 48;
            var left = dipRect.Left + (dipRect.Width - barWidth) / 2;
            var top = dipRect.Bottom + 10;

            left = Math.Max(8, Math.Min(left, ActualWidth - barWidth - 8));
            if (top + barHeight > ActualHeight - 8)
            {
                top = Math.Max(8, dipRect.Top - barHeight - 10);
            }

            Canvas.SetLeft(ActionBar, left);
            Canvas.SetTop(ActionBar, top);
        }

        private void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_pendingScreenRect is not { } rect || rect.Width < 8 || rect.Height < 8)
            {
                return;
            }

            SelectedScreenRect = rect;
            DialogResult = true;
            Close();
        }

        private void CancelSelectionButton_OnClick(object sender, RoutedEventArgs e)
        {
            ResetSelection();
        }

        private void ResetSelection()
        {
            _pendingScreenRect = null;
            SelectionRect.Visibility = Visibility.Collapsed;
            ActionBar.Visibility = Visibility.Collapsed;
            FillFullMask();
        }

        private void UpdateSelectionVisual(Point a, Point b)
        {
            var rect = NormalizeDipRect(a, b);
            Canvas.SetLeft(SelectionRect, rect.Left);
            Canvas.SetTop(SelectionRect, rect.Top);
            SelectionRect.Width = rect.Width;
            SelectionRect.Height = rect.Height;
            SelectionRect.Visibility = Visibility.Visible;
            UpdateMaskAround(rect);
        }

        private void UpdateMaskAround(Rect hole)
        {
            var w = ActualWidth;
            var h = ActualHeight;

            MaskTop.Width = w;
            MaskTop.Height = Math.Max(0, hole.Top);
            Canvas.SetLeft(MaskTop, 0);
            Canvas.SetTop(MaskTop, 0);

            MaskBottom.Width = w;
            MaskBottom.Height = Math.Max(0, h - hole.Bottom);
            Canvas.SetLeft(MaskBottom, 0);
            Canvas.SetTop(MaskBottom, hole.Bottom);

            MaskLeft.Width = Math.Max(0, hole.Left);
            MaskLeft.Height = hole.Height;
            Canvas.SetLeft(MaskLeft, 0);
            Canvas.SetTop(MaskLeft, hole.Top);

            MaskRight.Width = Math.Max(0, w - hole.Right);
            MaskRight.Height = hole.Height;
            Canvas.SetLeft(MaskRight, hole.Right);
            Canvas.SetTop(MaskRight, hole.Top);
        }

        private static Rect NormalizeDipRect(Point a, Point b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var width = Math.Abs(a.X - b.X);
            var height = Math.Abs(a.Y - b.Y);
            return new Rect(x, y, width, height);
        }

        private static Drawing.Rectangle NormalizeScreenRect(Drawing.Point a, Drawing.Point b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var width = Math.Abs(a.X - b.X);
            var height = Math.Abs(a.Y - b.Y);
            return new Drawing.Rectangle(x, y, width, height);
        }

        private static Drawing.Point GetCursorScreenPoint()
        {
            if (!GetCursorPos(out var point))
            {
                return Drawing.Point.Empty;
            }

            return new Drawing.Point(point.X, point.Y);
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
