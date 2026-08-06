using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer
{
    /// <summary>本地 3D 斜俯视找点分步调试窗口。</summary>
    public sealed class VM_Vision3dPointsStepDebugWindow : INotifyPropertyChanged, IDisposable
    {
        private Vision3dSectionPointsStepProbe? _probe;
        private ImageSource? _previewImage;
        private string _imagePath = string.Empty;
        private string _stepTitle = "请选择 3D ROI 截图后点「开始/重置」";
        private string _stepDetail = string.Empty;
        private string _stepProgress = "0 / 0";
        private Brush _statusBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        private bool _canStepNext;
        private bool _canStepPrev;
        private bool _canOpenMidGrayRangeTuner;
        private bool _disposed;

        public VM_Vision3dPointsStepDebugWindow(string? initialImagePath = null)
        {
            BrowseImageCommand = new RelayCommand(_ => BrowseImage());
            ResetCommand = new RelayCommand(_ => ResetSession(), _ => !string.IsNullOrWhiteSpace(ImagePath));
            StepNextCommand = new RelayCommand(_ => StepNext(), _ => CanStepNext);
            StepPrevCommand = new RelayCommand(_ => StepPrev(), _ => CanStepPrev);
            OpenMidGrayRangeTunerCommand = new RelayCommand(_ => OpenMidGrayRangeTuner(), _ => CanOpenMidGrayRangeTuner);
            if (!string.IsNullOrWhiteSpace(initialImagePath) && File.Exists(initialImagePath))
            {
                ImagePath = initialImagePath;
                ResetSession();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand BrowseImageCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand StepNextCommand { get; }
        public ICommand StepPrevCommand { get; }
        public ICommand OpenMidGrayRangeTunerCommand { get; }

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (Set(ref _imagePath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasImage));
                }
            }
        }

        public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath);

        public ImageSource? PreviewImage
        {
            get => _previewImage;
            private set => Set(ref _previewImage, value);
        }

        public string StepTitle
        {
            get => _stepTitle;
            private set => Set(ref _stepTitle, value);
        }

        public string StepDetail
        {
            get => _stepDetail;
            private set => Set(ref _stepDetail, value);
        }

        public string StepProgress
        {
            get => _stepProgress;
            private set => Set(ref _stepProgress, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => Set(ref _statusBrush, value);
        }

        public bool CanStepNext
        {
            get => _canStepNext;
            private set
            {
                if (Set(ref _canStepNext, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanStepPrev
        {
            get => _canStepPrev;
            private set
            {
                if (Set(ref _canStepPrev, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool CanOpenMidGrayRangeTuner
        {
            get => _canOpenMidGrayRangeTuner;
            private set
            {
                if (Set(ref _canOpenMidGrayRangeTuner, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private static readonly Brush BrushGray = CreateFrozenBrush(0xAA, 0xAA, 0xAA);
        private static readonly Brush BrushGreen = CreateFrozenBrush(0x4C, 0xAF, 0x50);
        private static readonly Brush BrushOrange = CreateFrozenBrush(0xE6, 0x7E, 0x22);
        private static readonly Brush BrushRed = CreateFrozenBrush(0xE7, 0x4C, 0x3C);

        private static Brush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private void BrowseImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 3D ROI 截图（vision_3d_*.png）",
                Filter = "图像|*.png;*.jpg;*.jpeg;*.bmp|全部|*.*",
                CheckFileExists = true,
            };
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                var dir = Path.GetDirectoryName(ImagePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                }
            }
            else
            {
                var def = @"C:\AutoSlice\cutpicture\jietu";
                if (Directory.Exists(def))
                {
                    dialog.InitialDirectory = def;
                }
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ImagePath = dialog.FileName;
            ResetSession();
        }

        private void ResetSession()
        {
            _probe?.Dispose();
            _probe = null;
            PreviewImage = null;
            CanStepNext = false;
            CanStepPrev = false;
            CanOpenMidGrayRangeTuner = false;
            StepProgress = "0 / 0";
            StatusBrush = BrushGray;

            if (!HasImage)
            {
                StepTitle = "请选择 3D ROI 截图后点「开始/重置」";
                StepDetail = string.Empty;
                return;
            }

            try
            {
                _probe = new Vision3dSectionPointsStepProbe(ImagePath);
                CanStepNext = true;
                StepTitle = "已就绪：点击「下一步」从①开始";
                StepDetail = $"图像：{ImagePath}\n共 {_probe.TotalSteps} 步，对应 Vision3dSectionPointsDetector 主链。";
                StepProgress = $"0 / {_probe.TotalSteps}";
                StatusBrush = BrushGreen;
                // 跑完①（原图/灰度）即可做灰度区间像素预览
                CanOpenMidGrayRangeTuner = _probe.CurrentStepIndex >= 0;
            }
            catch (Exception ex)
            {
                StepTitle = "初始化失败";
                StepDetail = ex.Message;
                StatusBrush = BrushRed;
            }
        }

        private void StepPrev()
        {
            if (_probe == null || !CanStepPrev)
            {
                return;
            }

            try
            {
                var frame = _probe.StepPrev();
                PreviewImage = MatToBitmapSource(frame.BgrImage);
                frame.BgrImage.Dispose();

                StepTitle = frame.Title;
                StepDetail = frame.Detail;
                StepProgress = $"{frame.StepIndex + 1} / {frame.TotalSteps}";
                StatusBrush = frame.Success ? BrushGreen : BrushOrange;
                if (frame.IsTerminal)
                {
                    StatusBrush = frame.Success ? BrushGreen : BrushRed;
                }

                CanStepNext = _probe.CanStepNext && !frame.IsTerminal;
                CanStepPrev = _probe.CanStepPrev;
                CanOpenMidGrayRangeTuner = _probe.CurrentStepIndex >= 0;
            }
            catch (Exception ex)
            {
                StepTitle = "步骤执行异常";
                StepDetail = ex.ToString();
                StatusBrush = BrushRed;
                CanStepNext = false;
                CanStepPrev = false;
                CanOpenMidGrayRangeTuner = false;
            }
        }

        private void StepNext()
        {
            if (_probe == null || !CanStepNext)
            {
                return;
            }

            try
            {
                var frame = _probe.StepNext();
                PreviewImage = MatToBitmapSource(frame.BgrImage);
                frame.BgrImage.Dispose();

                StepTitle = frame.Title;
                StepDetail = frame.Detail;
                StepProgress = $"{frame.StepIndex + 1} / {frame.TotalSteps}";
                StatusBrush = frame.Success ? BrushGreen : BrushOrange;
                CanStepNext = _probe.CanStepNext && !frame.IsTerminal;
                CanStepPrev = _probe.CanStepPrev;
                CanOpenMidGrayRangeTuner = _probe.CurrentStepIndex >= 0;

                if (frame.IsTerminal)
                {
                    StatusBrush = frame.Success ? BrushGreen : BrushRed;
                    CanStepNext = false;
                    CanStepPrev = _probe.CanStepPrev;
                    CanOpenMidGrayRangeTuner = _probe.CurrentStepIndex >= 0;
                }
            }
            catch (Exception ex)
            {
                StepTitle = "步骤执行异常";
                StepDetail = ex.ToString();
                StatusBrush = BrushRed;
                CanStepNext = false;
                CanStepPrev = false;
                CanOpenMidGrayRangeTuner = false;
            }
        }

        private void OpenMidGrayRangeTuner()
        {
            if (_probe == null)
            {
                return;
            }

            var win = new Vision3dMidGrayRangeTunerWindow(
                this,
                _probe.MidGrayLo,
                _probe.MidGrayHi);
            win.Show();
        }

        /// <summary>
        /// 给调参弹窗用：更新 probe 内部的中灰阈值覆盖，然后由弹窗自行渲染预览图。
        /// </summary>
        public void ApplyMidGrayRange(int lo, int hi)
        {
            _probe?.SetMidGrayRange(lo, hi);
        }

        /// <summary>
        /// 给调参弹窗用：只显示灰度落在 [lo,hi] 的原图像素，其余为黑。
        /// </summary>
        public ImageSource? GetMidGrayInRangePreviewImage(int lo, int hi)
        {
            if (_probe == null)
            {
                return null;
            }

            Mat? overlay = null;
            try
            {
                overlay = _probe.RenderMidGrayInRangePreview(lo, hi);
                return MatToBitmapSource(overlay);
            }
            finally
            {
                overlay?.Dispose();
            }
        }

        private static BitmapSource MatToBitmapSource(Mat mat)
        {
            Cv2.ImEncode(".png", mat, out var bytes);
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(name);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _probe?.Dispose();
            _probe = null;
            _disposed = true;
        }
    }
}
