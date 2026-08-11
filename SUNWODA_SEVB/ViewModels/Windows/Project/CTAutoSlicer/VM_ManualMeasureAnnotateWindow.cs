using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.ViewModels.Windows.Project.CTAutoSlicer
{
    /// <summary>二次手动标注全屏工作台 ViewModel。</summary>
    public sealed class VM_ManualMeasureAnnotateWindow : INotifyPropertyChanged
    {
        private readonly CTAutoSlicerLocalConfigService _localConfig = new();
        private ManualMeasureImageListItem? _selectedImage;
        private bool _isMeasureMode = true;
        private bool _hasUnsavedChanges;
        private string _statusText = string.Empty;
        private double _zoomPercent = 100;
        private string? _selectedMeasureId;
        private string _exportFolder = string.Empty;
        private bool _persistExportFolderEnabled;
        private int _currentImageWidthPx;
        private int _currentImageHeightPx;

        public VM_ManualMeasureAnnotateWindow(
            string rootFolder,
            double pixelSizeUm,
            int bottomBlackHeightPx,
            string? exportFolder = null)
        {
            RootFolder = Path.GetFullPath(rootFolder);
            PixelSizeUm = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            // 底部信息遮罩高度（图像像素）；≤0 时仍留最小可读区
            BottomBlackHeightPx = Math.Max(0, bottomBlackHeightPx);

            // 优先：调用方传入 → 本机 local.json → UNC 默认
            var initial = !string.IsNullOrWhiteSpace(exportFolder)
                ? exportFolder
                : _localConfig.LoadManualMeasureExportFolder();
            _exportFolder = NormalizeExportFolderInput(initial);

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
            BrowseExportFolderCommand = new RelayCommand(_ => BrowseExportFolder());
            ResetExportFolderCommand = new RelayCommand(_ =>
                ExportFolder = ManualMeasureExportHelper.DefaultManualExportFolder);

            // 把目录下已有 annotation.json / 本进程 AI 出图缓存载入，左栏显示 AI 尺寸
            OfflineCtAnnotationHelper.PreloadAiSummariesUnderFolder(RootFolder, PixelSizeUm);
            LoadImages();
            _persistExportFolderEnabled = true;
            // 首次打开若尚无本机记录，把当前默认写入 local.json
            PersistExportFolder(_exportFolder);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string RootFolder { get; }

        /// <summary>手动标注导出目录；默认 UNC，改选后写入本机 local.json。</summary>
        public string ExportFolder
        {
            get => _exportFolder;
            set
            {
                var normalized = NormalizeExportFolderInput(value);
                if (string.Equals(_exportFolder, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _exportFolder = normalized;
                OnPropertyChanged();
                PersistExportFolder(_exportFolder);
                StatusText = $"导出目录：{_exportFolder}";
            }
        }

        public double PixelSizeUm { get; }

        /// <summary>底部信息遮罩高度配置（图像像素，来自 OfflineCtBlackHeight；原图自动 ÷2）。</summary>
        public int BottomBlackHeightPx { get; }

        /// <summary>当前图片像素宽。</summary>
        public int CurrentImageWidthPx => _currentImageWidthPx;

        /// <summary>当前图片像素高。</summary>
        public int CurrentImageHeightPx => _currentImageHeightPx;

        /// <summary>当前图片尺寸文案，如 1844×1500。</summary>
        public string CurrentImageSizeText =>
            _currentImageWidthPx > 0 && _currentImageHeightPx > 0
                ? $"{_currentImageWidthPx}×{_currentImageHeightPx}"
                : "—";

        /// <summary>当前图实际应用的打黑高度（与 AI 出图同一规则）。</summary>
        public string CurrentBlackHeightText
        {
            get
            {
                if (_currentImageHeightPx <= 0 || BottomBlackHeightPx <= 0)
                {
                    return "—";
                }

                var h = OfflineCtAnnotationHelper.ResolveConfiguredBottomBlackHeightForPath(
                    CurrentImagePath,
                    BottomBlackHeightPx,
                    _currentImageHeightPx);
                h = CTAutoSlicerSettings.ClampBottomBlackHeight(h, _currentImageHeightPx);
                return $"{h} px";
            }
        }

        /// <summary>由窗口加载位图后写入当前图像素尺寸。</summary>
        public void SetCurrentImagePixelSize(int widthPx, int heightPx)
        {
            _currentImageWidthPx = Math.Max(0, widthPx);
            _currentImageHeightPx = Math.Max(0, heightPx);
            OnPropertyChanged(nameof(CurrentImageWidthPx));
            OnPropertyChanged(nameof(CurrentImageHeightPx));
            OnPropertyChanged(nameof(CurrentImageSizeText));
            OnPropertyChanged(nameof(CurrentBlackHeightText));
        }

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
                OnPropertyChanged(nameof(CurrentBlackHeightText));
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
        public ICommand BrowseExportFolderCommand { get; }
        public ICommand ResetExportFolderCommand { get; }

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

        /// <summary>
        /// 更新测距两端点并重算长度（拖拽微调时调用）。
        /// <paramref name="raiseChanged"/> 为 false 时不触发全量刷新，由界面自行重绘。
        /// </summary>
        public bool TryApplyMeasureGeometry(
            string measureId,
            double x1,
            double y1,
            double x2,
            double y2,
            bool raiseChanged = false)
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

            target.X1 = x1;
            target.Y1 = y1;
            target.X2 = x2;
            target.Y2 = y2;
            ManualMeasureExportHelper.RecalcLengths(target, PixelSizeUm);
            SelectedMeasureId = target.Id;
            if (raiseChanged)
            {
                HasUnsavedChanges = true;
                MeasuresChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
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

            var outDir = ResolveExportFolderForWrite();
            ManualMeasureExportHelper.ExportSession(
                RootFolder,
                PixelSizeUm,
                records,
                BottomBlackHeightPx,
                outDir,
                rangeCheck: null,
                renderImages: false);
            // 写回规范化后的绝对路径，便于状态栏与下次导出一致
            if (!string.Equals(_exportFolder, outDir, StringComparison.OrdinalIgnoreCase))
            {
                _exportFolder = outDir;
                OnPropertyChanged(nameof(ExportFolder));
            }

            HasUnsavedChanges = false;
            StatusText =
                $"已导出到 {outDir}（扁平图截屏 + {OfflineCtCsvExportHelper.ProductDimensionFileName} + {OfflineCtCsvExportHelper.ManualMeasureFileName}）";
            return outDir;
        }

        private void BrowseExportFolder()
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择手动标注导出目录（默认 result）",
                IsFolderPicker = true,
            };
            if (!string.IsNullOrWhiteSpace(ExportFolder) && Directory.Exists(ExportFolder))
            {
                dialog.InitialDirectory = ExportFolder;
            }
            else if (Directory.Exists(RootFolder))
            {
                dialog.InitialDirectory = RootFolder;
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok
                && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                ExportFolder = dialog.FileName;
            }
        }

        private string ResolveInitialExportFolder(string? exportFolder) =>
            NormalizeExportFolderInput(exportFolder);

        private void PersistExportFolder(string folder)
        {
            if (!_persistExportFolderEnabled || string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            try
            {
                _localConfig.SaveManualMeasureExportFolder(folder);
            }
            catch
            {
                // 本机配置写失败不阻断标注
            }
        }

        /// <summary>导出前把路径规范为绝对路径；空则回退 UNC 默认。</summary>
        private string ResolveExportFolderForWrite()
        {
            if (string.IsNullOrWhiteSpace(ExportFolder))
            {
                return ManualMeasureExportHelper.DefaultManualExportFolder;
            }

            try
            {
                return NormalizeUncOrFullPath(ExportFolder.Trim());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"导出目录无效：{ExportFolder}", ex);
            }
        }

        private string NormalizeExportFolderInput(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ManualMeasureExportHelper.DefaultManualExportFolder;
            }

            var trimmed = value.Trim();
            try
            {
                return NormalizeUncOrFullPath(trimmed);
            }
            catch
            {
                // 输入过程中路径可能暂不合法，先原样保留
                return trimmed;
            }
        }

        /// <summary>UNC 与本地路径统一规范化；保留 \\server\share 形态。</summary>
        private static string NormalizeUncOrFullPath(string path)
        {
            var full = Path.GetFullPath(path.Trim());
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private void LoadImages()
        {
            Images.Clear();
            var files = ManualMeasureExportHelper.EnumerateImagesRecursive(RootFolder, ExportFolder);
            var legacyDir = ManualMeasureExportHelper.ResolveLegacyExportDir(RootFolder);
            foreach (var abs in files)
            {
                var rel = ManualMeasureExportHelper.ToRelativePath(RootFolder, abs);
                var measures = ManualMeasureExportHelper.TryLoadImageMeasures(
                    ExportFolder,
                    rel,
                    legacyDir);
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
                ? "标注：左键两点测距；拖端点微调；单击直线整体拖；选中后 Delete 删除；拖标签/双击改名"
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
