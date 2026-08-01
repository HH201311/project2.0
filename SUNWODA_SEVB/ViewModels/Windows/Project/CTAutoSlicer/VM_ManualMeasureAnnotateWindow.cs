using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer
{
    /// <summary>二次手动标注全屏工作台 ViewModel。</summary>
    public sealed class VM_ManualMeasureAnnotateWindow : INotifyPropertyChanged
    {
        private ManualMeasureImageListItem? _selectedImage;
        private bool _isMeasureMode = true;
        private bool _hasUnsavedChanges;
        private string _statusText = string.Empty;
        private double _zoomPercent = 100;
        private string? _selectedMeasureId;

        public VM_ManualMeasureAnnotateWindow(
            string rootFolder,
            double pixelSizeUm,
            int bottomBlackHeightPx)
        {
            RootFolder = Path.GetFullPath(rootFolder);
            PixelSizeUm = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            // 底部信息遮罩高度（图像像素）；≤0 时仍留最小可读区
            BottomBlackHeightPx = Math.Max(0, bottomBlackHeightPx);

            SelectImageCommand = new RelayCommand(o =>
            {
                if (o is ManualMeasureImageListItem item)
                {
                    SelectedImage = item;
                }
            });
            ToggleMeasureModeCommand = new RelayCommand(_ => IsMeasureMode = !IsMeasureMode);
            UndoLastMeasureCommand = new RelayCommand(_ => UndoLastMeasure(), _ => (SelectedImage?.Measures.Count ?? 0) > 0);
            ClearCurrentMeasuresCommand = new RelayCommand(_ => ClearCurrentMeasures(), _ => (SelectedImage?.Measures.Count ?? 0) > 0);
            DeleteSelectedMeasureCommand = new RelayCommand(_ => DeleteSelectedMeasure(), _ => !string.IsNullOrEmpty(SelectedMeasureId));
            SelectPreviousImageCommand = new RelayCommand(_ => MoveSelection(-1), _ => Images.Count > 0);
            SelectNextImageCommand = new RelayCommand(_ => MoveSelection(1), _ => Images.Count > 0);
            ExportCommand = new RelayCommand(_ => Export(), _ => Images.Count > 0);

            LoadImages();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string RootFolder { get; }

        public double PixelSizeUm { get; }

        /// <summary>底部信息遮罩高度（图像像素，来自 OfflineCtBlackHeight）。</summary>
        public int BottomBlackHeightPx { get; }

        public ObservableCollection<ManualMeasureImageListItem> Images { get; } = [];

        public ManualMeasureImageListItem? SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (ReferenceEquals(_selectedImage, value))
                {
                    return;
                }

                _selectedImage = value;
                SelectedMeasureId = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentImagePath));
                OnPropertyChanged(nameof(CurrentRelativePath));
                OnPropertyChanged(nameof(AnnotatedCountText));
                RaiseCommandCanExecute();
                ImageSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? ImageSelectionChanged;

        public string CurrentImagePath => SelectedImage?.AbsolutePath ?? string.Empty;

        public string CurrentRelativePath => SelectedImage?.RelativePath ?? string.Empty;

        public bool IsMeasureMode
        {
            get => _isMeasureMode;
            set
            {
                if (_isMeasureMode == value)
                {
                    return;
                }

                _isMeasureMode = value;
                OnPropertyChanged();
                UpdateStatusHint();
            }
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set
            {
                if (_hasUnsavedChanges == value)
                {
                    return;
                }

                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText == value)
                {
                    return;
                }

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public double ZoomPercent
        {
            get => _zoomPercent;
            set
            {
                if (Math.Abs(_zoomPercent - value) < 0.01)
                {
                    return;
                }

                _zoomPercent = value;
                OnPropertyChanged();
            }
        }

        public string? SelectedMeasureId
        {
            get => _selectedMeasureId;
            set
            {
                if (_selectedMeasureId == value)
                {
                    return;
                }

                _selectedMeasureId = value;
                OnPropertyChanged();
                RaiseCommandCanExecute();
            }
        }

        public string AnnotatedCountText
        {
            get
            {
                var done = Images.Count(i => i.IsAnnotated);
                return $"已标 {done} / {Images.Count}";
            }
        }

        public string PixelSizeDisplay => $"{PixelSizeUm:G} μm/px";

        public ICommand SelectImageCommand { get; }
        public ICommand ToggleMeasureModeCommand { get; }
        public ICommand UndoLastMeasureCommand { get; }
        public ICommand ClearCurrentMeasuresCommand { get; }
        public ICommand DeleteSelectedMeasureCommand { get; }
        public ICommand SelectPreviousImageCommand { get; }
        public ICommand SelectNextImageCommand { get; }
        public ICommand ExportCommand { get; }

        public void AddMeasure(double x1, double y1, double x2, double y2)
        {
            if (SelectedImage is null)
            {
                return;
            }

            var label = $"M{SelectedImage.Measures.Count + 1}";
            var item = ManualMeasureExportHelper.CreateMeasure(x1, y1, x2, y2, PixelSizeUm, label);
            SelectedImage.Measures.Add(item);
            SelectedImage.NotifyAnnotatedChanged();
            SelectedMeasureId = item.Id;
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(AnnotatedCountText));
            RaiseCommandCanExecute();
            MeasuresChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>修改指定测距的显示名称；底部信息区与图上标签同步。</summary>
        public bool RenameMeasure(string measureId, string newLabel)
        {
            if (SelectedImage is null || string.IsNullOrWhiteSpace(measureId))
            {
                return false;
            }

            var target = SelectedImage.Measures.FirstOrDefault(m => m.Id == measureId);
            if (target is null)
            {
                return false;
            }

            var trimmed = (newLabel ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            if (string.Equals(target.Label, trimmed, StringComparison.Ordinal))
            {
                return true;
            }

            target.Label = trimmed;
            SelectedMeasureId = target.Id;
            HasUnsavedChanges = true;
            MeasuresChanged?.Invoke(this, EventArgs.Empty);
            StatusText = $"已改名：{trimmed}";
            return true;
        }

        public event EventHandler? MeasuresChanged;

        public void MarkDirty()
        {
            HasUnsavedChanges = true;
        }

        public string Export()
        {
            var records = Images.Select(i => new ManualMeasureImageRecord
            {
                RelativePath = i.RelativePath,
                AbsolutePath = i.AbsolutePath,
                Measures = i.Measures.ToList(),
            }).ToList();

            ManualMeasureExportHelper.ExportSession(
                RootFolder,
                PixelSizeUm,
                records,
                BottomBlackHeightPx);
            HasUnsavedChanges = false;
            var output = ManualMeasureExportHelper.BuildOutputDir(RootFolder);
            StatusText = $"已导出到 {output}";
            return output;
        }

        private void LoadImages()
        {
            Images.Clear();
            var files = ManualMeasureExportHelper.EnumerateImagesRecursive(RootFolder);
            foreach (var abs in files)
            {
                var rel = ManualMeasureExportHelper.ToRelativePath(RootFolder, abs);
                var measures = ManualMeasureExportHelper.TryLoadImageMeasures(RootFolder, rel);
                var item = new ManualMeasureImageListItem(abs, rel, measures);
                Images.Add(item);
            }

            SelectedImage = Images.FirstOrDefault();
            UpdateStatusHint();
            OnPropertyChanged(nameof(AnnotatedCountText));
        }

        private void UndoLastMeasure()
        {
            if (SelectedImage is null || SelectedImage.Measures.Count == 0)
            {
                return;
            }

            SelectedImage.Measures.RemoveAt(SelectedImage.Measures.Count - 1);
            SelectedImage.NotifyAnnotatedChanged();
            SelectedMeasureId = SelectedImage.Measures.LastOrDefault()?.Id;
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(AnnotatedCountText));
            RaiseCommandCanExecute();
            MeasuresChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearCurrentMeasures()
        {
            if (SelectedImage is null)
            {
                return;
            }

            SelectedImage.Measures.Clear();
            SelectedImage.NotifyAnnotatedChanged();
            SelectedMeasureId = null;
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(AnnotatedCountText));
            RaiseCommandCanExecute();
            MeasuresChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DeleteSelectedMeasure()
        {
            if (SelectedImage is null || string.IsNullOrEmpty(SelectedMeasureId))
            {
                return;
            }

            var target = SelectedImage.Measures.FirstOrDefault(m => m.Id == SelectedMeasureId);
            if (target is null)
            {
                return;
            }

            SelectedImage.Measures.Remove(target);
            // 删除后不重编号，保留用户自定义名称
            SelectedImage.NotifyAnnotatedChanged();
            SelectedMeasureId = null;
            HasUnsavedChanges = true;
            OnPropertyChanged(nameof(AnnotatedCountText));
            RaiseCommandCanExecute();
            MeasuresChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MoveSelection(int delta)
        {
            if (Images.Count == 0)
            {
                return;
            }

            var idx = SelectedImage is null ? 0 : Images.IndexOf(SelectedImage);
            if (idx < 0)
            {
                idx = 0;
            }

            idx = Math.Clamp(idx + delta, 0, Images.Count - 1);
            SelectedImage = Images[idx];
        }

        private void UpdateStatusHint()
        {
            StatusText = IsMeasureMode
                ? "标注模式：左键两点测距；拖拽标签避挡；双击标签改名；底部黑带汇总 mm"
                : "移动模式：左键拖拽平移；滚轮缩放；中键拖拽；Ctrl+Z 撤回；按 A 回标注";
        }

        private void RaiseCommandCanExecute()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>左侧图片列表项。</summary>
    public sealed class ManualMeasureImageListItem : INotifyPropertyChanged
    {
        public ManualMeasureImageListItem(
            string absolutePath,
            string relativePath,
            IEnumerable<ManualMeasureItem> measures)
        {
            AbsolutePath = absolutePath;
            RelativePath = relativePath;
            foreach (var m in measures)
            {
                Measures.Add(m);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string AbsolutePath { get; }

        public string RelativePath { get; }

        public string DisplayName => RelativePath;

        public ObservableCollection<ManualMeasureItem> Measures { get; } = [];

        public bool IsAnnotated => Measures.Count > 0;

        public string StatusText => IsAnnotated ? "已标" : "未标";

        public void NotifyAnnotatedChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAnnotated)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }
}
