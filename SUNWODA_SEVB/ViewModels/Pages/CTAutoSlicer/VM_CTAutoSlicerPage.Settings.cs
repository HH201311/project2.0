using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化切片页面 ViewModel（partial：Settings）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void LoadSettings()
        {
            _isLoadingSettings = true;
            VGAppPath = LoadStringSetting(nameof(VGAppPath), @"D:\App\VG\vgstudiomax20241.exe");
            VGOpenWaitTime = LoadIntSetting(nameof(VGOpenWaitTime), CTAutoSlicerSettings.DefaultVGOpenWaitTime);
            if (VGOpenWaitTime <= CTAutoSlicerSettings.LegacyVGOpenWaitTimeThreshold)
            {
                _logger.Info(
                    $"检测到旧版 VGOpenWaitTime={VGOpenWaitTime}ms，自动升级为 {CTAutoSlicerSettings.DefaultVGOpenWaitTime}ms");
                _projectSettingRepository.UpdateSettingValue(
                    nameof(VGOpenWaitTime),
                    VmName,
                    CTAutoSlicerSettings.DefaultVGOpenWaitTime);
                VGOpenWaitTime = CTAutoSlicerSettings.DefaultVGOpenWaitTime;
            }
            _vgShowFrontWaitTime = LoadIntSetting("VGShowFrontWaitTime", 2000);
            CTFileFolder = LoadStringSetting(nameof(CTFileFolder), string.Empty);
            MaxVgProcesses = LoadIntSetting(
                nameof(MaxVgProcesses),
                CTAutoSlicerSettings.DefaultMaxVgProcesses);
            QuickImportDialogWaitTime = LoadIntSetting(nameof(QuickImportDialogWaitTime), 10000);
            NewProjectDialogWaitTime = LoadIntSetting(nameof(NewProjectDialogWaitTime), 5000);
            FileSelectionDialogWaitTime = LoadIntSetting(nameof(FileSelectionDialogWaitTime), 15000);
            ImportTimeoutMinutes = LoadIntSetting(nameof(ImportTimeoutMinutes), 30);
            PostImportWaitTime = LoadIntSetting(nameof(PostImportWaitTime), 3000);
            StepRetryCount = LoadIntSetting(nameof(StepRetryCount), 3);
            StepRetryIntervalMs = LoadIntSetting(nameof(StepRetryIntervalMs), 2000);
            RenderingPresetNamePositive = LoadStringSetting(nameof(RenderingPresetNamePositive), "testlut_Z");
            RenderingPresetNameNegative = LoadStringSetting(nameof(RenderingPresetNameNegative), "testlut_F");
            PresetApplyWaitTime = LoadIntSetting(nameof(PresetApplyWaitTime), 2000);
            PresetDropdownWaitTime = LoadIntSetting(nameof(PresetDropdownWaitTime), 5000);
            ThickPlateMenuOffsetX = LoadIntSetting(nameof(ThickPlateMenuOffsetX), 90);
            ThickPlateMenuOffsetY = LoadIntSetting(nameof(ThickPlateMenuOffsetY), 127);
            ViewportClickOffsetY = LoadIntSetting(nameof(ViewportClickOffsetY), 0);
            _thickPlateStepDelayMs = LoadIntSetting("ThickPlateStepDelayMs", 500);
            _thickPlateDialogWaitTime = LoadIntSetting("ThickPlateDialogWaitTime", 3000);
            _thickPlateDropdownWaitTime = LoadIntSetting("ThickPlateDropdownWaitTime", 3000);
            _viewportQuadrant3ClickOffsetY = LoadIntSetting("ViewportQuadrant3ClickOffsetY", -100);
            _viewportMinWidth = LoadIntSetting("ViewportMinWidth", 400);
            _viewportMinHeight = LoadIntSetting("ViewportMinHeight", 300);
            _thickPlateStepRetryCount = LoadIntSetting("ThickPlateStepRetryCount", 1);
            _thickPlateMenuAfterRightClickDelayMs = LoadIntSetting("ThickPlateMenuAfterRightClickDelayMs", 200);
            SliceExportFolder = LoadStringSetting(nameof(SliceExportFolder), string.Empty);
            TopViewHandoffFolder = LoadStringSetting(nameof(TopViewHandoffFolder), string.Empty);
            _enableAiAnnotationAutoRun =
                LoadIntSetting(nameof(EnableAiAnnotationAutoRun), 0) != 0;
            // 机台角色存本机 JSON（Config/CTAutoSlicer/local.json），不落共享 DB
            PipelineRunMode = (int)_localConfigService.LoadPipelineRunMode();
            _logger.Info(
                $"已从本机配置恢复流水线模式：{(IsAiPipelineMode ? "AI标注" : "切图")}（{CTAutoSlicerLocalConfigService.ConfigFilePath}）");
            SliceExportImageWidth = LoadIntSetting(nameof(SliceExportImageWidth), 3000);
            SliceExportQuantity = LoadIntSetting(nameof(SliceExportQuantity), 100);
            SliceExportQuantityQj = LoadIntSetting(nameof(SliceExportQuantityQj), 150);
            // 三视角切片范围；若无新键则回填旧的单组 SliceRangeStart/End
            var legacyStart = LoadDoubleSetting(nameof(CTAutoSlicerSettings.SliceRangeStartMm), 20);
            var legacyEnd = LoadDoubleSetting(nameof(CTAutoSlicerSettings.SliceRangeEndMm), 70);
            SliceRangeFrontStartMm = LoadDoubleSetting(nameof(SliceRangeFrontStartMm), legacyStart);
            SliceRangeFrontEndMm = LoadDoubleSetting(nameof(SliceRangeFrontEndMm), legacyEnd);
            SliceRangeRightStartMm = LoadDoubleSetting(nameof(SliceRangeRightStartMm), legacyStart);
            SliceRangeRightEndMm = LoadDoubleSetting(nameof(SliceRangeRightEndMm), legacyEnd);
            SliceRangeTopStartMm = LoadDoubleSetting(nameof(SliceRangeTopStartMm), legacyStart);
            SliceRangeTopEndMm = LoadDoubleSetting(nameof(SliceRangeTopEndMm), legacyEnd);
            SliceExportDialogWaitTime = LoadIntSetting(nameof(SliceExportDialogWaitTime), 8000);
            SliceExportWriteTimeoutMs = LoadIntSetting(nameof(SliceExportWriteTimeoutMs), 60000);
            _histogramRoiX = LoadIntSetting(nameof(HistogramRoiX), 0);
            _histogramRoiY = LoadIntSetting(nameof(HistogramRoiY), 0);
            _histogramRoiWidth = LoadIntSetting(nameof(HistogramRoiWidth), 0);
            _histogramRoiHeight = LoadIntSetting(nameof(HistogramRoiHeight), 0);
            OnPropertyChanged(nameof(HistogramRoiX));
            OnPropertyChanged(nameof(HistogramRoiY));
            OnPropertyChanged(nameof(HistogramRoiWidth));
            OnPropertyChanged(nameof(HistogramRoiHeight));
            HistogramCaptureDir = LoadStringSetting(nameof(HistogramCaptureDir), string.Empty);
            VisionCaptureDir = LoadStringSetting(
                nameof(VisionCaptureDir),
                CTAutoSlicerSettings.DefaultVisionCaptureDir);
            Vision2dVectorCaptureDir = LoadStringSetting(
                nameof(Vision2dVectorCaptureDir),
                CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir);
            Vision3dInitialCaptureDir = LoadStringSetting(
                nameof(Vision3dInitialCaptureDir),
                CTAutoSlicerSettings.DefaultVision3dInitialCaptureDir);
            HistogramMaskColor = LoadStringSetting(nameof(HistogramMaskColor), "#6E6E6E");
            HistogramMaskTolerance = LoadIntSetting(nameof(HistogramMaskTolerance), 2);
            _dllDownloadBaseUrl = LoadStringSetting(nameof(CTAutoSlicerSettings.DllDownloadBaseUrl), CTAutoSlicerSettings.DefaultDllDownloadBaseUrl);
            _lutSegmentPoint1XPositive = LoadIntSetting(nameof(LutSegmentPoint1XPositive), 0);
            _lutSegmentPoint1YPositive = LoadIntSetting(nameof(LutSegmentPoint1YPositive), 0);
            _lutSegmentPoint2XPositive = LoadIntSetting(nameof(LutSegmentPoint2XPositive), 0);
            _lutSegmentPoint2YPositive = LoadIntSetting(nameof(LutSegmentPoint2YPositive), 0);
            _lutSegmentPointsCalibratedPositive =
                LoadIntSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedPositive), 0) != 0;
            _lutSegmentPoint1XNegative = LoadIntSetting(nameof(LutSegmentPoint1XNegative), 0);
            _lutSegmentPoint1YNegative = LoadIntSetting(nameof(LutSegmentPoint1YNegative), 0);
            _lutSegmentPoint2XNegative = LoadIntSetting(nameof(LutSegmentPoint2XNegative), 0);
            _lutSegmentPoint2YNegative = LoadIntSetting(nameof(LutSegmentPoint2YNegative), 0);
            _lutSegmentPointsCalibratedNegative =
                LoadIntSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedNegative), 0) != 0;

            // 兼容旧单套 LutSegmentPoint*：若正负极均未标定且旧键已标定，迁移到负极（默认 F）
            if (!_lutSegmentPointsCalibratedPositive
                && !_lutSegmentPointsCalibratedNegative
                && LoadIntSetting("LutSegmentPointsCalibrated", 0) != 0)
            {
                _lutSegmentPoint1XNegative = LoadIntSetting("LutSegmentPoint1X", 0);
                _lutSegmentPoint1YNegative = LoadIntSetting("LutSegmentPoint1Y", 0);
                _lutSegmentPoint2XNegative = LoadIntSetting("LutSegmentPoint2X", 0);
                _lutSegmentPoint2YNegative = LoadIntSetting("LutSegmentPoint2Y", 0);
                _lutSegmentPointsCalibratedNegative = true;
                SaveSetting(nameof(LutSegmentPoint1XNegative), _lutSegmentPoint1XNegative);
                SaveSetting(nameof(LutSegmentPoint1YNegative), _lutSegmentPoint1YNegative);
                SaveSetting(nameof(LutSegmentPoint2XNegative), _lutSegmentPoint2XNegative);
                SaveSetting(nameof(LutSegmentPoint2YNegative), _lutSegmentPoint2YNegative);
                SaveSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedNegative), 1);
                _logger.Info("已将旧版单一 LUT 关键段标点迁移为负极标点");
            }

            OnPropertyChanged(nameof(LutSegmentPoint1XPositive));
            OnPropertyChanged(nameof(LutSegmentPoint1YPositive));
            OnPropertyChanged(nameof(LutSegmentPoint2XPositive));
            OnPropertyChanged(nameof(LutSegmentPoint2YPositive));
            OnPropertyChanged(nameof(LutSegmentPoint1XNegative));
            OnPropertyChanged(nameof(LutSegmentPoint1YNegative));
            OnPropertyChanged(nameof(LutSegmentPoint2XNegative));
            OnPropertyChanged(nameof(LutSegmentPoint2YNegative));
            LutExpandLeftYFactor = LoadDoubleSetting(nameof(LutExpandLeftYFactor), 2.0);
            LutExpandRightYFactor = LoadDoubleSetting(nameof(LutExpandRightYFactor), 3.0);
            _lutExpandLeftYFactorText = null;
            _lutExpandRightYFactorText = null;
            OnPropertyChanged(nameof(LutExpandLeftYFactorText));
            OnPropertyChanged(nameof(LutExpandRightYFactorText));
            RefreshLutSegmentPointsDisplay();
            RefreshHistogramRoiDisplay();
            RefreshMaskColorBrush();

            _vision3dRoiX = LoadIntSetting(nameof(Vision3dRoiX), 0);
            _vision3dRoiY = LoadIntSetting(nameof(Vision3dRoiY), 0);
            _vision3dRoiWidth = LoadIntSetting(nameof(Vision3dRoiWidth), 0);
            _vision3dRoiHeight = LoadIntSetting(nameof(Vision3dRoiHeight), 0);
            OnPropertyChanged(nameof(Vision3dRoiX));
            OnPropertyChanged(nameof(Vision3dRoiY));
            OnPropertyChanged(nameof(Vision3dRoiWidth));
            OnPropertyChanged(nameof(Vision3dRoiHeight));
            RefreshVision3dRoiDisplay();

            _visionTopViewRoiX = LoadIntSetting(nameof(VisionTopViewRoiX), 0);
            _visionTopViewRoiY = LoadIntSetting(nameof(VisionTopViewRoiY), 0);
            _visionTopViewRoiWidth = LoadIntSetting(nameof(VisionTopViewRoiWidth), 0);
            _visionTopViewRoiHeight = LoadIntSetting(nameof(VisionTopViewRoiHeight), 0);
            OnPropertyChanged(nameof(VisionTopViewRoiX));
            OnPropertyChanged(nameof(VisionTopViewRoiY));
            OnPropertyChanged(nameof(VisionTopViewRoiWidth));
            OnPropertyChanged(nameof(VisionTopViewRoiHeight));
            RefreshVisionTopViewRoiDisplay();

            _simple321ToolbarClickX = LoadIntSetting(nameof(Simple321ToolbarClickX), 0);
            _simple321ToolbarClickY = LoadIntSetting(nameof(Simple321ToolbarClickY), 0);
            _simple321ToolbarClickCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Simple321ToolbarClickX));
            OnPropertyChanged(nameof(Simple321ToolbarClickY));
            RefreshSimple321ToolbarClickDisplay();

            _simple321OffsetEditX = LoadIntSetting(nameof(Simple321OffsetEditX), 0);
            _simple321OffsetEditY = LoadIntSetting(nameof(Simple321OffsetEditY), 0);
            _simple321OffsetEditCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Simple321OffsetEditX));
            OnPropertyChanged(nameof(Simple321OffsetEditY));
            RefreshSimple321OffsetEditDisplay();

            _simple321PointSource = Math.Clamp(
                LoadIntSetting(
                    nameof(Simple321PointSource),
                    (int)Core.Models.Project.CTAutoSlicer.Simple321PointSource.Local),
                0,
                1);
            OnPropertyChanged(nameof(Simple321PointSource));
            OnPropertyChanged(nameof(Simple321PointSourceDisplay));
            OnPropertyChanged(nameof(IsSimple321LocalPointSource));

            _postAlignCaptureButtonX = LoadIntSetting(nameof(PostAlignCaptureButtonX), 0);
            _postAlignCaptureButtonY = LoadIntSetting(nameof(PostAlignCaptureButtonY), 0);
            _postAlignCaptureButtonCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated), 0) != 0;
            _postAlignCaptureButtonIndex = LoadIntSetting(nameof(PostAlignCaptureButtonIndex), 0);
            OnPropertyChanged(nameof(PostAlignCaptureButtonX));
            OnPropertyChanged(nameof(PostAlignCaptureButtonY));
            OnPropertyChanged(nameof(PostAlignCaptureButtonIndex));
            RefreshPostAlignCaptureButtonDisplay();

            _postAlignDragStartX = LoadIntSetting(nameof(PostAlignDragStartX), 0);
            _postAlignDragStartY = LoadIntSetting(nameof(PostAlignDragStartY), 0);
            _postAlignDragEndX = LoadIntSetting(nameof(PostAlignDragEndX), 0);
            _postAlignDragEndY = LoadIntSetting(nameof(PostAlignDragEndY), 0);
            _postAlignDragCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.PostAlignDragCalibrated), 0) != 0;
            OnPropertyChanged(nameof(PostAlignDragStartX));
            OnPropertyChanged(nameof(PostAlignDragStartY));
            OnPropertyChanged(nameof(PostAlignDragEndX));
            OnPropertyChanged(nameof(PostAlignDragEndY));
            RefreshPostAlignDragDisplay();

            _enablePostAlignCaptureDragSteps =
                LoadIntSetting(nameof(EnablePostAlignCaptureDragSteps), 1) != 0;
            _postAlignClickWaitMs = LoadIntSetting(nameof(PostAlignClickWaitMs), 400);
            _postAlignDragStepDelayMs = LoadIntSetting(nameof(PostAlignDragStepDelayMs), 8);
            OnPropertyChanged(nameof(EnablePostAlignCaptureDragSteps));
            OnPropertyChanged(nameof(PostAlignClickWaitMs));
            OnPropertyChanged(nameof(PostAlignDragStepDelayMs));

            _pre321ViewClickX = LoadIntSetting(nameof(Pre321ViewClickX), 0);
            _pre321ViewClickY = LoadIntSetting(nameof(Pre321ViewClickY), 0);
            _pre321ViewClickCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Pre321ViewClickX));
            OnPropertyChanged(nameof(Pre321ViewClickY));
            RefreshPre321ViewClickDisplay();

            _pre321ViewDragStartX = LoadIntSetting(nameof(Pre321ViewDragStartX), 0);
            _pre321ViewDragStartY = LoadIntSetting(nameof(Pre321ViewDragStartY), 0);
            _pre321ViewDragEndX = LoadIntSetting(nameof(Pre321ViewDragEndX), 0);
            _pre321ViewDragEndY = LoadIntSetting(nameof(Pre321ViewDragEndY), 0);
            _pre321ViewDragCalibrated =
                LoadIntSetting(nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated), 0) != 0;
            OnPropertyChanged(nameof(Pre321ViewDragStartX));
            OnPropertyChanged(nameof(Pre321ViewDragStartY));
            OnPropertyChanged(nameof(Pre321ViewDragEndX));
            OnPropertyChanged(nameof(Pre321ViewDragEndY));
            RefreshPre321ViewDragDisplay();

            _pre321ViewClickWaitMs = LoadIntSetting(nameof(Pre321ViewClickWaitMs), 400);
            _pre321ViewDragStepDelayMs = LoadIntSetting(nameof(Pre321ViewDragStepDelayMs), 8);
            OnPropertyChanged(nameof(Pre321ViewClickWaitMs));
            OnPropertyChanged(nameof(Pre321ViewDragStepDelayMs));

            _mainWindowRememberSize =
                LoadIntSetting(nameof(MainWindowRememberSize), 0) != 0;
            _mainWindowWidth = LoadIntSetting(nameof(MainWindowWidth), 1280);
            _mainWindowHeight = LoadIntSetting(nameof(MainWindowHeight), 720);
            _mainWindowLeft = LoadIntSetting(nameof(MainWindowLeft), 80);
            _mainWindowTop = LoadIntSetting(nameof(MainWindowTop), 80);
            OnPropertyChanged(nameof(MainWindowRememberSize));
            OnPropertyChanged(nameof(MainWindowWidth));
            OnPropertyChanged(nameof(MainWindowHeight));
            OnPropertyChanged(nameof(MainWindowLeft));
            OnPropertyChanged(nameof(MainWindowTop));
            RefreshMainWindowSizeDisplay();

            _enableOfflineCtVisionAnnotation =
                LoadIntSetting(nameof(EnableOfflineCtVisionAnnotation), 0) != 0;
            _enableOfflineCtRemoteDllDownload =
                LoadIntSetting(nameof(EnableOfflineCtRemoteDllDownload), 0) != 0;
            OfflineCtModelPath = LoadStringSetting(nameof(OfflineCtModelPath), string.Empty);
            _offlineCtIsRunCuda = LoadIntSetting(nameof(OfflineCtIsRunCuda), 1) != 0;
            _offlineCtBlackHeight = LoadIntSetting(
                nameof(OfflineCtBlackHeight),
                CTAutoSlicerSettings.DefaultOfflineCtBlackHeight);
            _offlineCtContentShiftUpPx = LoadIntSetting(
                nameof(OfflineCtContentShiftUpPx),
                CTAutoSlicerSettings.DefaultOfflineCtContentShiftUpPx);
            _enableOfflineCtLRangeCheck =
                LoadIntSetting(nameof(EnableOfflineCtLRangeCheck), 0) != 0;
            _offlineCtL1MinMm = LoadDoubleSetting(
                nameof(OfflineCtL1MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL1MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL1MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL2MinMm = LoadDoubleSetting(
                nameof(OfflineCtL2MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL2MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL2MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL3MinMm = LoadDoubleSetting(
                nameof(OfflineCtL3MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL3MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL3MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL4MinMm = LoadDoubleSetting(
                nameof(OfflineCtL4MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL4MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL4MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL5MinMm = LoadDoubleSetting(
                nameof(OfflineCtL5MinMm), CTAutoSlicerSettings.DefaultOfflineCtLMinMm);
            _offlineCtL5MaxMm = LoadDoubleSetting(
                nameof(OfflineCtL5MaxMm), CTAutoSlicerSettings.DefaultOfflineCtLMaxMm);
            _offlineCtL1MinMmText = null;
            _offlineCtL1MaxMmText = null;
            _offlineCtL2MinMmText = null;
            _offlineCtL2MaxMmText = null;
            _offlineCtL3MinMmText = null;
            _offlineCtL3MaxMmText = null;
            _offlineCtL4MinMmText = null;
            _offlineCtL4MaxMmText = null;
            _offlineCtL5MinMmText = null;
            _offlineCtL5MaxMmText = null;
            _offlineCtTopViewSliceIndex = Math.Clamp(
                LoadIntSetting(
                    nameof(OfflineCtTopViewSliceIndex),
                    CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex),
                CTAutoSlicerSettings.MinOfflineCtTopViewSliceIndex,
                CTAutoSlicerSettings.MaxOfflineCtTopViewSliceIndex);
            var loadedPixelSizeUm = LoadDoubleSetting(
                nameof(OfflineCtPixelSizeUm),
                CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm);
            _offlineCtPixelSizeUm = loadedPixelSizeUm > 0
                ? loadedPixelSizeUm
                : CTAutoSlicerSettings.DefaultOfflineCtPixelSizeUm;
            _offlineCtPixelSizeUmText = null;
            _offlineCtResultImageMode = LoadIntSetting(
                nameof(OfflineCtResultImageMode),
                (int)Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode.Enhance);
            _offlineCtExportJson = LoadIntSetting(nameof(OfflineCtExportJson), 0) != 0;
            _offlineCtExportCsv = LoadIntSetting(nameof(OfflineCtExportCsv), 0) != 0;
            _offlineCtExportInputForNjct = LoadIntSetting(nameof(OfflineCtExportInputForNjct), 0) != 0;
            _offlineCtRotateTopViewCw90 = LoadIntSetting(nameof(OfflineCtRotateTopViewCw90), 1) != 0;
            _enableOfflineCtPreBrighten = LoadIntSetting(nameof(EnableOfflineCtPreBrighten), 1) != 0;
            _offlineCtPreBrightenMeanTrigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenMeanTrigger), 120.0);
            _offlineCtPreBrightenP50Trigger = LoadDoubleSetting(nameof(OfflineCtPreBrightenP50Trigger), 110.0);
            _offlineCtPreBrightenTargetMean = LoadDoubleSetting(nameof(OfflineCtPreBrightenTargetMean), 132.0);
            OfflineCtManualInputImagePath = LoadStringSetting(nameof(OfflineCtManualInputImagePath), string.Empty);
            OfflineCtManualOutputFolder = LoadStringSetting(nameof(OfflineCtManualOutputFolder), string.Empty);
            OfflineCtDllTestInputFolder = LoadStringSetting(nameof(OfflineCtDllTestInputFolder), string.Empty);
            OfflineCtDllTestOutputFolder = LoadStringSetting(nameof(OfflineCtDllTestOutputFolder), string.Empty);
            OfflineCtDllTestBlackHeight = LoadIntSetting(nameof(OfflineCtDllTestBlackHeight), 0);
            CutImgDllTestSectionFolder = LoadCutImgDllTestFolderSetting(
                nameof(CutImgDllTestSectionFolder),
                legacyKey: "CutImgDllTestSectionImagePath");
            CutImgDllTestVectorFolder = LoadCutImgDllTestFolderSetting(
                nameof(CutImgDllTestVectorFolder),
                legacyKey: "CutImgDllTestVectorImagePath");
            CutImgDllTestOutputDir = LoadStringSetting(
                nameof(CutImgDllTestOutputDir),
                CTAutoSlicerSettings.DefaultCutImgDllTestOutputDir);
            VisionLearn3dSampleDir = LoadStringSetting(
                nameof(VisionLearn3dSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir);
            VisionLearnTopViewSampleDir = LoadStringSetting(
                nameof(VisionLearnTopViewSampleDir),
                CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir);

            _themePageBgColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemePageBgColor),
                CTAutoSlicerSettings.DefaultThemePageBgColor));
            _themeOuterCardColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeOuterCardColor),
                CTAutoSlicerSettings.DefaultThemeOuterCardColor));
            _themeInnerCardColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeInnerCardColor),
                CTAutoSlicerSettings.DefaultThemeInnerCardColor));
            _themeSidebarColor = NormalizeHexColor(LoadStringSetting(
                nameof(ThemeSidebarColor),
                CTAutoSlicerSettings.DefaultThemeSidebarColor));
            _savedThemePageBgColor = _themePageBgColor;
            _savedThemeOuterCardColor = _themeOuterCardColor;
            _savedThemeInnerCardColor = _themeInnerCardColor;
            _savedThemeSidebarColor = _themeSidebarColor;
            HasUnsavedThemeChanges = false;
            LoadCustomThemePresets();
            LoadThemeEditFromSlot();
            OnPropertyChanged(nameof(ThemePageBgColor));
            OnPropertyChanged(nameof(ThemeOuterCardColor));
            OnPropertyChanged(nameof(ThemeInnerCardColor));
            OnPropertyChanged(nameof(ThemeSidebarColor));
            OnPropertyChanged(nameof(ThemePageBgBrush));
            OnPropertyChanged(nameof(ThemeOuterCardBrush));
            OnPropertyChanged(nameof(ThemeInnerCardBrush));
            OnPropertyChanged(nameof(ThemeSidebarBrush));
            ThemeColorsChanged?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged(nameof(EnableOfflineCtVisionAnnotation));
            OnPropertyChanged(nameof(EnableOfflineCtRemoteDllDownload));
            OnPropertyChanged(nameof(OfflineCtRemoteDllDownloadHint));
            OnPropertyChanged(nameof(EnableAiAnnotationAutoRun));
            OnPropertyChanged(nameof(IsAiAnnotationAutoModeActive));
            OnPropertyChanged(nameof(ShowAiAnnotationAutoRunOption));
            OnPropertyChanged(nameof(IsPrimaryActionEnabled));
            OnPropertyChanged(nameof(PrimaryActionButtonText));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            OnPropertyChanged(nameof(OfflineCtIsRunCuda));
            OnPropertyChanged(nameof(OfflineCtBlackHeight));
            OnPropertyChanged(nameof(OfflineCtBlackHeightHint));
            OnPropertyChanged(nameof(OfflineCtContentShiftUpPx));
            OnPropertyChanged(nameof(EnableOfflineCtLRangeCheck));
            OnPropertyChanged(nameof(OfflineCtL1MinMm));
            OnPropertyChanged(nameof(OfflineCtL1MinMmText));
            OnPropertyChanged(nameof(OfflineCtL1MaxMm));
            OnPropertyChanged(nameof(OfflineCtL1MaxMmText));
            OnPropertyChanged(nameof(OfflineCtL2MinMm));
            OnPropertyChanged(nameof(OfflineCtL2MinMmText));
            OnPropertyChanged(nameof(OfflineCtL2MaxMm));
            OnPropertyChanged(nameof(OfflineCtL2MaxMmText));
            OnPropertyChanged(nameof(OfflineCtL3MinMm));
            OnPropertyChanged(nameof(OfflineCtL3MinMmText));
            OnPropertyChanged(nameof(OfflineCtL3MaxMm));
            OnPropertyChanged(nameof(OfflineCtL3MaxMmText));
            OnPropertyChanged(nameof(OfflineCtL4MinMm));
            OnPropertyChanged(nameof(OfflineCtL4MinMmText));
            OnPropertyChanged(nameof(OfflineCtL4MaxMm));
            OnPropertyChanged(nameof(OfflineCtL4MaxMmText));
            OnPropertyChanged(nameof(OfflineCtL5MinMm));
            OnPropertyChanged(nameof(OfflineCtL5MinMmText));
            OnPropertyChanged(nameof(OfflineCtL5MaxMm));
            OnPropertyChanged(nameof(OfflineCtL5MaxMmText));
            OnPropertyChanged(nameof(OfflineCtTopViewSliceIndex));
            OnPropertyChanged(nameof(OfflineCtPixelSizeUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeUmText));
            OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeMmPerPx));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaUm));
            OnPropertyChanged(nameof(OfflineCtPixelSizeFormulaMm));
            OnPropertyChanged(nameof(OfflineCtResultImageMode));
            OnPropertyChanged(nameof(OfflineCtResultImageModeDisplay));
            OnPropertyChanged(nameof(OfflineCtExportJson));
            OnPropertyChanged(nameof(OfflineCtExportCsv));
            OnPropertyChanged(nameof(OfflineCtExportInputForNjct));
            OnPropertyChanged(nameof(OfflineCtRotateTopViewCw90));
            OnPropertyChanged(nameof(EnableOfflineCtPreBrighten));
            OnPropertyChanged(nameof(OfflineCtPreBrightenMeanTrigger));
            OnPropertyChanged(nameof(OfflineCtPreBrightenP50Trigger));
            OnPropertyChanged(nameof(OfflineCtPreBrightenTargetMean));
            OnPropertyChanged(nameof(OfflineCtDllTestInputFolder));
            OnPropertyChanged(nameof(OfflineCtDllTestOutputFolder));
            OnPropertyChanged(nameof(OfflineCtDllTestBlackHeight));
            OnPropertyChanged(nameof(OfflineCtDllTestBlackHeightHint));

            _isLoadingSettings = false;

            // 首次进入模块：设置加载完成后尝试恢复自定义主窗尺寸
            Dispatcher.BeginInvoke(new Action(TryApplyRememberedMainWindowSize), DispatcherPriority.Loaded);
            SyncAiAnnotationAutoRunMonitor();
        }

        private string LoadStringSetting(string name, string defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "string", name);
            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            return value?.ToString() ?? defaultValue;
        }

        private int LoadIntSetting(string name, int defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "int", name);
            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            return value == null ? defaultValue : Convert.ToInt32(value);
        }

        private double LoadDoubleSetting(string name, double defaultValue)
        {
            EnsureSettingExists(name, defaultValue, "double", name);
            var model = _projectSettingRepository.GetByNameAndVM(name, VmName);
            if (model == null)
            {
                return defaultValue;
            }

            // 历史若误存为 int，读取会截断小数；升级为 double + Invariant 字符串
            var type = model.Type ?? string.Empty;
            var isFloatType = type.Equals("double", StringComparison.OrdinalIgnoreCase)
                || type.Equals("float", StringComparison.OrdinalIgnoreCase)
                || type.Equals("decimal", StringComparison.OrdinalIgnoreCase);
            if (!isFloatType
                && TryParseFlexibleDouble(model.Value, out var upgraded))
            {
                model.Type = "double";
                model.Value = upgraded.ToString("G17", CultureInfo.InvariantCulture);
                _projectSettingRepository.Update(model);
                return upgraded;
            }

            if (TryParseFlexibleDouble(model.Value, out var parsed))
            {
                return parsed;
            }

            var value = _projectSettingRepository.GetSettingValue(name, VmName);
            if (value == null)
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                try
                {
                    return Convert.ToDouble(value, CultureInfo.CurrentCulture);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        /// <summary>持久化 double：InvariantCulture，并强制 Type=double（避免小数被 int 截断）。</summary>
        private void SaveDoubleSetting(string name, double value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            var text = value.ToString("G17", CultureInfo.InvariantCulture);
            var model = _projectSettingRepository.GetByNameAndVM(name, VmName);
            if (model == null)
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(name, VmName, text, "double", 1) { Remark = name });
                return;
            }

            model.Value = text;
            model.Type = "double";
            _projectSettingRepository.Update(model);
        }

        private static bool TryParseFlexibleDouble(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var raw = text.Trim();
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private void EnsureSettingExists(string name, object defaultValue, string type, string remark)
        {
            if (!_projectSettingRepository.Exists(model => model.Name == name && model.BelongToVM == VmName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(name, VmName, $"{defaultValue}", type, 1) { Remark = remark }
                );
            }
        }

        private void SavePipelineRunModeToLocalConfig(int mode)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            try
            {
                _localConfigService.SavePipelineRunMode(
                    (Core.Models.Project.CTAutoSlicer.PipelineRunMode)Math.Clamp(mode, 0, 1));
            }
            catch (Exception ex)
            {
                _logger.Error($"保存本机流水线模式失败：{CTAutoSlicerLocalConfigService.ConfigFilePath}", ex);
                MessageShower = $"保存本机流水线模式失败：{ex.Message}";
            }
        }

        private void SaveSetting(string name, object value)
        {
            if (_isLoadingSettings)
            {
                return;
            }

            // 键不存在时补建（类型用仓库可识别的短名，避免 Int32 导致下次加载失败）
            if (!_projectSettingRepository.Exists(model => model.Name == name && model.BelongToVM == VmName))
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        name,
                        VmName,
                        Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                        ResolveSettingTypeName(value),
                        1) { Remark = name });
                return;
            }

            _projectSettingRepository.UpdateSettingValue(name, VmName, value);
        }

        /// <summary>与 EnsureSettingExists / DataTypeConverter 对齐的类型短名。</summary>
        private static string ResolveSettingTypeName(object value) =>
            value switch
            {
                int or long or short or byte or sbyte or uint or ulong or ushort => "int",
                double or float or decimal => "double",
                bool => "bool",
                _ => "string",
            };

        private CTAutoSlicerSettings BuildSettings()
        {
            return new CTAutoSlicerSettings
            {
                VGAppPath = VGAppPath,
                VGOpenWaitTime = VGOpenWaitTime,
                VGShowFrontWaitTime = _vgShowFrontWaitTime,
                CTFileFolder = CTFileFolder,
                MaxVgProcesses = MaxVgProcesses,
                QuickImportDialogWaitTime = QuickImportDialogWaitTime,
                NewProjectDialogWaitTime = NewProjectDialogWaitTime,
                FileSelectionDialogWaitTime = FileSelectionDialogWaitTime,
                ImportTimeoutMinutes = ImportTimeoutMinutes,
                PostImportWaitTime = PostImportWaitTime,
                StepRetryCount = StepRetryCount,
                StepRetryIntervalMs = StepRetryIntervalMs,
                RenderingPresetNamePositive = RenderingPresetNamePositive,
                RenderingPresetNameNegative = RenderingPresetNameNegative,
                PresetApplyWaitTime = PresetApplyWaitTime,
                PresetDropdownWaitTime = PresetDropdownWaitTime,
                ThickPlateMenuOffsetX = ThickPlateMenuOffsetX,
                ThickPlateMenuOffsetY = ThickPlateMenuOffsetY,
                ViewportClickOffsetY = ViewportClickOffsetY,
                ThickPlateStepDelayMs = _thickPlateStepDelayMs,
                ThickPlateDialogWaitTime = _thickPlateDialogWaitTime,
                ThickPlateDropdownWaitTime = _thickPlateDropdownWaitTime,
                ViewportQuadrant3ClickOffsetY = _viewportQuadrant3ClickOffsetY,
                ViewportMinWidth = _viewportMinWidth,
                ViewportMinHeight = _viewportMinHeight,
                ThickPlateStepRetryCount = _thickPlateStepRetryCount,
                ThickPlateMenuAfterRightClickDelayMs = _thickPlateMenuAfterRightClickDelayMs,
                SliceExportFolder = SliceExportFolder,
                TopViewHandoffFolder = TopViewHandoffFolder,
                EnableAiAnnotationAutoRun = EnableAiAnnotationAutoRun,
                PipelineRunMode = (Core.Models.Project.CTAutoSlicer.PipelineRunMode)Math.Clamp(
                    _pipelineRunMode,
                    0,
                    1),
                SliceExportImageWidth = SliceExportImageWidth,
                SliceExportQuantity = SliceExportQuantity,
                SliceExportQuantityQj = SliceExportQuantityQj,
                SliceRangeFrontStartMm = SliceRangeFrontStartMm,
                SliceRangeFrontEndMm = SliceRangeFrontEndMm,
                SliceRangeRightStartMm = SliceRangeRightStartMm,
                SliceRangeRightEndMm = SliceRangeRightEndMm,
                SliceRangeTopStartMm = SliceRangeTopStartMm,
                SliceRangeTopEndMm = SliceRangeTopEndMm,
                SliceExportDialogWaitTime = SliceExportDialogWaitTime,
                SliceExportWriteTimeoutMs = SliceExportWriteTimeoutMs,
                HistogramRoiX = HistogramRoiX,
                HistogramRoiY = HistogramRoiY,
                HistogramRoiWidth = HistogramRoiWidth,
                HistogramRoiHeight = HistogramRoiHeight,
                HistogramCaptureDir = HistogramCaptureDir,
                HistogramCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                HistogramMaskColor = HistogramMaskColor,
                HistogramMaskTolerance = HistogramMaskTolerance,
                LutSegmentPoint1XPositive = LutSegmentPoint1XPositive,
                LutSegmentPoint1YPositive = LutSegmentPoint1YPositive,
                LutSegmentPoint2XPositive = LutSegmentPoint2XPositive,
                LutSegmentPoint2YPositive = LutSegmentPoint2YPositive,
                LutSegmentPointsCalibratedPositive = _lutSegmentPointsCalibratedPositive,
                LutSegmentPoint1XNegative = LutSegmentPoint1XNegative,
                LutSegmentPoint1YNegative = LutSegmentPoint1YNegative,
                LutSegmentPoint2XNegative = LutSegmentPoint2XNegative,
                LutSegmentPoint2YNegative = LutSegmentPoint2YNegative,
                LutSegmentPointsCalibratedNegative = _lutSegmentPointsCalibratedNegative,
                LutExpandLeftYFactor = LutExpandLeftYFactor,
                LutExpandRightYFactor = LutExpandRightYFactor,
                VisionCaptureDir = VisionCaptureDir,
                VisionCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision2dVectorCaptureDir = Vision2dVectorCaptureDir,
                Vision2dVectorCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision3dInitialCaptureDir = Vision3dInitialCaptureDir,
                Vision3dInitialCaptureKeepDays = CTAutoSlicerSettings.DefaultCaptureKeepDays,
                Vision3dInitialCaptureWaitMs = CTAutoSlicerSettings.DefaultVision3dInitialCaptureWaitMs,
                EnableVisionAlignmentSteps = true,
                Simple321PointSource = (Core.Models.Project.CTAutoSlicer.Simple321PointSource)Math.Clamp(
                    _simple321PointSource,
                    0,
                    1),
                Vision3dRoiX = Vision3dRoiX,
                Vision3dRoiY = Vision3dRoiY,
                Vision3dRoiWidth = Vision3dRoiWidth,
                Vision3dRoiHeight = Vision3dRoiHeight,
                VisionTopViewRoiX = VisionTopViewRoiX,
                VisionTopViewRoiY = VisionTopViewRoiY,
                VisionTopViewRoiWidth = VisionTopViewRoiWidth,
                VisionTopViewRoiHeight = VisionTopViewRoiHeight,
                Simple321ToolbarClickX = Simple321ToolbarClickX,
                Simple321ToolbarClickY = Simple321ToolbarClickY,
                Simple321ToolbarClickCalibrated = _simple321ToolbarClickCalibrated,
                Simple321OffsetEditX = Simple321OffsetEditX,
                Simple321OffsetEditY = Simple321OffsetEditY,
                Simple321OffsetEditCalibrated = _simple321OffsetEditCalibrated,
                PostAlignCaptureButtonX = PostAlignCaptureButtonX,
                PostAlignCaptureButtonY = PostAlignCaptureButtonY,
                PostAlignCaptureButtonCalibrated = _postAlignCaptureButtonCalibrated,
                PostAlignCaptureButtonIndex = PostAlignCaptureButtonIndex,
                PostAlignDragStartX = PostAlignDragStartX,
                PostAlignDragStartY = PostAlignDragStartY,
                PostAlignDragEndX = PostAlignDragEndX,
                PostAlignDragEndY = PostAlignDragEndY,
                PostAlignDragCalibrated = _postAlignDragCalibrated,
                EnablePostAlignCaptureDragSteps = EnablePostAlignCaptureDragSteps,
                PostAlignClickWaitMs = PostAlignClickWaitMs,
                PostAlignDragStepDelayMs = PostAlignDragStepDelayMs,
                Pre321ViewClickX = Pre321ViewClickX,
                Pre321ViewClickY = Pre321ViewClickY,
                Pre321ViewClickCalibrated = _pre321ViewClickCalibrated,
                Pre321ViewDragStartX = Pre321ViewDragStartX,
                Pre321ViewDragStartY = Pre321ViewDragStartY,
                Pre321ViewDragEndX = Pre321ViewDragEndX,
                Pre321ViewDragEndY = Pre321ViewDragEndY,
                Pre321ViewDragCalibrated = _pre321ViewDragCalibrated,
                Pre321ViewClickWaitMs = Pre321ViewClickWaitMs,
                Pre321ViewDragStepDelayMs = Pre321ViewDragStepDelayMs,
                MainWindowRememberSize = MainWindowRememberSize,
                MainWindowWidth = MainWindowWidth,
                MainWindowHeight = MainWindowHeight,
                MainWindowLeft = MainWindowLeft,
                MainWindowTop = MainWindowTop,
                EnableOfflineCtVisionAnnotation = EnableOfflineCtVisionAnnotation,
                EnableOfflineCtRemoteDllDownload = EnableOfflineCtRemoteDllDownload,
                OfflineCtModelPath = OfflineCtModelPath,
                OfflineCtIsRunCuda = OfflineCtIsRunCuda,
                OfflineCtBlackHeight = OfflineCtBlackHeight,
                OfflineCtContentShiftUpPx = OfflineCtContentShiftUpPx,
                EnableOfflineCtLRangeCheck = EnableOfflineCtLRangeCheck,
                OfflineCtL1MinMm = OfflineCtL1MinMm,
                OfflineCtL1MaxMm = OfflineCtL1MaxMm,
                OfflineCtL2MinMm = OfflineCtL2MinMm,
                OfflineCtL2MaxMm = OfflineCtL2MaxMm,
                OfflineCtL3MinMm = OfflineCtL3MinMm,
                OfflineCtL3MaxMm = OfflineCtL3MaxMm,
                OfflineCtL4MinMm = OfflineCtL4MinMm,
                OfflineCtL4MaxMm = OfflineCtL4MaxMm,
                OfflineCtL5MinMm = OfflineCtL5MinMm,
                OfflineCtL5MaxMm = OfflineCtL5MaxMm,
                OfflineCtTopViewSliceIndex = OfflineCtTopViewSliceIndex,
                OfflineCtPixelSizeUm = OfflineCtPixelSizeUm,
                OfflineCtResultImageMode = (Core.Models.Project.CTAutoSlicer.OfflineCtResultImageMode)Math.Clamp(
                    _offlineCtResultImageMode,
                    0,
                    2),
                OfflineCtExportJson = OfflineCtExportJson,
                OfflineCtExportCsv = OfflineCtExportCsv,
                OfflineCtExportInputForNjct = OfflineCtExportInputForNjct,
                OfflineCtRotateTopViewCw90 = OfflineCtRotateTopViewCw90,
                EnableOfflineCtPreBrighten = EnableOfflineCtPreBrighten,
                OfflineCtPreBrightenMeanTrigger = OfflineCtPreBrightenMeanTrigger,
                OfflineCtPreBrightenP50Trigger = OfflineCtPreBrightenP50Trigger,
                OfflineCtPreBrightenTargetMean = OfflineCtPreBrightenTargetMean,
                VisionClickIntervalMs = 300,
                VisionCaptureWaitMs = 2000,
                VisionAlignmentDialogWaitTime = 8000,
                DllDownloadBaseUrl = _dllDownloadBaseUrl,
            };
        }
    }
}
