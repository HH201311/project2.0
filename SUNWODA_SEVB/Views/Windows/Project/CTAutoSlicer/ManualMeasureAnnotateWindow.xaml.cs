using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer;
using Point = System.Windows.Point;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    /// <summary>
    /// 二次手动标注全屏工作台：两点测距；底部遮罩汇总；标签可改名。
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
        private Line? _previewLeader;
        private Ellipse? _anchorMarker;
        private TextBlock? _previewLabel;

        /// <summary>正在拖拽的测距标签 Id；空表示未拖拽。</summary>
        private string? _draggingLabelMeasureId;
        private bool _labelDragMoved;

        private static readonly SolidColorBrush ActiveModeBrush = new(Color.FromRgb(0x0E, 0x63, 0x9C));
        private static readonly SolidColorBrush IdleModeBrush = new(Color.FromRgb(0x2D, 0x2D, 0x30));
        private static readonly SolidColorBrush ActiveModeBorder = new(Color.FromRgb(0x11, 0x77, 0xBB));
        private static readonly SolidColorBrush IdleModeBorder = new(Color.FromRgb(0x3F, 0x3F, 0x46));

        private const double MinZoom = 0.05;
        private const double MaxZoom = 32.0;
        /// <summary>文字引出虚线长度（屏幕像素）。</summary>
        private const double LabelLeaderLength = 42.0;
        private const string MeasureLabelTagPrefix = "measure:";

        public ManualMeasureAnnotateWindow(
            string rootFolder,
            double pixelSizeUm,
            int bottomBlackHeightPx = 0)
        {
            InitializeComponent();
            _vm = new VM_ManualMeasureAnnotateWindow(rootFolder, pixelSizeUm, bottomBlackHeightPx);
            DataContext = _vm;
            _vm.ImageSelectionChanged += (_, _) => LoadSelectedImage();
            _vm.MeasuresChanged += (_, _) =>
            {
                CancelPendingMeasure();
                RedrawCommittedOverlays();
            };
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(VM_ManualMeasureAnnotateWindow.IsMeasureMode))
                {
                    UpdateModeUi();
                }
            };
            Loaded += (_, _) =>
            {
                LoadSelectedImage();
                UpdateModeUi();
            };
            Closing += OnClosing;
        }

        private void UpdateModeUi()
        {
            if (_vm.IsMeasureMode)
            {
                BtnAnnotateMode.Background = ActiveModeBrush;
                BtnAnnotateMode.BorderBrush = ActiveModeBorder;
                BtnMoveMode.Background = IdleModeBrush;
                BtnMoveMode.BorderBrush = IdleModeBorder;
                if (!_spaceDown && !_isPanning)
                {
                    Cursor = Cursors.Cross;
                }
            }
            else
            {
                BtnMoveMode.Background = ActiveModeBrush;
                BtnMoveMode.BorderBrush = ActiveModeBorder;
                BtnAnnotateMode.Background = IdleModeBrush;
                BtnAnnotateMode.BorderBrush = IdleModeBorder;
                if (!_spaceDown && !_isPanning)
                {
                    Cursor = Cursors.Hand;
                }
            }
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
            _previewLeader = null;
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

            FitToViewportAndCenter();
            RedrawCommittedOverlays();
            ImageListBox.ScrollIntoView(_vm.SelectedImage);
        }

        /// <summary>适应视口并居中（默认打开效果）。</summary>
        private void FitToViewportAndCenter()
        {
            if (_bitmap is null)
            {
                return;
            }

            void Apply()
            {
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
                    // 略小于 1 留边，保证完整可见并居中
                    _zoom = Math.Clamp(Math.Min(zx, zy) * 0.96, MinZoom, MaxZoom);
                }

                ApplyZoomTransform(centerAfter: true);
            }

            if (ImageScrollViewer.ViewportWidth <= 1)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Apply);
            }
            else
            {
                Apply();
            }
        }

        private void ApplyZoomTransform(bool centerAfter = false)
        {
            if (_bitmap is null)
            {
                return;
            }

            // 同步放大 ImageHost 宽高 = 滚动边界；叠加 Canvas 与图片同尺寸，标注随边界一起缩放
            var w = Math.Max(1, _bitmap.PixelWidth * _zoom);
            var h = Math.Max(1, _bitmap.PixelHeight * _zoom);
            ImageHost.Width = w;
            ImageHost.Height = h;
            MainImage.Width = w;
            MainImage.Height = h;
            OverlayCanvas.Width = w;
            OverlayCanvas.Height = h;

            // 强制重新布局，使 ScrollViewer Extent（可滚动边界）立即跟上内容尺寸
            ImageHost.InvalidateMeasure();
            ImageHost.InvalidateArrange();
            ImageScrollViewer.InvalidateMeasure();
            ImageScrollViewer.UpdateLayout();

            SyncZoomPercent();
            RedrawCommittedOverlays();
            if (_hasAnchor)
            {
                UpdatePreviewGraphics(_cursorImagePx);
            }

            if (centerAfter)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, CenterViewport);
            }
        }

        private void CenterViewport()
        {
            if (_bitmap is null)
            {
                return;
            }

            ImageScrollViewer.UpdateLayout();
            var w = ImageHost.ActualWidth > 0 ? ImageHost.ActualWidth : ImageHost.Width;
            var h = ImageHost.ActualHeight > 0 ? ImageHost.ActualHeight : ImageHost.Height;
            var vw = ImageScrollViewer.ViewportWidth;
            var vh = ImageScrollViewer.ViewportHeight;
            ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, (w - vw) / 2));
            ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, (h - vh) / 2));
        }

        private void SyncZoomPercent() => _vm.ZoomPercent = _zoom * 100.0;

        private void RedrawCommittedOverlays()
        {
            // 保留预览元素，先清再重画已确认 + 恢复预览
            var previewEls = new UIElement?[]
            {
                _previewDash, _previewLeader, _anchorMarker, _previewLabel,
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

            // 底部遮罩与汇总压在测距线之上（图像像素高度 × 缩放）
            DrawBottomInfoBand(measures);

            foreach (var el in previewEls)
            {
                if (el is not null && !OverlayCanvas.Children.Contains(el))
                {
                    OverlayCanvas.Children.Add(el);
                }
            }
        }

        /// <summary>按打黑高度在图像底部绘制黑色遮罩，并列出全部测距（mm）。</summary>
        private void DrawBottomInfoBand(IReadOnlyList<ManualMeasureItem>? measures)
        {
            if (_bitmap is null)
            {
                return;
            }

            var bandPx = Math.Clamp(_vm.BottomBlackHeightPx, 0, _bitmap.PixelHeight);
            if (bandPx <= 0)
            {
                return;
            }

            var canvasW = OverlayCanvas.Width;
            var canvasH = OverlayCanvas.Height;
            var bandH = bandPx * _zoom;
            var bandY = canvasH - bandH;

            var band = new System.Windows.Shapes.Rectangle
            {
                Width = canvasW,
                Height = bandH,
                Fill = Brushes.Black,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, bandY);
            OverlayCanvas.Children.Add(band);

            var lines = measures is { Count: > 0 }
                ? measures.Select(ManualMeasureExportHelper.FormatMeasureLabelMm).ToList()
                : ["（暂无测距 · 标注后显示于此）"];

            var fontSize = Math.Clamp(12.0 * Math.Sqrt(_zoom), 11, 22);
            var lineH = fontSize + 6;
            var y = bandY + 8;
            foreach (var line in lines)
            {
                if (y + lineH > canvasH - 4)
                {
                    break;
                }

                var tb = new TextBlock
                {
                    Text = line,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = fontSize,
                    FontWeight = FontWeights.SemiBold,
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(tb, 10);
                Canvas.SetTop(tb, y);
                OverlayCanvas.Children.Add(tb);
                y += lineH;
            }
        }

        private void DrawCommittedMeasure(ManualMeasureItem m, bool selected)
        {
            var p1 = ImageToCanvas(new Point(m.X1, m.Y1));
            var p2 = ImageToCanvas(new Point(m.X2, m.Y2));
            var stroke = selected
                ? new SolidColorBrush(Color.FromRgb(255, 200, 0))
                : new SolidColorBrush(Color.FromRgb(0, 220, 255));
            var leaderStroke = new SolidColorBrush(Color.FromRgb(180, 220, 255));

            var main = MakeLine(p1, p2, stroke, null, selected ? 2.5 : 2);
            main.IsHitTestVisible = false;
            OverlayCanvas.Children.Add(main);
            AddEndpointCross(p1, stroke);
            AddEndpointCross(p2, stroke);

            var mid = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            Point labelCenter;
            if (m.HasCustomLabelPosition)
            {
                labelCenter = ImageToCanvas(new Point(m.LabelX!.Value, m.LabelY!.Value));
            }
            else
            {
                (_, labelCenter) = GetPerpendicularLeader(p1, p2, LabelLeaderLength);
            }

            var dash = new DoubleCollection { 4, 3 };
            var leader = MakeLine(mid, labelCenter, leaderStroke, dash, 1.2);
            leader.IsHitTestVisible = false;
            OverlayCanvas.Children.Add(leader);

            // 上方标签：仅毫米；拖拽改位置，双击改名
            var label = new TextBlock
            {
                Text = ManualMeasureExportHelper.FormatMeasureLabelMm(m),
                Foreground = Brushes.Yellow,
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = Cursors.SizeAll,
                Tag = MeasureLabelTagPrefix + m.Id,
                ToolTip = "拖拽移动位置；双击修改名称",
                IsHitTestVisible = true,
            };
            OverlayCanvas.Children.Add(label);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PlaceLabelCentered(label, labelCenter);
        }

        private static void PlaceLabelCentered(TextBlock label, Point center)
        {
            var w = label.DesiredSize.Width;
            var h = label.DesiredSize.Height;
            Canvas.SetLeft(label, center.X - w / 2);
            Canvas.SetTop(label, center.Y - h / 2);
        }

        /// <summary>
        /// 从连线中点沿垂直方向拉出引出线；优先朝屏幕上方（Y 减小），避免文字压在测距线上。
        /// </summary>
        private static (Point Mid, Point LabelPos) GetPerpendicularLeader(Point p1, Point p2, double length)
        {
            var mid = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = Math.Sqrt(dx * dx + dy * dy);
            double nx;
            double ny;
            if (len < 1e-3)
            {
                nx = 0;
                ny = -1;
            }
            else
            {
                nx = -dy / len;
                ny = dx / len;
                // 优先朝上（屏幕 Y 减小）
                if (ny > 0 || (Math.Abs(ny) < 1e-6 && nx < 0))
                {
                    nx = -nx;
                    ny = -ny;
                }
            }

            return (mid, new Point(mid.X + nx * length, mid.Y + ny * length));
        }

        private static void PlaceLabelAtLeaderEnd(TextBlock label, Point mid, Point labelPos)
        {
            var w = label.DesiredSize.Width;
            var h = label.DesiredSize.Height;
            // 文字中心落在引出线末端外侧：按引出方向偏移半个文本框
            var ox = labelPos.X - mid.X;
            var oy = labelPos.Y - mid.Y;
            var olen = Math.Sqrt(ox * ox + oy * oy);
            var pad = 4.0;
            if (olen > 1e-3)
            {
                var ux = ox / olen;
                var uy = oy / olen;
                var cx = labelPos.X + ux * (w * 0.5 + pad);
                var cy = labelPos.Y + uy * (h * 0.5 + pad);
                Canvas.SetLeft(label, cx - w / 2);
                Canvas.SetTop(label, cy - h / 2);
            }
            else
            {
                Canvas.SetLeft(label, labelPos.X - w / 2);
                Canvas.SetTop(label, labelPos.Y - h - pad);
            }
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
            var leaderCyan = new SolidColorBrush(Color.FromRgb(180, 220, 255));

            if (_previewDash is null)
            {
                _previewDash = new Line { Stroke = cyan, StrokeThickness = 1.5, StrokeDashArray = dash, IsHitTestVisible = false };
                OverlayCanvas.Children.Add(_previewDash);
            }

            if (_previewLeader is null)
            {
                _previewLeader = new Line
                {
                    Stroke = leaderCyan,
                    StrokeThickness = 1.2,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false,
                };
                OverlayCanvas.Children.Add(_previewLeader);
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
            var mm = nPx * _vm.PixelSizeUm / 1000.0;
            // 预览与定稿一致：仅毫米（左上角实时条可附带像素供校对）
            var text = $"{mm:F4} mm";
            LiveDistanceText.Text = $"{nPx:F1} px  ·  {text}";
            _previewLabel!.Text = text;

            var (mid, labelPos) = GetPerpendicularLeader(p1, p2, LabelLeaderLength);
            _previewLeader!.X1 = mid.X;
            _previewLeader.Y1 = mid.Y;
            _previewLeader.X2 = labelPos.X;
            _previewLeader.Y2 = labelPos.Y;

            _previewLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            PlaceLabelAtLeaderEnd(_previewLabel, mid, labelPos);
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
            Remove(_previewLeader);
            Remove(_anchorMarker);
            Remove(_previewLabel);
            _previewDash = null;
            _previewLeader = null;
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
            UpdateModeUi();
        }

        private void BtnBrowseMode_Click(object sender, RoutedEventArgs e)
        {
            CancelPendingMeasure();
            _vm.IsMeasureMode = false;
            UpdateModeUi();
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
                    _vm.StatusText = "已取消起点，可重新左键定 P1";
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Z：撤回当前图上一条测距
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.None
                && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.None)
            {
                if (_vm.UndoLastMeasureCommand.CanExecute(null))
                {
                    _vm.UndoLastMeasureCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            // 标注模式：A / M
            if ((e.Key == Key.A || e.Key == Key.M) && Keyboard.Modifiers == ModifierKeys.None)
            {
                _vm.IsMeasureMode = true;
                UpdateModeUi();
                e.Handled = true;
                return;
            }

            // 移动/抓手：H
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.None)
            {
                CancelPendingMeasure();
                _vm.IsMeasureMode = false;
                UpdateModeUi();
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

            // 空格：临时抓手（同 PS），吞掉按键避免按钮/列表滚动
            if (e.Key == Key.Space)
            {
                _spaceDown = true;
                Cursor = Cursors.Hand;
                e.Handled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.Key == Key.Space)
            {
                _spaceDown = false;
                if (!_isPanning)
                {
                    UpdateModeUi();
                }

                e.Handled = true;
            }
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_bitmap is null)
            {
                return;
            }

            // 直接滚轮缩放（以鼠标位置为中心），同步扩大/缩小可滚动边界
            e.Handled = true;
            var mouseInViewer = e.GetPosition(ImageScrollViewer);
            var posInHost = e.GetPosition(ImageHost);
            var oldZoom = _zoom;
            var factor = e.Delta > 0 ? 1.12 : 1 / 1.12;
            var newZoom = Math.Clamp(oldZoom * factor, MinZoom, MaxZoom);
            if (Math.Abs(newZoom - oldZoom) < 1e-9)
            {
                return;
            }

            var ratio = newZoom / oldZoom;
            _zoom = newZoom;
            ApplyZoomTransform(centerAfter: false);

            // 布局完成后按比例校正滚动，使鼠标下像素点仍对准光标
            ImageScrollViewer.UpdateLayout();
            var newPosInHostX = posInHost.X * ratio;
            var newPosInHostY = posInHost.Y * ratio;
            var maxH = Math.Max(0, ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth);
            var maxV = Math.Max(0, ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight);
            ImageScrollViewer.ScrollToHorizontalOffset(Math.Clamp(newPosInHostX - mouseInViewer.X, 0, maxH));
            ImageScrollViewer.ScrollToVerticalOffset(Math.Clamp(newPosInHostY - mouseInViewer.Y, 0, maxV));
        }

        private bool ShouldPanWithLeftButton() =>
            _spaceDown || !_vm.IsMeasureMode;

        private void Viewport_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(ImageScrollViewer);

            // 右键：已定起点、未定终点时取消本次测距（与 Esc 相同）
            if (e.ChangedButton == MouseButton.Right)
            {
                if (_hasAnchor && _vm.IsMeasureMode)
                {
                    CancelPendingMeasure();
                    _vm.StatusText = "已右键取消起点，可重新左键定 P1";
                    e.Handled = true;
                }

                return;
            }

            // 中键按住拖拽 / 空格+左键 / 移动模式下左键 → 平移
            if (e.ChangedButton == MouseButton.Middle
                || (e.ChangedButton == MouseButton.Left && ShouldPanWithLeftButton()))
            {
                _isPanning = true;
                _panStart = pos;
                Cursor = Cursors.Hand;
                Mouse.Capture(ImageScrollViewer, CaptureMode.SubTree);
                e.Handled = true;
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            // 标签：双击改名；单击按下开始拖拽（未在定 P2 过程中）
            if (!_hasAnchor && TryHitMeasureLabel(pos, out var hitMeasureId))
            {
                if (e.ClickCount >= 2)
                {
                    EndLabelDrag(commit: false);
                    PromptRenameMeasure(hitMeasureId);
                    e.Handled = true;
                    return;
                }

                BeginLabelDrag(hitMeasureId, pos);
                e.Handled = true;
                return;
            }

            var imgPt = TryGetImagePixel(pos);
            if (imgPt is null)
            {
                return;
            }

            // 落在底部信息遮罩内：不开始测距，避免点到汇总区误标
            if (!_hasAnchor && IsInBottomBlackBand(imgPt.Value))
            {
                e.Handled = true;
                return;
            }

            // 标注模式：测距
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

        private bool IsInBottomBlackBand(Point imagePx)
        {
            if (_bitmap is null || _vm.BottomBlackHeightPx <= 0)
            {
                return false;
            }

            var bandPx = Math.Clamp(_vm.BottomBlackHeightPx, 0, _bitmap.PixelHeight);
            return imagePx.Y >= _bitmap.PixelHeight - bandPx;
        }

        private bool TryHitMeasureLabel(Point viewportPos, out string measureId)
        {
            measureId = string.Empty;
            var posInCanvas = ImageScrollViewer.TranslatePoint(viewportPos, OverlayCanvas);
            var hit = OverlayCanvas.InputHitTest(posInCanvas);
            DependencyObject? current = hit as DependencyObject;
            while (current is not null && current != OverlayCanvas)
            {
                if (current is FrameworkElement fe
                    && fe.Tag is string tag
                    && tag.StartsWith(MeasureLabelTagPrefix, StringComparison.Ordinal))
                {
                    measureId = tag[MeasureLabelTagPrefix.Length..];
                    return !string.IsNullOrEmpty(measureId);
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void BeginLabelDrag(string measureId, Point viewportPos)
        {
            _draggingLabelMeasureId = measureId;
            _labelDragMoved = false;
            _vm.SelectedMeasureId = measureId;
            Cursor = Cursors.SizeAll;
            Mouse.Capture(ImageScrollViewer, CaptureMode.SubTree);
            RedrawCommittedOverlays();
            // 不在按下瞬间改坐标：仅 MouseMove 超过阈值后才写入，避免单击/双击误改位置
            _ = viewportPos;
        }

        private void UpdateDraggingLabelPosition(Point viewportPos)
        {
            if (string.IsNullOrEmpty(_draggingLabelMeasureId) || _bitmap is null)
            {
                return;
            }

            var target = _vm.SelectedImage?.Measures.FirstOrDefault(m => m.Id == _draggingLabelMeasureId);
            if (target is null)
            {
                return;
            }

            var posInHost = ImageScrollViewer.TranslatePoint(viewportPos, ImageHost);
            var ix = Math.Clamp(posInHost.X / _zoom, 0, _bitmap.PixelWidth);
            var iy = Math.Clamp(posInHost.Y / _zoom, 0, _bitmap.PixelHeight);

            // 尚未自定义位置时：相对默认位置移动超过约 3px（图像系）才开始跟手
            if (!_labelDragMoved)
            {
                Point defaultCenter;
                if (target.HasCustomLabelPosition)
                {
                    defaultCenter = new Point(target.LabelX!.Value, target.LabelY!.Value);
                }
                else
                {
                    var p1 = new Point(target.X1, target.Y1);
                    var p2 = new Point(target.X2, target.Y2);
                    var c1 = ImageToCanvas(p1);
                    var c2 = ImageToCanvas(p2);
                    var (_, cLabel) = GetPerpendicularLeader(c1, c2, LabelLeaderLength);
                    defaultCenter = new Point(cLabel.X / _zoom, cLabel.Y / _zoom);
                }

                var dist = Math.Sqrt((ix - defaultCenter.X) * (ix - defaultCenter.X)
                                     + (iy - defaultCenter.Y) * (iy - defaultCenter.Y));
                if (dist < 3.0)
                {
                    return;
                }

                _labelDragMoved = true;
            }

            target.LabelX = ix;
            target.LabelY = iy;
            RedrawCommittedOverlays();
        }

        private void EndLabelDrag(bool commit)
        {
            if (string.IsNullOrEmpty(_draggingLabelMeasureId))
            {
                return;
            }

            if (commit && _labelDragMoved)
            {
                _vm.MarkDirty();
                _vm.StatusText = "已移动标签位置（导出时保留）";
            }

            _draggingLabelMeasureId = null;
            _labelDragMoved = false;
            if (Mouse.Captured == ImageScrollViewer)
            {
                Mouse.Capture(null);
            }

            UpdateModeUi();
        }

        private void PromptRenameMeasure(string measureId)
        {
            var target = _vm.SelectedImage?.Measures.FirstOrDefault(m => m.Id == measureId);
            if (target is null)
            {
                return;
            }

            _vm.SelectedMeasureId = measureId;
            RedrawCommittedOverlays();

            var dialog = new Window
            {
                Title = "修改测距名称",
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30)),
            };
            var box = new TextBox
            {
                Text = target.Label,
                Margin = new Thickness(16, 20, 16, 8),
                FontSize = 14,
            };
            var ok = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true,
            };
            var cancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 28,
                IsCancel = true,
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 8, 16, 16),
                Children = { ok, cancel },
            };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "测距名称",
                Foreground = Brushes.White,
                Margin = new Thickness(16, 16, 16, 4),
            });
            panel.Children.Add(box);
            panel.Children.Add(new TextBlock
            {
                Text = "名称将同步到图上标签与底部信息区",
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                Margin = new Thickness(16, 4, 16, 0),
                FontSize = 11,
            });
            panel.Children.Add(buttons);
            dialog.Content = panel;

            ok.Click += (_, _) =>
            {
                if (_vm.RenameMeasure(measureId, box.Text))
                {
                    dialog.DialogResult = true;
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show(dialog, "名称不能为空。", "修改名称", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            cancel.Click += (_, _) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            box.SelectAll();
            box.Focus();
            dialog.ShowDialog();
        }

        private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(ImageScrollViewer);

            if (!string.IsNullOrEmpty(_draggingLabelMeasureId)
                && e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateDraggingLabelPosition(pos);
                e.Handled = true;
                return;
            }

            if (_isPanning
                && (e.MiddleButton == MouseButtonState.Pressed
                    || e.LeftButton == MouseButtonState.Pressed
                    || Mouse.Captured == ImageScrollViewer))
            {
                var dx = pos.X - _panStart.X;
                var dy = pos.Y - _panStart.Y;
                if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
                {
                    _panStart = pos;
                    var maxH = Math.Max(0, ImageScrollViewer.ExtentWidth - ImageScrollViewer.ViewportWidth);
                    var maxV = Math.Max(0, ImageScrollViewer.ExtentHeight - ImageScrollViewer.ViewportHeight);
                    ImageScrollViewer.ScrollToHorizontalOffset(
                        Math.Clamp(ImageScrollViewer.HorizontalOffset - dx, 0, maxH));
                    ImageScrollViewer.ScrollToVerticalOffset(
                        Math.Clamp(ImageScrollViewer.VerticalOffset - dy, 0, maxV));
                }

                e.Handled = true;
                return;
            }

            if (!_vm.IsMeasureMode || !_hasAnchor || _spaceDown)
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
            if (e.ChangedButton == MouseButton.Left && !string.IsNullOrEmpty(_draggingLabelMeasureId))
            {
                EndLabelDrag(commit: true);
                e.Handled = true;
                return;
            }

            if (_isPanning && (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left))
            {
                _isPanning = false;
                if (Mouse.Captured == ImageScrollViewer)
                {
                    Mouse.Capture(null);
                }

                UpdateModeUi();
                e.Handled = true;
            }
        }

        private void Viewport_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!string.IsNullOrEmpty(_draggingLabelMeasureId))
            {
                EndLabelDrag(commit: true);
            }

            _isPanning = false;
            UpdateModeUi();
        }
    }
}
