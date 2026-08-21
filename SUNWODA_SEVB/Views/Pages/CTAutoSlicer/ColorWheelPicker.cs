using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SUNWODA_SEVB.Views.Pages.CTAutoSlicer
{
    /// <summary>
    /// 颜色助手风格：HSV 色盘（H=角度，S=半径）+ 下方饱和度/亮度两条可拖动色条。
    /// </summary>
    public class ColorWheelPicker : FrameworkElement
    {
        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorWheelPicker),
            new FrameworkPropertyMetadata(
                Colors.White,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

        private const double WheelSize = 200;
        private const double BarHeight = 18;
        private const double BarGap = 14;
        private const double MarkerRadius = 7;
        private const double BarMarkerRadius = 8;

        private WriteableBitmap? _wheelBitmap;
        private double _hue;
        private double _saturation = 0.35;
        private double _value = 1;
        private bool _syncing;
        private enum DragTarget
        {
            None,
            Wheel,
            Saturation,
            Value
        }

        private DragTarget _dragTarget;

        public ColorWheelPicker()
        {
            Focusable = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            ClipToBounds = false;
        }

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private double TotalHeight => WheelSize + BarGap + BarHeight + BarGap + BarHeight;

        private Rect SaturationBarRect => new(0, WheelSize + BarGap, WheelSize, BarHeight);

        private Rect ValueBarRect => new(0, WheelSize + BarGap + BarHeight + BarGap, WheelSize, BarHeight);

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (ColorWheelPicker)d;
            if (picker._syncing)
            {
                return;
            }

            picker.SyncFromColor((Color)e.NewValue);
            picker.InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize) =>
            new(WheelSize, TotalHeight);

        protected override Size ArrangeOverride(Size finalSize) =>
            new(WheelSize, TotalHeight);

        protected override void OnRender(DrawingContext dc)
        {
            EnsureWheelBitmap();
            if (_wheelBitmap != null)
            {
                dc.DrawImage(_wheelBitmap, new Rect(0, 0, WheelSize, WheelSize));
            }

            var center = new Point(WheelSize / 2, WheelSize / 2);
            var radius = (WheelSize / 2) - 2;
            var angle = _hue * Math.PI / 180.0;
            var wheelMarker = new Point(
                center.X + Math.Cos(angle) * _saturation * radius,
                center.Y + Math.Sin(angle) * _saturation * radius);
            DrawRingMarker(dc, wheelMarker);

            // 饱和度：白 → 当前色相纯色
            var satRect = SaturationBarRect;
            var satPure = ColorFromHsv(_hue, 1, Math.Max(_value, 0.15));
            var satBrush = new LinearGradientBrush(Colors.White, satPure, 0);
            DrawRoundBar(dc, satRect, satBrush);
            DrawBarMarker(dc, satRect, _saturation);

            // 亮度：黑 → 白
            var valRect = ValueBarRect;
            var valBrush = new LinearGradientBrush(Colors.Black, Colors.White, 0);
            DrawRoundBar(dc, valRect, valBrush);
            DrawBarMarker(dc, valRect, _value);
        }

        private static void DrawRoundBar(DrawingContext dc, Rect rect, Brush fill)
        {
            var radius = rect.Height / 2;
            dc.DrawRoundedRectangle(fill, new Pen(new SolidColorBrush(Color.FromRgb(200, 200, 200)), 1), rect, radius, radius);
        }

        private static void DrawRingMarker(DrawingContext dc, Point center)
        {
            dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 3), center, MarkerRadius, MarkerRadius);
            dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 1.2), center, MarkerRadius, MarkerRadius);
        }

        private static void DrawBarMarker(DrawingContext dc, Rect bar, double t)
        {
            t = Math.Clamp(t, 0, 1);
            var x = bar.X + t * bar.Width;
            var y = bar.Y + bar.Height / 2;
            dc.DrawEllipse(Brushes.White, new Pen(Brushes.Black, 1.5), new Point(x, y), BarMarkerRadius, BarMarkerRadius);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            CaptureMouse();
            var pos = e.GetPosition(this);
            if (SaturationBarRect.Contains(pos))
            {
                _dragTarget = DragTarget.Saturation;
                UpdateSaturationFromPoint(pos);
            }
            else if (ValueBarRect.Contains(pos))
            {
                _dragTarget = DragTarget.Value;
                UpdateValueFromPoint(pos);
            }
            else if (IsInWheel(pos))
            {
                _dragTarget = DragTarget.Wheel;
                UpdateWheelFromPoint(pos);
            }
            else
            {
                _dragTarget = DragTarget.None;
            }

            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton != MouseButtonState.Pressed || _dragTarget == DragTarget.None)
            {
                return;
            }

            var pos = e.GetPosition(this);
            switch (_dragTarget)
            {
                case DragTarget.Wheel:
                    UpdateWheelFromPoint(pos);
                    break;
                case DragTarget.Saturation:
                    UpdateSaturationFromPoint(pos);
                    break;
                case DragTarget.Value:
                    UpdateValueFromPoint(pos);
                    break;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _dragTarget = DragTarget.None;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            _dragTarget = DragTarget.None;
        }

        private bool IsInWheel(Point pos)
        {
            var center = new Point(WheelSize / 2, WheelSize / 2);
            var dx = pos.X - center.X;
            var dy = pos.Y - center.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= WheelSize / 2;
        }

        private void UpdateWheelFromPoint(Point pos)
        {
            var center = new Point(WheelSize / 2, WheelSize / 2);
            var dx = pos.X - center.X;
            var dy = pos.Y - center.Y;
            var radius = (WheelSize / 2) - 2;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            _saturation = Math.Clamp(dist / radius, 0, 1);
            var deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            if (deg < 0)
            {
                deg += 360;
            }

            _hue = deg;
            PushColorToProperty();
            InvalidateVisual();
        }

        private void UpdateSaturationFromPoint(Point pos)
        {
            _saturation = Math.Clamp((pos.X - SaturationBarRect.X) / SaturationBarRect.Width, 0, 1);
            PushColorToProperty();
            InvalidateVisual();
        }

        private void UpdateValueFromPoint(Point pos)
        {
            _value = Math.Clamp((pos.X - ValueBarRect.X) / ValueBarRect.Width, 0, 1);
            PushColorToProperty();
            InvalidateVisual();
        }

        private void PushColorToProperty()
        {
            _syncing = true;
            try
            {
                SelectedColor = ColorFromHsv(_hue, _saturation, _value);
            }
            finally
            {
                _syncing = false;
            }
        }

        private void SyncFromColor(Color color)
        {
            ColorToHsv(color, out _hue, out _saturation, out _value);
        }

        private void EnsureWheelBitmap()
        {
            if (_wheelBitmap != null)
            {
                return;
            }

            const int size = (int)WheelSize;
            var bmp = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            var pixels = new byte[size * size * 4];
            var center = size / 2.0;
            var maxR = center - 1;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    var index = (y * size + x) * 4;
                    if (dist > maxR)
                    {
                        pixels[index + 3] = 0;
                        continue;
                    }

                    var sat = dist / maxR;
                    var deg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                    if (deg < 0)
                    {
                        deg += 360;
                    }

                    var c = ColorFromHsv(deg, sat, 1);
                    pixels[index] = c.B;
                    pixels[index + 1] = c.G;
                    pixels[index + 2] = c.R;
                    pixels[index + 3] = 255;
                }
            }

            bmp.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            bmp.Freeze();
            _wheelBitmap = bmp;
        }

        public static Color ColorFromHsv(double h, double s, double v)
        {
            h = (h % 360 + 360) % 360;
            s = Math.Clamp(s, 0, 1);
            v = Math.Clamp(v, 0, 1);
            var c = v * s;
            var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            var m = v - c;
            double r1, g1, b1;
            if (h < 60)
            {
                r1 = c;
                g1 = x;
                b1 = 0;
            }
            else if (h < 120)
            {
                r1 = x;
                g1 = c;
                b1 = 0;
            }
            else if (h < 180)
            {
                r1 = 0;
                g1 = c;
                b1 = x;
            }
            else if (h < 240)
            {
                r1 = 0;
                g1 = x;
                b1 = c;
            }
            else if (h < 300)
            {
                r1 = x;
                g1 = 0;
                b1 = c;
            }
            else
            {
                r1 = c;
                g1 = 0;
                b1 = x;
            }

            return Color.FromRgb(
                (byte)Math.Round((r1 + m) * 255),
                (byte)Math.Round((g1 + m) * 255),
                (byte)Math.Round((b1 + m) * 255));
        }

        public static void ColorToHsv(Color color, out double h, out double s, out double v)
        {
            var r = color.R / 255.0;
            var g = color.G / 255.0;
            var b = color.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;
            v = max;
            s = max <= 0 ? 0 : delta / max;
            if (delta <= 0.00001)
            {
                h = 0;
                return;
            }

            if (Math.Abs(max - r) < 0.00001)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (Math.Abs(max - g) < 0.00001)
            {
                h = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                h = 60 * (((r - g) / delta) + 4);
            }

            if (h < 0)
            {
                h += 360;
            }
        }
    }
}
