using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CF;
using Dm.util;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Math;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using NJCT;
using OpenCvSharp;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTNCLX;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.CTNCLX;
using TouchSocket.Core;
using Xceed.Wpf.Toolkit;

namespace SUNWODA_SEVB.ViewModels.Pages.CTNCLX
{
    [Module("VM_CTNCLXAutoDetectionPage", "南昌离线CT极片Gap AI检测系统")]
    public class VM_CTNCLXAutoDetectionPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CTNCLXAutoDetectionPage> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
        private readonly HashSet<string> exts = new() { ".tif", ".png", ".jpg" };

        private bool isContinuousDetection;
        private bool isEnableFileSelect;
        private bool isEnableSetGapLimit;
        private int totalDetectionNum;
        private int okDetectionNum;
        private int ngDetectionNum;
        private double okRate;
        private string? fileSelectInitDir = "C:\\";
        private string? imageMonitorPath = "C:\\";
        private string? exportPath = "C:\\";
        private bool isSaveAIEnhanceResult;
        private double innerGapUpperLimit;
        private double innerGapLowerLimit;
        private double innerFiveMeanMeanUpperLimit;
        private double middleFiveMeanMeanUpperLimit;
        private double outsideFiveMeanMeanUpperLimit;
        private double jrGapMeanLowerLimit;
        private double innerFiveMeanLowerLimit;
        private double fiveGapMeanLimit;

        private List<double>? _detectionSattisticalData;
        private List<string>? _detectionSattisticalLabel;
        private List<string>? _detectionStatisticalLabelColor;

        private ImageSource? _lastProcessedImage;

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 是否连续检测
        /// </summary>
        public bool IsContinuousDetection
        {
            get => isContinuousDetection;
            set
            {
                if (isContinuousDetection != value)
                {
                    _projectSettingRepository.UpdateSettingValue(
                        "IsContinuousDetection",
                        "VM_CTNCLXAutoDetectionPage",
                        value
                    );
                    SetProperty(ref isContinuousDetection, value);
                }
                IsEnableFileSelect = !value;
                IsEnableSetGapLimit = !value;
            }
        }

        /// <summary>
        /// 文件选择是否可用
        /// </summary>
        public bool IsEnableFileSelect
        {
            get => isEnableFileSelect;
            set => SetProperty(ref isEnableFileSelect, value);
        }

        public bool IsEnableSetGapLimit
        {
            get => isEnableSetGapLimit;
            set => SetProperty(ref isEnableSetGapLimit, value);
        }

        /// <summary>
        /// 总检测数
        /// </summary>
        public int TotalDetectionNum
        {
            get => totalDetectionNum;
            set => SetProperty(ref totalDetectionNum, value);
        }

        /// <summary>
        /// OK数
        /// </summary>
        public int OKDetectionNum
        {
            get => okDetectionNum;
            set
            {
                _projectSettingRepository.UpdateSettingValue(
                    "OKDetectionNum",
                    "VM_CTNCLXAutoDetectionPage",
                    value
                );
                SetProperty(ref okDetectionNum, value);
                DetectionSattisticalData = new List<double>() { OKDetectionNum, NGDetectionNum };
            }
        }

        /// <summary>
        /// NG数
        /// </summary>
        public int NGDetectionNum
        {
            get => ngDetectionNum;
            set
            {
                _projectSettingRepository.UpdateSettingValue(
                    "NGDetectionNum",
                    "VM_CTNCLXAutoDetectionPage",
                    value
                );
                SetProperty(ref ngDetectionNum, value);
                DetectionSattisticalData = new List<double>() { OKDetectionNum, NGDetectionNum };
            }
        }

        /// <summary>
        /// 一次优率
        /// </summary>
        public double OKRate
        {
            get => okRate;
            set => SetProperty(ref okRate, double.Round(value, 1));
        }

        /// <summary>
        /// 文件选择初始目录
        /// </summary>
        public string? FileSelectInitDir
        {
            get => fileSelectInitDir;
            set
            {
                if (value != null)
                {
                    _projectSettingRepository.UpdateSettingValue(
                        "FileSelectInitDir",
                        "VM_CTNCLXAutoDetectionPage",
                        value
                    );
                    SetProperty(ref fileSelectInitDir, value);
                }
            }
        }

        /// <summary>
        /// 图片监控路径
        /// </summary>
        public string? ImageMonitorPath
        {
            get => imageMonitorPath;
            set
            {
                if (value != null)
                {
                    _projectSettingRepository.UpdateSettingValue(
                        "ImageMonitorPath",
                        "VM_CTNCLXAutoDetectionPage",
                        value
                    );
                    SetProperty(ref imageMonitorPath, value);
                }
            }
        }

        /// <summary>
        /// 导出路径
        /// </summary>
        public string? ExportPath
        {
            get => exportPath;
            set
            {
                if (value != null)
                {
                    _projectSettingRepository.UpdateSettingValue(
                        "ExportPath",
                        "VM_CTNCLXAutoDetectionPage",
                        value
                    );
                    SetProperty(ref exportPath, value);
                }
            }
        }

        /// <summary>
        /// 是否保存AI增强结果
        /// </summary>
        public bool IsSaveAIEnhanceResult
        {
            get => isSaveAIEnhanceResult;
            set
            {
                _projectSettingRepository.UpdateSettingValue(
                    "IsSaveAIEnhanceResult",
                    "VM_CTNCLXAutoDetectionPage",
                    value
                );
                SetProperty(ref isSaveAIEnhanceResult, value);
            }
        }

        /// <summary>
        /// 最内层 Gap 标准上限
        /// </summary>
        public double InnerGapUpperLimit
        {
            get => innerGapUpperLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "InnerGapUpperLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref innerGapUpperLimit, roundValue);
            }
        }

        /// <summary>
        /// 最内层 Gap 标准下限
        /// </summary>
        public double InnerGapLowerLimit
        {
            get => innerGapLowerLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "InnerGapLowerLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref innerGapLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 三方向内五层均值的均值标准上限
        /// </summary>
        public double InnerFiveMeanMeanUpperLimit
        {
            get => innerFiveMeanMeanUpperLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "InnerFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref innerFiveMeanMeanUpperLimit, roundValue);
            }
        }

        /// <summary>
        /// 三方向中五层均值的均值标准上限
        /// </summary>
        public double MiddleFiveMeanMeanUpperLimit
        {
            get => middleFiveMeanMeanUpperLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "MiddleFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref middleFiveMeanMeanUpperLimit, roundValue);
            }
        }

        /// <summary>
        /// 三方向外五层均值的均值标准上限
        /// </summary>
        public double OutsideFiveMeanMeanUpperLimit
        {
            get => outsideFiveMeanMeanUpperLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "OutsideFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref outsideFiveMeanMeanUpperLimit, roundValue);
            }
        }

        /// <summary>
        /// 整个卷芯的Gap均值下限
        /// </summary>
        public double JRGapMeanLowerLimit
        {
            get => jrGapMeanLowerLimit;
            set
            {
                var roundValue = Math.Round(value, 4);
                _projectSettingRepository.UpdateSettingValue(
                    "JRGapMeanLowerLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref jrGapMeanLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 0度方向内5层均值标准下限
        /// </summary>
        public double InnerFiveMeanLowerLimit
        {
            get => innerFiveMeanLowerLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "InnerFiveMeanLowerLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    roundValue
                );
                SetProperty(ref innerFiveMeanLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 五层均值 Gap 标准
        /// </summary>
        //public double FiveGapMeanLimit
        //{
        //    get => fiveGapMeanLimit;
        //    set
        //    {
        //        _projectSettingRepository.UpdateSettingValue(
        //            "FiveGapMeanLimit",
        //            "VM_CTNCLXAutoDetectionPage",
        //            value
        //        );
        //        SetProperty(ref fiveGapMeanLimit, value);
        //    }
        //}

        /// <summary>
        /// 检测功能清单
        /// </summary>
        public ObservableCollection<DetectionItemModel>? DetectionItems { get; set; }

        /// <summary>
        /// 检测统计信息列表
        /// </summary>
        public ObservableCollection<DetectionInfoModel>? DetectionInfoList { get; set; }

        /// <summary>
        /// 检测统计数据
        /// </summary>
        public List<double>? DetectionSattisticalData
        {
            get => _detectionSattisticalData;
            set => SetProperty(ref _detectionSattisticalData, value);
        }

        /// <summary>
        /// 检测统计标签
        /// </summary>
        public List<string>? DetectionSattisticalLabel
        {
            get => _detectionSattisticalLabel;
            set => SetProperty(ref _detectionSattisticalLabel, value);
        }

        /// <summary>
        /// 检测统计标签颜色
        /// </summary>
        public List<string>? DetectionStatisticalLabelColor
        {
            get => _detectionStatisticalLabelColor;
            set => SetProperty(ref _detectionStatisticalLabelColor, value);
        }

        /// <summary>
        /// CT检测算法实例
        /// </summary>
        //public object? CTDetectionAlgorithmIstance { get; set; }
        public NJSwdCT? CTDetectionAlgorithmIstance { get; set; }

        ///// <summary>
        ///// 抓点方法
        ///// </summary>
        //public MethodInfo? ActionGetPointMethod { get; set; }

        //public MethodInfo? ModelInitMethod { get; set; }

        //public string? ImagePath { get; set; }

        public int BottomBlackHeight { get; set; }

        public HObject? ResultImage { get; set; }

        public List<HObject>? AllPointRegion { get; set; }

        public List<HTuple>? AllRegionRow { get; set; }

        public List<HTuple>? AllRegionCol { get; set; }

        public List<HTuple>? AllJianXiValue { get; set; }

        public List<HTuple>? AllPrintfValue { get; set; }

        public string? RunInfo { get; set; }

        //public object[]? AlgorithmParameters { get; set; }

        /// <summary>
        /// Model 路径
        /// </summary>
        public string? ModelPath { get; set; }

        public IPredictor? ModelHandle { get; set; }

        public string? ModelInitErrorInfo { get; set; }

        public bool IsRunCuda { get; set; }

        //public object[]? ModelInitParameters { get; set; }

        public FileSystemWatcher? ImageFileSystemWatcher { get; set; }

        public static ConcurrentQueue<string>? ImagePathQueue { get; set; }

        public Thread? GapDetectionThread { get; set; }

        public string? CurrentProcessDir { get; set; }

        /// <summary>
        /// 最后处理图
        /// </summary>
        public ImageSource? LastProcessedImage
        {
            get => _lastProcessedImage;
            private set => SetProperty(ref _lastProcessedImage, value);
        }

        public bool LastImageIsOK { get; set; }

        #region 命令
        public ICommand? MonitorPathSelectCommand { get; set; }

        public ICommand? FileSelectCommand { get; set; }

        public ICommand? ExportPathSelectCommand { get; set; }

        public ICommand? ClearDetectionInfoCommand { get; set; }
        #endregion

        public VM_CTNCLXAutoDetectionPage(
            ILoggerService<VM_CTNCLXAutoDetectionPage> logger,
            IProjectSettingRepository projectSettingRepository
        )
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
        }

        public override void OnInitialize()
        {
            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsContinuousDetection"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsContinuousDetection",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{IsContinuousDetection}",
                        "bool",
                        1
                    )
                    {
                        Remark = $"是否连续检测",
                    }
                );
            }
            else
            {
                var _isContinuousDetection = _projectSettingRepository.GetSettingValue(
                    $"IsContinuousDetection",
                    "VM_CTNCLXAutoDetectionPage"
                );
                IsContinuousDetection = _isContinuousDetection;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"OKDetectionNum"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"OKDetectionNum",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{OKDetectionNum}",
                        "int",
                        1
                    )
                    {
                        Remark = $"OK数",
                    }
                );
            }
            else
            {
                var _OKDetectionNum = _projectSettingRepository.GetSettingValue(
                    $"OKDetectionNum",
                    "VM_CTNCLXAutoDetectionPage"
                );
                OKDetectionNum = _OKDetectionNum;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"NGDetectionNum"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"NGDetectionNum",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{NGDetectionNum}",
                        "int",
                        1
                    )
                    {
                        Remark = $"NG数",
                    }
                );
            }
            else
            {
                var _NGDetectionNum = _projectSettingRepository.GetSettingValue(
                    $"NGDetectionNum",
                    "VM_CTNCLXAutoDetectionPage"
                );
                NGDetectionNum = _NGDetectionNum;
            }

            TotalDetectionNum = OKDetectionNum + NGDetectionNum;
            if (TotalDetectionNum != 0)
                OKRate = OKDetectionNum * 100.0 / TotalDetectionNum;

            DetectionSattisticalData = new List<double>() { OKDetectionNum, NGDetectionNum };

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"FileSelectInitDir"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"FileSelectInitDir",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{FileSelectInitDir}",
                        "string",
                        1
                    )
                    {
                        Remark = $"文件选择初始目录",
                    }
                );
            }
            else
            {
                var _fileSelectInitDir = _projectSettingRepository.GetSettingValue(
                    $"FileSelectInitDir",
                    "VM_CTNCLXAutoDetectionPage"
                );
                FileSelectInitDir = _fileSelectInitDir;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ImageMonitorPath"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ImageMonitorPath",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{ImageMonitorPath}",
                        "string",
                        1
                    )
                    {
                        Remark = $"图片监控路径",
                    }
                );
            }
            else
            {
                var _imageMonitorPath = _projectSettingRepository.GetSettingValue(
                    $"ImageMonitorPath",
                    "VM_CTNCLXAutoDetectionPage"
                );
                ImageMonitorPath = _imageMonitorPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ExportPath" && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ExportPath",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{ExportPath}",
                        "string",
                        1
                    )
                    {
                        Remark = $"导出路径",
                    }
                );
            }
            else
            {
                var _exportPath = _projectSettingRepository.GetSettingValue(
                    $"ExportPath",
                    "VM_CTNCLXAutoDetectionPage"
                );
                ExportPath = _exportPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsSaveAIEnhanceResult"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsSaveAIEnhanceResult",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{IsSaveAIEnhanceResult}",
                        "bool",
                        1
                    )
                    {
                        Remark = $"是否保存AI增强结果",
                    }
                );
            }
            else
            {
                var _isSaveAIEnhanceResult = _projectSettingRepository.GetSettingValue(
                    $"IsSaveAIEnhanceResult",
                    "VM_CTNCLXAutoDetectionPage"
                );
                IsSaveAIEnhanceResult = _isSaveAIEnhanceResult;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerGapUpperLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerGapUpperLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{InnerGapUpperLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"最内层 Gap 标准上限",
                    }
                );
            }
            else
            {
                var _innerGapUpperLimit = _projectSettingRepository.GetSettingValue(
                    $"InnerGapUpperLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                InnerGapUpperLimit = _innerGapUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerGapLowerLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerGapLowerLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{InnerGapLowerLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"最内层 Gap 标准上限",
                    }
                );
            }
            else
            {
                var _innerGapLowerLimit = _projectSettingRepository.GetSettingValue(
                    $"InnerGapLowerLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                InnerGapLowerLimit = _innerGapLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerFiveMeanMeanUpperLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{InnerFiveMeanMeanUpperLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"三方向内五层均值的均值标准上限",
                    }
                );
            }
            else
            {
                var _innerFiveMeanMeanUpperLimit = _projectSettingRepository.GetSettingValue(
                    $"InnerFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                InnerFiveMeanMeanUpperLimit = _innerFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"MiddleFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"MiddleFiveMeanMeanUpperLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{MiddleFiveMeanMeanUpperLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"三方向中五层均值的均值标准上限",
                    }
                );
            }
            else
            {
                var _middleFiveMeanMeanUpperLimit = _projectSettingRepository.GetSettingValue(
                    $"MiddleFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                MiddleFiveMeanMeanUpperLimit = _middleFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"OutsideFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"OutsideFiveMeanMeanUpperLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{OutsideFiveMeanMeanUpperLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"三方向外五层均值的均值标准上限",
                    }
                );
            }
            else
            {
                var _outsideFiveMeanMeanUpperLimit = _projectSettingRepository.GetSettingValue(
                    $"OutsideFiveMeanMeanUpperLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                OutsideFiveMeanMeanUpperLimit = _outsideFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"JRGapMeanLowerLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"JRGapMeanLowerLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{JRGapMeanLowerLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"整个卷芯的Gap均值下限",
                    }
                );
            }
            else
            {
                var _jrGapMeanLowerLimit = _projectSettingRepository.GetSettingValue(
                    $"JRGapMeanLowerLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                JRGapMeanLowerLimit = _jrGapMeanLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerFiveMeanLowerLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerFiveMeanLowerLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{InnerFiveMeanLowerLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"0度方向内5层均值标准下限",
                    }
                );
            }
            else
            {
                var _innerFiveMeanLowerLimit = _projectSettingRepository.GetSettingValue(
                    $"InnerFiveMeanLowerLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                InnerFiveMeanLowerLimit = _innerFiveMeanLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ModelPath" && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ModelPath",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{ModelPath}",
                        "string",
                        1
                    )
                    {
                        Remark = $"Model 路径",
                    }
                );
            }
            else
            {
                var _modelPath = _projectSettingRepository.GetSettingValue(
                    $"ModelPath",
                    "VM_CTNCLXAutoDetectionPage"
                );
                ModelPath = _modelPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsRunCuda" && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsRunCuda",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{IsRunCuda}",
                        "bool",
                        1
                    )
                    {
                        Remark = $"Model 路径",
                    }
                );
            }
            else
            {
                var _isRunCuda = _projectSettingRepository.GetSettingValue(
                    $"IsRunCuda",
                    "VM_CTNCLXAutoDetectionPage"
                );
                IsRunCuda = _isRunCuda;
            }

            DetectionItems = new ObservableCollection<DetectionItemModel>()
            {
                new DetectionItemModel("极片OH", false),
                new DetectionItemModel("极片折角", false),
                new DetectionItemModel("极片破损", false),
                new DetectionItemModel("极片极片褶皱", false),
                new DetectionItemModel("极片破片", false),
                new DetectionItemModel("极片划痕", false),
                new DetectionItemModel("卷芯内异物", false),
                new DetectionItemModel("极耳翻折", false),
                new DetectionItemModel("极耳破损", false),
                new DetectionItemModel("卷芯测厚", false),
                new DetectionItemModel("极耳冗余", false),
                new DetectionItemModel("电芯群裕度", false),
                new DetectionItemModel("极柱裂纹", false),
                new DetectionItemModel("顶盖焊缝", false),
                new DetectionItemModel("铝壳底部腐蚀", false),
                new DetectionItemModel("Mylar膜破损检测", false),
                new DetectionItemModel("极片间Gap", true, true),
                new DetectionItemModel("汇流排焊接", false),
                new DetectionItemModel("电解液高度", false),
                new DetectionItemModel("析锂", false),
            };

            CurrentProcessDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            DetectionInfoList = new ObservableCollection<DetectionInfoModel>();

            DetectionSattisticalLabel = new List<string>() { "OK", "NG" };

            DetectionStatisticalLabelColor = new List<string>() { "#66BB6A", "#EF5350" };

            MonitorPathSelectCommand = new RelayCommand(MonitorPathSelect);

            FileSelectCommand = new RelayCommand(FileSelect);

            ExportPathSelectCommand = new RelayCommand(ExportPathSelect);

            ClearDetectionInfoCommand = new RelayCommand(ClearDetectionInfo);

            var startTime = DateTime.Now;
            if (!File.Exists(ModelPath))
            {
                ModelPath = ".\\model\\NCLXCT\\model.onnx";
            }
            ModelPath = ".\\model\\NCLXCT\\model.onnx";
            CTDetectionAlgorithmIstance = new NJSwdCT();
            CTDetectionAlgorithmIstance.ModelInit(
                ModelPath,
                out IPredictor modelHandle,
                out string modelInitErrorInfo,
                IsRunCuda
            );
            _logger.Info($"加载模型耗时：{(DateTime.Now - startTime).TotalSeconds} s");
            ModelHandle = modelHandle;
            ModelInitErrorInfo = modelInitErrorInfo;
            if (ModelHandle is null)
            {
                _logger.Info($"模型句柄为空");
                Growl.ErrorGlobal($"模型句柄为空");
            }
            if (!string.IsNullOrEmpty(ModelInitErrorInfo))
            {
                _logger.Info($"模型初始化错误信息：{ModelInitErrorInfo}");
                Growl.ErrorGlobal($"模型初始化错误信息：{ModelInitErrorInfo}");
            }
            

            ImagePathQueue = new ConcurrentQueue<string>();
            WatchImageFileFolder();
            SwitchWatchImageFileFolder(IsContinuousDetection);
            GapDetectionThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        if (!ImagePathQueue.IsEmpty)
                        {
                            _semaphore.Wait();
                            ImagePathQueue.TryDequeue(out string? imagePath);
                            _logger.Info($"获取到图片路径：{imagePath}");
                            GapDetection(imagePath);
                            //Thread.Sleep(5000);
                            _semaphore.Release();
                        }
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("检测线程错误", ex);
                    }
                }
            });
            GapDetectionThread.IsBackground = true;
            GapDetectionThread.Start();

            base.OnInitialize();
        }

        private async void MonitorPathSelect()
        {
            if (IsContinuousDetection)
            {
                await Dialog
                    .Show(new VM_MonitorPathSelectDialog().UIElement)
                    .Initialize<VM_MonitorPathSelectDialog>(vm => vm.Result = ImageMonitorPath)
                    .GetResultAsync<string?>()
                    .ContinueWith(m => ImageMonitorPath = m.Result);

                if (string.IsNullOrEmpty(ImageMonitorPath))
                {
                    IsContinuousDetection = false;
                    return;
                }
            }
            SwitchWatchImageFileFolder(IsContinuousDetection);
        }

        private void FileSelect()
        {
            if (string.IsNullOrEmpty(FileSelectInitDir) || !Path.Exists(FileSelectInitDir))
            {
                FileSelectInitDir = "C:\\";
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "获取CT切片图片";
            openFileDialog.InitialDirectory = FileSelectInitDir; // 设置初始目录
            openFileDialog.Filter = "图片 (*.tif;*.png;*.jpg)|*.tif;*.png;*.jpg"; // 设置文件过滤器
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true) // 显示对话框
            {
                Task.Run(() =>
                {
                    try
                    {
                        _semaphore.Wait();
                        // 获取选中文件的路径
                        var filePath = openFileDialog.FileName;
                        _logger.Info($"获取到图片路径：{filePath}");
                        GapDetection(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("检测错误", ex);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                });
            }
        }

        private void ExportPathSelect()
        {
            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = $"导出路径选择";
            folderDialog.InitialDirectory = ExportPath;
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                ExportPath = folderDialog.FolderName;
            }
        }

        private void ClearDetectionInfo()
        {
            if (DetectionInfoList != null)
            {
                if (DetectionInfoList.Count > 9)
                {
                    DetectionInfoList.RemoveAt(DetectionInfoList.Count - 1);
                }
                DetectionInfoList.Insert(
                    0,
                    new DetectionInfoModel(
                        DateTime.Now,
                        TotalDetectionNum,
                        OKDetectionNum,
                        NGDetectionNum,
                        OKRate
                    )
                );
                _logger.Info(
                    $"[统计清零]: 总检测数 {TotalDetectionNum} | OK数 {OKDetectionNum} | NG数 {NGDetectionNum} | 一次优率 {OKRate:F1}%"
                );
            }

            TotalDetectionNum = 0;
            OKDetectionNum = 0;
            NGDetectionNum = 0;
            OKRate = 0;
        }

        /// <summary>
        /// 监控功能初始化
        /// </summary>
        public void WatchImageFileFolder()
        {
            ImageFileSystemWatcher = new FileSystemWatcher();
            // 设置要监测的文件夹路径
            if (string.IsNullOrEmpty(ImageMonitorPath) || !Path.Exists(ImageMonitorPath))
            {
                ImageMonitorPath = "C:\\";
            }
            ImageFileSystemWatcher.Path = ImageMonitorPath!;
            ImageFileSystemWatcher.Created += OnCreated;
            // 不设置过滤器，监控所有类型的变化
            ImageFileSystemWatcher.Filter = "*.*";
            // 监控子目录的变化
            ImageFileSystemWatcher.IncludeSubdirectories = true;
        }

        /// <summary>
        /// 切换监控功能开启和关闭
        /// </summary>
        /// <param name="signal">开启/关闭信号</param>
        public void SwitchWatchImageFileFolder(bool signal)
        {
            if (ImageFileSystemWatcher != null)
            {
                ImageFileSystemWatcher.Path = ImageMonitorPath!;
                ImageFileSystemWatcher.EnableRaisingEvents = signal;
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            if (File.Exists(e.FullPath))
            {
                if (exts.Contains(Path.GetExtension(e.FullPath).ToLower()))
                {
                    ImagePathQueue!.Enqueue(e.FullPath);
                    _logger.Info($"图片入队：{e.FullPath}");
                }
            }
        }

        /// <summary>
        /// 图片渲染和保存
        /// </summary>
        /// <param name="originImage">原始图片</param>
        /// <param name="z45Points">正45°点集</param>
        /// <param name="z0Points">正0°点集</param>
        /// <param name="f45Points">负45°点集</param>
        /// <param name="z45LinePositions">正45°横线位置集</param>
        /// <param name="z0LinePositions">正0°横线位置集</param>
        /// <param name="f45LinePositions">负45°横线位置集</param>
        /// <param name="innerGap">内层Gap值集</param>
        /// <param name="z45MeanGap">正45°Gap均值集</param>
        /// <param name="z0MeanGap">正0°Gap均值集</param>
        /// <param name="f45MeanGap">负45°Gap均值集</param>
        /// <param name="resultCode">OK|NG</param>
        /// <param name="runInfo">算法运行信息</param>
        /// <param name="fileName">原始图片名称不带扩展名</param>
        /// <param name="outputPath">结果图输出路径</param>
        /// <returns></returns>
        public bool GenerateResultImage(
            HObject? originImage,
            List<List<(double row, double col)>> z45Points,
            List<List<(double row, double col)>> z0Points,
            List<List<(double row, double col)>> f45Points,
            List<List<(double row, double col)>> z45LinePositions,
            List<List<(double row, double col)>> z0LinePositions,
            List<List<(double row, double col)>> f45LinePositions,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            string resultCode,
            string runInfo,
            string fileName,
            string outputPath
        )
        {
            HWindow? tempWindow = null;
            try
            {
                HOperatorSet.GetImageSize(originImage, out HTuple width, out HTuple height);
                var imgWidth = width.I;
                var imgHeight = height.I;
                _logger.Info($"[图像渲染] 获取图片尺寸 [宽：{imgWidth}，高：{imgHeight}]");

                // ① 创建离屏不可见渲染窗口
                //    宽度至少 1000
                tempWindow = new HWindow();
                int windowWidth = Math.Max(imgWidth, 1000);
                int windowHeight = imgHeight;
                tempWindow.OpenWindow(
                    0,
                    0,
                    windowWidth,
                    windowHeight,
                    new HTuple(0),
                    "invisible",
                    ""
                );

                // ② 设置显示区域
                tempWindow.SetPart(0, 0, imgHeight, imgWidth - 1);

                // ③ 显示原图
                tempWindow.DispObj(originImage);

                // 计算底部矩形坐标
                //double row1 = imgHeight - BottomBlackHeight;
                //double col1 = 0;
                //double row2 = imgHeight - 1;
                //double col2 = imgWidth - 1;

                // 在窗口中画黑色实心矩形
                //tempWindow.SetColor("black");
                //tempWindow.SetDraw("fill");
                //tempWindow.DispRectangle1(row1, col1, row2, col2);

                // ④ 绘制顶部文本：条码 + 检测结果
                if (!string.IsNullOrEmpty(runInfo))
                {
                    DrawTopErrorInfo(fileName, runInfo, tempWindow);
                }
                else
                {
                    DrawTopInfo(fileName, resultCode, tempWindow);

                    // ⑤ 绘制点标记
                    DrawPointMarkers(z45Points, innerGap, z45MeanGap, tempWindow);
                    DrawPointMarkers(z0Points, innerGap, z0MeanGap, tempWindow);
                    DrawPointMarkers(f45Points, innerGap, f45MeanGap, tempWindow);
                    //if (AllPointRegion != null)
                    //{
                    //    foreach (var item in AllPointRegion)
                    //    {
                    //        tempWindow.SetColor("green");
                    //        tempWindow.DispObj(item);
                    //    }
                    //}

                    // ⑥ 绘制横线标记
                    //DrawLineMarkers(z45LinePositions, innerGap, z45MeanGap, Math.PI / 4, tempWindow);
                    //DrawLineMarkers(z0LinePositions, innerGap, z0MeanGap, 0.0, tempWindow);
                    //DrawLineMarkers(f45LinePositions, innerGap, f45MeanGap, -Math.PI / 4, tempWindow);
                    for (int i = 0; i < z0LinePositions.Count; i++)
                    {
                        DrawLineMarkersForSignalJR(
                            z45LinePositions[i],
                            z0LinePositions[i],
                            f45LinePositions[i],
                            z45MeanGap[i],
                            z0MeanGap[i],
                            f45MeanGap[i],
                            innerGap[i],
                            tempWindow
                        );
                    }

                    // ⑦ 绘制文字
                    double textCurentRow = imgHeight - BottomBlackHeight + 100;
                    DrawTextInfo(
                        InnerGapLowerLimit,
                        InnerGapUpperLimit,
                        InnerFiveMeanMeanUpperLimit,
                        MiddleFiveMeanMeanUpperLimit,
                        OutsideFiveMeanMeanUpperLimit,
                        JRGapMeanLowerLimit,
                        InnerFiveMeanLowerLimit,
                        innerGap,
                        z45MeanGap,
                        z0MeanGap,
                        f45MeanGap,
                        textCurentRow,
                        tempWindow
                    );
                }

                // ⑧ 导出标注图像
                string tempFile = Path.GetTempFileName() + ".png";
                try
                {
                    tempWindow.DumpWindow("png", tempFile);
                    File.Copy(tempFile, outputPath, overwrite: true);
                    _logger.Info($"[结果图渲染] 导出标注图像成功, 文件: {outputPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error($"[结果图渲染] 导出标注图像失败: {ex.Message}, 文件: {fileName}");
                    return false;
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                tempWindow?.CloseWindow();
                tempWindow?.Dispose();
            }
        }

        private void DrawTopInfo(string barcode, string resultCode, HWindow hWindow)
        {
            // 行 0，列 0：白色主文字（条码 + 视野编号 + 结果标签）
            hWindow.SetFont("Times New Roman-150");
            hWindow.SetColor("white");
            string headerText = $"\t\t电芯条码:{barcode}";
            var len = headerText.Length;
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");
            hWindow.DispText("\t\t检测结果:", "window", 200, 0, "white", "box", "false");

            // 行 0，列 x：结果值（OK=绿色 / NG=红色）
            bool isOk = resultCode.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
            string resColor = isOk ? "green" : "red";
            hWindow.SetColor(resColor);
            hWindow.DispText(resultCode, "window", 220, 850, resColor, "box", "false");
        }

        private void DrawTopErrorInfo(string barcode, string errorInfo, HWindow hWindow)
        {
            // 行 0，列 0：白色主文字（条码 + 视野编号 + 结果标签）
            hWindow.SetFont("Times New Roman-150");
            hWindow.SetColor("white");
            string headerText = $"\t\t电芯条码:{barcode}";
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");
            hWindow.DispText("\t\t错误信息:", "window", 150, 0, "white", "box", "false");

            var refatorErrorInfo = new StringBuilder();
            if (errorInfo.Length > 90)
            {
                var num = errorInfo.Length / 90;
                for (int i = 0; i < num; i++)
                {
                    refatorErrorInfo.Append(errorInfo.Substring(i * 90, 90));
                    refatorErrorInfo.Append("\n");
                }
                if (errorInfo.Length > 90 * num)
                {
                    refatorErrorInfo.Append(
                        errorInfo.Substring(90 * num, errorInfo.Length - 90 * num)
                    );
                }
            }
            else
            {
                refatorErrorInfo.Append(errorInfo);
            }

            hWindow.SetFont("Times New Roman-50");
            // 行 300，列 0：错误信息
            hWindow.SetColor("red");
            hWindow.DispText($"{refatorErrorInfo}", "window", 350, 150, "red", "box", "false");
        }

        private void DrawPointMarkers(
            List<List<(double row, double col)>> points,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> meanGap,
            HWindow hWindow
        )
        {
            HObject normalMark;
            HObject abnormalMark;
            HOperatorSet.GenEmptyObj(out normalMark);
            HOperatorSet.GenEmptyObj(out abnormalMark);
            try
            {
                for (int i = 0; i < points.Count; i++)
                {
                    for (int j = 0; j < points[i].Count; j++)
                    {
                        HObject star = HImageViewerHelper.GenFillCTStar(
                            points[i][j].row,
                            points[i][j].col,
                            0
                        );

                        //if (!meanGap[i][0].isOK)
                        //{
                        //    if (j == 0 || j == 5)
                        //    {
                        //        HOperatorSet.ConcatObj(abnormalMark, star, out HObject merged);
                        //        abnormalMark?.Dispose();
                        //        abnormalMark = merged;
                        //        continue;
                        //    }
                        //}

                        //if (!meanGap[i][1].isOK)
                        //{
                        //    if (j == 5 || j == 10)
                        //    {
                        //        HOperatorSet.ConcatObj(abnormalMark, star, out HObject merged);
                        //        abnormalMark?.Dispose();
                        //        abnormalMark = merged;
                        //        continue;
                        //    }
                        //}

                        //if (!meanGap[i][2].isOK)
                        //{
                        //    if (j == 11 || j == 16)
                        //    {
                        //        HOperatorSet.ConcatObj(abnormalMark, star, out HObject merged);
                        //        abnormalMark?.Dispose();
                        //        abnormalMark = merged;
                        //        continue;
                        //    }
                        //}

                        HOperatorSet.ConcatObj(normalMark, star, out HObject normalMerged);
                        normalMark?.Dispose();
                        normalMark = normalMerged;
                        star.Dispose();
                    }
                }

                if (normalMark != null)
                {
                    hWindow.SetColor("green");
                    hWindow.DispObj(normalMark);
                }
                if (abnormalMark != null)
                {
                    hWindow.SetColor("red");
                    hWindow.DispObj(abnormalMark);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("绘制点错误", ex);
            }
            finally
            {
                normalMark.Dispose();
                abnormalMark.Dispose();
            }
        }

        private void DrawLineMarkersForSignalJR(
            List<(double row, double col)> z45LinePositions,
            List<(double row, double col)> z0LinePositions,
            List<(double row, double col)> f45LinePositions,
            List<(double gap, bool isOK)> z45MeanGap,
            List<(double gap, bool isOK)> z0MeanGap,
            List<(double gap, bool isOK)> f45MeanGap,
            (double gap, bool isOK) innerGap,
            HWindow hWindow
        )
        {
            HObject normalMark;
            HObject abnormalMark;
            HOperatorSet.GenEmptyObj(out normalMark);
            HOperatorSet.GenEmptyObj(out abnormalMark);
            try
            {
                if (
                    !JRGapMeanIsOK(
                        z0MeanGap[0].gap,
                        z0MeanGap[1].gap,
                        z0MeanGap[2].gap,
                        z45MeanGap[2].gap,
                        f45MeanGap[2].gap,
                        JRGapMeanLowerLimit
                    )
                )
                {
                    for (int i = 0; i < z0LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            z0LinePositions[i].row,
                            z0LinePositions[i].col,
                            0,
                            16,
                            1
                        );

                        if (i == 0)
                        {
                            if (innerGap.isOK)
                            {
                                HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                                normalMark?.Dispose();
                                normalMark = normalMerged;
                                continue;
                            }
                        }

                        HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                        abnormalMark?.Dispose();
                        abnormalMark = merged;
                    }

                    for (int i = 0; i < z45LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            z45LinePositions[i].row,
                            z45LinePositions[i].col,
                            Math.PI / 4,
                            16,
                            1
                        );

                        if (i == 3 || i == 4)
                        {
                            HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                            abnormalMark?.Dispose();
                            abnormalMark = merged;
                        }
                        else
                        {
                            if (i == 0 || i == 1)
                            {
                                if (
                                    !FiveGapMeanMeanIsOK(
                                        z45MeanGap[0].gap,
                                        z0MeanGap[0].gap,
                                        f45MeanGap[0].gap,
                                        InnerFiveMeanMeanUpperLimit
                                    )
                                )
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }

                            if (i == 1 || i == 2)
                            {
                                if (
                                    !FiveGapMeanMeanIsOK(
                                        z45MeanGap[1].gap,
                                        z0MeanGap[1].gap,
                                        f45MeanGap[1].gap,
                                        MiddleFiveMeanMeanUpperLimit
                                    )
                                )
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }

                            HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                            normalMark?.Dispose();
                            normalMark = normalMerged;
                        }
                    }

                    for (int i = 0; i < f45LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            f45LinePositions[i].row,
                            f45LinePositions[i].col,
                            -Math.PI / 4,
                            16,
                            1
                        );

                        if (i == 3 || i == 4)
                        {
                            HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                            abnormalMark?.Dispose();
                            abnormalMark = merged;
                        }
                        else
                        {
                            if (i == 0 || i == 1)
                            {
                                if (
                                    !FiveGapMeanMeanIsOK(
                                        z45MeanGap[0].gap,
                                        z0MeanGap[0].gap,
                                        f45MeanGap[0].gap,
                                        InnerFiveMeanMeanUpperLimit
                                    )
                                )
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }

                            if (i == 1 || i == 2)
                            {
                                if (
                                    !FiveGapMeanMeanIsOK(
                                        z45MeanGap[1].gap,
                                        z0MeanGap[1].gap,
                                        f45MeanGap[1].gap,
                                        MiddleFiveMeanMeanUpperLimit
                                    )
                                )
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }

                            HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                            normalMark?.Dispose();
                            normalMark = normalMerged;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < z0LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            z0LinePositions[i].row,
                            z0LinePositions[i].col,
                            0,
                            16,
                            1
                        );

                        if (!innerGap.isOK)
                        {
                            if (i == 0 || i == 1)
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 1 || i == 2)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[0].gap,
                                    z0MeanGap[0].gap,
                                    f45MeanGap[0].gap,
                                    InnerFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 2 || i == 3)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[1].gap,
                                    z0MeanGap[1].gap,
                                    f45MeanGap[1].gap,
                                    MiddleFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 4 || i == 5)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[2].gap,
                                    z0MeanGap[2].gap,
                                    f45MeanGap[2].gap,
                                    OutsideFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                        normalMark?.Dispose();
                        normalMark = normalMerged;
                    }

                    for (int i = 0; i < z45LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            z45LinePositions[i].row,
                            z45LinePositions[i].col,
                            Math.PI / 4,
                            16,
                            1
                        );

                        if (i == 0 || i == 1)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[0].gap,
                                    z0MeanGap[0].gap,
                                    f45MeanGap[0].gap,
                                    InnerFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 1 || i == 2)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[1].gap,
                                    z0MeanGap[1].gap,
                                    f45MeanGap[1].gap,
                                    MiddleFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 3 || i == 4)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[2].gap,
                                    z0MeanGap[2].gap,
                                    f45MeanGap[2].gap,
                                    OutsideFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                        normalMark?.Dispose();
                        normalMark = normalMerged;
                    }

                    for (int i = 0; i < f45LinePositions.Count; i++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            f45LinePositions[i].row,
                            f45LinePositions[i].col,
                            -Math.PI / 4,
                            16,
                            1
                        );

                        if (i == 0 || i == 1)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[0].gap,
                                    z0MeanGap[0].gap,
                                    f45MeanGap[0].gap,
                                    InnerFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 1 || i == 2)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[1].gap,
                                    z0MeanGap[1].gap,
                                    f45MeanGap[1].gap,
                                    MiddleFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (i == 3 || i == 4)
                        {
                            if (
                                !FiveGapMeanMeanIsOK(
                                    z45MeanGap[2].gap,
                                    z0MeanGap[2].gap,
                                    f45MeanGap[2].gap,
                                    OutsideFiveMeanMeanUpperLimit
                                )
                            )
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                        normalMark?.Dispose();
                        normalMark = normalMerged;
                    }
                }

                if (normalMark != null)
                {
                    hWindow.SetColor("green");
                    hWindow.DispObj(normalMark);
                }
                if (abnormalMark != null)
                {
                    hWindow.SetColor("red");
                    hWindow.DispObj(abnormalMark);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("绘制线错误", ex);
            }
            finally
            {
                normalMark.Dispose();
                abnormalMark.Dispose();
            }
        }

        private void DrawLineMarkers(
            List<List<(double row, double col)>> linePositions,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> meanGap,
            double degree,
            HWindow hWindow
        )
        {
            HObject normalMark;
            HObject abnormalMark;
            HOperatorSet.GenEmptyObj(out normalMark);
            HOperatorSet.GenEmptyObj(out abnormalMark);
            try
            {
                for (int i = 0; i < linePositions.Count; i++)
                {
                    for (int j = 0; j < linePositions[i].Count; j++)
                    {
                        HObject line = HImageViewerHelper.GenFillRectangle(
                            linePositions[i][j].row,
                            linePositions[i][j].col,
                            degree,
                            16,
                            1
                        );

                        if (!innerGap[i].isOK && degree == 0.0)
                        {
                            if (j == 0 || j == 1)
                            {
                                HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                abnormalMark?.Dispose();
                                abnormalMark = merged;
                                continue;
                            }
                        }

                        if (degree == 0.0)
                        {
                            if (!meanGap[i][0].isOK)
                            {
                                if (j == 1 || j == 2)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                            if (!meanGap[i][1].isOK)
                            {
                                if (j == 2 || j == 3)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                            if (!meanGap[i][2].isOK)
                            {
                                if (j == 4 || j == 5)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            if (!meanGap[i][0].isOK)
                            {
                                if (j == 0 || j == 1)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                            if (!meanGap[i][1].isOK)
                            {
                                if (j == 1 || j == 2)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                            if (!meanGap[i][2].isOK)
                            {
                                if (j == 3 || j == 4)
                                {
                                    HOperatorSet.ConcatObj(abnormalMark, line, out HObject merged);
                                    abnormalMark?.Dispose();
                                    abnormalMark = merged;
                                    continue;
                                }
                            }
                        }

                        //if (j == 3 || j == 4)
                        //{
                        //    HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                        //    normalMark?.Dispose();
                        //    normalMark = normalMerged;
                        //    line.Dispose();
                        //}

                        HOperatorSet.ConcatObj(normalMark, line, out HObject normalMerged);
                        normalMark?.Dispose();
                        normalMark = normalMerged;
                        line.Dispose();
                    }
                }

                if (normalMark != null)
                {
                    hWindow.SetColor("green");
                    hWindow.DispObj(normalMark);
                }
                if (abnormalMark != null)
                {
                    hWindow.SetColor("red");
                    hWindow.DispObj(abnormalMark);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("绘制线错误", ex);
            }
            finally
            {
                normalMark.Dispose();
                abnormalMark.Dispose();
            }
        }

        private void DrawTextInfo(
            double innerGapLowerLimit,
            double innerGapUpperLimit,
            double innerFiveMeanMeanUpperLimit,
            double middleFiveMeanMeanUpperLimit,
            double outsideFiveMeanMeanUpperLimit,
            double jrGapMeanLowerLimit,
            double innerFiveMeanLowerLimit,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            double textInitRow,
            HWindow hWindow
        )
        {
            hWindow.SetFont("Times New Roman-100");

            const double titleX = 200; // 标签列 X 起始
            const double valueX = 1500; // 数值列 X
            const double lineHeight = 150; // 行间距（像素）
            double textCurrentRow = textInitRow;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "数值单位: mm",
                "orange",
                $"",
                "green",
                valueX
            );
            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "最内层Gap标准:",
                "orange",
                $"{innerGapLowerLimit:F3} ≤ Gap ≤ {innerGapUpperLimit:F3}",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "内|中|外均值（上限标准）:",
                "orange",
                $"{innerFiveMeanMeanUpperLimit:F3}|{middleFiveMeanMeanUpperLimit:F3}|{outsideFiveMeanMeanUpperLimit:F3}",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "整个卷芯Gap均值（标准）:",
                "orange",
                $"Gap ≥ {jrGapMeanLowerLimit:F4}",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "0°内5圈均值:",
                "orange",
                $"Gap ≥ {innerFiveMeanLowerLimit:F3}",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + 2 * lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "JR1内层Gap值:",
                "orange",
                $"{innerGap[0].gap:F3}",
                innerGap[0].isOK ? "green" : "red",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "JR2内层Gap值:",
                "orange",
                $"{innerGap[1].gap:F3}",
                innerGap[1].isOK ? "green" : "red",
                valueX
            );

            DrawJRMeanGapTable(
                hWindow,
                textInitRow,
                valueX + 1200,
                z45MeanGap,
                z0MeanGap,
                f45MeanGap
            );
        }

        /// <summary>
        /// 辅助：在指定行绘制标签文本（<paramref name="labelColor"/>）和数值文本（<paramref name="valueColor"/>）。
        /// </summary>
        private void DrawLabelValue(
            HWindow hWindow,
            double row,
            double labelX,
            string label,
            string labelColor,
            string value,
            string valueColor,
            double valueX
        )
        {
            hWindow.SetColor(labelColor);
            hWindow.DispText(label, "window", row, labelX, labelColor, "box", "false");
            hWindow.SetColor(valueColor);
            hWindow.DispText(value, "window", row, valueX, valueColor, "box", "false");
        }

        private void DrawJRMeanGapTable(
            HWindow hWindow,
            double textInitRow,
            double tableInitCol,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap
        )
        {
            var labelColor = "orange";
            const double lineHeight = 150; // 行间距（像素）
            var textCurrentRow = textInitRow;
            //hWindow.SetColor(labelColor);
            //hWindow.DispText("JR1:", "window", textInitRow, tableInitCol, labelColor, "box", "false");
            //textCurrentRow = textCurrentRow + lineHeight;

            for (var i = 0; i < z0MeanGap.Count; i++)
            {
                hWindow.SetColor(labelColor);
                hWindow.DispText(
                    $"JR{i + 1}:",
                    "window",
                    textCurrentRow,
                    tableInitCol,
                    labelColor,
                    "box",
                    "false"
                );
                textCurrentRow = textCurrentRow + lineHeight;

                DrawJRMeanGapSingleLine(
                    hWindow,
                    textCurrentRow,
                    tableInitCol,
                    "0°|+45°|-45°内五层Gap平均值:",
                    "orange",
                    z0MeanGap[i][0],
                    z45MeanGap[i][0],
                    f45MeanGap[i][0],
                    "内"
                );
                textCurrentRow = textCurrentRow + lineHeight;

                DrawJRMeanGapSingleLine(
                    hWindow,
                    textCurrentRow,
                    tableInitCol,
                    "0°|+45°|-45°中五层Gap平均值:",
                    "orange",
                    z0MeanGap[i][1],
                    z45MeanGap[i][1],
                    f45MeanGap[i][1],
                    "中"
                );
                textCurrentRow = textCurrentRow + lineHeight;

                DrawJRMeanGapSingleLine(
                    hWindow,
                    textCurrentRow,
                    tableInitCol,
                    "0°|+45°|-45°外五层Gap平均值:",
                    "orange",
                    z0MeanGap[i][2],
                    z45MeanGap[i][2],
                    f45MeanGap[i][2],
                    "外"
                );
                textCurrentRow = textCurrentRow + lineHeight;

                hWindow.SetColor(labelColor);
                hWindow.DispText(
                    $"整个卷芯Gap检测均值:",
                    "window",
                    textCurrentRow,
                    tableInitCol,
                    labelColor,
                    "box",
                    "false"
                );

                var jrGapMean = Math.Round(
                    (
                        z0MeanGap[i][0].gap
                        + z0MeanGap[i][1].gap
                        + z0MeanGap[i][2].gap
                        + z45MeanGap[i][2].gap
                        + f45MeanGap[i][2].gap
                    ) / 5,
                    4
                );
                var valueColor = jrGapMean >= JRGapMeanLowerLimit ? "green" : "red";
                var value = $"{jrGapMean:F4}";
                hWindow.SetColor(labelColor);
                hWindow.DispText(
                    value,
                    "window",
                    textCurrentRow,
                    tableInitCol + 1100,
                    valueColor,
                    "box",
                    "false"
                );
                textCurrentRow = textCurrentRow + lineHeight;
            }
        }

        private void DrawJRMeanGapSingleLine(
            HWindow hWindow,
            double textInitRow,
            double tableInitCol,
            string label,
            string labelColor,
            (double gap, bool isOK) z0MeanGap,
            (double gap, bool isOK) z45MeanGap,
            (double gap, bool isOK) f45MeanGap,
            string layer
        )
        {
            var charPix = 50;
            var currentTextCol = tableInitCol;
            hWindow.SetColor(labelColor);
            hWindow.DispText(
                label,
                "window",
                textInitRow,
                currentTextCol,
                labelColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + label.Length * (charPix + 15);

            var valueColor = z0MeanGap.isOK ? "green" : "red";
            var value = $"{z0MeanGap.gap:F3}";
            hWindow.SetColor(valueColor);
            hWindow.DispText(
                value,
                "window",
                textInitRow,
                currentTextCol,
                valueColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + value.Length * charPix;

            hWindow.SetColor("green");
            hWindow.DispText("|", "window", textInitRow, currentTextCol, "green", "box", "false");
            currentTextCol = currentTextCol + charPix;

            valueColor = z45MeanGap.isOK ? "green" : "red";
            value = $"{z45MeanGap.gap:F3}";
            hWindow.SetColor(valueColor);
            hWindow.DispText(
                value,
                "window",
                textInitRow,
                currentTextCol,
                valueColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + value.Length * charPix;

            hWindow.SetColor("green");
            hWindow.DispText("|", "window", textInitRow, currentTextCol, "green", "box", "false");
            currentTextCol = currentTextCol + charPix;

            valueColor = f45MeanGap.isOK ? "green" : "red";
            value = $"{f45MeanGap.gap:F3}";
            hWindow.SetColor(valueColor);
            hWindow.DispText(
                value,
                "window",
                textInitRow,
                currentTextCol,
                valueColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + value.Length * (charPix + 15);

            label = "均值:";
            hWindow.SetColor(labelColor);
            hWindow.DispText(
                label,
                "window",
                textInitRow,
                currentTextCol,
                labelColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + label.Length * (charPix + 50);

            var gapMeanvalue = Math.Round((z0MeanGap.gap + z45MeanGap.gap + f45MeanGap.gap) / 3, 3);
            switch (layer)
            {
                case "内":
                    valueColor = gapMeanvalue <= InnerFiveMeanMeanUpperLimit ? "green" : "red";
                    break;
                case "中":
                    valueColor = gapMeanvalue <= MiddleFiveMeanMeanUpperLimit ? "green" : "red";
                    break;
                case "外":
                    valueColor = gapMeanvalue <= OutsideFiveMeanMeanUpperLimit ? "green" : "red";
                    break;
            }
            value = $"{gapMeanvalue:F3}";
            hWindow.SetColor(valueColor);
            hWindow.DispText(
                value,
                "window",
                textInitRow,
                currentTextCol,
                valueColor,
                "box",
                "false"
            );
            currentTextCol = currentTextCol + value.Length * charPix;
        }

        /// <summary>
        /// 构建输出路径
        /// </summary>
        /// <param name="baseOutputDir">基础输出目录</param>
        /// <param name="sourceImageFilePath">原始图片路径</param>
        /// <returns></returns>
        private string BuildOutputPath(string? baseOutputDir, string? sourceImageFilePath)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sourceImageFilePath);
            //var ext = Path.GetExtension(sourceImageFilePath); // tif格式太大
            var ext = ".png";
            //var ext = ".jpg"; // jpg太模糊

            var outputFileName = $"{nameWithoutExt}_result{ext}";

            if (string.IsNullOrEmpty(baseOutputDir))
            {
                var sourceDir = Path.GetDirectoryName(sourceImageFilePath) ?? ".";
                return Path.Combine(sourceDir, outputFileName);
            }

            return Path.Combine(baseOutputDir, outputFileName);
        }

        /// <summary>
        /// Gap 检测
        /// </summary>
        /// <param name="imageFilePath">图片路径</param>
        public void GapDetection(string? imageFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imageFilePath))
                    return;

                // 解析图片信息
                var fileInfo = new FileInfo(imageFilePath);
                var fileName = Path.GetFileNameWithoutExtension(imageFilePath);
                FileSelectInitDir = fileInfo.DirectoryName;
                BottomBlackHeight = 1600;

                _logger.Info($"开始调用算法");
                var startTime = DateTime.Now;
                CTDetectionAlgorithmIstance!.ModelAction(
                    imageFilePath,
                    ModelHandle,
                    BottomBlackHeight,
                    out HObject resultImage,
                    out List<HObject> allPointRegion,
                    out List<HTuple> allRegionRow,
                    out List<HTuple> allRegionCol,
                    out List<HTuple> allJianXiValue,
                    out List<HTuple> allPrintfValue,
                    out string runInfo
                );
                ResultImage = resultImage;
                AllPointRegion = allPointRegion;
                AllRegionRow = allRegionRow;
                AllRegionCol = allRegionCol;
                AllJianXiValue = allJianXiValue;
                AllPrintfValue = allPrintfValue;
                RunInfo = runInfo;
                var duringTime = (DateTime.Now - startTime).TotalSeconds;
                _logger.Info($"算法耗时：{duringTime} s");
                if (ResultImage is null)
                {
                    _logger.Info($"检测图片 [{imageFilePath}] 算法输出图片为空, 获取原图显示");
                    HOperatorSet.ReadImage(out HObject sourceImage, imageFilePath);
                    ResultImage = sourceImage;
                }
                if (!string.IsNullOrEmpty(RunInfo))
                {
                    _logger.Info($"检测图片 [{imageFilePath}] 算法调用错误信息：{RunInfo}");
                }

                // 渲染绘图参数初始化
                List<List<(double row, double col)>> z45Points =
                    new List<List<(double row, double col)>>();
                List<List<(double row, double col)>> z0Points =
                    new List<List<(double row, double col)>>();
                List<List<(double row, double col)>> f45Points =
                    new List<List<(double row, double col)>>();

                List<List<(double row, double col)>> z45LinePositions =
                    new List<List<(double row, double col)>>();
                List<List<(double row, double col)>> z0LinePositions =
                    new List<List<(double row, double col)>>();
                List<List<(double row, double col)>> f45LinePositions =
                    new List<List<(double row, double col)>>();

                List<(double gap, bool isOK)> innerGap = new List<(double gap, bool isOK)>();
                List<List<(double gap, bool isOK)>> z45MeanGap =
                    new List<List<(double gap, bool isOK)>>();
                List<List<(double gap, bool isOK)>> z0MeanGap =
                    new List<List<(double gap, bool isOK)>>();
                List<List<(double gap, bool isOK)>> f45MeanGap =
                    new List<List<(double gap, bool isOK)>>();

                if (string.IsNullOrEmpty(ExportPath))
                {
                    ExportPath = "C:\\";
                }
                if (!Path.Exists(ExportPath))
                {
                    Directory.CreateDirectory(ExportPath);
                }
                var outputPath = BuildOutputPath(ExportPath, imageFilePath);

                if (AllPointRegion != null)
                {
                    foreach (var pointRegion in AllPointRegion)
                    {
                        // 极耳各方位点集
                        var jrz45Points = new List<(double row, double col)>();
                        var jrz0Points = new List<(double row, double col)>();
                        var jrf45Points = new List<(double row, double col)>();

                        // 区域解析点位置
                        HOperatorSet.Union1(pointRegion, out HObject i);
                        HOperatorSet.Connection(i, out i);
                        HOperatorSet.AreaCenter(
                            i,
                            out HTuple area,
                            out HTuple rows,
                            out HTuple cols
                        );

                        // 解析点并进行分类
                        if (rows.Length == 51 && cols.Length == 51)
                        {
                            for (int n = 0; n < 17; n++)
                            {
                                jrf45Points.Add((rows[n], cols[n]));
                                jrz45Points.Add((rows[n + 17], cols[n + 17]));
                                jrz0Points.Add((rows[n + 17 * 2], cols[n + 17 * 2]));
                            }
                        }

                        // 加入所有极耳方位点集
                        f45Points.Add(jrf45Points);
                        z45Points.Add(jrz45Points);
                        z0Points.Add(jrz0Points);

                        // 释放资源
                        i.Dispose();
                        area.Dispose();
                        rows.Dispose();
                        cols.Dispose();
                    }
                }

                if (AllRegionRow != null && AllRegionCol != null)
                {
                    if (AllRegionRow.Count == AllRegionCol.Count)
                    {
                        for (int i = 0; i < AllRegionRow.Count; i++)
                        {
                            // 极耳各方位横线位置集
                            var jrz45LinePositions = new List<(double row, double col)>();
                            var jrz0LinePositions = new List<(double row, double col)>();
                            var jrf45LinePositions = new List<(double row, double col)>();

                            // 解析横线位置并进行分类
                            if (AllRegionRow[i].Length == 16 && AllRegionCol[i].Length == 16)
                            {
                                jrz0LinePositions.Add((AllRegionRow[i][0], AllRegionCol[i][0]));
                                for (int n = 0; n < 5; n++)
                                {
                                    jrf45LinePositions.Add(
                                        (AllRegionRow[i][n + 1], AllRegionCol[i][n + 1])
                                    );
                                    jrz45LinePositions.Add(
                                        (AllRegionRow[i][(n + 1) + 5], AllRegionCol[i][(n + 1) + 5])
                                    );
                                    jrz0LinePositions.Add(
                                        (
                                            AllRegionRow[i][(n + 1) + 5 * 2],
                                            AllRegionCol[i][(n + 1) + 5 * 2]
                                        )
                                    );
                                }
                            }

                            // 加入所有极耳方位横线位置集
                            f45LinePositions.Add(jrf45LinePositions);
                            z45LinePositions.Add(jrz45LinePositions);
                            z0LinePositions.Add(jrz0LinePositions);
                        }
                    }
                }

                if (AllPrintfValue != null)
                {
                    foreach (var printfValue in AllPrintfValue)
                    {
                        // 极耳各方位Gap均值集
                        var jrz45MeanGap = new List<(double gap, bool isOK)>();
                        var jrz0MeanGap = new List<(double gap, bool isOK)>();
                        var jrf45MeanGap = new List<(double gap, bool isOK)>();

                        // 解析Gap均值并进行分类
                        if (printfValue.Length == 10)
                        {
                            // px * 精度（0.011）-0.185/2-0.136/2-0.01
                            double innerGapValue = Math.Round(
                                (double)(printfValue[0] * 0.011 - 0.185 / 2 - 0.136 / 2 - 0.01),
                                3
                            );
                            innerGap.Add(
                                (
                                    innerGapValue,
                                    InnerGapLowerLimit <= innerGapValue
                                        && innerGapValue <= InnerGapUpperLimit
                                )
                            );

                            for (int n = 0; n < 3; n++)
                            {
                                // (px * 精度（0.011）-0.185-0.136)/2-0.01
                                double f45MeanGapValue = Math.Round(
                                    (double)(
                                        (printfValue[n + 1] * 0.011 - 0.185 - 0.136) / 2 - 0.01
                                    ),
                                    3
                                );
                                ;
                                double z45MeanGapValue = Math.Round(
                                    (double)(
                                        (printfValue[(n + 1) + 3] * 0.011 - 0.185 - 0.136) / 2
                                        - 0.01
                                    ),
                                    3
                                );
                                double z0MeanGapValue = Math.Round(
                                    (double)(
                                        (printfValue[(n + 1) + 3 * 2] * 0.011 - 0.185 - 0.136) / 2
                                        - 0.01
                                    ),
                                    3
                                );

                                if (n == 2)
                                {
                                    jrf45MeanGap.Add((f45MeanGapValue, true));
                                    jrz45MeanGap.Add((z45MeanGapValue, true));
                                }
                                else
                                {
                                    jrf45MeanGap.Add((f45MeanGapValue, true));
                                    jrz45MeanGap.Add((z45MeanGapValue, true));
                                }

                                if (n == 0)
                                {
                                    jrz0MeanGap.Add(
                                        (z0MeanGapValue, z0MeanGapValue >= InnerFiveMeanLowerLimit)
                                    );
                                }
                                else
                                {
                                    jrz0MeanGap.Add((z0MeanGapValue, true));
                                }
                            }
                        }

                        // 加入所有极耳方位Gap均值集
                        f45MeanGap.Add(jrf45MeanGap);
                        z45MeanGap.Add(jrz45MeanGap);
                        z0MeanGap.Add(jrz0MeanGap);
                    }
                }

                if (!string.IsNullOrEmpty(RunInfo))
                {
                    LastImageIsOK = false;
                }
                else
                {
                    LastImageIsOK = IsOK(innerGap, z45MeanGap, z0MeanGap, f45MeanGap);
                }

                var resultCode = LastImageIsOK ? "OK" : "NG";

                _logger.Info($"[检测结果判定]: 图片 {imageFilePath} => {resultCode}");

                var rendered = GenerateResultImage(
                    ResultImage,
                    z45Points,
                    z0Points,
                    f45Points,
                    z45LinePositions,
                    z0LinePositions,
                    f45LinePositions,
                    innerGap,
                    z45MeanGap,
                    z0MeanGap,
                    f45MeanGap,
                    resultCode,
                    RunInfo,
                    fileName,
                    outputPath
                );

                RunOnUIThread(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    if (rendered)
                        bmp.UriSource = new Uri(outputPath, UriKind.Absolute);
                    else
                        bmp.UriSource = new Uri(imageFilePath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad; // 立即读入内存，释放文件句柄
                    bmp.EndInit();
                    bmp.Freeze(); // 跨线程安全
                    LastProcessedImage = bmp;
                });

                if (string.IsNullOrEmpty(RunInfo))
                {
                    SaveSingleCSVFile(
                        fileName,
                        resultCode,
                        innerGap,
                        z0MeanGap,
                        z45MeanGap,
                        f45MeanGap
                    );
                    SaveCSVFileByDay(
                        fileName,
                        resultCode,
                        innerGap,
                        z0MeanGap,
                        z45MeanGap,
                        f45MeanGap
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检测错误", ex);
            }
            finally
            {
                TotalDetectionNum++;
                if (LastImageIsOK)
                {
                    OKDetectionNum++;
                }
                else
                {
                    NGDetectionNum++;
                }
                if (TotalDetectionNum != 0)
                    OKRate = OKDetectionNum * 100.0 / TotalDetectionNum;

                ResultImage?.Dispose();
                ResultImage = null;
                if (AllPointRegion != null)
                {
                    foreach (var item in AllPointRegion)
                    {
                        item.Dispose();
                    }
                }
                AllPointRegion = null;
                if (AllRegionRow != null)
                {
                    foreach (var item in AllRegionRow)
                    {
                        item.Dispose();
                    }
                }
                AllRegionRow = null;
                if (AllRegionCol != null)
                {
                    foreach (var item in AllRegionCol)
                    {
                        item.Dispose();
                    }
                }
                AllRegionCol = null;
                if (AllJianXiValue != null)
                {
                    foreach (var item in AllJianXiValue)
                    {
                        item.Dispose();
                    }
                }
                AllJianXiValue = null;
                if (AllPrintfValue != null)
                {
                    foreach (var item in AllPrintfValue)
                    {
                        item.Dispose();
                    }
                }
                AllPrintfValue = null;
            }
        }

        private void SaveSingleCSVFile(
            string fileName,
            string resultCode,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap
        )
        {
            var dataTable = new DataTable();
            // ===== 构建表头 =====
            dataTable.Columns.Add("记录时间", typeof(DateTime));
            dataTable.Columns.Add("电芯条码", typeof(string));
            dataTable.Columns.Add("检测结果", typeof(string));

            dataTable.Columns.Add("最内层Gap标准下限", typeof(double));
            dataTable.Columns.Add("最内层Gap标准上限", typeof(double));
            dataTable.Columns.Add("内均值上限", typeof(double));
            dataTable.Columns.Add("中均值上限", typeof(double));
            dataTable.Columns.Add("外均值上限", typeof(double));
            dataTable.Columns.Add("整个卷芯Gap均值下限", typeof(double));
            dataTable.Columns.Add("0°内5圈均值下限", typeof(double));

            for (int i = 0; i < 2; i++)
            {
                dataTable.Columns.Add($"JR{i + 1}内层Gap值", typeof(double));
            }

            for (int i = 0; i < 2; i++)
            {
                dataTable.Columns.Add($"JR{i + 1}内五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}整个卷芯Gap检测均值", typeof(double));
            }

            var row = dataTable.NewRow();
            row["记录时间"] = DateTime.Now;
            row["电芯条码"] = $"{fileName}";
            row["检测结果"] = $"{resultCode}";

            row["最内层Gap标准下限"] = InnerGapLowerLimit;
            row["最内层Gap标准上限"] = InnerGapUpperLimit;
            row["内均值上限"] = InnerFiveMeanMeanUpperLimit;
            row["中均值上限"] = MiddleFiveMeanMeanUpperLimit;
            row["外均值上限"] = OutsideFiveMeanMeanUpperLimit;
            row["整个卷芯Gap均值下限"] = JRGapMeanLowerLimit;
            row["0°内5圈均值下限"] = InnerFiveMeanLowerLimit;

            for (int i = 0; i < 2; i++)
            {
                row[$"JR{i + 1}内层Gap值"] = innerGap[i].gap;
            }

            for (int i = 0; i < 2; i++)
            {
                row[$"JR{i + 1}内五层0°方向Gap平均值"] = z0MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层+45°方向Gap平均值"] = z45MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层-45°方向Gap平均值"] = f45MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][0].gap + z45MeanGap[i][0].gap + f45MeanGap[i][0].gap) / 3,
                    3
                );

                row[$"JR{i + 1}中五层0°方向Gap平均值"] = z0MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层+45°方向Gap平均值"] = z45MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层-45°方向Gap平均值"] = f45MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][1].gap + z45MeanGap[i][1].gap + f45MeanGap[i][1].gap) / 3,
                    3
                );

                row[$"JR{i + 1}外五层0°方向Gap平均值"] = z0MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层+45°方向Gap平均值"] = z45MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层-45°方向Gap平均值"] = f45MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][2].gap + z45MeanGap[i][2].gap + f45MeanGap[i][2].gap) / 3,
                    3
                );

                row[$"JR{i + 1}整个卷芯Gap检测均值"] = Math.Round(
                    (
                        z0MeanGap[i][0].gap
                        + z0MeanGap[i][1].gap
                        + z0MeanGap[i][2].gap
                        + z45MeanGap[i][2].gap
                        + f45MeanGap[i][2].gap
                    ) / 5,
                    4
                );
            }

            dataTable.Rows.Add(row);

            var filePath = Path.Combine(ExportPath!, $"{fileName}.csv");
            CsvSaveHelper.WriteToFile(filePath, dataTable);
        }

        private void SaveCSVFileByDay(
            string barcode,
            string resultCode,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap
        )
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var directory = Path.GetFullPath(Path.Combine(ExportPath!, "汇总"));
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            var filePath = Path.Combine(directory, $"Gap检测汇总_{date}.csv");

            var dataTable = new DataTable();
            // ===== 构建表头 =====
            dataTable.Columns.Add("记录时间", typeof(DateTime));
            dataTable.Columns.Add("电芯条码", typeof(string));
            dataTable.Columns.Add("检测结果", typeof(string));

            dataTable.Columns.Add("最内层Gap标准下限", typeof(double));
            dataTable.Columns.Add("最内层Gap标准上限", typeof(double));
            dataTable.Columns.Add("内均值上限", typeof(double));
            dataTable.Columns.Add("中均值上限", typeof(double));
            dataTable.Columns.Add("外均值上限", typeof(double));
            dataTable.Columns.Add("整个卷芯Gap均值下限", typeof(double));
            dataTable.Columns.Add("0°内5圈均值下限", typeof(double));

            for (int i = 0; i < 2; i++)
            {
                dataTable.Columns.Add($"JR{i + 1}内层Gap值", typeof(double));
            }

            for (int i = 0; i < 2; i++)
            {
                dataTable.Columns.Add($"JR{i + 1}内五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}内五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}中五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层0°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层+45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层-45°方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}外五层三方向Gap平均值", typeof(double));
                dataTable.Columns.Add($"JR{i + 1}整个卷芯Gap检测均值", typeof(double));
            }

            var row = dataTable.NewRow();
            row["记录时间"] = DateTime.Now;
            row["电芯条码"] = $"{barcode}";
            row["检测结果"] = $"{resultCode}";

            row["最内层Gap标准下限"] = InnerGapLowerLimit;
            row["最内层Gap标准上限"] = InnerGapUpperLimit;
            row["内均值上限"] = InnerFiveMeanMeanUpperLimit;
            row["中均值上限"] = MiddleFiveMeanMeanUpperLimit;
            row["外均值上限"] = OutsideFiveMeanMeanUpperLimit;
            row["整个卷芯Gap均值下限"] = JRGapMeanLowerLimit;
            row["0°内5圈均值下限"] = InnerFiveMeanLowerLimit;

            for (int i = 0; i < 2; i++)
            {
                row[$"JR{i + 1}内层Gap值"] = innerGap[i].gap;
            }

            for (int i = 0; i < 2; i++)
            {
                row[$"JR{i + 1}内五层0°方向Gap平均值"] = z0MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层+45°方向Gap平均值"] = z45MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层-45°方向Gap平均值"] = f45MeanGap[i][0].gap;
                row[$"JR{i + 1}内五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][0].gap + z45MeanGap[i][0].gap + f45MeanGap[i][0].gap) / 3,
                    3
                );

                row[$"JR{i + 1}中五层0°方向Gap平均值"] = z0MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层+45°方向Gap平均值"] = z45MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层-45°方向Gap平均值"] = f45MeanGap[i][1].gap;
                row[$"JR{i + 1}中五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][1].gap + z45MeanGap[i][1].gap + f45MeanGap[i][1].gap) / 3,
                    3
                );

                row[$"JR{i + 1}外五层0°方向Gap平均值"] = z0MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层+45°方向Gap平均值"] = z45MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层-45°方向Gap平均值"] = f45MeanGap[i][2].gap;
                row[$"JR{i + 1}外五层三方向Gap平均值"] = Math.Round(
                    (z0MeanGap[i][2].gap + z45MeanGap[i][2].gap + f45MeanGap[i][2].gap) / 3,
                    3
                );

                row[$"JR{i + 1}整个卷芯Gap检测均值"] = Math.Round(
                    (
                        z0MeanGap[i][0].gap
                        + z0MeanGap[i][1].gap
                        + z0MeanGap[i][2].gap
                        + z45MeanGap[i][2].gap
                        + f45MeanGap[i][2].gap
                    ) / 5,
                    4
                );
            }

            dataTable.Rows.Add(row);

            CsvSaveHelper.AppendToFile(filePath, dataTable);
        }

        private bool IsOK(
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap
        )
        {
            var isInnerGapOK = !innerGap.Any(g => g.isOK == false); // 规范1：首内圈
            var isFiveMeanMeanOK = true; // 规范1：内|中|外五层均值的均值
            var isAllJRGapMeanOK = true; // 规范2：整个卷芯的Gap均值
            for (int i = 0; i < z0MeanGap.Count; i++)
            {
                // 内
                var isInnerFiveMeanMeanOK = FiveGapMeanMeanIsOK(
                    z45MeanGap[i][0].gap,
                    z0MeanGap[i][0].gap,
                    f45MeanGap[i][0].gap,
                    InnerFiveMeanMeanUpperLimit
                );
                // 中
                var isMiddleFiveMeanMeanOK = FiveGapMeanMeanIsOK(
                    z45MeanGap[i][1].gap,
                    z0MeanGap[i][1].gap,
                    f45MeanGap[i][1].gap,
                    MiddleFiveMeanMeanUpperLimit
                );
                // 外
                var isOutsideFiveMeanMeanOK = FiveGapMeanMeanIsOK(
                    z45MeanGap[i][2].gap,
                    z0MeanGap[i][2].gap,
                    f45MeanGap[i][2].gap,
                    OutsideFiveMeanMeanUpperLimit
                );

                var isjrGapMeanOK = JRGapMeanIsOK(
                    z0MeanGap[i][0].gap,
                    z0MeanGap[i][1].gap,
                    z0MeanGap[i][2].gap,
                    z45MeanGap[i][2].gap,
                    f45MeanGap[i][2].gap,
                    JRGapMeanLowerLimit
                );

                isFiveMeanMeanOK =
                    isFiveMeanMeanOK
                    && isInnerFiveMeanMeanOK
                    && isMiddleFiveMeanMeanOK
                    && isOutsideFiveMeanMeanOK;
                isAllJRGapMeanOK = isAllJRGapMeanOK && isjrGapMeanOK;
            }

            var isZ45MeanGapOK = !z45MeanGap.Any(jr => jr.Any(g => g.isOK == false));
            var isZ0MeanGapOK = !z0MeanGap.Any(jr => jr.Any(g => g.isOK == false)); // 规范3：0度方向上内层均值
            var isF45MeanGapOK = !f45MeanGap.Any(jr => jr.Any(g => g.isOK == false));

            return (
                isInnerGapOK
                && isFiveMeanMeanOK
                && isAllJRGapMeanOK
                && isZ45MeanGapOK
                && isZ0MeanGapOK
                && isF45MeanGapOK
            );
        }

        private bool FiveGapMeanMeanIsOK(double z45, double z0, double f45, double upperLimit)
        {
            var fiveMeanMean = Math.Round((z45 + z0 + f45) / 3, 3);
            return fiveMeanMean <= upperLimit;
        }

        private bool JRGapMeanIsOK(
            double z0Inner,
            double z0Middle,
            double z0Outside,
            double z45Outside,
            double f45Outside,
            double lowerLimit
        )
        {
            var jrGapMean = Math.Round(
                (z0Inner + z0Middle + z0Outside + z45Outside + f45Outside) / 5,
                4
            );
            return jrGapMean >= lowerLimit;
        }
    }
}
