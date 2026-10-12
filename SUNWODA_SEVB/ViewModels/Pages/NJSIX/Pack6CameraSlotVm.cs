using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.ViewModels.Pages.NJSIX
{
    /// <summary>十相机预览窗状态。</summary>
    public sealed class Pack6CameraSlotVm : ModelBase
    {
        private string _lastStatus = "-";
        private string _lastFileName = "-";
        private string _lastResult = "-";
        private string _lastPreviewPath = string.Empty;
        private string _fullImagePath = string.Empty;
        private Brush _statusBrush = Brushes.Gray;
        private ImageSource? _preview;

        public Pack6CameraSlotVm(Pack6CameraCatalog.CameraDef def)
        {
            CameraName = def.CameraName;
            QueueName = def.QueueName;
            DiskHint = def.DiskHint;
            Title = $"{def.DisplayTitle}（{def.DiskHint}）";
            DesignShots = def.DesignShotCount;
        }

        public string CameraName { get; }
        public string QueueName { get; }
        public string DiskHint { get; }
        public string Title { get; }
        public int DesignShots { get; }

        /// <summary>双击全屏用的图片路径（优先标注图）。</summary>
        public string FullImagePath
        {
            get => _fullImagePath;
            private set => SetProperty(ref _fullImagePath, value);
        }

        public string LastStatus
        {
            get => _lastStatus;
            set => SetProperty(ref _lastStatus, value);
        }

        public string LastFileName
        {
            get => _lastFileName;
            set => SetProperty(ref _lastFileName, value);
        }

        public string LastResult
        {
            get => _lastResult;
            set => SetProperty(ref _lastResult, value);
        }

        public Brush StatusBrush
        {
            get => _statusBrush;
            set => SetProperty(ref _statusBrush, value);
        }

        public ImageSource? Preview
        {
            get => _preview;
            set => SetProperty(ref _preview, value);
        }

        public void ApplyFrame(Pack6FrameResult frame)
        {
            LastStatus = $"{DateTime.Now:HH:mm:ss} #{frame.ShotIndex}";
            LastFileName = Path.GetFileName(frame.FilePath) ?? "-";
            if (frame.IsVisionAlgorithmError)
            {
                LastResult = "视觉检测异常";
                StatusBrush = Brushes.OrangeRed;
            }
            else
            {
                LastResult = frame.IsOk ? "OK" : "NG";
                StatusBrush = frame.IsOk ? Brushes.ForestGreen : Brushes.IndianRed;
            }

            var previewPath = !string.IsNullOrWhiteSpace(frame.PreviewImagePath) &&
                              File.Exists(frame.PreviewImagePath)
                ? frame.PreviewImagePath
                : frame.FilePath;
            FullImagePath = previewPath;
            TryLoadPreview(previewPath);
        }

        private void TryLoadPreview(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                path.StartsWith("sim://", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
                return;

            // 同路径不重复解码；缩略图足够预览，避免 5472 宽图常驻
            if (string.Equals(path, _lastPreviewPath, StringComparison.OrdinalIgnoreCase) &&
                Preview is not null)
                return;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.DecodePixelWidth = 160;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                Preview = bmp;
                _lastPreviewPath = path;
            }
            catch
            {
                // 预览失败不影响检测
            }
        }
    }
}
