using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CF;
using Dm.util;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using NJCT;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTNCLX;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.Common;
using SUNWODA_SEVB.ViewModels.Dialogs.CTNCLX;

namespace SUNWODA_SEVB.ViewModels.Pages.CTNCLX
{
    [Module("VM_CTNCLXAutoDetectionPage", "南昌离线CT极片Gap AI检测系统")]
    public class VM_CTNCLXAutoDetectionPage : ViewModelBase
    {
        private readonly ILoggerService<VM_CTNCLXAutoDetectionPage> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;

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
        private double innerGapLimit;
        private double fiveGapMeanLimit;

        private List<double>? _detectionSattisticalData;
        private List<string>? _detectionSattisticalLabel;
        private List<string>? _detectionStatisticalLabelColor;

        private ImageSource? _lastProcessedImage;

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
        /// 最内层 Gap 标准
        /// </summary>
        public double InnerGapLimit
        {
            get => innerGapLimit;
            set
            {
                _projectSettingRepository.UpdateSettingValue(
                    "InnerGapLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    value
                );
                SetProperty(ref innerGapLimit, value);
            }
        }

        /// <summary>
        /// 五层均值 Gap 标准
        /// </summary>
        public double FiveGapMeanLimit
        {
            get => fiveGapMeanLimit;
            set
            {
                _projectSettingRepository.UpdateSettingValue(
                    "FiveGapMeanLimit",
                    "VM_CTNCLXAutoDetectionPage",
                    value
                );
                SetProperty(ref fiveGapMeanLimit, value);
            }
        }

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

        #region 命令
        public ICommand? MonitorPathSelectCommand { get; set; }

        public ICommand? FileSelectCommand { get; set; }

        public ICommand? ExportPathSelectCommand { get; set; }
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
                    model.Name == $"InnerGapLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerGapLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{InnerGapLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"最内层 Gap 标准",
                    }
                );
            }
            else
            {
                var _innerGapLimit = _projectSettingRepository.GetSettingValue(
                    $"InnerGapLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                InnerGapLimit = _innerGapLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"FiveGapMeanLimit"
                    && model.BelongToVM == "VM_CTNCLXAutoDetectionPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"FiveGapMeanLimit",
                        "VM_CTNCLXAutoDetectionPage",
                        $"{FiveGapMeanLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"五层均值 Gap 标准",
                    }
                );
            }
            else
            {
                var _fiveGapMeanLimit = _projectSettingRepository.GetSettingValue(
                    $"FiveGapMeanLimit",
                    "VM_CTNCLXAutoDetectionPage"
                );
                FiveGapMeanLimit = _fiveGapMeanLimit;
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

            if (!File.Exists(ModelPath))
            {
                ModelPath = ".\\model\\NCLXCT\\model.onnx";
                var startTime = DateTime.Now;
                //var modelLoadingWindow = new VM_ModelLoadingWindow(ModelPath);
                //modelLoadingWindow.ShowDialog();
                //DownloadModel(ModelPath);
                _logger.Info($"下载模型耗时：{(DateTime.Now - startTime).TotalSeconds} s");
            }
            ModelPath = ".\\model\\NCLXCT\\model.onnx";
            CTDetectionAlgorithmIstance = new NJSwdCT();
            CTDetectionAlgorithmIstance.ModelInit(
                ModelPath,
                out IPredictor modelHandle,
                out string modelInitErrorInfo,
                IsRunCuda
            );
            ModelHandle = modelHandle;
            ModelInitErrorInfo = modelInitErrorInfo;
            _logger.Info($"模型句柄是否为空：{ModelHandle is null}");
            _logger.Info($"模型初始化错误信息：{ModelInitErrorInfo}");

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
                            ImagePathQueue.TryDequeue(out string? imagePath);
                            _logger.Info($"获取到图片路径：{imagePath}");
                            GapDetection(imagePath);
                            Thread.Sleep(5000);
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

            //var ctGapAlgorithmType = LoadAssembly($"{UserDefineVariableInfo.DicVariables["GapAlgorithmDllName"]}").GetType("NJCT.NJSwdCT");
            //CTDetectionAlgorithmIstance = Activator.CreateInstance(ctGapAlgorithmType);
            //ActionGetPointMethod = ctGapAlgorithmType.GetMethod("ActionGetPoint");
            //ModelInitMethod = ctGapAlgorithmType.GetMethod("ModelInit");

            //if (!File.Exists(ModelPath))
            //{
            //    ModelPath = "..\\Model\\model.onnx";
            //    var startTime = DateTime.Now;
            //    var modelLoadingWindow = new VM_ModelLoadingWindow(ModelPath);
            //    modelLoadingWindow.ShowDialog();
            //    //DownloadModel(ModelPath);
            //    _logger.Info($"下载模型耗时：{(DateTime.Now - startTime).TotalSeconds} s");
            //}
            //ModelInitParameters = new object[4] { ModelPath, ModelHandle, ModelInitErrorInfo, IsRunCuda };
            //_logger.Info("开始加载模型");
            //ModelInitMethod?.Invoke(CTDetectionAlgorithmIstance, ModelInitParameters);
            //ModelHandle = ModelInitParameters[1] as IPredictor;
            //_logger.Info($"模型句柄是否为空：{ModelHandle is null}");
            //_logger.Info($"模型初始化错误信息：{ModelInitErrorInfo}");
            //if (File.Exists("..\\Model\\model.onnx"))
            //    File.Delete("..\\Model\\model.onnx");

            //AlgorithmParameters = new object[9] { ImagePath, ModelHandle, BottomBlackHeight, ResultImage, Z45Point, F45Point, F0Point, NeiCengPoints, RunInfo };
            //WatchImageFileFolder();
            //SwitchWatchImageFileFolder(IsContinuousDetection);
            //ImagePathQueue = new ConcurrentQueue<string>();
            //GapDetectionThread = new Thread(() =>
            //{
            //    while (true)
            //    {
            //        try
            //        {
            //            if (!ImagePathQueue.IsEmpty)
            //            {
            //                ImagePathQueue.TryDequeue(out string imagePath);
            //                AlgorithmRunInfoContent.Clear();
            //                _logger.Info($"获取到图片路径：{imagePath}");
            //                AlgorithmRunInfoContent.Append($"{imagePath},");
            //                GapDetection(imagePath);
            //                Thread.Sleep(5000);
            //            }
            //            Thread.Sleep(1000);
            //        }
            //        catch (Exception ex)
            //        {
            //            _logger.Error("检测线程错误", ex);
            //        }
            //    }
            //});
            //GapDetectionThread.IsBackground = true;
            //GapDetectionThread.Start();

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
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "获取CT切片图片";
            openFileDialog.InitialDirectory = FileSelectInitDir; // 设置初始目录
            openFileDialog.Filter = "图片 (*.tif)|*.tif"; // 设置文件过滤器
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == true) // 显示对话框
            {
                Task.Run(() =>
                {
                    try
                    {
                        // 获取选中文件的路径
                        var filePath = openFileDialog.FileName;
                        _logger.Info($"获取到图片路径：{filePath}");
                        GapDetection(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("检测错误", ex);
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

        /// <summary>
        /// 监控功能初始化
        /// </summary>
        public void WatchImageFileFolder()
        {
            ImageFileSystemWatcher = new FileSystemWatcher();
            // 设置要监测的文件夹路径
            ImageFileSystemWatcher.Path = ImageMonitorPath!;
            ImageFileSystemWatcher.Created += OnCreated;
            // 不设置过滤器，监控所有类型的变化
            ImageFileSystemWatcher.Filter = "*.tif";
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
                ImagePathQueue!.Enqueue(e.FullPath);
            }

            //if (Directory.Exists(e.FullPath))
            //{
            //    _logger.Info($"文件夹创建：{e.FullPath}");
            //    if (e.Name!.Contains("\\") && e.Name.EndsWith("denoise_result"))
            //    {
            //        Task.Run(() =>
            //        {
            //            try
            //            {
            //                var isGetImagePath = false;
            //                var startTime = DateTime.Now;
            //                while (!isGetImagePath)
            //                {
            //                    if ((DateTime.Now - startTime).TotalSeconds > 1200)
            //                        break;
            //                    if (File.Exists($"{e.FullPath}\\result.bmp"))
            //                    {
            //                        ImagePathQueue!.Enqueue($"{e.FullPath}\\result.bmp");
            //                        isGetImagePath = true;
            //                    }
            //                    Thread.Sleep(1000);
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                _logger.Error("图片入队错误", ex);
            //            }
            //        });
            //    }
            //}
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
                double row1 = imgHeight - BottomBlackHeight;
                double col1 = 0;
                double row2 = imgHeight - 1;
                double col2 = imgWidth - 1;

                // 在窗口中画黑色实心矩形
                tempWindow.SetColor("black");
                tempWindow.SetDraw("fill");
                tempWindow.DispRectangle1(row1, col1, row2, col2);

                // ④ 绘制顶部文本：条码 + 检测结果
                var isInnerGapNG = innerGap.Any(g => g.isOK == false);
                var isZ45MeanGapNG = z45MeanGap.Any(jr => jr.Any(g => g.isOK == false));
                var isZ0MeanGapNG = z0MeanGap.Any(jr => jr.Any(g => g.isOK == false));
                var isF45MeanGapNG = f45MeanGap.Any(jr => jr.Any(g => g.isOK == false));
                var resultCode =
                    (isInnerGapNG || isZ45MeanGapNG || isZ0MeanGapNG || isF45MeanGapNG)
                        ? "NG"
                        : "OK";
                DrawTopInfo(fileName, resultCode, tempWindow);

                // ⑤ 绘制点标记
                //DrawPointMarkers(z45Points, innerGap, z45MeanGap, tempWindow);
                //DrawPointMarkers(z0Points, innerGap, z0MeanGap, tempWindow);
                //DrawPointMarkers(f45Points, innerGap, f45MeanGap, tempWindow);
                if (AllPointRegion != null)
                {
                    foreach (var item in AllPointRegion)
                    {
                        tempWindow.SetColor("green");
                        tempWindow.DispObj(item);
                    }
                }

                // ⑥ 绘制横线标记
                DrawLineMarkers(z45LinePositions, innerGap, z45MeanGap, Math.PI / 4, tempWindow);
                DrawLineMarkers(z0LinePositions, innerGap, z0MeanGap, 0.0, tempWindow);
                DrawLineMarkers(f45LinePositions, innerGap, f45MeanGap, - Math.PI / 4, tempWindow);

                // ⑦ 绘制文字
                double textCurentRow = row1 + 100;
                DrawTextInfo(
                    InnerGapLimit,
                    FiveGapMeanLimit,
                    innerGap,
                    z45MeanGap,
                    z0MeanGap,
                    f45MeanGap,
                    textCurentRow,
                    tempWindow
                );

                // ⑧ 导出标注图像
                string tempFile = Path.GetTempFileName() + ".tif";
                try
                {
                    tempWindow.DumpWindow("tiff", tempFile);
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
            string headerText = $"\t\t电芯条码:{barcode}\t\t\t检测结果:";
            var len = headerText.Length;
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");

            // 行 0，列 x：结果值（OK=绿色 / NG=红色）
            bool isOk = resultCode.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
            string resColor = isOk ? "green" : "red";
            hWindow.SetColor(resColor);
            hWindow.DispText(resultCode, "window", 20, len * 108, resColor, "box", "false");
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
                            8,
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
            double innerGapLimit,
            double fiveGapMeanLimit,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            double textInitRow,
            HWindow hWindow
        )
        {
            hWindow.SetFont("Times New Roman-120");

            const double titleX = 200; // 标签列 X 起始
            const double valueX = 1300; // 数值列 X
            const double lineHeight = 150; // 行间距（像素）
            double textCurrentRow = textInitRow;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "最内层Gap标准:",
                "orange",
                $"Gap ≤ {innerGapLimit:F3} mm",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "五层均值Gap标准:",
                "orange",
                $"Gap ≥ {fiveGapMeanLimit:F3} mm",
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
                $"{innerGap[0].gap:F3} mm",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "JR1内层Gap值:",
                "orange",
                $"{innerGap[1].gap:F3} mm",
                "green",
                valueX
            );
        }

        /// <summary>
        /// 辅助：在指定行绘制标签文本（<paramref name="labelColor"/>）和数值文本（<paramref name="valueColor"/>）。
        /// </summary>
        private static void DrawLabelValue(
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

        /// <summary>
        /// 构建输出路径
        /// </summary>
        /// <param name="baseOutputDir">基础输出目录</param>
        /// <param name="sourceImageFilePath">原始图片路径</param>
        /// <returns></returns>
        private string BuildOutputPath(string? baseOutputDir, string? sourceImageFilePath)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sourceImageFilePath);
            var ext = Path.GetExtension(sourceImageFilePath);

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
                BottomBlackHeight = 1500;

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
                _logger.Info($"输出图片是否为空：{(ResultImage is null)}");
                _logger.Info($"算法调用错误信息：{RunInfo}");

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

                var outputPath = BuildOutputPath(ExportPath, imageFilePath);

                foreach (var pointRegion in AllPointRegion)
                {
                    // 极耳各方位点集
                    var jrz45Points = new List<(double row, double col)>();
                    var jrz0Points = new List<(double row, double col)>();
                    var jrf45Points = new List<(double row, double col)>();

                    // 区域解析点位置
                    HOperatorSet.Union1(pointRegion, out HObject i);
                    HOperatorSet.Connection(i, out i);
                    HOperatorSet.AreaCenter(i, out HTuple area, out HTuple rows, out HTuple cols);

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
                    z0Points.Add(jrf45Points);

                    // 释放资源
                    i.Dispose();
                    area.Dispose();
                    rows.Dispose();
                    cols.Dispose();
                }

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

                foreach (var printfValue in AllPrintfValue)
                {
                    // 极耳各方位Gap均值集
                    var jrz45MeanGap = new List<(double gap, bool isOK)>();
                    var jrz0MeanGap = new List<(double gap, bool isOK)>();
                    var jrf45MeanGap = new List<(double gap, bool isOK)>();

                    // 解析Gap均值并进行分类
                    if (printfValue.Length == 10)
                    {
                        innerGap.Add((printfValue[0], printfValue[0] <= InnerGapLimit));

                        for (int n = 0; n < 3; n++)
                        {
                            if (n == 2)
                            {
                                jrf45MeanGap.Add(
                                    (printfValue[n + 1], printfValue[n + 1] >= FiveGapMeanLimit)
                                );
                                jrz45MeanGap.Add(
                                    (
                                        printfValue[(n + 1) + 3],
                                        printfValue[(n + 1) + 3] >= FiveGapMeanLimit
                                    )
                                );
                            }
                            else
                            {
                                jrf45MeanGap.Add((printfValue[n + 1], true));
                                jrz45MeanGap.Add((printfValue[(n + 1) + 3], true));
                            }

                            jrz0MeanGap.Add(
                                (
                                    printfValue[(n + 1) + 3 * 2],
                                    printfValue[(n + 1) + 3 * 2] >= FiveGapMeanLimit
                                )
                            );
                        }
                    }

                    // 加入所有极耳方位Gap均值集
                    f45MeanGap.Add(jrf45MeanGap);
                    z45MeanGap.Add(jrz45MeanGap);
                    z0MeanGap.Add(jrz0MeanGap);
                }

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
            }
            catch (Exception ex)
            {
                _logger.Error("检测错误", ex);
            }
            finally
            {
                ResultImage?.Dispose();
                if (AllPointRegion != null)
                {
                    foreach (var item in AllPointRegion)
                    {
                        item.Dispose();
                    }
                }
                if (AllRegionRow != null)
                {
                    foreach (var item in AllRegionRow)
                    {
                        item.Dispose();
                    }
                }
                if (AllRegionCol != null)
                {
                    foreach (var item in AllRegionCol)
                    {
                        item.Dispose();
                    }
                }
                if (AllJianXiValue != null)
                {
                    foreach (var item in AllJianXiValue)
                    {
                        item.Dispose();
                    }
                }
                if (AllPrintfValue != null)
                {
                    foreach (var item in AllPrintfValue)
                    {
                        item.Dispose();
                    }
                }
            }

            //SaveCurrentTime = DateTime.Now;
            //var fileInfo = new FileInfo(imageFilePath);
            //var fileName = Path.GetFileNameWithoutExtension(imageFilePath);
            //FileSelectInitDir = fileInfo.DirectoryName;
            //AlgorithmParameters[0] = imageFilePath;
            //AlgorithmParameters[2] = 1000;
            //_logger.Info($"开始调用算法");
            //var startTime = DateTime.Now;
            //ActionGetPointMethod?.Invoke(CTDetectionAlgorithmIstance, AlgorithmParameters);
            //var duringTime = (DateTime.Now - startTime).TotalSeconds;
            //_logger.Info($"算法耗时：{duringTime} s");
            //_logger.Info($"输出图片是否为空：{(AlgorithmParameters[3] is null)}");
            //_logger.Info($"算法调用错误信息：{AlgorithmParameters[8]}");
            //AlgorithmRunInfoContent.Append($"{duringTime},{(AlgorithmParameters[3] is null)},{AlgorithmParameters[8]}");

            //// 保存时间判断
            //if (
            //SaveCurrentTime.Year > SaveOldTime.Year
            //|| (SaveCurrentTime.Year == SaveOldTime.Year && SaveCurrentTime.Month > SaveOldTime.Month)
            //|| (
            //SaveCurrentTime.Year == SaveOldTime.Year
            //                        && SaveCurrentTime.Month == SaveOldTime.Month
            //                        && SaveCurrentTime.Day > SaveOldTime.Day
            //)
            //                )
            //{
            //    SaveOldTime = SaveCurrentTime;
            //}

            //var imageParentDir = Directory.GetParent(FileSelectInitDir);
            //if (!(AlgorithmParameters[3] is null))
            //{
            //    // 保存增强图并显示
            //    if (imageParentDir is null) //脚位目录是否存在
            //    {
            //        DispatcherHelper.UI_Dispatcher.Invoke(() =>
            //        {
            //            OriginImage = AlgorithmParameters[3] as HObject;
            //            if (!Directory.Exists($"{FileSelectInitDir}"))
            //                Directory.CreateDirectory($"{FileSelectInitDir}");
            //            HOperatorSet.WriteImage(OriginImage, "jpeg 90", 0, $"{FileSelectInitDir}\\{fileName}_ai_res.jpg");
            //            DetectionResultImage = new HImage($"{FileSelectInitDir}\\{fileName}_ai_res.jpg");
            //            if (!IsSaveAIEnhanceResult)
            //                File.Delete($"{FileSelectInitDir}\\{fileName}_ai_res.jpg");
            //        });
            //    }
            //    else
            //    {
            //        if (imageParentDir.Parent is null) // SN码目录是否存在
            //        {
            //            //var saveDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\OK\\{imageParentDir.Name}";
            //            var saveDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\Temp\\{imageParentDir.Name}";
            //            DispatcherHelper.UI_Dispatcher.Invoke(() =>
            //            {
            //                OriginImage = AlgorithmParameters[3] as HObject;
            //                if (!Directory.Exists(saveDir))
            //                    Directory.CreateDirectory(saveDir);
            //                HOperatorSet.WriteImage(OriginImage, "jpeg 90", 0, $"{saveDir}\\{fileName}_ai_res.jpg");
            //                DetectionResultImage = new HImage($"{saveDir}\\{fileName}_ai_res.jpg");
            //                if (!IsSaveAIEnhanceResult)
            //                    File.Delete($"{saveDir}\\{fileName}_ai_res.jpg");
            //            });
            //            FileAndDirHelper.CopyFileToDir(imageFilePath, $"{saveDir}\\OrignImage");
            //        }
            //        else
            //        {
            //            //var saveDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\OK\\{imageParentDir.Parent.Name}\\{imageParentDir.Name}";
            //            var saveDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\Temp\\{imageParentDir.Parent.Name}\\{imageParentDir.Name}";
            //            DispatcherHelper.UI_Dispatcher.Invoke(() =>
            //            {
            //                OriginImage = AlgorithmParameters[3] as HObject;
            //                if (!Directory.Exists(saveDir))
            //                    Directory.CreateDirectory(saveDir);
            //                HOperatorSet.WriteImage(OriginImage, "jpeg 90", 0, $"{saveDir}\\{fileName}_ai_res.jpg");
            //                DetectionResultImage = new HImage($"{saveDir}\\{fileName}_ai_res.jpg");
            //                if (!IsSaveAIEnhanceResult)
            //                    File.Delete($"{saveDir}\\{fileName}_ai_res.jpg");
            //            });
            //            FileAndDirHelper.CopyFileToDir(imageFilePath, $"{saveDir}\\OrignImage");
            //        }
            //    }
            //}

            //// 根据算法返回错误信息，显示相应文字
            //var isError = AlgorithmParameters[8].ToString() != "";
            //if (!isError)
            //{
            //    DrawMarkAndText(imageParentDir?.Parent is null ? "" : imageParentDir.Parent.Name, imageParentDir is null ? "" : (CellCornerTranslateDic.ContainsKey(imageParentDir.Name.ToUpper()) ? CellCornerTranslateDic[imageParentDir.Name.ToUpper()] : "未知角位"));
            //}
            //else
            //{
            //    DrawErrorText(imageParentDir?.Parent is null ? "" : imageParentDir.Parent.Name, imageParentDir is null ? "" : (CellCornerTranslateDic.ContainsKey(imageParentDir.Name.ToUpper()) ? CellCornerTranslateDic[imageParentDir.Name.ToUpper()] : "未知角位"), AlgorithmParameters[8].ToString());
            //}

            //// 保存检测结果图和Gap值
            //if (imageParentDir is null)
            //{
            //    if (!Directory.Exists($"{FileSelectInitDir}\\DetectionRes"))
            //        Directory.CreateDirectory($"{FileSelectInitDir}\\DetectionRes");
            //    SaveGapDetectionResultImage($"{FileSelectInitDir}\\DetectionRes\\{fileName}.jpg");
            //    SaveGapToCsv(isError, $"{FileSelectInitDir}\\DetectionRes", $"{fileName}_Gap", "", "");
            //}
            //else
            //{
            //    if (imageParentDir.Parent is null)
            //    {
            //        var saveOKDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\OK\\{imageParentDir.Name}";
            //        var saveNGDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\NG\\{imageParentDir.Name}";
            //        var saveTempDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\Temp\\{imageParentDir.Name}";
            //        if (!Directory.Exists($"{saveTempDir}\\DetectionRes"))
            //            Directory.CreateDirectory($"{saveTempDir}\\DetectionRes");
            //        SaveGapDetectionResultImage($"{saveTempDir}\\DetectionRes\\{fileName}.jpg");
            //        SaveGapToCsv(isError, $"{saveTempDir}\\DetectionRes", $"{fileName}_Gap", "", imageParentDir.Name);
            //        if (isError || !IsNGorOKByNCGap(NCGaps))
            //        {
            //            if (!Directory.Exists(saveNGDir))
            //                Directory.CreateDirectory(saveNGDir);
            //        }
            //        else
            //        {
            //            if (!Directory.Exists(saveOKDir))
            //                Directory.CreateDirectory(saveOKDir);
            //        }
            //        Thread.Sleep(6000);
            //        SortGapDetectionResult(isError, saveTempDir, saveOKDir, saveNGDir);
            //    }
            //    else
            //    {
            //        var saveOKDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\OK\\{imageParentDir.Parent.Name}\\{imageParentDir.Name}";
            //        var saveNGDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\NG\\{imageParentDir.Parent.Name}\\{imageParentDir.Name}";
            //        var saveTempDir = $"{ExportPath}\\{SaveOldTime:yyyy_MM_dd}\\Temp\\{imageParentDir.Parent.Name}\\{imageParentDir.Name}";
            //        if (!Directory.Exists($"{saveTempDir}\\DetectionRes"))
            //            Directory.CreateDirectory($"{saveTempDir}\\DetectionRes");
            //        SaveGapDetectionResultImage($"{saveTempDir}\\DetectionRes\\{fileName}.jpg");
            //        SaveGapToCsv(isError, $"{saveTempDir}\\DetectionRes", $"{fileName}_Gap", imageParentDir.Parent.Name, imageParentDir.Name);
            //        if (isError || !IsNGorOKByNCGap(NCGaps))
            //        {
            //            if (!Directory.Exists(saveNGDir))
            //                Directory.CreateDirectory(saveNGDir);
            //        }
            //        else
            //        {
            //            if (!Directory.Exists(saveOKDir))
            //                Directory.CreateDirectory(saveOKDir);
            //        }
            //        Thread.Sleep(6000);
            //        SortGapDetectionResult(isError, saveTempDir, saveOKDir, saveNGDir);
            //    }
            //}

            //for (int i = 3; i < AlgorithmParameters.Length - 1; i++)
            //{
            //    if (i == 3)
            //    {
            //        ((HObject)AlgorithmParameters[i])?.Dispose();
            //        OriginImage?.Dispose();
            //    }
            //    else
            //    {
            //        List<HObject> areaObjs = AlgorithmParameters[i] as List<HObject>;
            //        foreach (HObject areaObj in areaObjs)
            //        {
            //            areaObj?.Dispose();
            //        }
            //    }
            //}

            //lock (CSVHelper.fileObj)
            //{
            //    CSVHelper.SaveCSVFile(
            //        $"{CurrentProcessDir}\\..\\算法调用信息",
            //        $"{SaveOldTime:yyyy_MM_dd}",
            //        AlgorithmRunInfoHeader,
            //        AlgorithmRunInfoContent.ToString()
            //    );
            //}
        }

        ///// <summary>
        ///// 通过内层Gap评判OK或NG
        ///// </summary>
        ///// <param name="ncGaps"></param>
        ///// <returns></returns>
        //public bool IsNGorOKByNCGap(List<List<double>> ncGaps)
        //{
        //    var result = true;
        //    foreach (var ncGap in ncGaps)
        //    {
        //        result = result && ncGap[0] < Gap1Limit && ncGap[1] < Gap2Limit;
        //    }
        //    return result;
        //}

        ///// <summary>
        ///// 绘制标记和文本
        ///// </summary>
        //public void DrawMarkAndText(string snName, string cellCorner)
        //{
        //    //var Z45Gaps = new List<List<double>>();
        //    //var Z0Gaps = new List<List<double>>();
        //    //var F45Gaps = new List<List<double>>();
        //    //var NCGaps = new List<List<double>>();

        //    Z45Gaps.Clear();
        //    Z0Gaps.Clear();
        //    F45Gaps.Clear();
        //    NCGaps.Clear();

        //    for (int i = 4; i < AlgorithmParameters.Length - 1; i++)
        //    {
        //        List<HObject> areaObjs = AlgorithmParameters[i] as List<HObject>;
        //        foreach (var areaObj in areaObjs)
        //        {
        //            // 获取各区域中心点坐标
        //            HOperatorSet.AreaCenter(areaObj, out HTuple area, out HTuple rows, out HTuple cols);
        //            var jrGaps = new List<double>();
        //            HObject areaObjUsingCTStar = null;
        //            for (int j = 0; j < rows.Length; j++)
        //            {
        //                // 计算Gap值
        //                if (j != rows.Length - 1)
        //                {
        //                    HOperatorSet.DistancePp(rows[rows.Length - j - 1], cols[rows.Length - j - 1], rows[rows.Length - j - 2], cols[rows.Length - j - 2], out HTuple dip);
        //                    //像素与Gap值换算公式：1 *0.033-0.135
        //                    //像素与Gap值换算公式：1 *0.03667579-0.135
        //                    //jrGaps.Add(Math.Abs((double)(dip / 2 * 0.03667579 - 0.135)));
        //                    jrGaps.Add(Math.Abs((double)(dip / 2 * 0.03667579)));
        //                }

        //                if (j == 0)
        //                {
        //                    areaObjUsingCTStar = HImageViewerHelper.GenFillCTStar(rows[j], cols[j], 0);
        //                }
        //                else
        //                {
        //                    // 当是内层Gap时，绘制直线
        //                    if (i == 7)
        //                    {
        //                        if (j == rows.Length - 3)
        //                        {
        //                            HOperatorSet.DistancePp(rows[j + 1], cols[j + 1], rows[j], cols[j], out HTuple dip);
        //                            DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //                            {
        //                                //MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579 - 0.135)) < Gap2Limit ? "green" : "red")}"));
        //                                MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579)) < Gap2Limit ? "green" : "red")}"));
        //                            });
        //                        }
        //                        else if (j == rows.Length - 2)
        //                        {
        //                            HOperatorSet.DistancePp(rows[j + 1], cols[j + 1], rows[j], cols[j], out HTuple dip);
        //                            DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //                            {
        //                                //MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579 - 0.135)) < Gap1Limit ? "green" : "red")}"));
        //                                MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579)) < Gap1Limit ? "green" : "red")}"));
        //                            });
        //                        }
        //                        else if (j == rows.Length - 1)
        //                        {
        //                            HOperatorSet.DistancePp(rows[j], cols[j], rows[j - 1], cols[j - 1], out HTuple dip);
        //                            DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //                            {
        //                                //MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579 - 0.135)) < Gap1Limit ? "green" : "red")}"));
        //                                MarkList.Add(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1, $"{(Math.Abs((double)(dip / 2 * 0.03667579)) < Gap1Limit ? "green" : "red")}"));
        //                            });
        //                        }
        //                        //areaObjUsingCTStar = areaObjUsingCTStar.ConcatObj(HImageViewerHelper.GenFillRectangle(rows[j], cols[j], 0, 20, 1).HObj);
        //                    }
        //                    else
        //                    {
        //                        // 当0度内层Gap时，不绘制标记点
        //                        if (i == 6 && j > rows.Length - 4)
        //                        {
        //                            continue;
        //                        }
        //                        else
        //                        {
        //                            //if (i == 4)
        //                            //{
        //                            //    areaObjUsingCTStar = areaObjUsingCTStar.ConcatObj(HImageViewerHelper.GenFillCTStar(rows[j], cols[j], 0));
        //                            //}
        //                            //else
        //                            //{
        //                            //    // 当±45度内层4个点不绘制标记点
        //                            //    if (j < rows.Length - 4)
        //                            //    {
        //                            //        areaObjUsingCTStar = areaObjUsingCTStar.ConcatObj(HImageViewerHelper.GenFillCTStar(rows[j], cols[j], 0));
        //                            //    }
        //                            //}

        //                            // 当±45度内层4个点不绘制标记点
        //                            if (j < rows.Length - 4)
        //                            {
        //                                areaObjUsingCTStar = areaObjUsingCTStar.ConcatObj(HImageViewerHelper.GenFillCTStar(rows[j], cols[j], 0));
        //                            }
        //                        }
        //                    }
        //                }
        //            }

        //            // Gap值储存
        //            switch (i)
        //            {
        //                case 4:
        //                    Z45Gaps.Add(jrGaps);
        //                    break;
        //                case 5:
        //                    F45Gaps.Add(jrGaps);
        //                    break;
        //                case 6:
        //                    Z0Gaps.Add(jrGaps);
        //                    break;
        //                case 7:
        //                    NCGaps.Add(jrGaps);
        //                    break;
        //            }

        //            DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //            {
        //                MarkList.Add(new CHObject(areaObjUsingCTStar, "green"));
        //            });
        //        }
        //    }

        //    DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //    {
        //        TextList.Add(new CHText($"电芯SN码：{snName}-{cellCorner}", 0, 100, 100, "white"));
        //        TextList.Add(new CHText("检测结果：", 0, 3072, 100, "white"));
        //        var isNGorOK = IsNGorOKByNCGap(NCGaps);
        //        TotalDetectionNum++;
        //        if (isNGorOK)
        //            OKDetectionNum++;
        //        else
        //            NGDetectionNum++;
        //        OKRate = OKDetectionNum * 100.0 / TotalDetectionNum;
        //        TextList.Add(new CHText($"{(isNGorOK ? "OK" : "NG")}", 0, 3672, 100, $"{(isNGorOK ? "green" : "red")}"));

        //        TextList.Add(new CHText($"Gap1标准: <{Gap1Limit:F3} mm\nGap2标准: <{Gap2Limit:F3} mm", 1400, 200, 50, "white"));
        //    });

        //    for (int i = 0; i < Z0Gaps.Count; i++)
        //    {
        //        var maxCount = Math.Max(Z45Gaps[i].Count, Z0Gaps[i].Count);
        //        maxCount = Math.Max(maxCount, F45Gaps[i].Count);
        //        maxCount = Math.Max(maxCount, NCGaps[i].Count);

        //        DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //        {
        //            TextList.Add(new CHText($"JR{i + 1}:", 1570 + i * 170, 200, 50, "white"));
        //            TextList.Add(new CHText($"Gap1值:[{NCGaps[i][0]:F3}]", 1620 + i * 170, 300, 50, $"{(NCGaps[i][0] < Gap1Limit ? "green" : "red")}"));
        //            TextList.Add(new CHText($"Gap2值:[{NCGaps[i][1]:F3}]", 1690 + i * 170, 300, 50, $"{(NCGaps[i][1] < Gap2Limit ? "green" : "red")}"));
        //            TextList.Add(new CHText($"JR{i + 1}:", 1400, 800 + i * 800, 50, "white"));
        //            StringBuilder stringBuilder1 = new StringBuilder();
        //            StringBuilder stringBuilder2 = new StringBuilder();
        //            for (int j = 2; j < maxCount; j++)
        //            {
        //                if (j < maxCount / 2 + 1)
        //                {
        //                    //if (j == 2)
        //                    //{
        //                    //    stringBuilder1.Append($"L{j+1:D2}: {(j < F45Gaps[i].Count ? F45Gaps[i][j] : 0):F3}|{(j < NCGaps[i].Count ? NCGaps[i][j] : 0):F3}|{(j < Z45Gaps[i].Count ? Z45Gaps[i][j] : 0):F3}\n");
        //                    //}
        //                    //else
        //                    //{
        //                    //    stringBuilder1.Append($"L{j+1:D2}: {(j < F45Gaps[i].Count ? F45Gaps[i][j] : 0):F3}|{(j < Z0Gaps[i].Count ? Z0Gaps[i][j - 3] : 0):F3}|{(j < Z45Gaps[i].Count ? Z45Gaps[i][j] : 0):F3}\n");
        //                    //}
        //                    stringBuilder1.Append($"L{j + 1:D2}: {(j < F45Gaps[i].Count ? F45Gaps[i][j] : 0):F3}|{(j < Z0Gaps[i].Count ? Z0Gaps[i][j] : 0):F3}|{(j < Z45Gaps[i].Count ? Z45Gaps[i][j] : 0):F3}\n");
        //                    //TextList.Add(new CHText($"L{j:D2}: 2.422|3.422|3.422", 1470 + (j - 2) * 35, 800 + i * 800, 25, "green"));
        //                }
        //                else
        //                {
        //                    stringBuilder2.Append($"L{j + 1:D2}: {(j < F45Gaps[i].Count ? F45Gaps[i][j] : 0):F3}|{(j < Z0Gaps[i].Count ? Z0Gaps[i][j] : 0):F3}|{(j < Z45Gaps[i].Count ? Z45Gaps[i][j] : 0):F3}\n");
        //                    //TextList.Add(new CHText($"L{j:D2}: 2.422|3.422|3.422", 1470 + (j - (maxCount / 2) - 1) * 35, 1100 + i * 800, 25, "green"));
        //                }
        //            }
        //            TextList.Add(new CHText($"{stringBuilder1}", 1470, 800 + i * 800, 25, "green"));
        //            TextList.Add(new CHText($"{stringBuilder2}", 1470, 1100 + i * 800, 25, "green"));
        //        });
        //    }

        //    UpdatePlot();


        //}

        //public void DrawErrorText(string snName, string cellCorner, string errorInfo)
        //{
        //    DispatcherHelper.UI_Dispatcher.Invoke(() =>
        //    {
        //        TextList.Add(new CHText($"电芯SN码：{snName}-{cellCorner}", 0, 100, 50, "white"));
        //        TextList.Add(new CHText("检测结果：", 0, 1536, 50, "white"));
        //        TextList.Add(new CHText($"NG", 0, 1836, 50, $"red"));
        //        errorInfo = $"成像模糊";
        //        var refatorErrorInfo = new StringBuilder();
        //        if (errorInfo.Length > 50)
        //        {
        //            var num = errorInfo.Length / 50;
        //            for (int i = 0; i < num; i++)
        //            {
        //                refatorErrorInfo.Append(errorInfo.Substring(i * 50, 50));
        //                refatorErrorInfo.Append("\n");
        //            }
        //            if (errorInfo.Length > 50 * num)
        //            {
        //                refatorErrorInfo.Append(errorInfo.Substring(50 * num, errorInfo.Length - 50 * num));
        //            }
        //        }
        //        else
        //        {
        //            refatorErrorInfo.Append(errorInfo);
        //        }
        //        TextList.Add(new CHText($"{refatorErrorInfo}", 75, 100, 50, "red"));
        //    });
        //}

        //public void SaveGapToCsv(bool isError, string filePath, string fileName, string sn, string cellCorner)
        //{
        //    if (!isError)
        //    {
        //        var gapContent = new StringBuilder();
        //        gapContent.Append($"{sn},{cellCorner},");
        //        foreach (var ncGap in NCGaps)
        //        {
        //            gapContent.Append($"{ncGap[0]:F3},{ncGap[1]:F3},");
        //        }
        //        lock (CSVHelper.fileObj)
        //        {
        //            CSVHelper.SaveCSVFile(filePath, fileName, "sn,cellCorner,jr1Gap1,jr1Gap2,jr2Gap1,jr2Gap2,jr3Gap1,jr3Gap2,jr4Gap1,jr4Gap2", gapContent.ToString());
        //        }
        //    }
        //}

        //public void SaveGapDetectionResultImage(string fileName)
        //{
        //    DetectionResultImageCurrentViewer.SaveWindowImage(fileName);
        //}

        //public void SortGapDetectionResult(bool isError, string tempDir, string okDir, string ngDir)
        //{
        //    if (isError || !IsNGorOKByNCGap(NCGaps))
        //    {
        //        FileAndDirHelper.MoveDirToDir(tempDir, ngDir);
        //    }
        //    else
        //    {
        //        FileAndDirHelper.MoveDirToDir(tempDir, okDir);
        //    }
        //}
    }
}
