using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SUNWODA_SEVB.ViewModels.Pages.JRRY;

namespace SUNWODA_SEVB.Views.Pages.JRRY
{
    public partial class AnnotationWindow : Window
    {
        private AnnotationWindowViewModel? _viewModel;
        // 用于管理当前绘制的点和折线的 UI 元素
        private readonly Polyline _currentPolyline;
        private readonly ObservableCollection<Ellipse> _currentPointMarkers = [];

        // 已完成线段的 UI 元素（每次重绘时清空重建）
        private readonly List<Polyline> _completedPolylines = [];
        private readonly List<Ellipse> _completedPointMarkers = [];

        public AnnotationWindow()
        {
            InitializeComponent();
            // 初始化当前绘制的折线，默认隐藏                        
            _currentPolyline = new Polyline { Stroke = Brushes.Red, StrokeThickness = 2 };
            AnnotationCanvas.Children.Add(_currentPolyline);
        }

        public AnnotationWindow(AnnotationWindowViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            viewModel.RequestClose += (_, _) => Close();

            // 监听当前点集合变化 → 更新红色折线和点标记
            viewModel.AnnotationPoints.CollectionChanged += OnCurrentPointsChanged;
            // 监听已完成线段集合变化 → 重绘蓝色历史折线
            viewModel.CompletedSegments.CollectionChanged += OnCompletedSegmentsChanged;
        }

        /// <summary>
        /// 当前点集合变化事件处理：更新红色折线和点标记
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCurrentPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 更新折线
            _currentPolyline.Points = [.. _viewModel!.AnnotationPoints];

            // 清除现有点标记
            foreach (var marker in _currentPointMarkers)
                AnnotationCanvas.Children.Remove(marker);

            _currentPointMarkers.Clear();

            // 为当前折线的每个点添加点标记
            foreach (var point in _viewModel.AnnotationPoints)
            {
                var ellipse = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = Brushes.Yellow,
                };
                Canvas.SetLeft(ellipse, point.X - 3);
                Canvas.SetTop(ellipse, point.Y - 3);
                _currentPointMarkers.Add(ellipse);
                AnnotationCanvas.Children.Add(ellipse);
            }
        }

        /// <summary>
        /// 已完成线段集合变化事件处理：清除原有蓝色折线和点标记，重新绘制每条已完成线段及其点标记
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCompletedSegmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 清除原有蓝色元素
            foreach (var poly in _completedPolylines)
                AnnotationCanvas.Children.Remove(poly);

            foreach (var marker in _completedPointMarkers)
                AnnotationCanvas.Children.Remove(marker);

            _completedPolylines.Clear();
            _completedPointMarkers.Clear();

            // 重新绘制每一条已完成线段（蓝色）
            foreach (var segment in _viewModel!.CompletedSegments)
            {
                var polyline = new Polyline
                {
                    Stroke = Brushes.Blue,
                    StrokeThickness = 2,
                    Points = [.. segment.Points]
                };
                _completedPolylines.Add(polyline);
                AnnotationCanvas.Children.Add(polyline);

                // 为每个点添加蓝色标记
                foreach (var point in segment.Points)
                {
                    var ellipse = new Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = Brushes.Green,
                    };
                    Canvas.SetLeft(ellipse, point.X - 3);
                    Canvas.SetTop(ellipse, point.Y - 3);
                    _completedPointMarkers.Add(ellipse);
                    AnnotationCanvas.Children.Add(ellipse);
                }
            }
        }

        /// <summary>
        /// 鼠标左键点击事件处理：如果处于绘制模式，则将点击位置添加为当前标注点
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AnnotationCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel?.IsDrawingMode == true)
            {
                Point clickPoint = e.GetPosition(AnnotationCanvas);
                _viewModel.AddAnnotationPoint(clickPoint);
            }
        }
    }
}