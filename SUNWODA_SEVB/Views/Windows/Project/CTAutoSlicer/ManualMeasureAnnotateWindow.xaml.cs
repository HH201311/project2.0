using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer;
using Point = System.Windows.Point;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 二次手动标注全屏工作台：两点测距 + 实时橡皮筋虚线。
    /// </summary>
    public partial class ManualMeasureAnnotateWindow : Window
    {
        private readonly VM_ManualMeasureAnnotateWindow _vm;
        private BitmapImage? _bitmap;
        private double _zoom = 1.0;
        private bool _hasAnchor;
        private Point _anchorImagePx;
        private Point _cursorImagePx;
        private bool _isPanning;
        private Point _panStart;
        private bool _spaceDown;
        private Line? _previewDash;
        private Ellipse? _anchorMarker;
        private TextBlock? _previewLabel;

        private const double MinZoom = 0.1;
        private const double MaxZoom = 8.0;

        public ManualMeasureAnnotateWindow(string rootFolder, double pixelSizeUm)
        {
            InitializeComponent();
            _vm = new VM_ManualMeasureAnnotateWindow(rootFolder, pixelSizeUm);
            DataContext = _vm;
            _vm.ImageSelectionChanged += (_, _) => LoadSelectedImage();
            _vm.MeasuresChanged += (_, _) =>
            {
                CancelPendingMeasure();
                RedrawCommittedOverlays();
            };
            Loaded += (_, _) => LoadSelectedImage();
            Closing += OnClosing;
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_vm.HasUnsavedChanges)
            {
                return;
            }

            var r = MessageBox.Show(
                this,
                "存在未导出的标注变更，是否仍要关闭？\n（选择「否」可返回后点击导出）",
                "二次手动标注",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }

        private void LoadSelectedImage()
        {
            CancelPendingMeasure();
            OverlayCanvas.Children.Clear();
            _previewDash = null;
            _anchorMarker = null;
            _previewLabel = null;
            LiveDistanceText.Text = string.Empty;

            var path = _vm.CurrentImagePath;
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                MainImage.Source = null;
                _bitmap = null;
                return;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            _bitmap = bmp;
            MainImage.Source = bmp;

            FitToViewport();
            ApplyZoomTransform();
            RedrawCommittedOverlays();
            ImageListBox.ScrollIntoView(_vm.SelectedImage);
        }

        private void FitToViewport()
        {
            if (_bitmap is null)
            {
                return;
            }

            var vw = ImageScrollViewer.ViewportWidth;
            var vh = ImageScrollViewer.ViewportHeight;
            if (vw <= 1 || vh <= 1)
            {
                _zoom = 1.0;
            }
            else
            {
                var zx = vw / _bitmap.PixelWidth;
                var zy = vh / _bitmap.PixelHeight;
                _zoom = Math.Clamp(Math.Min(zx, zy), MinZoom, MaxZoom);
            }

            SyncZoomPercent();
        }

        private void ApplyZoomTransform()
        {
            if (_bitmap is null)
            {
                return;
            }

            var w = _bitmap.PixelWidth * _zoom;
            var h = _bitmap.PixelHeight * _zoom;
            MainImage.Width = w;
            MainImage.Height = h;
            OverlayCanvas.Width = w;
            OverlayCanvas.Height = h;
            ImageHost.Width = w;
            ImageHost.Height = h;
            SyncZoomPercent();
            RedrawCommittedOverlays();
            if (_hasAnchor)
            {
                UpdatePreviewGraphics(_cursorImagePx);
            }
        }

        private void SyncZoomPercent() => _vm.ZoomPercent = _zoom * 100.0;

        private void RedrawCommittedOverlays()
        {
            // 保留预览元素，先清再重画已确认 + 恢复预览
            var previewEls = new UIElement?[]
            {
                _previewDash, _anchorMarker, _previewLabel,
            };
            OverlayCanvas.Children.Clear();

            var measures = _vm.SelectedImage?.Measures;
            if (measures is not null)
            {
                foreach (var m in measures)
                {
                    DrawCommittedMeasure(m, string.Equals(m.Id, _vm.SelectedMeasureId, StringComparison.Ordinal));
                }
            }

            foreach (var el in previewEls)
            {
                if (el is not null && !OverlayCanvas.Children.Contains(el))
                {
                    OverlayCanvas.Children.Add(el);
                }
            }
        }

        private void DrawCommittedMeasure(ManualMeasureItem m, bool selected)
        {
            var p1 = ImageToCanvas(new Point(m.X1, m.Y1));
            var p2 = ImageToCanvas(new Point(m.X2, m.Y2));
            var stroke = selected
                ? new SolidColorBrush(Color.FromRgb(255, 200, 0))
                : new SolidColorBrush(Color.FromRgb(0, 220, 255));

            var main = MakeLine(p1, p2, stroke, null, selected ? 2.5 : 2);
            main.IsHitTestVisible = false;
            OverlayCanvas.Children.Add(main);
            AddEndpointCross(p1, stroke);
            AddEndpointCross(p2, stroke);

            var mid = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            var label = new TextBlock
            {
                Text = $"{m.Label}: {m.PixelLength:F1}px / {m.LengthUm:F2}μm / {m.LengthMm:F4}mm",
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false,
            };
            OverlayCanvas.Children.Add(label);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, mid.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, mid.Y - label.DesiredSize.Height - 6);
        }

        private void AddEndpointCross(Point c, Brush stroke)
        {
            const double r = 6;
            var h = MakeLine(new Point(c.X - r, c.Y), new Point(c.X + r, c.Y), stroke, null, 1.5);
            var v = MakeLine(new Point(c.X, c.Y - r), new Point(c.X, c.Y + r), stroke, null, 1.5);
            h.IsHitTestVisible = false;
            v.IsHitTestVisible = false;
            OverlayCanvas.Children.Add(h);
            OverlayCanvas.Children.Add(v);
        }

        private static Line MakeLine(Point a, Point b, Brush stroke, DoubleCollection? dash, double thickness = 1.5) =>
            new()
            {
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeDashArray = dash,
            };

        private Point ImageToCanvas(Point imagePx) =>
            new(imagePx.X * _zoom, imagePx.Y * _zoom);

        private Point? TryGetImagePixel(Point viewportPos)
        {
            if (_bitmap is null)
            {
                return null;
            }

            // viewportPos 相对 ScrollViewer；换算到 ImageHost
            var posInHost = ImageScrollViewer.TranslatePoint(viewportPos, ImageHost);
            var ix = posInHost.X / _zoom;
            var iy = posInHost.Y / _zoom;
            if (ix < 0 || iy < 0 || ix >= _bitmap.PixelWidth || iy >= _bitmap.PixelHeight)
            {
                return null;
            }

            return new Point(ix, iy);
        }

        private Point ConstrainOrthogonal(Point anchor, Point raw)
        {
            var dx = Math.Abs(raw.X - anchor.X);
            var dy = Math.Abs(raw.Y - anchor.Y);
            return dx >= dy
                ? new Point(raw.X, anchor.Y)
                : new Point(anchor.X, raw.Y);
        }

        private void EnsurePreviewElements()
        {
            var dash = new DoubleCollection { 6, 4 };
            var cyan = new SolidColorBrush(Color.FromRgb(0, 230, 255));

            if (_previewDash is null)
            {
                _previewDash = new Line { Stroke = cyan, StrokeThickness = 1.5, StrokeDashArray = dash, IsHitTestVisible = false };
                OverlayCanvas.Children.Add(_previewDash);
            }

            if (_anchorMarker is null)
            {
                _anchorMarker = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Stroke = cyan,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(80, 0, 230, 255)),
                    IsHitTestVisible = false,
                };
                OverlayCanvas.Children.Add(_anchorMarker);
            }

            if (_previewLabel is null)
            {
                _previewLabel = new TextBlock
                {
                    Foreground = Brushes.Yellow,
                    Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Padding = new Thickness(4, 2, 4, 2),
                    IsHitTestVisible = false,
                };
                OverlayCanvas.Children.Add(_previewLabel);
            }
        }

        private void UpdatePreviewGraphics(Point imagePx)
        {
            if (!_hasAnchor)
            {
                return;
            }

            EnsurePreviewElements();
            var p1 = ImageToCanvas(_anchorImagePx);
            var p2 = ImageToCanvas(imagePx);

            _previewDash!.X1 = p1.X;
            _previewDash.Y1 = p1.Y;
            _previewDash.X2 = p2.X;
            _previewDash.Y2 = p2.Y;

            Canvas.SetLeft(_anchorMarker!, p1.X - 5);
            Canvas.SetTop(_anchorMarker!, p1.Y - 5);

            var nPx = Math.Sqrt(
                (imagePx.X - _anchorImagePx.X) * (imagePx.X - _anchorImagePx.X)
                + (imagePx.Y - _anchorImagePx.Y) * (imagePx.Y - _anchorImagePx.Y));
            var um = nPx * _vm.PixelSizeUm;
            var mm = um / 1000.0;
            var text = $"{nPx:F1} px  |  {um:F2} μm  |  {mm:F4} mm";
            LiveDistanceText.Text = text;
            _previewLabel!.Text = text;
            var mid = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            _previewLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(_previewLabel, mid.X - _previewLabel.DesiredSize.Width / 2);
            Canvas.SetTop(_previewLabel, mid.Y - _previewLabel.DesiredSize.Height - 6);
        }

        private void ClearPreviewElements()
        {
            void Remove(UIElement? el)
            {
                if (el is not null && OverlayCanvas.Children.Contains(el))
                {
                    OverlayCanvas.Children.Remove(el);
                }
            }

            Remove(_previewDash);
            Remove(_anchorMarker);
            Remove(_previewLabel);
            _previewDash = null;
            _anchorMarker = null;
            _previewLabel = null;
            LiveDistanceText.Text = string.Empty;
        }

        private void CancelPendingMeasure()
        {
            _hasAnchor = false;
            ClearPreviewElements();
        }

        private void BtnMeasureMode_Click(object sender, RoutedEventArgs e)
        {
            _vm.IsMeasureMode = true;
            Cursor = Cursors.Cross;
        }

        private void BtnBrowseMode_Click(object sender, RoutedEventArgs e)
        {
            CancelPendingMeasure();
            _vm.IsMeasureMode = false;
            Cursor = Cursors.Arrow;
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = _vm.Export();
                MessageBox.Show(this, $"已导出到：\n{path}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"导出失败：{ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (_hasAnchor)
                {
                    CancelPendingMeasure();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.None)
            {
                _vm.IsMeasureMode = true;
                Cursor = Cursors.Cross;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (_vm.DeleteSelectedMeasureCommand.CanExecute(null))
                {
                    _vm.DeleteSelectedMeasureCommand.Execute(null);
                    RedrawCommittedOverlays();
                }

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                _vm.SelectPreviousImageCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                _vm.SelectNextImageCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                _spaceDown = true;
                Cursor = Cursors.Hand;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key == Key.Space)
            {
                _spaceDown = false;
                Cursor = _vm.IsMeasureMode ? Cursors.Cross : Cursors.Arrow;
            }
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_bitmap is null)
            {
                return;
            }

            var factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
            ApplyZoomTransform();
            e.Handled = true;
        }

        private void Viewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ImageScrollViewer);

            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && _spaceDown))
            {
                _isPanning = true;
                _panStart = pos;
                ImageScrollViewer.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            // 允许命中已有测距线：若点在 Overlay 透明线上，由 hit 处理
            var imgPt = TryGetImagePixel(pos);
            if (imgPt is null)
            {
                return;
            }

            if (!_vm.IsMeasureMode)
            {
                TrySelectNearestMeasure(imgPt.Value);
                e.Handled = true;
                return;
            }

            var pt = imgPt.Value;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _hasAnchor)
            {
                pt = ConstrainOrthogonal(_anchorImagePx, pt);
            }

            if (!_hasAnchor)
            {
                _hasAnchor = true;
                _anchorImagePx = pt;
                _cursorImagePx = pt;
                UpdatePreviewGraphics(pt);
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                pt = ConstrainOrthogonal(_anchorImagePx, pt);
            }

            _vm.AddMeasure(_anchorImagePx.X, _anchorImagePx.Y, pt.X, pt.Y);
            e.Handled = true;
        }

        private void TrySelectNearestMeasure(Point imagePx)
        {
            var measures = _vm.SelectedImage?.Measures;
            if (measures is null || measures.Count == 0)
            {
                return;
            }

            const double thresholdPx = 8.0;
            string? bestId = null;
            var bestDist = double.MaxValue;
            foreach (var m in measures)
            {
                var d = DistancePointToSegment(imagePx, new Point(m.X1, m.Y1), new Point(m.X2, m.Y2));
                if (d < bestDist)
                {
                    bestDist = d;
                    bestId = m.Id;
                }
            }

            if (bestId is not null && bestDist <= thresholdPx / Math.Max(_zoom, 0.01))
            {
                _vm.SelectedMeasureId = bestId;
                RedrawCommittedOverlays();
            }
        }

        private static double DistancePointToSegment(Point p, Point a, Point b)
        {
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len2 = dx * dx + dy * dy;
            if (len2 < 1e-9)
            {
                var ddx = p.X - a.X;
                var ddy = p.Y - a.Y;
                return Math.Sqrt(ddx * ddx + ddy * ddy);
            }

            var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
            var projX = a.X + t * dx;
            var projY = a.Y + t * dy;
            var ex = p.X - projX;
            var ey = p.Y - projY;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ImageScrollViewer);

            if (_isPanning && e.MiddleButton == MouseButtonState.Pressed
                || _isPanning && e.LeftButton == MouseButtonState.Pressed && _spaceDown)
            {
                var dx = pos.X - _panStart.X;
                var dy = pos.Y - _panStart.Y;
                _panStart = pos;
                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - dx);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - dy);
                e.Handled = true;
                return;
            }

            if (!_vm.IsMeasureMode || !_hasAnchor)
            {
                return;
            }

            var imgPt = TryGetImagePixel(pos);
            if (imgPt is null)
            {
                return;
            }

            var pt = imgPt.Value;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                pt = ConstrainOrthogonal(_anchorImagePx, pt);
            }

            _cursorImagePx = pt;
            UpdatePreviewGraphics(pt);
        }

        private void Viewport_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
            {
                _isPanning = false;
                ImageScrollViewer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void Viewport_LostMouseCapture(object sender, MouseEventArgs e)
        {
            _isPanning = false;
        }
    }
}
