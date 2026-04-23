using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SUNWODA_SEVB.Core.Common.Commands;

namespace SUNWODA_SEVB.Component.UserControls.Project.CTOL
{
    /// <summary>
    /// ImageShowControl.xaml 的交互逻辑
    /// </summary>
    public partial class ImageShowControl : UserControl, INotifyPropertyChanged
    {
        #region 常量
        private const double ZoomStep = 0.1;
        private const double MinScale = 0.2;
        private const double MaxScale = 5.0;
        #endregion

        #region 字段
        private bool _isImageDragging; //是否正在拖动
        private Point _startMousePoint; // 拖动起始鼠标位置
        private double _startTranslateX; // 拖动起始X偏移
        private double _startTranslateY; // 拖动起始Y偏移

        private double _realTimeImageScaleX = 1.0;
        private double _realTimeImageScaleY = 1.0;
        private double _realTimeImageTranslateX;
        private double _realTimeImageTranslateY;

        private FrameworkElement? _dragContainer;
        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        #region 属性
        /// <summary>
        /// 实时图片X方向缩放比例
        /// </summary>
        public double RealTimeImageScaleX
        {
            get => _realTimeImageScaleX;
            set => SetProperty(ref _realTimeImageScaleX, value);
        }

        /// <summary>
        /// 实时图片Y方向缩放比例
        /// </summary>
        public double RealTimeImageScaleY
        {
            get => _realTimeImageScaleY;
            set => SetProperty(ref _realTimeImageScaleY, value);
        }

        /// <summary>
        /// 图片拖动X轴偏移
        /// </summary>
        public double RealTimeImageTranslateX
        {
            get => _realTimeImageTranslateX;
            set => SetProperty(ref _realTimeImageTranslateX, value);
        }

        /// <summary>
        /// 图片拖动Y轴偏移
        /// </summary>
        public double RealTimeImageTranslateY
        {
            get => _realTimeImageTranslateY;
            set => SetProperty(ref _realTimeImageTranslateY, value);
        }
        #endregion

        #region 命令
        /// <summary>
        /// 鼠标滚轮缩放
        /// </summary>
        public ICommand ZoomRealTimeImageCommand { get; }

        /// <summary>
        /// 鼠标按下命令（开始拖动）
        /// </summary>
        public ICommand ImageMouseDownCommand { get; }

        /// <summary>
        /// 鼠标移动命令（拖动中）
        /// </summary>
        public ICommand ImageMouseMoveCommand { get; }

        /// <summary>
        /// 鼠标松开命令（结束拖动）
        /// </summary>
        public ICommand ImageMouseUpCommand { get; }

        /// <summary>
        /// 重置缩放
        /// </summary>
        public ICommand ResetRealTimeImageZoomCommand { get; }

        /// <summary>
        /// 放大按钮
        /// </summary>
        public ICommand ZoomInRealTimeCommand { get; }

        /// <summary>
        /// 缩小按钮
        /// </summary>
        public ICommand ZoomOutRealTimeCommand { get; }
        #endregion

        #region 依赖属性
        /// <summary>
        /// 图片源
        /// </summary>
        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            "Source",
            typeof(ImageSource),
            typeof(ImageShowControl),
            new PropertyMetadata(null, OnSourceChanged)
        );
        #endregion

        /// <summary>
        /// 触发属性已更改事件
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static void OnSourceChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            if (d is ImageShowControl imgShowControl)
            {
                if (e.NewValue is ImageSource newSource)
                    imgShowControl.imageControl.Source = newSource;
            }
        }

        /// <summary>
        /// 设置属性值并触发通知
        /// </summary>
        protected virtual bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null
        )
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public ImageShowControl()
        {
            InitializeComponent();
            ZoomRealTimeImageCommand = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImage);
            ImageMouseDownCommand = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDown);
            ImageMouseMoveCommand = new RelayCommand<MouseEventArgs>(OnImageMouseMove);
            ImageMouseUpCommand = new RelayCommand<MouseEventArgs>(OnImageMouseUp);
            ResetRealTimeImageZoomCommand = new RelayCommand(OnResetRealTimeZoom);
            ZoomInRealTimeCommand = new RelayCommand(OnZoomInRealTime);
            ZoomOutRealTimeCommand = new RelayCommand(OnZoomOutRealTime);

            SizeChanged += ImageShowControl_SizeChanged;
            Loaded += ImageShowControl_Loaded;
        }

        private void ImageShowControl_Loaded(object sender, RoutedEventArgs e)
        {
            OnResetRealTimeZoom();
        }

        private void ImageShowControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 窗口大小变化后，修正当前平移，避免图片跑偏
            ClampTranslate(imageControl);
        }

        /// <summary>
        /// 滚轮缩放
        /// </summary>
        /// <param name="e"></param>
        private void OnZoomRealTimeImage(MouseWheelEventArgs? e)
        {
            if (e == null)
                return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大
                RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }
            else
            {
                // 缩小
                RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
                RealTimeImageScaleY = RealTimeImageScaleX;
            }

            // 缩放后把平移也校正到合理范围
            ClampTranslate(imageControl);

            e.Handled = true;
        }

        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDown(MouseButtonEventArgs? e)
        {
            if (e == null) return;

            if (e.Source is Image image)
            {
                _isImageDragging = true;

                // 取一个稳定坐标系：Image 的父容器（你的结构里就是 Grid）
                _dragContainer = image.Parent as FrameworkElement ?? this;

                // 起点：容器坐标系（不要再用 image 坐标系）
                _startMousePoint = e.GetPosition(_dragContainer);

                _startTranslateX = RealTimeImageTranslateX;
                _startTranslateY = RealTimeImageTranslateY;

                image.CaptureMouse();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMove(MouseEventArgs? e)
        {
            if (e == null) return;
            if (!_isImageDragging) return;
            if (e.Source is not Image image) return;

            var container = _dragContainer ?? (image.Parent as FrameworkElement) ?? this;

            // 当前点：容器坐标系
            Point currentMousePoint = e.GetPosition(container);

            double offsetX = currentMousePoint.X - _startMousePoint.X;
            double offsetY = currentMousePoint.Y - _startMousePoint.Y;

            RealTimeImageTranslateX = _startTranslateX + offsetX;
            RealTimeImageTranslateY = _startTranslateY + offsetY;

            // 限制不完全消失（用你现有的 ClampTranslate，或我之前给的版本）
            ClampTranslate(image);

            e.Handled = true;
        }

        private void ClampTranslate(Image image)
        {
            if (image.Parent is not FrameworkElement container)
                return;

            double containerW = container.ActualWidth;
            double containerH = container.ActualHeight;
            if (containerW <= 0 || containerH <= 0) return;

            // 至少保留可见像素（你可以调整）
            const double minVisible = 30.0;

            // 关键：拿到“真实变换后”的图片边界（包含 RenderTransformOrigin、Scale、Translate）
            Rect bounds;
            try
            {
                GeneralTransform gt = image.TransformToAncestor(container);
                bounds = gt.TransformBounds(new Rect(0, 0, image.ActualWidth, image.ActualHeight));
            }
            catch
            {
                // 在某些布局/未加载阶段 TransformToAncestor 可能异常，直接跳过
                return;
            }

            // 计算需要纠正的 delta（容器坐标系像素）
            double dx = 0;
            double dy = 0;

            // 左侧不能超过 containerW - minVisible（即至少露出 minVisible）
            if (bounds.Left > containerW - minVisible)
                dx = (containerW - minVisible) - bounds.Left;

            // 右侧不能小于 minVisible
            if (bounds.Right < minVisible)
                dx = minVisible - bounds.Right;

            // 上侧不能超过 containerH - minVisible
            if (bounds.Top > containerH - minVisible)
                dy = (containerH - minVisible) - bounds.Top;

            // 下侧不能小于 minVisible
            if (bounds.Bottom < minVisible)
                dy = minVisible - bounds.Bottom;

            // 应用纠偏（Translate 是 Scale 后执行，单位正好也是容器像素，可直接加）
            if (dx != 0) RealTimeImageTranslateX += dx;
            if (dy != 0) RealTimeImageTranslateY += dy;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUp(MouseEventArgs? e)
        {
            if (e == null) return;

            if (e.Source is Image image)
            {
                _isImageDragging = false;
                _dragContainer = null;
                image.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnResetRealTimeZoom()
        {
            RealTimeImageScaleX = 1.0;
            RealTimeImageScaleY = 1.0;
            RealTimeImageTranslateX = 0; // 回到X轴初始位置
            RealTimeImageTranslateY = 0; // 回到Y轴初始位置

            // 布局更新后再次校正，确保窗口缩小时仍然以当前容器为准
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ClampTranslate(imageControl);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnZoomInRealTime()
        {
            RealTimeImageScaleX = Math.Min(RealTimeImageScaleX + ZoomStep, MaxScale);
            RealTimeImageScaleY = RealTimeImageScaleX;
            ClampTranslate(imageControl);
        }

        private void OnZoomOutRealTime()
        {
            RealTimeImageScaleX = Math.Max(RealTimeImageScaleX - ZoomStep, MinScale);
            RealTimeImageScaleY = RealTimeImageScaleX;
            ClampTranslate(imageControl);
        }
    }
}
