using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Drawing = System.Drawing;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 全屏预览已标定 ROI：外围半透明遮罩，中间镂空高亮矩形（物理屏幕像素换算 DIP）。
    /// Esc / 点击遮罩关闭。
    /// </summary>
    public partial class RoiPreviewOverlayWindow : Window
    {
        private readonly Drawing.Rectangle _screenRect;
        private readonly string _label;

        public RoiPreviewOverlayWindow(Drawing.Rectangle screenRect, string label)
        {
            InitializeComponent();
            _screenRect = screenRect;
            _label = string.IsNullOrWhiteSpace(label) ? "ROI 预览" : label;
            Title = _label;
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            MouseLeftButtonDown += (_, _) => Close();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;

            var dipRect = ScreenRectToDipRect(_screenRect);
            UpdateMaskAround(dipRect);
            PlaceLabel(dipRect);

            Activate();
            Focus();
            Keyboard.Focus(this);
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void PlaceLabel(Rect dipRect)
        {
            LabelText.Text =
                $"{_label}  X={_screenRect.X}, Y={_screenRect.Y}, W={_screenRect.Width}, H={_screenRect.Height}";
            LabelBorder.UpdateLayout();

            var labelWidth = LabelBorder.ActualWidth > 0 ? LabelBorder.ActualWidth : 320;
            var labelHeight = LabelBorder.ActualHeight > 0 ? LabelBorder.ActualHeight : 32;
            var left = dipRect.Left;
            var top = dipRect.Top - labelHeight - 8;
            if (top < 8)
            {
                top = dipRect.Bottom + 8;
            }

            left = Math.Max(8, Math.Min(left, ActualWidth - labelWidth - 8));
            Canvas.SetLeft(LabelBorder, left);
            Canvas.SetTop(LabelBorder, top);
        }

        private void UpdateMaskAround(Rect hole)
        {
            var w = ActualWidth > 0 ? ActualWidth : Width;
            var h = ActualHeight > 0 ? ActualHeight : Height;

            Canvas.SetLeft(SelectionRect, hole.Left);
            Canvas.SetTop(SelectionRect, hole.Top);
            SelectionRect.Width = Math.Max(0, hole.Width);
            SelectionRect.Height = Math.Max(0, hole.Height);

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

        /// <summary>
        /// 物理屏幕像素 → 相对 VirtualScreen 左上角的 WPF DIP 矩形。
        /// </summary>
        private Rect ScreenRectToDipRect(Drawing.Rectangle screenRect)
        {
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                var fromDevice = source.CompositionTarget.TransformFromDevice;
                var topLeft = fromDevice.Transform(new Point(screenRect.Left, screenRect.Top));
                var bottomRight = fromDevice.Transform(new Point(screenRect.Right, screenRect.Bottom));
                return new Rect(
                    topLeft.X - SystemParameters.VirtualScreenLeft,
                    topLeft.Y - SystemParameters.VirtualScreenTop,
                    Math.Max(0, bottomRight.X - topLeft.X),
                    Math.Max(0, bottomRight.Y - topLeft.Y));
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            return new Rect(
                screenRect.Left / dpi.DpiScaleX - SystemParameters.VirtualScreenLeft,
                screenRect.Top / dpi.DpiScaleY - SystemParameters.VirtualScreenTop,
                screenRect.Width / dpi.DpiScaleX,
                screenRect.Height / dpi.DpiScaleY);
        }
    }
}
