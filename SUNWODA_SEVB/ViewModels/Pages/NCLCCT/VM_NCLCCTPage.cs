using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CF;
using HalconDotNet;
using HandyControl.Controls;
using HandyControl.Tools.Extension;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using SharpCompress.Common;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.NCLCCT;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Dialogs.NCLCCT;
using TouchSocket.Core;

namespace SUNWODA_SEVB.ViewModels.Pages.NCLCCT
{
    /// <summary>
    /// 南昌华为离线 CT（NCLCCT）。界面流程参照小米 CTNCLX；
    /// 视觉算法反射调用 <c>NJCT.NChwSwdCT</c>（见 <see cref="TryLoadVisionAssembly"/> 等）。
    /// </summary>
    [Module("VM_NCLCCTPage", "NCLCCT")]
    public class VM_NCLCCTPage : ViewModelBase
    {
        private const string HuaweiAlgorithmTypeName = "NJCT.NChwSwdCT";
        private const string DefaultHuaweiDllRelativePath = @"Dlls\NCLCCT\NJCT.dll";
        private const string DefaultHuaweiModelRelativePath = @"model\NCLCCT\model_hw.onnx";
        private const string DefaultCudaRuntimeRelativePath = @"Dlls\CUDA12";

        /// <summary>SysCenter 下载地址（与 CTNCLX / CTAutoSlicer 同网关）。</summary>
        private const string DefaultDllDownloadUrl =
            "http://10.117.243.32:7097/sysCenter/interfaceApi/downloadFile";

        private const string DefaultDllDownloadBackupUrl =
            "http://10.201.88.151:10100/sysCenter/interfaceApi/downloadFile";

        /// <summary>南昌华为 CT 云端公钥（视觉下发）。</summary>
        private const string DefaultHuaweiPublicKey =
            "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQClpDmgPZXQ5wDz6WLSgJaViddxzwRM5PQmS7UD1limrc7YlfUQQkW2XPZ7Lxs1sumPPGxIA+Yq6TS6MyH+qYpYqhRmcqtDgDKexvfuE8+Tl3ORs7G6ckJJIWSfz7peWs27ihr+5bk8p9PIOm02i9Q1ON3uEFmqaPQl4bcmH25khwIDAQAB";

        /// <summary>与云端目录 nc_LiXianCT_HW 对应的 AppKey。</summary>
        private const string DefaultHuaweiAppKey = "nc_lixian_ct_hw";

        /// <summary>云端 DLL 编码（对应 NJCT.dll）。</summary>
        private const string DefaultHuaweiDllPythonCode = "NC_LiXianCT_HW_Dll";

        /// <summary>云端模型编码（对应 390000[hw].onnx）。</summary>
        private const string DefaultHuaweiModelPythonCode = "NC_LiXianCT_HW_Model";

        private string? _visionDllDirectory;
        private bool _visionResolveHooked;
        private bool _cudaRuntimePathReady;
        private bool _halconNativePathReady;
        private readonly ILoggerService<VM_NCLCCTPage> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
        private readonly HashSet<string> exts = new() { ".tif", ".png", ".jpg" };
        /// <summary>
        /// 监控文件夹仅处理文件名（不含扩展名）以该后缀结尾的图片，例如 manualA..._2-R_origin.png
        /// </summary>
        private const string MonitorImageNameSuffix = "R_origin";
        private readonly HttpClient _httpClient = new HttpClient();

        //private readonly HttpHelper _httpHelper;

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
        private double z0NeToAnRatioSecondLapLowerLimit;
        private double z0NeToAnRatioThreeToSixLapLowerLimit;
        private double pixPercision = 0.0135;
        /// <summary>像素精度 mm/px；实际值 = 像素数×像素精度 − 分组常量。</summary>
        private double huaweiPxToMmScale = 0.01;
        /// <summary>Gap1/2/3/10/11 固定减量 mm。</summary>
        private const double HuaweiGapOffsetAMm = 0.12;
        /// <summary>Gap4/5/6/7/8/9 固定减量 mm（与 A 组同为 0.12）。</summary>
        private const double HuaweiGapOffsetBMm = 0.12;
        /// <summary>规范3：Gap1~3 上限，默认 0.055。</summary>
        private double huaweiNorm3UpperMm = 0.055;
        /// <summary>规范2：Gap4~9 单值下限，默认 0.020。</summary>
        private double huaweiNorm2LowerMm = 0.020;
        /// <summary>规范2：Gap4~9 上限，默认 0.061。</summary>
        private double huaweiNorm2MeanUpperMm = 0.061;
        /// <summary>规范1：Gap10 上限，默认 0.200。</summary>
        private double huaweiNorm1UpperMm = 0.200;
        private ObservableCollection<DetectionItemModel>? detectionItems;
        private ObservableCollection<DetectionInfoModel>? detectionInfoList;

        private List<double>? _detectionSattisticalData;
        private List<string>? _detectionSattisticalLabel;
        private List<string>? _detectionStatisticalLabelColor;

        private ImageSource? _lastProcessedImage;

        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private readonly ConcurrentDictionary<string, byte> _processingFiles = new();

        private bool isInitCompleted = false;

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
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                        "VM_NCLCCTPage",
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
                        "VM_NCLCCTPage",
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
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref innerFiveMeanLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 0°方向负凸到正凹比值第2圈规格下限
        /// </summary>
        public double Z0NeToAnRatioSecondLapLowerLimit
        {
            get => z0NeToAnRatioSecondLapLowerLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "Z0NeToAnRatioSecondLapLowerLimit",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref z0NeToAnRatioSecondLapLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 0°方向负凸到正凹比值第3圈到第6圈规格下限
        /// </summary>
        public double Z0NeToAnRatioThreeToSixLapLowerLimit
        {
            get => z0NeToAnRatioThreeToSixLapLowerLimit;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "Z0NeToAnRatioThreeToSixLapLowerLimit",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref z0NeToAnRatioThreeToSixLapLowerLimit, roundValue);
            }
        }

        /// <summary>
        /// 像素精度（遗留字段；华为换算改用 HuaweiPxToMmScale）
        /// </summary>
        public double PixPercision
        {
            get => pixPercision;
            set
            {
                var roundValue = Math.Round(value, 4);
                _projectSettingRepository.UpdateSettingValue(
                    "PixPercision",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref pixPercision, roundValue);
            }
        }

        /// <summary>华为：像素精度 mm/px（实际值=像素数×精度−分组常量）。</summary>
        public double HuaweiPxToMmScale
        {
            get => huaweiPxToMmScale;
            set
            {
                var roundValue = Math.Round(value, 4);
                _projectSettingRepository.UpdateSettingValue(
                    "HuaweiPxToMmScale",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref huaweiPxToMmScale, roundValue);
            }
        }

        /// <summary>规范3：Gap1~3 上限 mm。</summary>
        public double HuaweiNorm3UpperMm
        {
            get => huaweiNorm3UpperMm;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "HuaweiNorm3UpperMm",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref huaweiNorm3UpperMm, roundValue);
            }
        }

        /// <summary>规范2：Gap4~9 单值下限 mm。</summary>
        public double HuaweiNorm2LowerMm
        {
            get => huaweiNorm2LowerMm;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "HuaweiNorm2LowerMm",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref huaweiNorm2LowerMm, roundValue);
            }
        }

        /// <summary>规范2：Gap4~9 上限 mm（与下限组成 0.02≤Gap≤0.061）。</summary>
        public double HuaweiNorm2MeanUpperMm
        {
            get => huaweiNorm2MeanUpperMm;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "HuaweiNorm2MeanUpperMm",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref huaweiNorm2MeanUpperMm, roundValue);
            }
        }

        /// <summary>规范1：Gap10 上限 mm。</summary>
        public double HuaweiNorm1UpperMm
        {
            get => huaweiNorm1UpperMm;
            set
            {
                var roundValue = Math.Round(value, 3);
                _projectSettingRepository.UpdateSettingValue(
                    "HuaweiNorm1UpperMm",
                    "VM_NCLCCTPage",
                    roundValue
                );
                SetProperty(ref huaweiNorm1UpperMm, roundValue);
            }
        }

        /// <summary>
        /// 检测功能清单
        /// </summary>
        public ObservableCollection<DetectionItemModel>? DetectionItems
        {
            get => detectionItems;
            set => SetProperty(ref detectionItems, value);
        }

        /// <summary>
        /// 检测统计信息列表
        /// </summary>
        public ObservableCollection<DetectionInfoModel>? DetectionInfoList
        {
            get => detectionInfoList;
            set => SetProperty(ref detectionInfoList, value);
        }

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
        /// 后台是否初始化完成
        /// </summary>
        public bool IsInitCompleted
        {
            get => isInitCompleted;
            set => SetProperty(ref isInitCompleted, value);
        }

        /// <summary>
        /// CT检测算法实例
        /// </summary>
        public object? CTDetectionAlgorithmIstance { get; set; }

        //public NJSwdCT? CTDetectionAlgorithmIstance { get; set; }

        public MethodInfo? ModelActionMethod { get; set; }

        public MethodInfo? ModelInitMethod { get; set; }

        public object[]? AlgorithmParameters { get; set; }
        public object[]? ModelInitParameters { get; set; }

        public string? ImagePath { get; set; }

        public int BottomBlackHeight { get; set; }

        public HObject? ResultImage { get; set; }

        public List<HObject>? AllPointRegion { get; set; }

        public List<HTuple>? AllRegionRow { get; set; }

        public List<HTuple>? AllRegionCol { get; set; }

        public List<List<HTuple>>? LingDuNei5BiZhi { get; set; }

        public List<HTuple>? AllJianXiValue { get; set; }

        public List<HTuple>? AllPrintfValue { get; set; }

        public string? RunInfo { get; set; }

        /// <summary>
        /// 模型路径（南昌华为 ONNX）。
        /// </summary>
        public string? ModelPath { get; set; } = DefaultHuaweiModelRelativePath;

        /// <summary>
        /// 模型句柄。华为 DLL 签名为 <c>CF.IPredictor</c>，以 object 持有，避免与宿主 CF 程序集身份冲突时强转失败。
        /// </summary>
        public object? ModelHandle { get; set; }

        public string? ModelInitErrorInfo { get; set; }

        public bool IsRunCuda { get; set; } = true;

        /// <summary>远端下载地址；本机有 Dlls\NCLCCT\NJCT.dll 时优先本地，否则走云端。</summary>
        public string? NCLCCTDLLDownloadUrl { get; set; } = DefaultDllDownloadUrl;

        /// <summary>远端备用下载地址。</summary>
        public string? NCLCCTDLLDownloadBackupUrl { get; set; } = DefaultDllDownloadBackupUrl;

        /// <summary>算法 DLL 相对输出目录路径。</summary>
        public string? AlgorithmDllName { get; set; } = DefaultHuaweiDllRelativePath;

        /// <summary>反射类型全名。</summary>
        public string? AlgorithmTypeName { get; set; } = HuaweiAlgorithmTypeName;

        public string? PublicKey { get; set; } = DefaultHuaweiPublicKey;

        public string? AppKey { get; set; } = DefaultHuaweiAppKey;

        public string? DllPythonCode { get; set; } = DefaultHuaweiDllPythonCode;

        public string? ModelPythonCode { get; set; } = DefaultHuaweiModelPythonCode;

        /// <summary>华为推理输出的抓点区域。</summary>
        public List<HObject>? HuaweiObjRegions { get; set; }

        /// <summary>华为推理输出的对齐/区域行坐标。</summary>
        public List<HTuple>? HuaweiAlignRows { get; set; }

        /// <summary>华为推理输出的对齐/区域列坐标。</summary>
        public List<HTuple>? HuaweiAlignCols { get; set; }

        /// <summary>华为推理输出的间隙测量值。</summary>
        public List<HTuple>? HuaweiGapValues { get; set; }

        /// <summary>华为推理输出的打印/标注值。</summary>
        public List<HTuple>? HuaweiPrintValues { get; set; }

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
        private ICommand? _monitorPathSelectCommand;
        public ICommand? MonitorPathSelectCommand
        {
            get => _monitorPathSelectCommand;
            set => SetProperty(ref _monitorPathSelectCommand, value);
        }

        private ICommand? _fileSelectCommand;
        public ICommand? FileSelectCommand
        {
            get => _fileSelectCommand;
            set => SetProperty(ref _fileSelectCommand, value);
        }

        public ICommand? _dirSelectCommand;
        public ICommand? DirSelectCommand
        {
            get => _dirSelectCommand;
            set => SetProperty(ref _dirSelectCommand, value);
        }

        private ICommand? _exportPathSelectCommand;
        public ICommand? ExportPathSelectCommand
        {
            get => _exportPathSelectCommand;
            set => SetProperty(ref _exportPathSelectCommand, value);
        }

        private ICommand? _clearDetectionInfoCommand;
        public ICommand? ClearDetectionInfoCommand
        {
            get => _clearDetectionInfoCommand;
            set => SetProperty(ref _clearDetectionInfoCommand, value);
        }
        #endregion

        public VM_NCLCCTPage(
            ILoggerService<VM_NCLCCTPage> logger,
            IProjectSettingRepository projectSettingRepository
        )
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            //_httpHelper = new HttpHelper();
        }

        /// <summary>
        /// 加载或初始化华为 Gap 相关设置（首次写入字段默认值，避免界面全 0）。
        /// </summary>
        private void EnsureHuaweiGapSetting(
            string name,
            Func<double> getDefault,
            Action<double> setValue,
            string remark
        )
        {
            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == name && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                var def = getDefault();
                _projectSettingRepository.Add(
                    new ProjectSettingModel(name, "VM_NCLCCTPage", $"{def}", "float", 1)
                    {
                        Remark = remark,
                    }
                );
            }
            else
            {
                var loaded = _projectSettingRepository.GetSettingValue(name, "VM_NCLCCTPage");
                setValue((double)loaded);
            }
        }

        //public override void OnInitialize()
        protected override async Task OnInitializeAsync()
        {
            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsContinuousDetection"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsContinuousDetection",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                IsContinuousDetection = _isContinuousDetection;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"OKDetectionNum"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"OKDetectionNum",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                OKDetectionNum = _OKDetectionNum;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"NGDetectionNum"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"NGDetectionNum",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
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
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"FileSelectInitDir",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                FileSelectInitDir = _fileSelectInitDir;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ImageMonitorPath"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ImageMonitorPath",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                ImageMonitorPath = _imageMonitorPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ExportPath" && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ExportPath",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                ExportPath = _exportPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsSaveAIEnhanceResult"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsSaveAIEnhanceResult",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                IsSaveAIEnhanceResult = _isSaveAIEnhanceResult;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerGapUpperLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerGapUpperLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                InnerGapUpperLimit = _innerGapUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerGapLowerLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerGapLowerLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                InnerGapLowerLimit = _innerGapLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerFiveMeanMeanUpperLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                InnerFiveMeanMeanUpperLimit = _innerFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"MiddleFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"MiddleFiveMeanMeanUpperLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                MiddleFiveMeanMeanUpperLimit = _middleFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"OutsideFiveMeanMeanUpperLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"OutsideFiveMeanMeanUpperLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                OutsideFiveMeanMeanUpperLimit = _outsideFiveMeanMeanUpperLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"JRGapMeanLowerLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"JRGapMeanLowerLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                JRGapMeanLowerLimit = _jrGapMeanLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"InnerFiveMeanLowerLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"InnerFiveMeanLowerLimit",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                InnerFiveMeanLowerLimit = _innerFiveMeanLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"Z0NeToAnRatioSecondLapLowerLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"Z0NeToAnRatioSecondLapLowerLimit",
                        "VM_NCLCCTPage",
                        $"{Z0NeToAnRatioSecondLapLowerLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"0度方向负凸到正凹比值第2圈规格下限",
                    }
                );
            }
            else
            {
                var _z0NeToAnRatioSecondLapLowerLimit = _projectSettingRepository.GetSettingValue(
                    $"Z0NeToAnRatioSecondLapLowerLimit",
                    "VM_NCLCCTPage"
                );
                Z0NeToAnRatioSecondLapLowerLimit = _z0NeToAnRatioSecondLapLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"Z0NeToAnRatioThreeToSixLapLowerLimit"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"Z0NeToAnRatioThreeToSixLapLowerLimit",
                        "VM_NCLCCTPage",
                        $"{Z0NeToAnRatioThreeToSixLapLowerLimit}",
                        "float",
                        1
                    )
                    {
                        Remark = $"0度方向负凸到正凹比值第3圈到第6圈规格下限",
                    }
                );
            }
            else
            {
                var _z0NeToAnRatioThreeToSixLapLowerLimit =
                    _projectSettingRepository.GetSettingValue(
                        $"Z0NeToAnRatioThreeToSixLapLowerLimit",
                        "VM_NCLCCTPage"
                    );
                Z0NeToAnRatioThreeToSixLapLowerLimit = _z0NeToAnRatioThreeToSixLapLowerLimit;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"PixPercision"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"PixPercision",
                        "VM_NCLCCTPage",
                        $"{PixPercision}",
                        "float",
                        1
                    )
                    {
                        Remark = $"像素精度",
                    }
                );
            }
            else
            {
                var _pixPercision = _projectSettingRepository.GetSettingValue(
                    $"PixPercision",
                    "VM_NCLCCTPage"
                );
                PixPercision = _pixPercision;
            }

            EnsureHuaweiGapSetting(
                "HuaweiPxToMmScale",
                () => HuaweiPxToMmScale,
                v => HuaweiPxToMmScale = v,
                "华为像素精度 mm/px"
            );
            EnsureHuaweiGapSetting(
                "HuaweiNorm3UpperMm",
                () => HuaweiNorm3UpperMm,
                v => HuaweiNorm3UpperMm = v,
                "规范3 Gap1~3上限"
            );
            EnsureHuaweiGapSetting(
                "HuaweiNorm2LowerMm",
                () => HuaweiNorm2LowerMm,
                v => HuaweiNorm2LowerMm = v,
                "规范2 Gap4~9单值下限"
            );
            EnsureHuaweiGapSetting(
                "HuaweiNorm2MeanUpperMm",
                () => HuaweiNorm2MeanUpperMm,
                v => HuaweiNorm2MeanUpperMm = v,
                "规范2 Gap4~9上限"
            );
            EnsureHuaweiGapSetting(
                "HuaweiNorm1UpperMm",
                () => HuaweiNorm1UpperMm,
                v => HuaweiNorm1UpperMm = v,
                "规范1 Gap10上限"
            );

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ModelPath" && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ModelPath",
                        "VM_NCLCCTPage",
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
                    "VM_NCLCCTPage"
                );
                ModelPath = _modelPath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"IsRunCuda" && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"IsRunCuda",
                        "VM_NCLCCTPage",
                        $"{IsRunCuda}",
                        "bool",
                        1
                    )
                    {
                        Remark = $"是否启用Cuda",
                    }
                );
            }
            else
            {
                var _isRunCuda = _projectSettingRepository.GetSettingValue(
                    $"IsRunCuda",
                    "VM_NCLCCTPage"
                );
                IsRunCuda = _isRunCuda;
            }
            // 本机按 GPU 部署，禁止被历史配置改成 CPU
            if (!IsRunCuda)
            {
                IsRunCuda = true;
                _projectSettingRepository.UpdateSettingValue(
                    "IsRunCuda",
                    "VM_NCLCCTPage",
                    true
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"NCLCCTDLLDownloadUrl"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"NCLCCTDLLDownloadUrl",
                        "VM_NCLCCTPage",
                        $"{NCLCCTDLLDownloadUrl}",
                        "string",
                        1
                    )
                    {
                        Remark = $"dll下载url",
                    }
                );
            }
            else
            {
                var _ctNCLXDLLDownloadUrl = _projectSettingRepository.GetSettingValue(
                    $"NCLCCTDLLDownloadUrl",
                    "VM_NCLCCTPage"
                );
                NCLCCTDLLDownloadUrl = _ctNCLXDLLDownloadUrl;
            }
            if (string.IsNullOrWhiteSpace(NCLCCTDLLDownloadUrl))
            {
                NCLCCTDLLDownloadUrl = DefaultDllDownloadUrl;
                _projectSettingRepository.UpdateSettingValue(
                    "NCLCCTDLLDownloadUrl",
                    "VM_NCLCCTPage",
                    NCLCCTDLLDownloadUrl
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"NCLCCTDLLDownloadBackupUrl"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"NCLCCTDLLDownloadBackupUrl",
                        "VM_NCLCCTPage",
                        $"{NCLCCTDLLDownloadBackupUrl}",
                        "string",
                        1
                    )
                    {
                        Remark = $"dll下载备用url",
                    }
                );
            }
            else
            {
                var backupUrl = _projectSettingRepository.GetSettingValue(
                    $"NCLCCTDLLDownloadBackupUrl",
                    "VM_NCLCCTPage"
                );
                NCLCCTDLLDownloadBackupUrl = backupUrl;
            }
            if (string.IsNullOrWhiteSpace(NCLCCTDLLDownloadBackupUrl))
            {
                NCLCCTDLLDownloadBackupUrl = DefaultDllDownloadBackupUrl;
                _projectSettingRepository.UpdateSettingValue(
                    "NCLCCTDLLDownloadBackupUrl",
                    "VM_NCLCCTPage",
                    NCLCCTDLLDownloadBackupUrl
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"AlgorithmDllName"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"AlgorithmDllName",
                        "VM_NCLCCTPage",
                        $"{AlgorithmDllName}",
                        "string",
                        1
                    )
                    {
                        Remark = $"算法dll名称",
                    }
                );
            }
            else
            {
                var _algorithmDllName = _projectSettingRepository.GetSettingValue(
                    $"AlgorithmDllName",
                    "VM_NCLCCTPage"
                );
                AlgorithmDllName = _algorithmDllName;
            }
            if (string.IsNullOrWhiteSpace(AlgorithmDllName) || AlgorithmDllName == "NJCT.dll")
            {
                AlgorithmDllName = DefaultHuaweiDllRelativePath;
                _projectSettingRepository.UpdateSettingValue(
                    "AlgorithmDllName",
                    "VM_NCLCCTPage",
                    AlgorithmDllName
                );
            }
            if (string.IsNullOrWhiteSpace(AlgorithmTypeName))
            {
                AlgorithmTypeName = HuaweiAlgorithmTypeName;
            }
            if (
                string.IsNullOrWhiteSpace(ModelPath)
                || ModelPath.Contains("NCLXCT", StringComparison.OrdinalIgnoreCase)
                || ModelPath.EndsWith("model.onnx", StringComparison.OrdinalIgnoreCase)
            )
            {
                ModelPath = DefaultHuaweiModelRelativePath;
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"PublicKey" && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"PublicKey",
                        "VM_NCLCCTPage",
                        $"{PublicKey}",
                        "string",
                        1
                    )
                    {
                        Remark = $"下载公钥",
                    }
                );
            }
            else
            {
                var _publicKey = _projectSettingRepository.GetSettingValue(
                    $"PublicKey",
                    "VM_NCLCCTPage"
                );
                PublicKey = _publicKey;
            }
            if (string.IsNullOrWhiteSpace(PublicKey) || PublicKey != DefaultHuaweiPublicKey)
            {
                PublicKey = DefaultHuaweiPublicKey;
                _projectSettingRepository.UpdateSettingValue(
                    "PublicKey",
                    "VM_NCLCCTPage",
                    PublicKey
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"AppKey" && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"AppKey",
                        "VM_NCLCCTPage",
                        $"{AppKey}",
                        "string",
                        1
                    )
                    {
                        Remark = $"应用密钥",
                    }
                );
            }
            else
            {
                var _appKey = _projectSettingRepository.GetSettingValue(
                    $"AppKey",
                    "VM_NCLCCTPage"
                );
                AppKey = _appKey;
            }
            if (string.IsNullOrWhiteSpace(AppKey) || AppKey != DefaultHuaweiAppKey)
            {
                AppKey = DefaultHuaweiAppKey;
                _projectSettingRepository.UpdateSettingValue(
                    "AppKey",
                    "VM_NCLCCTPage",
                    AppKey
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"DllPythonCode"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"DllPythonCode",
                        "VM_NCLCCTPage",
                        $"{DllPythonCode}",
                        "string",
                        1
                    )
                    {
                        Remark = $"dll下载码",
                    }
                );
            }
            else
            {
                var _dllPythonCode = _projectSettingRepository.GetSettingValue(
                    $"DllPythonCode",
                    "VM_NCLCCTPage"
                );
                DllPythonCode = _dllPythonCode;
            }
            if (
                string.IsNullOrWhiteSpace(DllPythonCode)
                || DllPythonCode != DefaultHuaweiDllPythonCode
            )
            {
                DllPythonCode = DefaultHuaweiDllPythonCode;
                _projectSettingRepository.UpdateSettingValue(
                    "DllPythonCode",
                    "VM_NCLCCTPage",
                    DllPythonCode
                );
            }

            if (
                !_projectSettingRepository.Exists(model =>
                    model.Name == $"ModelPythonCode"
                    && model.BelongToVM == "VM_NCLCCTPage"
                )
            )
            {
                _projectSettingRepository.Add(
                    new ProjectSettingModel(
                        $"ModelPythonCode",
                        "VM_NCLCCTPage",
                        $"{ModelPythonCode}",
                        "string",
                        1
                    )
                    {
                        Remark = $"model下载码",
                    }
                );
            }
            else
            {
                var _modelPythonCode = _projectSettingRepository.GetSettingValue(
                    $"ModelPythonCode",
                    "VM_NCLCCTPage"
                );
                ModelPythonCode = _modelPythonCode;
            }
            if (
                string.IsNullOrWhiteSpace(ModelPythonCode)
                || ModelPythonCode != DefaultHuaweiModelPythonCode
            )
            {
                ModelPythonCode = DefaultHuaweiModelPythonCode;
                _projectSettingRepository.UpdateSettingValue(
                    "ModelPythonCode",
                    "VM_NCLCCTPage",
                    ModelPythonCode
                );
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

            DirSelectCommand = new RelayCommand(DirSelect);

            ExportPathSelectCommand = new RelayCommand(ExportPathSelect);

            ClearDetectionInfoCommand = new RelayCommand(ClearDetectionInfo);

            // 南昌华为视觉依赖 Halcon 原生库：halcon.dll 在输出目录 Dlls\，须先加入 DLL 搜索路径
            // （否则 P/Invoke LoadLibrary("halcon") 只搜 exe 目录会报 0x8007007E）
            EnsureHalconNativeRuntimePath();
            // NJCT 内部硬编码 OpenCV imwrite("D:\\TEST.png")；不可写时会被吞成「当前图像暂不支持检测」
            EnsureNjctTempPngWritable();

            // 南昌华为视觉：NJCT.NChwSwdCT
            // 加载策略：本地 Dlls\NCLCCT\NJCT.dll 优先；不存在则 RSA 签名请求 SysCenter 云端
            // ModelInit(string, ref IPredictor, ref string, bool)
            // ModelAction 9 参（新 DLL 已去掉 AllJianXiValue）
            _logger.Info("开始加载南昌华为视觉 DLL / 模型");
            var assembly = TryLoadVisionAssembly(
                AlgorithmDllName,
                PublicKey,
                AppKey,
                DllPythonCode
            );
            if (assembly == null)
            {
                _logger.Info("算法 dll 加载失败，启用备用 url 重试");
                NCLCCTDLLDownloadUrl = NCLCCTDLLDownloadBackupUrl;
                assembly = TryLoadVisionAssembly(
                    AlgorithmDllName,
                    PublicKey,
                    AppKey,
                    DllPythonCode
                );
            }
            if (assembly != null)
            {
                TryInitVisionModel(assembly);
            }
            else
            {
                Growl.WarningGlobal("南昌华为视觉 DLL 未加载，检测将无法推理");
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

                            try
                            {
                                GapDetection(imagePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("检测错误", ex);
                            }
                            finally
                            {
                                if (!string.IsNullOrEmpty(imagePath))
                                {
                                    _processingFiles.TryRemove(imagePath, out _);
                                }

                                _semaphore.Release();
                            }

                        }
                        Thread.Sleep(1000);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("检测线程错误", ex);
                        _semaphore.Release();
                    }
                }
            });
            GapDetectionThread.IsBackground = true;
            GapDetectionThread.Start();

            IsInitCompleted = true;
            //base.OnInitialize();
            await base.OnInitializeAsync();
        }

        private async void MonitorPathSelect()
        {
            if (IsContinuousDetection)
            {
                await Dialog
                    .Show(new VM_NCLCCTMonitorPathSelectDialog().UIElement)
                    .Initialize<VM_NCLCCTMonitorPathSelectDialog>(vm => vm.Result = ImageMonitorPath)
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

                        if (!IsMonitorTargetImageName(filePath))
                        {
                            _logger.Info($"非 R_origin 图片，跳过：{filePath}");
                            return;
                        }

                        if (!_processingFiles.TryAdd(filePath, 0))
                            return;

                        using Mat im = Cv2.ImRead(filePath);

                        if (im.Empty())
                        {
                            _processingFiles.TryRemove(filePath, out _);
                            _logger.Info($"图片读取为空：{filePath}");
                            return;
                        }

                        // 不做尺寸限制，仅按文件名 R_origin 过滤
                            ImagePathQueue!.Enqueue(filePath);
                        _logger.Info($"图片入队：{filePath}（{im.Width}*{im.Height}）");
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

        private void DirSelect()
        {
            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = $"导入文件夹选择";
            folderDialog.InitialDirectory = FileSelectInitDir;
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                Task.Run(() =>
                {
                    try
                    {
                        _semaphore.Wait();
                        var files = Directory
                            .EnumerateFiles(
                                folderDialog.FolderName,
                                "*.*",
                                SearchOption.TopDirectoryOnly
                            )
                            .Where(file =>
                                exts.Contains(Path.GetExtension(file).ToLower())
                                && IsMonitorTargetImageName(file)
                            );

                        foreach (var filePath in files)
                        {
                            if (!_processingFiles.TryAdd(filePath, 0))
                                continue;

                            using Mat im = Cv2.ImRead(filePath);

                            if (im.Empty())
                            {
                                _processingFiles.TryRemove(filePath, out _);
                                _logger.Info($"图片读取为空：{filePath}");
                                continue;
                            }

                            // 不做尺寸限制，仅按文件名 R_origin 过滤
                                ImagePathQueue!.Enqueue(filePath);
                            _logger.Info($"图片入队：{filePath}（{im.Width}*{im.Height}）");
                        }
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
            ImageFileSystemWatcher.Renamed += OnRenamed;
            ImageFileSystemWatcher.Error += OnError;
            // 仅通知 *R_origin.* 文件名；HandleFile 内再校验扩展名与精确后缀
            ImageFileSystemWatcher.Filter = $"*{MonitorImageNameSuffix}.*";
            // 监控子目录的变化
            ImageFileSystemWatcher.IncludeSubdirectories = true;
        }

        /// <summary>
        /// 是否为监控目标图：无扩展名部分以 R_origin 结尾（如 xxx-R_origin.png）
        /// </summary>
        private static bool IsMonitorTargetImageName(string filePath)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            return nameWithoutExt.EndsWith(
                MonitorImageNameSuffix,
                StringComparison.OrdinalIgnoreCase
            );
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _logger.Error("监控文件夹出现错误", e.GetException());
            Growl.ErrorGlobal($"监控文件夹出现错误：{e.GetException()}");
            ;
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

        //private void OnCreated(object source, FileSystemEventArgs e)
        //{
        //    try
        //    {
        //        if (File.Exists(e.FullPath))
        //        {
        //            if (exts.Contains(Path.GetExtension(e.FullPath).ToLower()))
        //            {
        //                Thread.Sleep(100);
        //                //HOperatorSet.ReadImage(out HObject aaimg, e.FullPath);
        //                //HOperatorSet.GetImageSize(aaimg, out HTuple width, out HTuple height);
        //                //if (width.D == 1950 && height.D <= 1000)
        //                Mat im = Cv2.ImRead(e.FullPath);
        //                if (im.Width == 1950 && im.Height <= 1000)
        //                {
        //                    ImagePathQueue!.Enqueue(e.FullPath);
        //                    _logger.Info($"图片入队：{e.FullPath}");
        //                }
        //                //aaimg.Dispose();
        //                //width.Dispose();
        //                //height.Dispose();
        //                im.Dispose();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.Error($"图片 {e.FullPath} 入队异常", ex);
        //    }
        //}

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            HandleFile(e.FullPath);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            HandleFile(e.FullPath);
        }

        private void HandleFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.Info($"图片不存在：{filePath}");
                    return;
                }

                // 监控仅处理 *R_origin.*（如 manualA20260911101229_2-R_origin.png）
                if (!IsMonitorTargetImageName(filePath))
                {
                    _logger.Info($"非 R_origin 图片，跳过监控：{filePath}");
                    return;
                }

                string ext = Path.GetExtension(filePath).ToLower();

                if (!exts.Contains(ext))
                {
                    _logger.Info($"图片格式不支持：{filePath}");
                    return;
                }

                if (!_processingFiles.TryAdd(filePath, 0))
                    return;

                Thread.Sleep(100);

                if (!WaitFileReady(filePath))
                {
                    _processingFiles.TryRemove(filePath, out _);
                    _logger.Info($"图片未写入完成：{filePath}");
                    return;
                }

                using Mat im = Cv2.ImRead(filePath);

                if (im.Empty())
                {
                    _processingFiles.TryRemove(filePath, out _);
                    _logger.Info($"图片读取为空：{filePath}");
                    return;
                }

                // 不做尺寸限制，仅按文件名 R_origin 过滤（上方已校验）
                    ImagePathQueue!.Enqueue(filePath);
                _logger.Info($"图片入队：{filePath}（{im.Width}*{im.Height}）");
            }
            catch (Exception ex)
            {
                _processingFiles.TryRemove(filePath, out _);

                _logger.Error($"图片 {filePath} 入队异常", ex);
            }
        }

        private bool WaitFileReady(string path)
        {
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    using FileStream fs = File.Open(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.None
                    );

                    return true;
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }

            return false;
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
        /// <param name="z0NeToAnRatio">正0°方向负凸到正凹比值</param>
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
            List<List<(double row, double col)>> z0NeToAnLayerPositions,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            List<List<(double ratio, bool isOK)>> z0NeToAnRatio,
            string resultCode,
            string? runInfo,
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

                    DrawPointMarkers(z0NeToAnLayerPositions, z0NeToAnRatio, tempWindow);
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
                    double textCurentRow = imgHeight - BottomBlackHeight;
                    DrawTextInfo(
                        InnerGapLowerLimit,
                        InnerGapUpperLimit,
                        InnerFiveMeanMeanUpperLimit,
                        MiddleFiveMeanMeanUpperLimit,
                        OutsideFiveMeanMeanUpperLimit,
                        JRGapMeanLowerLimit,
                        InnerFiveMeanLowerLimit,
                        Z0NeToAnRatioSecondLapLowerLimit,
                        Z0NeToAnRatioThreeToSixLapLowerLimit,
                        innerGap,
                        z45MeanGap,
                        z0MeanGap,
                        f45MeanGap,
                        z0NeToAnRatio,
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
            hWindow.SetFont("Times New Roman-70");
            hWindow.SetColor("white");
            string headerText = $"\t\t电芯条码:{barcode}";
            var len = headerText.Length;
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");
            hWindow.DispText("\t\t检测结果:", "window", 90, 0, "white", "box", "false");

            // 行 0，列 x：结果值（OK=绿色 / NG=红色）
            bool isOk = resultCode.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
            string resColor = isOk ? "green" : "red";
            hWindow.SetColor(resColor);
            hWindow.DispText(resultCode, "window", 100, 400, resColor, "box", "false");
        }

        private void DrawTopErrorInfo(string barcode, string errorInfo, HWindow hWindow)
        {
            // 行 0，列 0：白色主文字（条码 + 视野编号 + 结果标签）
            hWindow.SetFont("Times New Roman-70");
            hWindow.SetColor("white");
            string headerText = $"\t\t电芯条码:{barcode}";
            hWindow.DispText(headerText, "window", 0, 0, "white", "box", "false");
            hWindow.DispText("\t\t错误信息:", "window", 90, 0, "white", "box", "false");

            var refatorErrorInfo = new StringBuilder();
            if (errorInfo.Length > 180)
            {
                var num = errorInfo.Length / 180;
                for (int i = 0; i < num; i++)
                {
                    refatorErrorInfo.Append(errorInfo.Substring(i * 180, 180));
                    refatorErrorInfo.Append("\n");
                }
                if (errorInfo.Length > 180 * num)
                {
                    refatorErrorInfo.Append(
                        errorInfo.Substring(180 * num, errorInfo.Length - 180 * num)
                    );
                }
            }
            else
            {
                refatorErrorInfo.Append(errorInfo);
            }

            hWindow.SetFont("Times New Roman-25");
            // 行 300，列 0：错误信息
            hWindow.SetColor("red");
            hWindow.DispText($"{refatorErrorInfo}", "window", 170, 70, "red", "box", "false");
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
                            0,
                            3
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

        private void DrawPointMarkers(
            List<List<(double row, double col)>> points,
            List<List<(double ratio, bool isOK)>> z0NeToAnRatio,
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
                            0,
                            3
                        );

                        if (!z0NeToAnRatio[i][j].isOK)
                        {
                            HOperatorSet.ConcatObj(abnormalMark, star, out HObject merged);
                            abnormalMark?.Dispose();
                            abnormalMark = merged;
                            continue;
                        }

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
                            2
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
                            2
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
                            2
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
                            2
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
                            2
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
                            2
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
            double z0NeToAnRatioSecondLapLowerLimit,
            double z0NeToAnRatioThreeToSixLapLowerLimit,
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            List<List<(double ratio, bool isOK)>> z0NeToAnRatio,
            double textInitRow,
            HWindow hWindow
        )
        {
            hWindow.SetFont("Times New Roman-30");

            const double titleX = 100; // 标签列 X 起始
            const double valueX = 500; // 数值列 X
            const double lineHeight = 50; // 行间距（像素）
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

            textCurrentRow = textCurrentRow + lineHeight;

            DrawLabelValue(
                hWindow,
                textCurrentRow,
                titleX,
                "0°方向负凸到正凹比值规格:",
                "orange",
                $"第2圈 ≥ {z0NeToAnRatioSecondLapLowerLimit:F3} |  第3~6圈 ≥ {z0NeToAnRatioThreeToSixLapLowerLimit:F3}",
                "green",
                valueX
            );

            textCurrentRow = textCurrentRow + 2 * lineHeight;

            hWindow.SetColor("orange");
            hWindow.DispText(
                "JR1|JR2内层Gap值:",
                "window",
                textCurrentRow,
                titleX,
                "orange",
                "box",
                "false"
            );
            hWindow.SetColor(innerGap[0].isOK ? "green" : "red");
            hWindow.DispText(
                $"{innerGap[0].gap:F3}",
                "window",
                textCurrentRow,
                valueX,
                innerGap[0].isOK ? "green" : "red",
                "box",
                "false"
            );
            hWindow.SetColor("green");
            hWindow.DispText($"|", "window", textCurrentRow, valueX + 90, "green", "box", "false");
            hWindow.SetColor(innerGap[1].isOK ? "green" : "red");
            hWindow.DispText(
                $"{innerGap[1].gap:F3}",
                "window",
                textCurrentRow,
                valueX + 120,
                innerGap[1].isOK ? "green" : "red",
                "box",
                "false"
            );

            //DrawLabelValue(
            //    hWindow,
            //    textCurrentRow,
            //    titleX,
            //    "JR1内层Gap值:",
            //    "orange",
            //    $"{innerGap[0].gap:F3}",
            //    innerGap[0].isOK ? "green" : "red",
            //    valueX
            //);

            //textCurrentRow = textCurrentRow + lineHeight;

            //DrawLabelValue(
            //    hWindow,
            //    textCurrentRow,
            //    titleX,
            //    "JR2内层Gap值:",
            //    "orange",
            //    $"{innerGap[1].gap:F3}",
            //    innerGap[1].isOK ? "green" : "red",
            //    valueX
            //);

            //DrawJRMeanGapTable(
            //    hWindow,
            //    textInitRow,
            //    valueX + 600,
            //    z45MeanGap,
            //    z0MeanGap,
            //    f45MeanGap
            //);

            DrawJRMeanGapTable(
                hWindow,
                textInitRow,
                valueX + 600,
                z45MeanGap[0],
                z0MeanGap[0],
                f45MeanGap[0],
                z0NeToAnRatio[0],
                1
            );

            DrawJRMeanGapTable(
                hWindow,
                textInitRow,
                valueX + 1800,
                z45MeanGap[1],
                z0MeanGap[1],
                f45MeanGap[1],
                z0NeToAnRatio[1],
                2
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
            const double lineHeight = 40; // 行间距（像素）
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
                    tableInitCol + 275,
                    valueColor,
                    "box",
                    "false"
                );
                textCurrentRow = textCurrentRow + lineHeight;
            }
        }

        private void DrawJRMeanGapTable(
            HWindow hWindow,
            double textInitRow,
            double tableInitCol,
            List<(double gap, bool isOK)> z45MeanGap,
            List<(double gap, bool isOK)> z0MeanGap,
            List<(double gap, bool isOK)> f45MeanGap,
            List<(double ratio, bool isOK)> z0NeToAnRatio,
            int index
        )
        {
            var labelColor = "orange";
            const double lineHeight = 50; // 行间距（像素）
            var textCurrentRow = textInitRow;

            hWindow.SetColor(labelColor);
            hWindow.DispText(
                $"JR{index}:",
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
                z0MeanGap[0],
                z45MeanGap[0],
                f45MeanGap[0],
                "内"
            );
            textCurrentRow = textCurrentRow + lineHeight;

            DrawJRMeanGapSingleLine(
                hWindow,
                textCurrentRow,
                tableInitCol,
                "0°|+45°|-45°中五层Gap平均值:",
                "orange",
                z0MeanGap[1],
                z45MeanGap[1],
                f45MeanGap[1],
                "中"
            );
            textCurrentRow = textCurrentRow + lineHeight;

            DrawJRMeanGapSingleLine(
                hWindow,
                textCurrentRow,
                tableInitCol,
                "0°|+45°|-45°外五层Gap平均值:",
                "orange",
                z0MeanGap[2],
                z45MeanGap[2],
                f45MeanGap[2],
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
                    z0MeanGap[0].gap
                    + z0MeanGap[1].gap
                    + z0MeanGap[2].gap
                    + z45MeanGap[2].gap
                    + f45MeanGap[2].gap
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
                tableInitCol + 360,
                valueColor,
                "box",
                "false"
            );
            textCurrentRow = textCurrentRow + lineHeight;

            DrawJRZ0NeToAnRatioSingleLine(
                hWindow,
                textCurrentRow,
                tableInitCol,
                "0°方向负凸到正凹比值(第2圈|第3圈|第4圈|第5圈|第6圈):",
                "orange",
                z0NeToAnRatio
            );
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
            var charPix = 15;
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
            currentTextCol = currentTextCol + label.Length * (charPix + 3);

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
            currentTextCol = currentTextCol + value.Length * (charPix + 3);

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
            currentTextCol = currentTextCol + label.Length * (charPix + 12);

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

        private void DrawJRZ0NeToAnRatioSingleLine(
            HWindow hWindow,
            double textInitRow,
            double tableInitCol,
            string label,
            string labelColor,
            List<(double ratio, bool isOK)> z0NeToAnRatio
        )
        {
            var charPix = 15;
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
            currentTextCol = currentTextCol + label.Length * (charPix + 7);

            for (int i = 0; i < z0NeToAnRatio.Count; i++)
            {
                var valueColor = z0NeToAnRatio[i].isOK ? "green" : "red";
                var value = $"{z0NeToAnRatio[i].ratio:F3}";
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

                if (i != (z0NeToAnRatio.Count - 1))
                {
                    hWindow.SetColor("green");
                    hWindow.DispText(
                        "|",
                        "window",
                        textInitRow,
                        currentTextCol,
                        "green",
                        "box",
                        "false"
                    );
                    currentTextCol = currentTextCol + charPix;
                }
            }
        }

        /// <summary>
        /// 构建输出路径
        /// </summary>
        /// <param name="baseOutputDir">基础输出目录</param>
        /// <param name="sourceImageFilePath">原始图片路径</param>
        /// <returns></returns>
        /// <summary>
        /// 解析可写的结果导出根目录。
        /// 优先用界面配置的 ExportPath（且非盘符根目录）；否则落到源图旁「检测结果」。
        /// </summary>
        private string ResolveWritableExportRoot(string? sourceImageDirectory)
        {
            static bool IsDriveRoot(string path)
            {
                try
                {
                    var full = Path.GetFullPath(path)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var root = Path.GetPathRoot(full)
                        ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return !string.IsNullOrEmpty(root)
                        && string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }

            if (
                !string.IsNullOrWhiteSpace(ExportPath)
                && !IsDriveRoot(ExportPath)
            )
            {
                return Path.GetFullPath(ExportPath);
            }

            var fallbackParent = !string.IsNullOrWhiteSpace(sourceImageDirectory)
                ? sourceImageDirectory!
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NCLCCT"
                );
            var fallback = Path.Combine(fallbackParent, "检测结果");
            if (string.IsNullOrWhiteSpace(ExportPath) || IsDriveRoot(ExportPath))
            {
                ExportPath = fallback;
                _logger.Info($"导出路径未配置或为盘符根目录，已改用: {fallback}");
            }
            return Path.GetFullPath(fallback);
        }

        private string BuildOutputPath(string? baseOutputDir, string? sourceImageFilePath)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(sourceImageFilePath);
            //var ext = Path.GetExtension(sourceImageFilePath); // tif格式太大
            var ext = ".png";
            //var ext = ".jpg"; // jpg太模糊

            string relativeDir = string.Empty;
            if (IsContinuousDetection)
            {
                var sourceFullPath = Path.GetFullPath(sourceImageFilePath!);
                var relativePath = Path.GetRelativePath(ImageMonitorPath!, sourceFullPath);
                var parts = relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToList();
                var relativeDirParts = parts.Take(parts.Count - 1).ToArray();

                relativeDir = Path.Combine(relativeDirParts);
            }
            baseOutputDir = string.IsNullOrWhiteSpace(relativeDir)
                ? baseOutputDir
                : Path.Combine(baseOutputDir!, relativeDir);

            var outputFileName = $"{nameWithoutExt}_result{ext}";

            if (string.IsNullOrEmpty(baseOutputDir))
            {
                var sourceDir = Path.GetDirectoryName(sourceImageFilePath) ?? ".";
                return Path.Combine(sourceDir, outputFileName);
            }

            if (!Directory.Exists(baseOutputDir))
            {
                Directory.CreateDirectory(baseOutputDir);
            }

            return Path.Combine(baseOutputDir, outputFileName);
        }

        /// <summary>
        /// Gap 检测
        /// </summary>
        /// <param name="imageFilePath">图片路径</param>
        public void GapDetection(string? imageFilePath)
            {
                if (string.IsNullOrEmpty(imageFilePath))
                    return;

            EnsureHalconNativeRuntimePath();

            try
            {
                var fileInfo = new FileInfo(imageFilePath);
                var fileName = Path.GetFileNameWithoutExtension(imageFilePath);
                FileSelectInitDir = fileInfo.DirectoryName;
                BottomBlackHeight = 0;

                _logger.Info("开始调用华为算法 NChwSwdCT");
                // 新 DLL ModelAction 9 参：0图 1句柄 2黑边 3结果图 4点区域 5行 6列 7打印 8运行信息
                // 已无旧版第 7 参 AllJianXiValue
                AlgorithmParameters = new object[9];
                AlgorithmParameters[0] = imageFilePath;
                AlgorithmParameters[1] = ModelHandle!;
                AlgorithmParameters[2] = BottomBlackHeight;
                AlgorithmParameters[3] = new HObject();
                AlgorithmParameters[4] = new List<HObject>();
                AlgorithmParameters[5] = new List<HTuple>();
                AlgorithmParameters[6] = new List<HTuple>();
                AlgorithmParameters[7] = new List<HTuple>();
                AlgorithmParameters[8] = "";

                var startTime = DateTime.Now;
                var visionInvoked = TryInvokeVisionAlgorithm(imageFilePath);
                if (visionInvoked)
                {
                ResultImage = AlgorithmParameters[3] as HObject;
                AllPointRegion = AlgorithmParameters[4] as List<HObject>;
                AllRegionRow = AlgorithmParameters[5] as List<HTuple>;
                AllRegionCol = AlgorithmParameters[6] as List<HTuple>;
                AllJianXiValue = null;
                AllPrintfValue = AlgorithmParameters[7] as List<HTuple>;
                RunInfo = AlgorithmParameters[8] as string;
                    LingDuNei5BiZhi = null;
                    HuaweiObjRegions = AllPointRegion;
                    HuaweiAlignRows = AllRegionRow;
                    HuaweiAlignCols = AllRegionCol;
                    HuaweiGapValues = AllJianXiValue;
                    HuaweiPrintValues = AllPrintfValue;
                                    }
                                    else
                                    {
                    ResultImage = null;
                    AllPointRegion = null;
                    AllRegionRow = null;
                    AllRegionCol = null;
                    LingDuNei5BiZhi = null;
                    AllJianXiValue = null;
                    AllPrintfValue = null;
                    if (string.IsNullOrEmpty(RunInfo))
                        RunInfo = "视觉DLL推理失败";
                }

                _logger.Info($"算法耗时：{(DateTime.Now - startTime).TotalSeconds} s");
                if (ResultImage is null || !ResultImage.IsInitialized())
                {
                    _logger.Info($"检测图片 [{imageFilePath}] 算法输出图片为空, 获取原图显示");
                    HOperatorSet.ReadImage(out HObject sourceImage, imageFilePath);
                    ResultImage = sourceImage;
                }
                if (!string.IsNullOrEmpty(RunInfo))
                {
                    _logger.Info($"检测图片 [{imageFilePath}] 算法调用信息：{RunInfo}");
                }

                // 导出目录：禁止默认落到 C:\ 根目录（无管理员权限会 WriteImage #3142 / Access Denied）
                var exportRoot = ResolveWritableExportRoot(fileInfo.DirectoryName);
                if (!Directory.Exists(exportRoot))
                    Directory.CreateDirectory(exportRoot);

                var outputPath = BuildOutputPath(exportRoot, imageFilePath);
                var alignPoints = ExtractPointsFromRegions(AllPointRegion);
                var gapsOk = EvaluateHuaweiGapsOk(
                    AllPrintfValue,
                    AllRegionRow,
                    AllRegionCol,
                    out var gapFailDetail
                );
                // OK：DLL 无报错 且 Gap1~10 均在规格内
                LastImageIsOK = string.IsNullOrEmpty(RunInfo) && gapsOk;
                var resultCode = LastImageIsOK ? "OK" : "NG";
                if (!gapsOk)
                    _logger.Info($"[Gap规格判定] NG：{gapFailDetail}");
                // 注意：Gap 超限不写入 RunInfo，否则结果图会走错误顶栏而跳过底栏测量显示
                _logger.Info(
                    $"[检测结果判定]: 图片 {imageFilePath} => {resultCode}, 点数={alignPoints.Count}, gapsOk={gapsOk}"
                    + (gapsOk ? string.Empty : $", {gapFailDetail}")
                );

                var rendered = GenerateHuaweiResultImage(
                    ResultImage,
                    AllPointRegion,
                    alignPoints,
                    AllRegionRow,
                    AllRegionCol,
                    AllJianXiValue,
                    AllPrintfValue,
                    resultCode,
                    RunInfo,
                    fileName,
                    outputPath
                );

                RunOnUIThread(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(
                        rendered ? outputPath : imageFilePath,
                        UriKind.Absolute
                    );
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    LastProcessedImage = bmp;
                });

                var date = DateTime.Now.ToString("yyyyMMdd");
                var directory = Path.GetFullPath(Path.Combine(exportRoot, $"汇总\\{date}"));
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                try
                {
                    var csvPath = Path.Combine(directory, $"NCLCCT_{date}.csv");
                    var needHeader = !File.Exists(csvPath);
                    using var sw = new StreamWriter(csvPath, append: true, Utf8Bom);
                    if (needHeader)
                        sw.WriteLine("时间,文件名,结果,点数,RunInfo,结果图");
                    var safeInfo = (RunInfo ?? string.Empty).Replace("\"", "''");
                    sw.WriteLine(
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{fileName},{resultCode},{alignPoints.Count},\"{safeInfo}\",{outputPath}"
                    );
                }
                catch (Exception csvEx)
                {
                    _logger.Warn($"写汇总CSV失败: {csvEx.Message}");
                }

                var monitorRoot = string.IsNullOrEmpty(ImageMonitorPath)
                    ? fileInfo.DirectoryName!
                    : ImageMonitorPath!;
                var relativePath = Path.GetRelativePath(monitorRoot, Path.GetFullPath(imageFilePath));
                var parts = relativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToList();
                var relativeDirParts = parts.Take(Math.Max(0, parts.Count - 1)).ToArray();

                if (IsContinuousDetection)
                {
                    var outputSourceImageFilePath = Path.Combine(
                        directory,
                        $"{string.Join("-", relativeDirParts)}-{Path.GetFileName(imageFilePath)}"
                    );
                        File.Copy(imageFilePath, outputSourceImageFilePath, overwrite: true);
                        _logger.Info($"原图移动：{imageFilePath} --> {outputSourceImageFilePath}");
                }
                else
                {
                    var outputSourceImageFilePath = Path.Combine(
                        Path.GetDirectoryName(outputPath)!,
                        Path.GetFileName(imageFilePath)
                    );
                        File.Copy(imageFilePath, outputSourceImageFilePath, overwrite: true);
                        _logger.Info($"原图移动：{imageFilePath} --> {outputSourceImageFilePath}");
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
                    OKDetectionNum++;
                else
                    NGDetectionNum++;
                OKRate =
                    TotalDetectionNum == 0
                        ? 0
                        : Math.Round(OKDetectionNum * 100.0 / TotalDetectionNum, 2);
                DetectionSattisticalData = new List<double> { OKDetectionNum, NGDetectionNum };
                RunOnUIThread(() =>
                {
                    DetectionInfoList ??= new ObservableCollection<DetectionInfoModel>();
                    if (DetectionInfoList.Count > 9)
                        DetectionInfoList.RemoveAt(DetectionInfoList.Count - 1);
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
                });

                ResultImage?.Dispose();
                ResultImage = null;
                if (AllPointRegion != null)
                {
                    foreach (var item in AllPointRegion)
                        item?.Dispose();
                }
                AllPointRegion = null;
                if (AllRegionRow != null)
                {
                    foreach (var item in AllRegionRow)
                        item?.Dispose();
                }
                AllRegionRow = null;
                if (AllRegionCol != null)
                {
                    foreach (var item in AllRegionCol)
                        item?.Dispose();
                }
                AllRegionCol = null;
                if (AllJianXiValue != null)
                {
                    foreach (var item in AllJianXiValue)
                        item?.Dispose();
                }
                AllJianXiValue = null;
                if (AllPrintfValue != null)
                {
                    foreach (var item in AllPrintfValue)
                        item?.Dispose();
                }
                AllPrintfValue = null;
                HuaweiObjRegions = null;
                HuaweiAlignRows = null;
                HuaweiAlignCols = null;
                HuaweiGapValues = null;
                HuaweiPrintValues = null;
            }
        }


        private bool IsOK(
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            List<List<(double ratio, bool isOK)>> z0NeToAnRatio
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

            // 规范3：0度方向比值规格
            var isZ0NeToAnRatioOK = !z0NeToAnRatio.Any(jr => jr.Any(r => r.isOK == false));

            return (
                isInnerGapOK
                && isFiveMeanMeanOK
                && isAllJRGapMeanOK
                && isZ45MeanGapOK
                && isZ0MeanGapOK
                && isF45MeanGapOK
                && isZ0NeToAnRatioOK
            );
        }

        private List<bool> IsJROK(
            List<(double gap, bool isOK)> innerGap,
            List<List<(double gap, bool isOK)>> z45MeanGap,
            List<List<(double gap, bool isOK)>> z0MeanGap,
            List<List<(double gap, bool isOK)>> f45MeanGap,
            List<List<(double ratio, bool isOK)>> z0NeToAnRatio
        )
        {
            var isJROKList = new List<bool>();
            for (int i = 0; i < innerGap.Count; i++)
            {
                var isInnerGapOK = innerGap[i].isOK; // 规范1：首内圈
                var isFiveMeanMeanOK = true; // 规范1：内|中|外五层均值的均值
                var isAllJRGapMeanOK = true; // 规范2：整个卷芯的Gap均值

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

                var isZ45MeanGapOK = !z45MeanGap[i].Any(g => g.isOK == false);
                var isZ0MeanGapOK = !z0MeanGap[i].Any(g => g.isOK == false); // 规范3：0度方向上内层均值
                var isF45MeanGapOK = !f45MeanGap[i].Any(g => g.isOK == false);

                // 规范3：0度方向比值规格
                var isZ0NeToAnRatioOK = !z0NeToAnRatio[i].Any(r => r.isOK == false);

                isJROKList.Add(isInnerGapOK
                && isFiveMeanMeanOK
                && isAllJRGapMeanOK
                && isZ45MeanGapOK
                && isZ0MeanGapOK
                && isF45MeanGapOK
                && isZ0NeToAnRatioOK);
            }

            return isJROKList;
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

        /// <summary>
        /// 从抓点区域提取全部点中心（华为不固定 51 点）。
        /// </summary>
        private List<(double row, double col)> ExtractPointsFromRegions(List<HObject>? regions)
        {
            var points = new List<(double row, double col)>();
            if (regions == null)
                return points;

            foreach (var region in regions)
            {
                if (region == null || !region.IsInitialized())
                    continue;
                try
                {
                    HOperatorSet.Union1(region, out HObject united);
                    HOperatorSet.Connection(united, out HObject connected);
                    HOperatorSet.AreaCenter(
                        connected,
                        out HTuple area,
                        out HTuple rows,
                        out HTuple cols
                    );
                    for (int i = 0; i < rows.Length; i++)
                    {
                        points.Add((rows[i].D, cols[i].D));
                    }
                    united.Dispose();
                    connected.Dispose();
                    area.Dispose();
                    rows.Dispose();
                    cols.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"提取抓点失败: {ex.Message}");
                }
            }
            return points;
        }

        /// <summary>
        /// 华为结果图：检测内容缩放上中放置，下方 1/4 黑色遮罩写尺寸数值（人工查看）。
        /// <para>
        /// 探针实测：<c>AllRegionRow/Col</c> 成对出现（0-1,2-3…）；
        /// 同列异行之对画黄横线；<c>AllPrintfValue</c> 写入底部遮罩。
        /// </para>
        /// </summary>
        private bool GenerateHuaweiResultImage(
            HObject? originImage,
            List<HObject>? regions,
            List<(double row, double col)> alignPoints,
            List<HTuple>? regionRows,
            List<HTuple>? regionCols,
            List<HTuple>? gapValues,
            List<HTuple>? printValues,
            string resultCode,
            string? runInfo,
            string fileName,
            string outputPath
        )
        {
            HWindow? contentWindow = null;
            HWindow? finalWindow = null;
            HObject? contentImg = null;
            HObject? canvasImg = null;
            try
            {
                if (originImage == null || !originImage.IsInitialized())
                    return false;

                HOperatorSet.GetImageSize(originImage, out HTuple width, out HTuple height);
                var imgWidth = width.I;
                var imgHeight = height.I;

                // —— ① 原尺寸叠加几何标注（数值不写在图上，改写底部遮罩）——
                contentWindow = new HWindow();
                contentWindow.OpenWindow(
                    0,
                    0,
                    Math.Max(imgWidth, 100),
                    Math.Max(imgHeight, 100),
                    new HTuple(0),
                    "invisible",
                    ""
                );
                contentWindow.SetPart(0, 0, imgHeight - 1, imgWidth - 1);
                contentWindow.DispObj(originImage);

                if (regions != null)
                {
                    contentWindow.SetDraw("fill");
                    contentWindow.SetColor("green");
                    contentWindow.SetLineWidth(1);
                    foreach (var region in regions)
                    {
                        if (region != null && region.IsInitialized())
                            contentWindow.DispObj(region);
                    }
                }

                var points = new List<(double row, double col)>();
                if (regionRows != null && regionCols != null && regionRows.Count > 0)
                {
                    var rows = regionRows[0];
                    var cols = regionCols[0];
                    if (rows != null && cols != null)
                    {
                        var n = Math.Min(rows.Length, cols.Length);
                        for (int i = 0; i < n; i++)
                            points.Add((rows[i].D, cols[i].D));
                    }
                }
                if (points.Count == 0 && alignPoints != null)
                    points.AddRange(alignPoints);

                HTuple? printTuple =
                    printValues != null && printValues.Count > 0 ? printValues[0] : null;

                DrawHuaweiPointsAndLimitLines(
                    contentWindow,
                    points,
                    printTuple, // 在点对中点标 L1/L2…，与底栏一致
                    imgWidth,
                    resultCode
                );

                HOperatorSet.DumpWindowImage(out contentImg, contentWindow);

                // —— ② 内容缩至 80%（下方留约 1/5），水平居中、贴顶 ——
                const double contentScale = 0.8;
                HOperatorSet.HomMat2dIdentity(out HTuple hom);
                HOperatorSet.HomMat2dScale(hom, contentScale, contentScale, 0, 0, out hom);
                var offsetCol = (imgWidth - imgWidth * contentScale) / 2.0;
                const double offsetRow = 0.0; // 上中：贴顶 + 水平居中
                HOperatorSet.HomMat2dTranslate(hom, offsetRow, offsetCol, out hom);
                HOperatorSet.AffineTransImageSize(
                    contentImg,
                    out canvasImg,
                    hom,
                    "constant",
                    imgWidth,
                    imgHeight
                );

                // —— ③ 终稿：画布可加宽；左右栏留小缝即可，避免过大空白 ——
                const int bandFontSize = 56;
                // 左栏最长行约到 1080～1120，测量区紧跟其后
                const double measureCol0 = 1140;
                const double measureColWidth = 430;
                const int measureCols = 4; // Gap 每列 3 行 → 最多约 4 列
                var canvasW = Math.Max(
                    imgWidth,
                    (int)(measureCol0 + measureCols * measureColWidth + 60)
                );

                finalWindow = new HWindow();
                finalWindow.OpenWindow(
                    0,
                    0,
                    Math.Max(canvasW, 100),
                    Math.Max(imgHeight, 100),
                    new HTuple(0),
                    "invisible",
                    ""
                );
                finalWindow.SetPart(0, 0, imgHeight - 1, canvasW - 1);
                // 先铺黑底（加宽区域），再贴检测图
                finalWindow.SetColor("black");
                finalWindow.SetDraw("fill");
                finalWindow.DispRectangle1(0.0, 0.0, imgHeight - 1.0, canvasW - 1.0);
                finalWindow.DispObj(canvasImg);

                var bandH = Math.Max(1, imgHeight / 4);
                var bandTop = imgHeight - bandH;
                finalWindow.DispRectangle1(bandTop, 0.0, imgHeight - 1.0, canvasW - 1.0);

                // 顶部/底部文字
                if (!string.IsNullOrEmpty(runInfo))
                {
                    DrawTopErrorInfo(fileName, runInfo, finalWindow);
                }
                else
                {
                    DrawTopInfo(fileName, resultCode, finalWindow);
                    DrawHuaweiBottomValueBand(
                        finalWindow,
                        bandTop,
                        printTuple,
                        bandFontSize,
                        measureCol0,
                        measureColWidth
                    );
                }

                var outDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                HOperatorSet.DumpWindowImage(out HObject dumpImg, finalWindow);
                var writePath = outputPath.Replace('\\', '/');
                if (!writePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    writePath += ".png";
                HOperatorSet.WriteImage(dumpImg, "png", 0, writePath);
                dumpImg.Dispose();
                _logger.Info(
                    $"华为结果图已写出: {outputPath}（内容缩放={contentScale:P0}，底栏={bandH}px）"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("生成华为结果图失败", ex);
                return false;
            }
            finally
            {
                try
                {
                    contentWindow?.CloseWindow();
                }
                catch
                {
                    // ignore
                }
                contentWindow?.Dispose();
                try
                {
                    finalWindow?.CloseWindow();
                }
                catch
                {
                    // ignore
                }
                finalWindow?.Dispose();
                contentImg?.Dispose();
                canvasImg?.Dispose();
            }
        }

        /// <summary>
        /// 底部遮罩：左栏管控规格；右栏 Gap 实测（每列 3 行）。画布与列起点由调用方按字号加宽。
        /// </summary>
        private void DrawHuaweiBottomValueBand(
            HWindow window,
            double textInitRow,
            HTuple? printTuple,
            int bandFontSize,
            double measureCol0,
            double measureColWidth
        )
        {
            window.SetFont($"Times New Roman-Bold-{bandFontSize}");

            const double titleX = 40;
            // 「管控 Gap4~6:」约 350px，数值紧跟
            const double leftValueX = 390;
            var lineHeight = bandFontSize + 28.0; // ≈84
            double textCurrentRow = textInitRow;

            DrawLabelValue(
                window,
                textCurrentRow,
                titleX,
                "数值单位: mm",
                "orange",
                "",
                "green",
                leftValueX
            );
            textCurrentRow += lineHeight;

            DrawLabelValue(
                window,
                textCurrentRow,
                titleX,
                "管控 Gap1~3:",
                "orange",
                $"Gap ≤ {HuaweiNorm3UpperMm:F3} mm",
                "green",
                leftValueX
            );
            textCurrentRow += lineHeight;

            DrawLabelValue(
                window,
                textCurrentRow,
                titleX,
                "管控 Gap4~6:",
                "orange",
                $"{HuaweiNorm2LowerMm:F3} ≤ Gap ≤ {HuaweiNorm2MeanUpperMm:F3} mm",
                "green",
                leftValueX
            );
            textCurrentRow += lineHeight;

            DrawLabelValue(
                window,
                textCurrentRow,
                titleX,
                "管控 Gap10:",
                "orange",
                $"Gap ≤ {HuaweiNorm1UpperMm:F3} mm",
                "green",
                leftValueX
            );

            var showCount = printTuple?.Length ?? 0;
            var mms = BuildHuaweiGapMmList(printTuple, showCount);

            // Gap 标签与数值紧凑：Gap1~9 / Gap10~11 宽度不同
            const int perColumn = 3;
            var measureHeaderRow = textInitRow;
            window.SetColor("orange");
            window.DispText(
                "测量尺寸 (mm):",
                "window",
                measureHeaderRow,
                measureCol0,
                "orange",
                "box",
                "false"
            );

            if (mms.Length > 0)
            {
                for (int i = 0; i < mms.Length; i++)
                {
                    var gapIndex = i + 1;
                    var colIndex = i / perColumn;
                    var rowIndex = i % perColumn;
                    var col = measureCol0 + colIndex * measureColWidth;
                    var row = measureHeaderRow + lineHeight * (1 + rowIndex);
                    var valueColor = IsHuaweiGapValueDisplayOk(gapIndex, mms) ? "green" : "red";
                    // Bold-56：「Gap9:」≈150，「Gap11:」≈190
                    var valueOffset = gapIndex >= 10 ? 195.0 : 155.0;
                    DrawLabelValue(
                        window,
                        row,
                        col,
                        $"Gap{gapIndex}:",
                        "orange",
                        $"{mms[i]:F3} mm",
                        valueColor,
                        col + valueOffset
                    );
                }
            }
            else
            {
                window.SetColor("orange");
                window.DispText(
                    "(无测量返回)",
                    "window",
                    measureHeaderRow + lineHeight,
                    measureCol0,
                    "orange",
                    "box",
                    "false"
                );
            }
        }

        /// <summary>
        /// 华为实际 Gap(mm)：
        /// Gap1/2/3/10/11 = 像素数×像素精度 − 0.12；
        /// Gap4/5/6/7/8/9 = 像素数×像素精度 − 0.12。
        /// </summary>
        private double ConvertPrintfPxToMm(double px, int gapIndex1Based)
        {
            var scale = HuaweiPxToMmScale > 0 ? HuaweiPxToMmScale : 0.01;
            var offset =
                gapIndex1Based >= 4 && gapIndex1Based <= 9
                    ? HuaweiGapOffsetBMm
                    : HuaweiGapOffsetAMm;
            return px * scale - offset;
        }

        private double[] BuildHuaweiGapMmList(HTuple? printTuple, int count)
        {
            if (printTuple == null || count <= 0)
                return Array.Empty<double>();
            var n = Math.Min(count, printTuple.Length);
            var mms = new double[n];
            for (int i = 0; i < n; i++)
                mms[i] = ConvertPrintfPxToMm(printTuple[i].D, i + 1);
            return mms;
        }

        /// <summary>当前参与 OK/NG 的 Gap：1~6 与 10。</summary>
        private static bool IsHuaweiGapControlled(int gapIndex1Based) =>
            (gapIndex1Based >= 1 && gapIndex1Based <= 6) || gapIndex1Based == 10;

        /// <summary>
        /// 显示红绿：仅管控项按规格着色；非管控项一律绿（不因超限标红）。
        /// </summary>
        private bool IsHuaweiGapValueDisplayOk(int gapIndex1Based, double[] mms)
        {
            var i = gapIndex1Based - 1;
            if (i < 0 || i >= mms.Length)
                return true;
            if (!IsHuaweiGapControlled(gapIndex1Based))
                return true;

            var mm = mms[i];
            if (gapIndex1Based >= 1 && gapIndex1Based <= 3)
                return mm <= HuaweiNorm3UpperMm;
            if (gapIndex1Based >= 4 && gapIndex1Based <= 6)
                return mm >= HuaweiNorm2LowerMm && mm <= HuaweiNorm2MeanUpperMm;
            if (gapIndex1Based == 10)
                return mm <= HuaweiNorm1UpperMm;
            return true;
        }

        /// <summary>
        /// 整图判定：仅 Gap1~6、Gap10；其余 Printf 不参与 OK/NG。
        /// </summary>
        private bool EvaluateHuaweiGapsOk(
            List<HTuple>? printValues,
            List<HTuple>? regionRows,
            List<HTuple>? regionCols,
            out string failDetail
        )
        {
            failDetail = string.Empty;
            HTuple? printTuple =
                printValues != null && printValues.Count > 0 ? printValues[0] : null;
            if (printTuple == null || printTuple.Length == 0)
            {
                failDetail = "无Gap测量值(Printf为空)";
                return false;
            }

            var mms = BuildHuaweiGapMmList(printTuple, printTuple.Length);
            var fails = new List<string>();

            for (int i = 0; i < mms.Length; i++)
            {
                var gapIndex = i + 1;
                if (!IsHuaweiGapControlled(gapIndex))
                    continue;
                var mm = mms[i];
                if (IsHuaweiGapValueDisplayOk(gapIndex, mms))
                    continue;

                if (gapIndex >= 1 && gapIndex <= 3)
                    fails.Add($"Gap{gapIndex}={mm:F3}>{HuaweiNorm3UpperMm:F3}");
                else if (gapIndex >= 4 && gapIndex <= 6)
                    fails.Add(
                        $"Gap{gapIndex}={mm:F3}不在[{HuaweiNorm2LowerMm:F3},{HuaweiNorm2MeanUpperMm:F3}]"
                    );
                else if (gapIndex == 10)
                    fails.Add($"Gap10={mm:F3}>{HuaweiNorm1UpperMm:F3}");
            }

            if (fails.Count == 0)
                return true;

            failDetail = string.Join("; ", fails);
            return false;
        }

        /// <summary>
        /// 绘制华为抓点短横线，并对「同列异行」点对绘制黄横线；中点标 Gap 实际值（mm）。
        /// Gap7：端点取视觉点对，竖线左偏并用虚线从锚点引出（其余 Gap 直接按点对连线）。
        /// </summary>
        private void DrawHuaweiPointsAndLimitLines(
            HWindow tempWindow,
            List<(double row, double col)> points,
            HTuple? printTuple,
            int imgWidth,
            string resultCode
        )
        {
            if (points.Count == 0)
                return;

            const double colSameEps = 8.0; // 同列判定
            const double tickHalf = 14.0; // 点位短横线半宽
            // 黄横线全长固定 30 px
            const double limitHalf = 15.0;
            // Gap7 尺寸线相对视觉点列左偏像素
            const double gap7LineOffset = 80.0;

            const string measureFont = "Times New Roman-Bold-48";

            // ① 每个抓点画短水平刻度
            tempWindow.SetColor("green");
            tempWindow.SetLineWidth(2);
            tempWindow.SetDraw("margin");
            for (int i = 0; i < points.Count; i++)
            {
                var (row, col) = points[i];
                DispRegionLine(tempWindow, row, col - tickHalf, row, col + tickHalf);
                tempWindow.SetColor(resultCode == "OK" ? "green" : "red");
                DispRegionLine(tempWindow, row - 5, col, row + 5, col);
                tempWindow.SetColor("green");
            }

            // ② 点对：同列黄横线；中点标注 Gap（规范1/2/3 着色）
            var pairCount = points.Count / 2;
            var mms = BuildHuaweiGapMmList(printTuple, pairCount);
            tempWindow.SetFont(measureFont);

            for (int p = 0; p < pairCount; p++)
            {
                var gapIndex = p + 1;
                var (r0, c0) = points[p * 2];
                var (r1, c1) = points[p * 2 + 1];
                var sameCol = Math.Abs(c0 - c1) <= colSameEps;
                var midRow = (r0 + r1) / 2.0;
                var midCol = (c0 + c1) / 2.0;
                double? gap7TextCol = null;

                if (gapIndex == 7)
                {
                    // 端点行/列取自视觉点对；竖线左偏，两端用虚线接到锚点
                    var cx = (c0 + c1) / 2.0;
                    var lineRow0 = Math.Min(r0, r1);
                    var lineRow1 = Math.Max(r0, r1);
                    var gap7Col = Math.Max(limitHalf + 5, cx - gap7LineOffset);

                    tempWindow.SetColor("yellow");
                    tempWindow.SetLineWidth(5);
                    DispRegionLine(tempWindow, lineRow0, cx - limitHalf, lineRow0, cx + limitHalf);
                    DispRegionLine(tempWindow, lineRow1, cx - limitHalf, lineRow1, cx + limitHalf);

                    tempWindow.SetColor("orange");
                    tempWindow.SetLineWidth(5);
                    DispDashedRegionLine(tempWindow, lineRow0, gap7Col, lineRow0, cx);
                    DispDashedRegionLine(tempWindow, lineRow1, gap7Col, lineRow1, cx);

                    DispRegionLine(tempWindow, lineRow0, gap7Col, lineRow1, gap7Col);
                    DispRegionLine(
                        tempWindow,
                        lineRow0,
                        gap7Col - limitHalf,
                        lineRow0,
                        gap7Col + limitHalf
                    );
                    DispRegionLine(
                        tempWindow,
                        lineRow1,
                        gap7Col - limitHalf,
                        lineRow1,
                        gap7Col + limitHalf
                    );

                    midRow = (lineRow0 + lineRow1) / 2.0;
                    gap7TextCol = Math.Max(5, gap7Col - 220);
                }
                else if (sameCol)
                {
                    // Gap4/5/6 同列竖向量测线加粗，其余保持原线宽
                    var thickVertical = gapIndex is >= 4 and <= 6;
                    tempWindow.SetColor("yellow");
                    tempWindow.SetLineWidth(thickVertical ? 5 : 3);
                    var cx = (c0 + c1) / 2.0;
                    DispRegionLine(tempWindow, r0, cx - limitHalf, r0, cx + limitHalf);
                    DispRegionLine(tempWindow, r1, cx - limitHalf, r1, cx + limitHalf);

                    tempWindow.SetColor("orange");
                    tempWindow.SetLineWidth(thickVertical ? 5 : 1);
                    DispRegionLine(tempWindow, r0, cx, r1, cx);
                }
                else
                {
                    tempWindow.SetColor("cyan");
                    tempWindow.SetLineWidth(1);
                    DispRegionLine(tempWindow, r0, c0, r1, c1);
                }

                var textRow = (int)Math.Max(5, midRow - 20);
                var textCol = gap7TextCol.HasValue
                    ? (int)gap7TextCol.Value
                    : (int)Math.Min(imgWidth - 120, Math.Max(5, midCol + 12));
                string label;
                string labelColor;
                if (p < mms.Length)
                {
                    label = $"Gap{gapIndex}:{mms[p]:F3}";
                    labelColor = IsHuaweiGapValueDisplayOk(gapIndex, mms) ? "green" : "red";
                }
                else
                {
                    label = $"Gap{gapIndex}";
                    labelColor = "green";
                }

                tempWindow.DispText(
                    label,
                    "image",
                    textRow,
                    textCol,
                    labelColor,
                    "box",
                    "false"
                );
            }
        }

        private static void DispRegionLine(
            HWindow window,
            double r1,
            double c1,
            double r2,
            double c2
        )
        {
            HOperatorSet.GenRegionLine(out HObject line, r1, c1, r2, c2);
            window.DispObj(line);
            line.Dispose();
        }

        /// <summary>用短线段模拟虚线（DispObj 区域线不受 SetLineStyle 影响）。</summary>
        private static void DispDashedRegionLine(
            HWindow window,
            double r1,
            double c1,
            double r2,
            double c2,
            double dashLen = 12.0,
            double gapLen = 8.0
        )
        {
            var dr = r2 - r1;
            var dc = c2 - c1;
            var len = Math.Sqrt(dr * dr + dc * dc);
            if (len < 1e-6)
                return;

            var ur = dr / len;
            var uc = dc / len;
            var pos = 0.0;
            var draw = true;
            while (pos < len)
            {
                var seg = draw ? dashLen : gapLen;
                var next = Math.Min(len, pos + seg);
                if (draw)
                {
                    DispRegionLine(
                        window,
                        r1 + ur * pos,
                        c1 + uc * pos,
                        r1 + ur * next,
                        c1 + uc * next
                    );
                }
                pos = next;
                draw = !draw;
            }
        }

        /// <summary>
        /// NJCT.NChwSwdCT 内部会 <c>cv::imwrite("D:\\TEST.png")</c>。
        /// 若 D: 根目录对当前用户不可写，异常被吞成「当前图像暂不支持检测」（与分辨率无关）。
        /// </summary>
        private void EnsureNjctTempPngWritable()
        {
            const string tempPng = @"D:\TEST.png";
            try
            {
                if (!Directory.Exists(@"D:\"))
                {
                    _logger.Error("未找到 D: 盘。华为 NJCT 硬编码写 D:\\TEST.png，无 D: 将无法检测。");
                    Growl.ErrorGlobal("缺少 D: 盘：华为算法需要写入 D:\\TEST.png");
                    return;
                }

                var probe = new byte[] { 0x00 };
                File.WriteAllBytes(tempPng, probe);
                _logger.Info($"D:\\TEST.png 可写检查通过（{tempPng}）");
            }
            catch (Exception ex)
            {
                _logger.Error(
                    $"D:\\TEST.png 不可写：{ex.Message}。将导致算法返回「当前图像暂不支持检测」。请以管理员运行 环境安装包 内 fix_d_test_png.ps1",
                    ex
                );
                Growl.ErrorGlobal(
                    "D:\\TEST.png 不可写（华为 DLL 硬编码）。请管理员运行 fix_d_test_png.ps1 后重试"
                );
            }
        }

        /// <summary>
        /// 将输出目录 <c>Dlls\</c> 加入原生 DLL 搜索路径，并设置 Halcon 许可环境变量。
        /// <c>halcondotnet</c> 的 P/Invoke 加载名为 <c>halcon</c>，默认不搜子目录 Dlls。
        /// </summary>
        private void EnsureHalconNativeRuntimePath()
        {
            if (_halconNativePathReady)
                return;

            try
            {
                var dllsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Dlls"));
                var halconDll = Path.Combine(dllsDir, "halcon.dll");
                if (!File.Exists(halconDll))
                {
                    _logger.Error(
                        $"未找到 Halcon 原生库: {halconDll}。请确认 SUNWODA_SEVB.Core\\Dlls\\halcon.dll 已复制到输出目录。"
                    );
                    Growl.ErrorGlobal("缺少 halcon.dll（应在程序目录 Dlls\\ 下）");
                    return;
                }

                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var parts = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
                bool HasDir(string dir) =>
                    parts.Any(p => string.Equals(p, dir, StringComparison.OrdinalIgnoreCase));

                if (!HasDir(dllsDir))
                {
                    Environment.SetEnvironmentVariable("PATH", dllsDir + ";" + path);
                }

                try
                {
                    // LOAD_LIBRARY_SEARCH_DEFAULT_DIRS：应用目录 + System32 + AddDllDirectory 用户目录
                    NativeDllSearch.SetDefaultDllDirectories(0x00001000);
                    NativeDllSearch.AddDllDirectory(dllsDir);
                }
                catch (Exception nativeEx)
                {
                    _logger.Warn($"AddDllDirectory(Dlls) 失败，将仅依赖 PATH: {nativeEx.Message}");
                }

                // Halcon 默认按 cwd 找 license.dat；进程 cwd 多为 exe 目录而非 Dlls
                var existingLicense = Environment.GetEnvironmentVariable("HALCON_LICENSE_FILE");
                if (string.IsNullOrWhiteSpace(existingLicense))
                {
                    var licenseCandidates = new[]
                    {
                        Path.Combine(dllsDir, "license.dat"),
                        Path.Combine(dllsDir, "license", "license.dat"),
                        Path.Combine(AppContext.BaseDirectory, "license.dat"),
                    };
                    var licensePath = licenseCandidates.FirstOrDefault(File.Exists);
                    if (licensePath != null)
                    {
                        Environment.SetEnvironmentVariable("HALCON_LICENSE_FILE", licensePath);
                        _logger.Info($"已设置 HALCON_LICENSE_FILE={licensePath}");
                    }
                    else
                    {
                        _logger.Warn(
                            $"未在 {dllsDir} 找到 license.dat；若后续出现 Halcon 授权错误请补齐许可。"
                        );
                    }
                }

                _halconNativePathReady = true;
                _logger.Info($"Halcon 原生库路径已就绪: {dllsDir}");
            }
            catch (Exception ex)
            {
                _logger.Error("配置 Halcon 原生库路径失败", ex);
            }
        }

        /// <summary>
        /// 将 CUDA 12 + cuDNN 9 运行库目录加入进程 PATH / DLL 搜索路径。
        /// ORT GPU 1.20.1 需要这些依赖；仅有驱动、无 Toolkit 时会报 LoadLibrary 126。
        /// </summary>
        private void EnsureCudaRuntimePath()
        {
            if (_cudaRuntimePathReady)
                return;

            // Halcon 与 CUDA 均依赖 AddDllDirectory；先保证 Dlls 根目录已注册
            EnsureHalconNativeRuntimePath();

            try
            {
                var cudaDir = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, DefaultCudaRuntimeRelativePath)
                );
                var nativeDir = Path.GetFullPath(
                    Path.Combine(AppContext.BaseDirectory, @"runtimes\win-x64\native")
                );

                if (!Directory.Exists(cudaDir) || !File.Exists(Path.Combine(cudaDir, "cudart64_12.dll")))
                {
                    _logger.Error(
                        $"CUDA 运行库目录缺失或不全: {cudaDir}。ORT GPU 需要 CUDA 12 + cuDNN 9（如 cudart64_12.dll）。"
                    );
                    Growl.ErrorGlobal("CUDA 运行库缺失：请部署 Dlls\\CUDA12（CUDA12+cuDNN9）");
                    return;
                }

                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                var parts = path.Split(';', StringSplitOptions.RemoveEmptyEntries);
                bool HasDir(string dir) =>
                    parts.Any(p => string.Equals(p, dir, StringComparison.OrdinalIgnoreCase));

                var prefix = new List<string>();
                if (!HasDir(cudaDir))
                    prefix.Add(cudaDir);
                if (Directory.Exists(nativeDir) && !HasDir(nativeDir))
                    prefix.Add(nativeDir);
                if (prefix.Count > 0)
                {
                    Environment.SetEnvironmentVariable(
                        "PATH",
                        string.Join(";", prefix) + ";" + path
                    );
                }

                // 补充用户 DLL 目录（避免 SetDllDirectory 只能设一个目录的问题）
                try
                {
                    NativeDllSearch.SetDefaultDllDirectories(0x00001000);
                    NativeDllSearch.AddDllDirectory(cudaDir);
                    if (Directory.Exists(nativeDir))
                        NativeDllSearch.AddDllDirectory(nativeDir);
                }
                catch (Exception nativeEx)
                {
                    _logger.Warn($"AddDllDirectory 失败（将仅依赖 PATH）: {nativeEx.Message}");
                }

                _cudaRuntimePathReady = true;
                _logger.Info($"CUDA 运行库路径已就绪: {cudaDir}");
            }
            catch (Exception ex)
            {
                _logger.Error("配置 CUDA 运行库路径失败", ex);
            }
        }

        private static class NativeDllSearch
        {
            [System.Runtime.InteropServices.DllImport(
                "kernel32",
                CharSet = System.Runtime.InteropServices.CharSet.Unicode,
                SetLastError = true
            )]
            public static extern IntPtr AddDllDirectory(string newDirectory);

            [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
            public static extern bool SetDefaultDllDirectories(uint directoryFlags);
        }

        /// <summary>
        /// 加载南昌华为视觉 DLL（类名 <c>NJCT.NChwSwdCT</c>）。
        /// 策略：本机 <c>Dlls\NCLCCT\NJCT.dll</c> 存在则优先加载；否则用公钥签名请求 SysCenter。
        /// CF.dll 始终依赖本机 <c>Dlls\NCLCCT\</c>（不随云端下载）。
        /// </summary>
        private Assembly? TryLoadVisionAssembly(
            string? dllName,
            string? publicKey,
            string? appKey,
            string? pythonCode
        )
        {
            try
            {
                EnsureCudaRuntimePath();

                var relative = string.IsNullOrWhiteSpace(dllName)
                    ? DefaultHuaweiDllRelativePath
                    : dllName!;
                var dllPath = Path.IsPathRooted(relative)
                    ? relative
                    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relative));

                // 解析目录始终指向 NCLCCT 算法目录，供 CF / 原生依赖解析
                _visionDllDirectory = Path.GetDirectoryName(dllPath);
                if (string.IsNullOrEmpty(_visionDllDirectory))
                {
                    _visionDllDirectory = Path.GetFullPath(
                        Path.Combine(AppContext.BaseDirectory, "Dlls", "NCLCCT")
                    );
                }
                EnsureVisionAssemblyResolveHook();
                PrepareVisionNativeSearchPath(_visionDllDirectory);

                Assembly? assembly = null;
                if (File.Exists(dllPath))
                {
                    _logger.Info($"[NCLCCT云端] 本机已有算法 DLL，优先本地加载: {dllPath}");
                    PreloadHuaweiCf(_visionDllDirectory);
                    assembly = Assembly.LoadFrom(dllPath);
                }
                else
                {
                    _logger.Info(
                        $"[NCLCCT云端] 本机无算法 DLL，请求 SysCenter：pythonCode={pythonCode}，url={NCLCCTDLLDownloadUrl}"
                    );
                    assembly = DownloadAndLoadVisionAssembly(
                        publicKey,
                        appKey,
                        pythonCode,
                        NCLCCTDLLDownloadUrl
                    );
                    if (assembly != null)
                        PreloadHuaweiCf(_visionDllDirectory);
                }

                if (assembly == null)
                    return null;

                return BindVisionAlgorithmType(assembly, dllPath);
            }
            catch (Exception ex)
            {
                _logger.Error("视觉DLL加载失败", ex);
                Growl.ErrorGlobal($"视觉DLL加载失败: {ex.Message}");
                return null;
            }
        }

        private void PrepareVisionNativeSearchPath(string? visionDir)
        {
            if (string.IsNullOrEmpty(visionDir))
                return;
            try
            {
                NativeDllSearch.AddDllDirectory(visionDir);
            }
            catch
            {
                // ignore
            }
        }

        private void PreloadHuaweiCf(string? visionDir)
        {
            if (string.IsNullOrEmpty(visionDir))
                return;
            var cfPath = Path.Combine(visionDir, "CF.dll");
            if (!File.Exists(cfPath))
            {
                _logger.Warn($"华为 CF.dll 不存在（云端只下 NJCT）：{cfPath}");
                return;
            }
            try
            {
                Assembly.LoadFrom(cfPath);
                _logger.Info($"已预加载华为 CF: {cfPath}");
            }
            catch (Exception cfEx)
            {
                _logger.Warn($"预加载华为 CF 失败，将回退宿主 CF: {cfEx.Message}");
            }
        }

        private Assembly? DownloadAndLoadVisionAssembly(
            string? publicKey,
            string? appKey,
            string? pythonCode,
            string? downloadUrl
        )
        {
            if (
                string.IsNullOrWhiteSpace(publicKey)
                || string.IsNullOrWhiteSpace(appKey)
                || string.IsNullOrWhiteSpace(pythonCode)
                || string.IsNullOrWhiteSpace(downloadUrl)
            )
            {
                _logger.Error(
                    "[NCLCCT云端] 下载参数不完整（PublicKey/AppKey/DllPythonCode/Url）"
                );
                Growl.ErrorGlobal("云端下载参数不完整，无法拉取算法 DLL");
                return null;
            }

            string signature;
            try
            {
                signature = RasHelper.RSAPublicKey(publicKey, appKey);
            }
            catch (Exception ex)
            {
                _logger.Error("[NCLCCT云端] RSA 签名失败", ex);
                Growl.ErrorGlobal($"云端签名失败: {ex.Message}");
                return null;
            }

            var json = new JObject
            {
                { "appKey", appKey },
                { "executeType", "PRED" },
                { "pythonCode", pythonCode },
                { "signature", signature },
            };

            var dllBytes = HttpPost(json, downloadUrl);
            if (dllBytes == null || dllBytes.Length == 0)
            {
                _logger.Error($"[NCLCCT云端] 下载 dll 失败：pythonCode={pythonCode}");
                Growl.ErrorGlobal("下载算法 DLL 失败");
                return null;
            }

            if (dllBytes.Length < 2 || dllBytes[0] != 0x4D || dllBytes[1] != 0x5A)
            {
                _logger.Error(
                    $"[NCLCCT云端] 下载内容不是有效 PE DLL（字节数={dllBytes.Length}）"
                );
                Growl.ErrorGlobal("云端返回内容不是有效 DLL");
                return null;
            }

            _logger.Info($"[NCLCCT云端] 算法 DLL 下载成功，字节数={dllBytes.Length}（内存加载，不落盘）");
            return Assembly.Load(dllBytes);
        }

        private Assembly? BindVisionAlgorithmType(Assembly assembly, string dllPathForLog)
        {
            var typeName = string.IsNullOrWhiteSpace(AlgorithmTypeName)
                ? HuaweiAlgorithmTypeName
                : AlgorithmTypeName!;
            var ctAlgorithmType = assembly.GetType(typeName);
            if (ctAlgorithmType == null)
            {
                _logger.Error($"未找到算法类型: {typeName}");
                Growl.ErrorGlobal($"未找到算法类型: {typeName}");
                return null;
            }

            CTDetectionAlgorithmIstance = Activator.CreateInstance(ctAlgorithmType);
            ModelInitMethod = ctAlgorithmType.GetMethod("ModelInit");
            ModelActionMethod = ctAlgorithmType.GetMethod("ModelAction");
            if (ModelInitMethod == null || ModelActionMethod == null)
            {
                _logger.Error("ModelInit / ModelAction 方法未找到");
                Growl.ErrorGlobal("ModelInit / ModelAction 方法未找到");
                return null;
            }

            _logger.Info($"视觉DLL加载成功: {dllPathForLog}, 类型={typeName}");
            return assembly;
        }

        private void EnsureVisionAssemblyResolveHook()
        {
            if (_visionResolveHooked)
                return;
            AppDomain.CurrentDomain.AssemblyResolve += OnVisionAssemblyResolve;
            _visionResolveHooked = true;
        }

        private Assembly? OnVisionAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                var name = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(_visionDllDirectory))
                    return null;
                var candidate = Path.Combine(_visionDllDirectory, $"{name}.dll");
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }
            catch
            {
                // ignore
            }
            return null;
        }

        /// <summary>
        /// 调用 <c>NChwSwdCT.ModelInit</c>；模型文件缺失时按 ModelPythonCode 从云端下载。
        /// </summary>
        private void TryInitVisionModel(Assembly assembly)
        {
            _ = assembly;
            try
            {
                EnsureCudaRuntimePath();
                if (!_cudaRuntimePathReady && IsRunCuda)
                {
                    Growl.ErrorGlobal("CUDA 运行库未就绪，无法以 GPU 初始化模型");
                    return;
                }

                var modelPath = ModelPath;
                if (string.IsNullOrWhiteSpace(modelPath))
                    modelPath = DefaultHuaweiModelRelativePath;
                if (!Path.IsPathRooted(modelPath))
                    modelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelPath));

                if (!File.Exists(modelPath))
                {
                    _logger.Info(
                        $"[NCLCCT云端] 本机无模型，请求下载：pythonCode={ModelPythonCode}，目标={modelPath}"
                    );
                    DownloadModel(modelPath, PublicKey, AppKey, ModelPythonCode);
                }

                if (!File.Exists(modelPath))
                {
                    _logger.Error($"模型文件不存在: {modelPath}");
                    Growl.ErrorGlobal($"模型文件不存在: {modelPath}");
                    return;
                }

                ModelPath = modelPath;
                ModelInitErrorInfo = string.Empty;
                ModelInitParameters = new object[4]
                {
                    modelPath,
                    null!,
                    ModelInitErrorInfo,
                    IsRunCuda,
                };

                var start = DateTime.Now;
                _logger.Info($"开始 ModelInit: {modelPath}, CUDA={IsRunCuda}");
                ModelInitMethod?.Invoke(CTDetectionAlgorithmIstance, ModelInitParameters);
                ModelHandle = ModelInitParameters[1];
                ModelInitErrorInfo = ModelInitParameters[2] as string ?? string.Empty;
                _logger.Info($"ModelInit 耗时 {(DateTime.Now - start).TotalSeconds:F2}s");

                if (ModelHandle is null)
                {
                    _logger.Error("模型句柄为空");
                    Growl.ErrorGlobal("模型句柄为空");
                }
                if (!string.IsNullOrEmpty(ModelInitErrorInfo))
                {
                    _logger.Error($"模型初始化错误: {ModelInitErrorInfo}");
                    Growl.ErrorGlobal($"模型初始化错误: {ModelInitErrorInfo}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("模型初始化异常", ex);
                Growl.ErrorGlobal($"模型初始化异常: {ex.Message}");
            }
        }

        /// <summary>从 SysCenter 下载 ONNX 模型并写入本地路径。</summary>
        private void DownloadModel(
            string modelPath,
            string? publicKey,
            string? appKey,
            string? pythonCode
        )
        {
            try
            {
                if (
                    string.IsNullOrWhiteSpace(publicKey)
                    || string.IsNullOrWhiteSpace(appKey)
                    || string.IsNullOrWhiteSpace(pythonCode)
                    || string.IsNullOrWhiteSpace(NCLCCTDLLDownloadUrl)
                )
                {
                    _logger.Error("[NCLCCT云端] 模型下载参数不完整");
                    Growl.ErrorGlobal("云端下载参数不完整，无法拉取模型");
                    return;
                }

                var signature = RasHelper.RSAPublicKey(publicKey, appKey);
                var json = new JObject
                {
                    { "appKey", appKey },
                    { "executeType", "PRED" },
                    { "pythonCode", pythonCode },
                    { "signature", signature },
                };

                var modelStream = HttpPost(json, NCLCCTDLLDownloadUrl!);
                if (modelStream == null || modelStream.Length == 0)
                {
                    _logger.Error("下载模型失败");
                    Growl.ErrorGlobal("下载模型失败");
                    return;
                }

                var dir = Path.GetDirectoryName(modelPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllBytes(modelPath, modelStream);
                _logger.Info(
                    $"[NCLCCT云端] 模型已落盘: {modelPath}，字节数={modelStream.Length}"
                );
            }
            catch (Exception ex)
            {
                _logger.Error("下载模型失败", ex);
                Growl.ErrorGlobal($"下载模型失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 调用 <c>NChwSwdCT.ModelAction</c>（9 参，无 AllJianXiValue），结果写回 <see cref="AlgorithmParameters"/>。
        /// </summary>
        private bool TryInvokeVisionAlgorithm(string? imageFilePath)
        {
            try
            {
                EnsureHalconNativeRuntimePath();
                if (string.IsNullOrEmpty(imageFilePath))
                    return false;
                if (CTDetectionAlgorithmIstance == null || ModelActionMethod == null)
                {
                    _logger.Warn("算法实例未就绪，跳过推理");
                    return false;
                }
                if (ModelHandle is null)
                {
                    _logger.Warn("模型句柄为空，跳过推理");
                    return false;
                }
                if (AlgorithmParameters == null || AlgorithmParameters.Length < 9)
                {
                    _logger.Warn("AlgorithmParameters 未按 9 参准备");
                    return false;
                }

                AlgorithmParameters[0] = imageFilePath;
                AlgorithmParameters[1] = ModelHandle;
                AlgorithmParameters[2] = BottomBlackHeight;
                ModelActionMethod.Invoke(CTDetectionAlgorithmIstance, AlgorithmParameters);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"视觉DLL推理异常 [{imageFilePath}]", ex);
                RunInfo = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }

        private byte[]? HttpPost(JObject json, string url)
        {
            try
            {
                var jsonData = JsonConvert.SerializeObject(json);
                using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // 同步发送 POST 请求
                using var response = _httpClient.PostAsync(url, content).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                // 同步获取字节数组
                var buffer = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                return buffer;
            }
            catch (Exception ex)
            {
                _logger.Error($"HttpPostSync异常：{ex}");
                return null;
            }
        }
    }
}
