using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer
{
    public partial class Vision3dMidGrayRangeTunerWindow : Window
    {
        private readonly VM_Vision3dPointsStepDebugWindow _mainVm;
        private int _lo;
        private int _hi;
        private const int GrayMin = 0;
        private const int GrayMax = 255;

        private double _trackLeft;
        private double _trackWidth;
        private bool _rangeReady;
        private DispatcherTimer? _debounceTimer;
        private bool _isInitializing = true;

        public Vision3dMidGrayRangeTunerWindow(
            VM_Vision3dPointsStepDebugWindow mainVm,
            int initialLo,
            int initialHi)
        {
            InitializeComponent();
            _mainVm = mainVm ?? throw new ArgumentNullException(nameof(mainVm));

            _isInitializing = true;

            _lo = Math.Clamp(initialLo, 0, 255);
            _hi = Math.Clamp(initialHi, 0, 255);
            if (_lo > _hi)
            {
                (_lo, _hi) = (_hi, _lo);
            }

            // 先初始化计时器：避免 ValueChanged 在赋初值时触发而 _debounceTimer 仍为 null
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            MidValueText.Text = ((int)Math.Round((_lo + _hi) / 2.0)).ToString();
            LoValueText.Text = _lo.ToString();
            HiValueText.Text = _hi.ToString();

            UpdatePreview();

            // 拇指位置在 RangeCanvas_Loaded 里初始化
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RangeCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化期间忽略拖拽回调，避免时序问题
            _isInitializing = true;

            _trackLeft = Canvas.GetLeft(TrackBorder);
            _trackWidth = TrackBorder.ActualWidth;
            _rangeReady = _trackWidth > 1;

            if (_rangeReady)
            {
                SetThumbValue(LeftThumb, _lo);
                SetThumbValue(RightThumb, _hi);
            }

            _isInitializing = false;
            UpdatePreview();
        }

        private void LeftThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_isInitializing || !_rangeReady)
            {
                return;
            }

            var thumbHalf = LeftThumb.ActualWidth > 0 ? LeftThumb.ActualWidth / 2.0 : LeftThumb.Width / 2.0;
            var minLeft = _trackLeft - thumbHalf;
            var rightCenter = Canvas.GetLeft(RightThumb) +
                              (RightThumb.ActualWidth > 0 ? RightThumb.ActualWidth / 2.0 : RightThumb.Width / 2.0);
            var maxLeft = rightCenter - thumbHalf;
            var nextLeft = Canvas.GetLeft(LeftThumb) + e.HorizontalChange;
            nextLeft = Math.Clamp(nextLeft, minLeft, maxLeft);
            Canvas.SetLeft(LeftThumb, nextLeft);

            var lo = GetThumbValue(LeftThumb);
            lo = Math.Clamp(lo, GrayMin, _hi);
            _lo = lo;
            SetThumbValue(LeftThumb, _lo);

            LoValueText.Text = _lo.ToString();
            MidValueText.Text = ((int)Math.Round((_lo + _hi) / 2.0)).ToString();

            RestartDebounce();
        }

        private void RightThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_isInitializing || !_rangeReady)
            {
                return;
            }

            var thumbHalf = RightThumb.ActualWidth > 0 ? RightThumb.ActualWidth / 2.0 : RightThumb.Width / 2.0;
            var maxLeft = _trackLeft + _trackWidth - thumbHalf;
            var leftCenter = Canvas.GetLeft(LeftThumb) +
                             (LeftThumb.ActualWidth > 0 ? LeftThumb.ActualWidth / 2.0 : LeftThumb.Width / 2.0);
            var minLeft = leftCenter - thumbHalf;
            var nextLeft = Canvas.GetLeft(RightThumb) + e.HorizontalChange;
            nextLeft = Math.Clamp(nextLeft, minLeft, maxLeft);
            Canvas.SetLeft(RightThumb, nextLeft);

            var hi = GetThumbValue(RightThumb);
            hi = Math.Clamp(hi, _lo, GrayMax);
            _hi = hi;
            SetThumbValue(RightThumb, _hi);

            HiValueText.Text = _hi.ToString();
            MidValueText.Text = ((int)Math.Round((_lo + _hi) / 2.0)).ToString();

            RestartDebounce();
        }

        private void SetThumbValue(Thumb thumb, int value)
        {
            var v = Math.Clamp(value, GrayMin, GrayMax);
            var t = (v - GrayMin) / (double)(GrayMax - GrayMin);
            var xCenter = _trackLeft + t * _trackWidth;
            var thumbHalf = thumb.ActualWidth > 0 ? thumb.ActualWidth / 2.0 : thumb.Width / 2.0;
            Canvas.SetLeft(thumb, xCenter - thumbHalf);
        }

        private int GetThumbValue(Thumb thumb)
        {
            var thumbHalf = thumb.ActualWidth > 0 ? thumb.ActualWidth / 2.0 : thumb.Width / 2.0;
            var xCenter = Canvas.GetLeft(thumb) + thumbHalf;
            var t = (xCenter - _trackLeft) / Math.Max(1e-6, _trackWidth);
            t = Math.Clamp(t, 0.0, 1.0);
            return (int)Math.Round(GrayMin + t * (GrayMax - GrayMin));
        }

        private void RestartDebounce()
        {
            if (_isInitializing)
            {
                return;
            }

            if (_debounceTimer == null)
            {
                return;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            MidValueText.Text = ((int)Math.Round((_lo + _hi) / 2.0)).ToString();
            LoValueText.Text = _lo.ToString();
            HiValueText.Text = _hi.ToString();

            _mainVm.ApplyMidGrayRange(_lo, _hi);
            var img = _mainVm.GetMidGrayInRangePreviewImage(_lo, _hi);
            PreviewImage.Source = img;
        }
    }
}

