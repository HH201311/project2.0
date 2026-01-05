extern alias halcondotnet;
using Dm.filter;
using halcondotnet::HalconDotNet;
using HandyControl.Controls;
using iText.IO.Image;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using MySqlX.XDevAPI.Common;
using NetTaste;
using OpenCvSharp;
using OpenCvSharp.Text;
using OxyPlot;
using OxyPlot.Legends;
using OxyPlot.Series;
using SUNWODA_SEVB.Component.UserControls;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Tool;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.ViewModels.Pages.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Yolo11seg_CT06;
using ZZC1OnlineCTModel;
using ZZC6OnlineCTModel;
using HalconDotNet = halcondotnet::HalconDotNet;
using HObject = halcondotnet::HalconDotNet.HObject;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    [Module("VM_CTAutoDetectionC06", "在线CT AI检测系统v2.0")]
    public class VM_CTAutoDetectionC06Page : ViewModelBase
    {
        private IPredictor? LABmodel;
        private IPredictor? LCDmodel;
        private IPredictor? RABmodel;
        private IPredictor? RCDmodel;
        ZZC6CTModel ai = new ZZC6CTModel();

        #region 结果界面操作
        #region LAB
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommandLAB { get; }
        // 鼠标移动命令（拖动中）
        public ICommand ImageMouseMoveCommandLAB { get; }
        // 鼠标松开命令（结束拖动）
        public ICommand ImageMouseUpCommandLAB { get; }
        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommandLAB { get; }
        // 放大按钮
        public ICommand ZoomInRealTimeCommandLAB { get; }
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommandLAB { get; }
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommandLAB { get; }
        // 选择结果路径的命令
        public ICommand SelectResultImagePath { get; }
        private const double ZoomStepLAB = 0.1;
        private const double MinScaleLAB = 0.2;
        private const double MaxScaleLAB = 5.0;
        private double _realTimeImageScaleXLAB = 1.0;
        private double _realTimeImageScaleYLAB = 1.0;
        public double RealTimeImageScaleYLAB
        {
            get => _realTimeImageScaleYLAB;
            set { _realTimeImageScaleYLAB = value; OnPropertyChanged(); }
        }

        // 图片拖动X轴偏移
        private double _realTimeImageTranslateXLAB;
        public double RealTimeImageTranslateXLAB
        {
            get => _realTimeImageTranslateXLAB;
            set { _realTimeImageTranslateXLAB = value; OnPropertyChanged(); }
        }

        // 图片拖动Y轴偏移
        private double _realTimeImageTranslateYLAB;
        public double RealTimeImageTranslateYLAB
        {
            get => _realTimeImageTranslateYLAB;
            set { _realTimeImageTranslateYLAB = value; OnPropertyChanged(); }
        }
        public double RealTimeImageScaleXLAB
        {
            get => _realTimeImageScaleXLAB;
            set { _realTimeImageScaleXLAB = value; OnPropertyChanged(); }
        }
        // 是否正在拖动
        private bool _isImageDraggingLAB;
        private System.Windows.Point _startMousePointLAB; // 拖动起始鼠标位置
        private double _startTranslateXLAB; // 拖动起始X偏移
        private double _startTranslateYLAB; // 拖动起始Y偏移
        #endregion
        #region LCD
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommandLCD { get; }
        // 鼠标移动命令（拖动中）
        public ICommand ImageMouseMoveCommandLCD { get; }
        // 鼠标松开命令（结束拖动）
        public ICommand ImageMouseUpCommandLCD { get; }
        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommandLCD { get; }
        // 放大按钮
        public ICommand ZoomInRealTimeCommandLCD { get; }
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommandLCD { get; }
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommandLCD { get; }
        // 选择结果路径的命令
        //public ICommand SelectResultImagePath { get; }
        private const double ZoomStepLCD = 0.1;
        private const double MinScaleLCD = 0.2;
        private const double MaxScaleLCD = 5.0;
        private double _realTimeImageScaleXLCD = 1.0;
        private double _realTimeImageScaleYLCD = 1.0;
        public double RealTimeImageScaleYLCD
        {
            get => _realTimeImageScaleYLCD;
            set { _realTimeImageScaleYLCD = value; OnPropertyChanged(); }
        }

        // 图片拖动X轴偏移
        private double _realTimeImageTranslateXLCD;
        public double RealTimeImageTranslateXLCD
        {
            get => _realTimeImageTranslateXLCD;
            set { _realTimeImageTranslateXLCD = value; OnPropertyChanged(); }
        }

        // 图片拖动Y轴偏移
        private double _realTimeImageTranslateYLCD;
        public double RealTimeImageTranslateYLCD
        {
            get => _realTimeImageTranslateYLCD;
            set { _realTimeImageTranslateYLCD = value; OnPropertyChanged(); }
        }
        public double RealTimeImageScaleXLCD
        {
            get => _realTimeImageScaleXLCD;
            set { _realTimeImageScaleXLCD = value; OnPropertyChanged(); }
        }
        // 是否正在拖动
        private bool _isImageDraggingLCD;
        private System.Windows.Point _startMousePointLCD; // 拖动起始鼠标位置
        private double _startTranslateXLCD; // 拖动起始X偏移
        private double _startTranslateYLCD; // 拖动起始Y偏移
        #endregion
        #region RAB
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommandRAB { get; }
        // 鼠标移动命令（拖动中）
        public ICommand ImageMouseMoveCommandRAB { get; }
        // 鼠标松开命令（结束拖动）
        public ICommand ImageMouseUpCommandRAB { get; }
        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommandRAB { get; }
        // 放大按钮
        public ICommand ZoomInRealTimeCommandRAB { get; }
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommandRAB { get; }
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommandRAB { get; }
        // 选择结果路径的命令
        //public ICommand SelectResultImagePath { get; }
        private const double ZoomStepRAB = 0.1;
        private const double MinScaleRAB = 0.2;
        private const double MaxScaleRAB = 5.0;
        private double _realTimeImageScaleXRAB = 1.0;
        private double _realTimeImageScaleYRAB = 1.0;
        public double RealTimeImageScaleYRAB
        {
            get => _realTimeImageScaleYRAB;
            set { _realTimeImageScaleYRAB = value; OnPropertyChanged(); }
        }

        // 图片拖动X轴偏移
        private double _realTimeImageTranslateXRAB;
        public double RealTimeImageTranslateXRAB
        {
            get => _realTimeImageTranslateXRAB;
            set { _realTimeImageTranslateXRAB = value; OnPropertyChanged(); }
        }

        // 图片拖动Y轴偏移
        private double _realTimeImageTranslateYRAB;
        public double RealTimeImageTranslateYRAB
        {
            get => _realTimeImageTranslateYRAB;
            set { _realTimeImageTranslateYRAB = value; OnPropertyChanged(); }
        }
        public double RealTimeImageScaleXRAB
        {
            get => _realTimeImageScaleXRAB;
            set { _realTimeImageScaleXRAB = value; OnPropertyChanged(); }
        }
        // 是否正在拖动
        private bool _isImageDraggingRAB;
        private System.Windows.Point _startMousePointRAB; // 拖动起始鼠标位置
        private double _startTranslateXRAB; // 拖动起始X偏移
        private double _startTranslateYRAB; // 拖动起始Y偏移
        #endregion
        #region RCD
        // 鼠标按下命令（开始拖动）
        public ICommand ImageMouseDownCommandRCD { get; }
        // 鼠标移动命令（拖动中）
        public ICommand ImageMouseMoveCommandRCD { get; }
        // 鼠标松开命令（结束拖动）
        public ICommand ImageMouseUpCommandRCD { get; }
        // 鼠标滚轮缩放
        public ICommand ZoomRealTimeImageCommandRCD { get; }
        // 放大按钮
        public ICommand ZoomInRealTimeCommandRCD { get; }
        // 缩小按钮
        public ICommand ZoomOutRealTimeCommandRCD { get; }
        // 重置缩放
        public ICommand ResetRealTimeImageZoomCommandRCD { get; }
        // 选择结果路径的命令
        //public ICommand SelectResultImagePath { get; }
        private const double ZoomStepRCD = 0.1;
        private const double MinScaleRCD = 0.2;
        private const double MaxScaleRCD = 5.0;
        private double _realTimeImageScaleXRCD = 1.0;
        private double _realTimeImageScaleYRCD = 1.0;
        public double RealTimeImageScaleYRCD
        {
            get => _realTimeImageScaleYRCD;
            set { _realTimeImageScaleYRCD = value; OnPropertyChanged(); }
        }

        // 图片拖动X轴偏移
        private double _realTimeImageTranslateXRCD;
        public double RealTimeImageTranslateXRCD
        {
            get => _realTimeImageTranslateXRCD;
            set { _realTimeImageTranslateXRCD = value; OnPropertyChanged(); }
        }

        // 图片拖动Y轴偏移
        private double _realTimeImageTranslateYRCD;
        public double RealTimeImageTranslateYRCD
        {
            get => _realTimeImageTranslateYRCD;
            set { _realTimeImageTranslateYRCD = value; OnPropertyChanged(); }
        }
        public double RealTimeImageScaleXRCD
        {
            get => _realTimeImageScaleXRCD;
            set { _realTimeImageScaleXRCD = value; OnPropertyChanged(); }
        }
        // 是否正在拖动
        private bool _isImageDraggingRCD;
        private System.Windows.Point _startMousePointRCD; // 拖动起始鼠标位置
        private double _startTranslateXRCD; // 拖动起始X偏移
        private double _startTranslateYRCD; // 拖动起始Y偏移
        #endregion
        #endregion

        #region 数据检索模块
        private int _currentPage = 1;
        private int _pageSize = 100;
        private bool _isQuerying;
        private DateTime? _endDateTime;
        private DateTime? _startDateTime;
        private string? _selectedAbnormalType;
        private int _totalCount;
        private CancellationTokenSource? _autoLogoutCancellationTokenSource;
        public ObservableCollection<Dictionary<string, object>> PagedAbnormalDataList { get; set; } = new();
        // 分页信息文本
        public string PageInfoText => $"第 {CurrentPage} 页 / 共 {TotalPages} 页";
        // 总页数
        public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
        public bool CanPreviousPage => CurrentPage > 1;
        public bool CanNextPage => CurrentPage < TotalPages;
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand QueryCommand { get; set; }
        public ICommand ExportCommand { get; set; }

        // 异常类型下拉列表
        public ObservableCollection<string> AbnormalTypeList { get; set; } = new ObservableCollection<string>
        {
        "全部","无异常", "正极少片", "正极多片",  "OH不良", "负极差不良",  "负极多片", "负极少片", "负极边缘间隙不良","正极边缘间隙不良","正极中间间隙不良","负极压伤","正极Y方向掉片"
        };
        public DateTime? StartDateTime
        {
            get => _startDateTime;
            set { _startDateTime = value; OnPropertyChanged(); }
        }
        public DateTime? EndDateTime
        {
            get => _endDateTime;
            set { _endDateTime = value; OnPropertyChanged(); }
        }

        public bool IsQuerying
        {
            get => _isQuerying;
            set { _isQuerying = value; OnPropertyChanged(); }
        }
        public string? SelectedAbnormalType
        {
            get => _selectedAbnormalType;
            set { _selectedAbnormalType = value; OnPropertyChanged(); }
        }
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(); // 通知 TotalCount 变化
                    OnPropertyChanged(nameof(TotalPages)); // 连锁通知 TotalPages
                    OnPropertyChanged(nameof(CanNextPage)); // 连锁通知 CanNextPage
                    OnPropertyChanged(nameof(PageInfoText));
                }
            }
        }
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanPreviousPage));
                    OnPropertyChanged(nameof(CanNextPage));
                    OnPropertyChanged(nameof(PageInfoText));
                }
            }
        }
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (_pageSize != value)
                {
                    _pageSize = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(CanNextPage));
                    OnPropertyChanged(nameof(PageInfoText));
                }
            }
        }

        //上一页
        private async void ExecutePreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                await LoadPageDataAsync(CurrentPage);
            }
        }
        //下一页
        private async void ExecuteNextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                await LoadPageDataAsync(CurrentPage);
            }
        }
        #endregion

        #region 字段和属性
        private int okDetectionNum;
        private int ngDetectionNum;
        private int ZJDP;
        private int ZJSP;
        private int FJDP;
        private int FJSP;
        private int OHBL;
        private int CPBL;
        private int ZJYDP;
        private int FJBYJXBL;
        private int ZJBYJXBL;
        private int ZJZJJXBL;
        private int FJYS;
        private string? ratio_ZJDP;
        private string? ratio_ZJSP;
        private string? ratio_FJDP;
        private string? ratio_FJSP;
        private string? ratio_JXBL;
        private string? ratio_OHBL;
        private string? ratio_CPBL;
        private string? ratio_ZJYDP;
        private string? ratio_ZFJXNG;
        private string? ratio_FJBYJXBL;
        private string? ratio_ZJBYJXBL;
        private string? ratio_FJYS;
        private string? ratio_ZJZJJXBL;
        private string _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "set_config_CTOL.json");
        private int _historyTotalCount = 0;
        private int _historyOKCount = 0;
        private int _historyNGCount = 0;
        private int _historyCount_ZJDP;
        private int _historyCount_ZJSP;
        private int _historyCount_FJDP;
        private int _historyCount_FJSP;
        private int _historyCount_OHBL;
        private int _historyCount_CPBL;
        private int _historyCount_ZJYDP;
        private int _historyCount_FJBYJXBL;
        private int _historyCount_ZJBYJXBL;
        private int _historyCount_ZJZJJXBL;
        private int _historyCount_FJYS;
        private readonly object _statisticsLock = new();
        private readonly string _imeresultRoot;
        private readonly string _archiveTargetRoot;
        private DefectFileMoverService _fileMoverService;
        private readonly SimpleDailyTaskService _dailyTaskService;
        private readonly ILoggerService _logger;
        private readonly IProjectSettingRepository _projectsetting;
        private readonly VM_UserCenter _userCenterViewModel;
        private readonly INavigationService _navigationService;

        // 结果存储路径属性
        private string? _selectedResultImagePath;
        public string? SelectedResultImagePath
        {
            get => _selectedResultImagePath;
            set
            {
                _selectedResultImagePath = value;
                OnPropertyChanged();
            }
        }
        public int Count_ZJZJJXBL
        {
            get => ZJZJJXBL;
            set { ZJZJJXBL = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_FJYS
        {
            get => FJYS;
            set { FJYS = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_ZJBYJXBL
        {
            get => ZJBYJXBL;
            set { ZJBYJXBL = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_FJBYJXBL
        {
            get => FJBYJXBL;
            set { FJBYJXBL = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_ZJDP
        {
            get => ZJDP;
            set { ZJDP = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_ZJSP
        {
            get => ZJSP;
            set { ZJSP = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_FJDP
        {
            get => FJDP;
            set { FJDP = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_FJSP
        {
            get => FJSP;
            set { FJSP = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_OHBL
        {
            get => OHBL;
            set { OHBL = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_CPBL
        {
            get => CPBL;
            set { CPBL = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }
        public int Count_ZJYDP
        {
            get => ZJYDP;
            set { ZJYDP = value; OnPropertyChanged(); UpdateRiskRatios(); SaveConfig(); }
        }

        public string? Ratio_ZJDP
        {
            get => ratio_ZJDP;
            set { ratio_ZJDP = value; OnPropertyChanged(); }
        }
        public string? Ratio_ZJSP
        {
            get => ratio_ZJSP;
            set { ratio_ZJSP = value; OnPropertyChanged(); }
        }
        public string? Ratio_FJDP
        {
            get => ratio_FJDP;
            set { ratio_FJDP = value; OnPropertyChanged(); }
        }
        public string? Ratio_FJSP
        {
            get => ratio_FJSP;
            set { ratio_FJSP = value; OnPropertyChanged(); }
        }
        public string? Ratio_JXBL
        {
            get => ratio_JXBL;
            set { ratio_JXBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_OHBL
        {
            get => ratio_OHBL;
            set { ratio_OHBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_CPBL
        {
            get => ratio_CPBL;
            set { ratio_CPBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_ZJYDP
        {
            get => ratio_ZJYDP;
            set { ratio_ZJYDP = value; OnPropertyChanged(); }
        }
        public string? Ratio_ZFJXNG
        {
            get => ratio_ZFJXNG;
            set { ratio_ZFJXNG = value; OnPropertyChanged(); }
        }
        public string? Ratio_FJBYJXBL
        {
            get => ratio_FJBYJXBL;
            set { ratio_FJBYJXBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_ZJBYJXBL
        {
            get => ratio_ZJBYJXBL;
            set { ratio_ZJBYJXBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_ZJZJJXBL
        {
            get => ratio_ZJZJJXBL;
            set { ratio_ZJZJJXBL = value; OnPropertyChanged(); }
        }
        public string? Ratio_FJYS
        {
            get => ratio_FJYS;
            set { ratio_FJYS = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// OK数
        /// </summary>
        public int OKDetectionNum
        {
            get => okDetectionNum;
            set
            {
                okDetectionNum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDetectionNum));
                OnPropertyChanged(nameof(OKRate));
                OnPropertyChanged(nameof(NGRate));
                UpdatePieChartData();
                SaveConfig();
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
                ngDetectionNum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalDetectionNum));
                OnPropertyChanged(nameof(OKRate));
                OnPropertyChanged(nameof(NGRate));
                UpdatePieChartData();
                SaveConfig();

            }
        }
        // 总检测数
        public int TotalDetectionNum => OKDetectionNum + NGDetectionNum;
        /// <summary>
        /// 一次优率
        /// </summary>
        public string OKRate
        {
            get
            {
                if (TotalDetectionNum == 0)
                    return "0.0%";
                double rate = (double)OKDetectionNum / TotalDetectionNum * 100;
                return $"{rate:F2}%";
            }
        }
        public string NGRate
        {
            get
            {
                if (TotalDetectionNum == 0)
                    return "0.0%";
                double rate = (double)NGDetectionNum / TotalDetectionNum * 100;
                return $"{rate:F2}%";
            }
        }
        #endregion

        #region 1#-47#阈值属性
        /// <summary>
        /// Gap1标准（初始值：0.5）
        /// </summary>
        private double gap1Limit;
        public double Gap1Limit
        {
            get => gap1Limit;
            set
            {
                gap1Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap2标准（初始值：2.75）
        /// </summary>
        private double gap2Limit;
        public double Gap2Limit
        {
            get => gap2Limit;
            set
            {
                gap2Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap3标准（初始值：2.2）
        /// </summary>
        private double gap3Limit;
        public double Gap3Limit
        {
            get => gap3Limit;
            set
            {
                gap3Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap4标准（初始值：0.7）
        /// </summary>
        private double gap4Limit;
        public double Gap4Limit
        {
            get => gap4Limit;
            set
            {
                gap4Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap5标准（初始值：0.45）
        /// </summary>
        private double gap5Limit;
        public double Gap5Limit
        {
            get => gap5Limit;
            set
            {
                gap5Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap6标准（初始值：0.35）
        /// </summary>
        private double gap6Limit;
        public double Gap6Limit
        {
            get => gap6Limit;
            set
            {
                gap6Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap7标准（初始值：0.5）
        /// </summary>
        private double gap7Limit;
        public double Gap7Limit
        {
            get => gap7Limit;
            set
            {
                gap7Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap8标准（初始值：2.4）
        /// </summary>
        private double gap8Limit;
        public double Gap8Limit
        {
            get => gap8Limit;
            set
            {
                gap8Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap9标准（初始值：2.4）
        /// </summary>
        private double gap9Limit;
        public double Gap9Limit
        {
            get => gap9Limit;
            set
            {
                gap9Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap10标准（初始值：0.7）
        /// </summary>
        private double gap10Limit;
        public double Gap10Limit
        {
            get => gap10Limit;
            set
            {
                gap10Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap11标准（初始值：0.45）
        /// </summary>
        private double gap11Limit;
        public double Gap11Limit
        {
            get => gap11Limit;
            set
            {
                gap11Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap12标准（初始值：0.35）
        /// </summary>
        private double gap12Limit;
        public double Gap12Limit
        {
            get => gap12Limit;
            set
            {
                gap12Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap13标准（初始值：0.5）
        /// </summary>
        private double gap13Limit;
        public double Gap13Limit
        {
            get => gap13Limit;
            set
            {
                gap13Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap14标准（初始值：2）
        /// </summary>
        private double gap14Limit;
        public double Gap14Limit
        {
            get => gap14Limit;
            set
            {
                gap14Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap15标准（初始值：0.7）
        /// </summary>
        private double gap15Limit;
        public double Gap15Limit
        {
            get => gap15Limit;
            set
            {
                gap15Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap16标准（初始值：0.45）
        /// </summary>
        private double gap16Limit;
        public double Gap16Limit
        {
            get => gap16Limit;
            set
            {
                gap16Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap17标准（初始值：0.35）
        /// </summary>
        private double gap17Limit;
        public double Gap17Limit
        {
            get => gap17Limit;
            set
            {
                gap17Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap18标准（初始值：0.5）
        /// </summary>
        private double gap18Limit;
        public double Gap18Limit
        {
            get => gap18Limit;
            set
            {
                gap18Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap19标准（初始值：2.4）
        /// </summary>
        private double gap19Limit;
        public double Gap19Limit
        {
            get => gap19Limit;
            set
            {
                gap19Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap20标准（初始值：0.7）
        /// </summary>
        private double gap20Limit;
        public double Gap20Limit
        {
            get => gap20Limit;
            set
            {
                gap20Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap21标准（初始值：0.45）
        /// </summary>
        private double gap21Limit;
        public double Gap21Limit
        {
            get => gap21Limit;
            set
            {
                gap21Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap22标准（初始值：0.35）
        /// </summary>
        private double gap22Limit;
        public double Gap22Limit
        {
            get => gap22Limit;
            set
            {
                gap22Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap23标准（初始值：0.5）
        /// </summary>
        private double gap23Limit;
        public double Gap23Limit
        {
            get => gap23Limit;
            set
            {
                gap23Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap24标准（初始值：2.75）
        /// </summary>
        private double gap24Limit;
        public double Gap24Limit
        {
            get => gap24Limit;
            set
            {
                gap24Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap25标准（初始值：0.7）
        /// </summary>
        private double gap25Limit;
        public double Gap25Limit
        {
            get => gap25Limit;
            set
            {
                gap25Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap26标准（初始值：0.45）
        /// </summary>
        private double gap26Limit;
        public double Gap26Limit
        {
            get => gap26Limit;
            set
            {
                gap26Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap27标准（初始值：0.35）
        /// </summary>
        private double gap27Limit;
        public double Gap27Limit
        {
            get => gap27Limit;
            set
            {
                gap27Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap28标准（初始值：0.5）
        /// </summary>
        private double gap28Limit;
        public double Gap28Limit
        {
            get => gap28Limit;
            set
            {
                gap28Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap29标准（初始值：2.4）
        /// </summary>
        private double gap29Limit;
        public double Gap29Limit
        {
            get => gap29Limit;
            set
            {
                gap29Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap30标准（初始值：0.7）
        /// </summary>
        private double gap30Limit;
        public double Gap30Limit
        {
            get => gap30Limit;
            set
            {
                gap30Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap31标准（初始值：0.45）
        /// </summary>
        private double gap31Limit;
        public double Gap31Limit
        {
            get => gap31Limit;
            set
            {
                gap31Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap32标准（初始值：0.35）
        /// </summary>
        private double gap32Limit;
        public double Gap32Limit
        {
            get => gap32Limit;
            set
            {
                gap32Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap33标准（初始值：0.5）
        /// </summary>
        private double gap33Limit;
        public double Gap33Limit
        {
            get => gap33Limit;
            set
            {
                gap33Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap34标准（初始值：2）
        /// </summary>
        private double gap34Limit;
        public double Gap34Limit
        {
            get => gap34Limit;
            set
            {
                gap34Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap35标准（初始值：2.2）
        /// </summary>
        private double gap35Limit;
        public double Gap35Limit
        {
            get => gap35Limit;
            set
            {
                gap35Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap36标准（初始值：0.7）
        /// </summary>
        private double gap36Limit;
        public double Gap36Limit
        {
            get => gap36Limit;
            set
            {
                gap36Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap37标准（初始值：0.45）
        /// </summary>
        private double gap37Limit;
        public double Gap37Limit
        {
            get => gap37Limit;
            set
            {
                gap37Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap38标准（初始值：0.35）
        /// </summary>
        private double gap38Limit;
        public double Gap38Limit
        {
            get => gap38Limit;
            set
            {
                gap38Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap39标准（初始值：0.5）
        /// </summary>
        private double gap39Limit;
        public double Gap39Limit
        {
            get => gap39Limit;
            set
            {
                gap39Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap40标准（初始值：2.4）
        /// </summary>
        private double gap40Limit;
        public double Gap40Limit
        {
            get => gap40Limit;
            set
            {
                gap40Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap41标准（初始值：2.4）
        /// </summary>
        private double gap41Limit;
        public double Gap41Limit
        {
            get => gap41Limit;
            set
            {
                gap41Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap42标准（初始值：0.7）
        /// </summary>
        private double gap42Limit;
        public double Gap42Limit
        {
            get => gap42Limit;
            set
            {
                gap42Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap43标准（初始值：0.45）
        /// </summary>
        private double gap43Limit;
        public double Gap43Limit
        {
            get => gap43Limit;
            set
            {
                gap43Limit = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gap44标准（初始值：0.35）
        /// </summary>
        private double gap44Limit;
        public double Gap44Limit
        {
            get => gap44Limit;
            set
            {
                gap44Limit = value;
                OnPropertyChanged();
            }
        }
        private double gap45Limit;
        public double Gap45Limit
        {
            get => gap45Limit;
            set
            {
                gap45Limit = value;
                OnPropertyChanged();
            }
        }
        private double gap46Limit;
        public double Gap46Limit
        {
            get => gap46Limit;
            set
            {
                gap46Limit = value;
                OnPropertyChanged();
            }
        }
        private double gap47Limit;
        public double Gap47Limit
        {
            get => gap47Limit;
            set
            {
                gap47Limit = value;
                OnPropertyChanged();
            }
        }

        private double _tempGap1Limit; public double TempGap1Limit { get => _tempGap1Limit; set => SetProperty(ref _tempGap1Limit, value); }
        private double _tempGap2Limit; public double TempGap2Limit { get => _tempGap2Limit; set => SetProperty(ref _tempGap2Limit, value); }
        private double _tempGap3Limit; public double TempGap3Limit { get => _tempGap3Limit; set => SetProperty(ref _tempGap3Limit, value); }
        private double _tempGap4Limit; public double TempGap4Limit { get => _tempGap4Limit; set => SetProperty(ref _tempGap4Limit, value); }
        private double _tempGap5Limit; public double TempGap5Limit { get => _tempGap5Limit; set => SetProperty(ref _tempGap5Limit, value); }
        private double _tempGap6Limit; public double TempGap6Limit { get => _tempGap6Limit; set => SetProperty(ref _tempGap6Limit, value); }
        private double _tempGap7Limit; public double TempGap7Limit { get => _tempGap7Limit; set => SetProperty(ref _tempGap7Limit, value); }
        private double _tempGap8Limit; public double TempGap8Limit { get => _tempGap8Limit; set => SetProperty(ref _tempGap8Limit, value); }
        private double _tempGap9Limit; public double TempGap9Limit { get => _tempGap9Limit; set => SetProperty(ref _tempGap9Limit, value); }
        private double _tempGap10Limit; public double TempGap10Limit { get => _tempGap10Limit; set => SetProperty(ref _tempGap10Limit, value); }
        private double _tempGap11Limit; public double TempGap11Limit { get => _tempGap11Limit; set => SetProperty(ref _tempGap11Limit, value); }
        private double _tempGap12Limit; public double TempGap12Limit { get => _tempGap12Limit; set => SetProperty(ref _tempGap12Limit, value); }
        private double _tempGap13Limit; public double TempGap13Limit { get => _tempGap13Limit; set => SetProperty(ref _tempGap13Limit, value); }
        private double _tempGap14Limit; public double TempGap14Limit { get => _tempGap14Limit; set => SetProperty(ref _tempGap14Limit, value); }
        private double _tempGap15Limit; public double TempGap15Limit { get => _tempGap15Limit; set => SetProperty(ref _tempGap15Limit, value); }
        private double _tempGap16Limit; public double TempGap16Limit { get => _tempGap16Limit; set => SetProperty(ref _tempGap16Limit, value); }
        private double _tempGap17Limit; public double TempGap17Limit { get => _tempGap17Limit; set => SetProperty(ref _tempGap17Limit, value); }
        private double _tempGap18Limit; public double TempGap18Limit { get => _tempGap18Limit; set => SetProperty(ref _tempGap18Limit, value); }
        private double _tempGap19Limit; public double TempGap19Limit { get => _tempGap19Limit; set => SetProperty(ref _tempGap19Limit, value); }
        private double _tempGap20Limit; public double TempGap20Limit { get => _tempGap20Limit; set => SetProperty(ref _tempGap20Limit, value); }
        private double _tempGap21Limit; public double TempGap21Limit { get => _tempGap21Limit; set => SetProperty(ref _tempGap21Limit, value); }
        private double _tempGap22Limit; public double TempGap22Limit { get => _tempGap22Limit; set => SetProperty(ref _tempGap22Limit, value); }
        private double _tempGap23Limit; public double TempGap23Limit { get => _tempGap23Limit; set => SetProperty(ref _tempGap23Limit, value); }
        private double _tempGap24Limit; public double TempGap24Limit { get => _tempGap24Limit; set => SetProperty(ref _tempGap24Limit, value); }
        private double _tempGap25Limit; public double TempGap25Limit { get => _tempGap25Limit; set => SetProperty(ref _tempGap25Limit, value); }
        private double _tempGap26Limit; public double TempGap26Limit { get => _tempGap26Limit; set => SetProperty(ref _tempGap26Limit, value); }
        private double _tempGap27Limit; public double TempGap27Limit { get => _tempGap27Limit; set => SetProperty(ref _tempGap27Limit, value); }
        private double _tempGap28Limit; public double TempGap28Limit { get => _tempGap28Limit; set => SetProperty(ref _tempGap28Limit, value); }
        private double _tempGap29Limit; public double TempGap29Limit { get => _tempGap29Limit; set => SetProperty(ref _tempGap29Limit, value); }
        private double _tempGap30Limit; public double TempGap30Limit { get => _tempGap30Limit; set => SetProperty(ref _tempGap30Limit, value); }
        private double _tempGap31Limit; public double TempGap31Limit { get => _tempGap31Limit; set => SetProperty(ref _tempGap31Limit, value); }
        private double _tempGap32Limit; public double TempGap32Limit { get => _tempGap32Limit; set => SetProperty(ref _tempGap32Limit, value); }
        private double _tempGap33Limit; public double TempGap33Limit { get => _tempGap33Limit; set => SetProperty(ref _tempGap33Limit, value); }
        private double _tempGap34Limit; public double TempGap34Limit { get => _tempGap34Limit; set => SetProperty(ref _tempGap34Limit, value); }
        private double _tempGap35Limit; public double TempGap35Limit { get => _tempGap35Limit; set => SetProperty(ref _tempGap35Limit, value); }
        private double _tempGap36Limit; public double TempGap36Limit { get => _tempGap36Limit; set => SetProperty(ref _tempGap36Limit, value); }
        private double _tempGap37Limit; public double TempGap37Limit { get => _tempGap37Limit; set => SetProperty(ref _tempGap37Limit, value); }
        private double _tempGap38Limit; public double TempGap38Limit { get => _tempGap38Limit; set => SetProperty(ref _tempGap38Limit, value); }
        private double _tempGap39Limit; public double TempGap39Limit { get => _tempGap39Limit; set => SetProperty(ref _tempGap39Limit, value); }
        private double _tempGap40Limit; public double TempGap40Limit { get => _tempGap40Limit; set => SetProperty(ref _tempGap40Limit, value); }
        private double _tempGap41Limit; public double TempGap41Limit { get => _tempGap41Limit; set => SetProperty(ref _tempGap41Limit, value); }
        private double _tempGap42Limit; public double TempGap42Limit { get => _tempGap42Limit; set => SetProperty(ref _tempGap42Limit, value); }
        private double _tempGap43Limit; public double TempGap43Limit { get => _tempGap43Limit; set => SetProperty(ref _tempGap43Limit, value); }
        private double _tempGap44Limit; public double TempGap44Limit { get => _tempGap44Limit; set => SetProperty(ref _tempGap44Limit, value); }
        private double _tempGap45Limit; public double TempGap45Limit { get => _tempGap45Limit; set => SetProperty(ref _tempGap45Limit, value); }
        private double _tempGap46Limit; public double TempGap46Limit { get => _tempGap46Limit; set => SetProperty(ref _tempGap46Limit, value); }
        private double _tempGap47Limit; public double TempGap47Limit { get => _tempGap47Limit; set => SetProperty(ref _tempGap47Limit, value); }
        #endregion

        /// OKNG统计饼图
        public PlotModel? PieModel { get; set; }
        // 清空列表命令
        public ICommand ClearListCommand { get; private set; }
        // 统计清零命令
        public ICommand ClearStatisticsCommand { get; private set; }
        public ICommand SaveSettingsCommand { get; private set; }
        // 重置按钮命令
        public ICommand ResetSettingsCommand { get; private set; }
        private List<dynamic> _fullDetectionDataList = new List<dynamic>();
        // 检测数据集合/动态对象集合
        private ObservableCollection<dynamic> _detectionDataList;
        public ObservableCollection<dynamic> DetectionDataList
        {
            get => _detectionDataList;
            set { _detectionDataList = value; OnPropertyChanged(); }
        }

        // 线程安全锁对象
        private readonly object _countLock = new object();
        private readonly ConcurrentQueue<string> ImagePathQueueLAB = new ConcurrentQueue<string>(); // LAB
        private readonly ConcurrentQueue<string> ImagePathQueueLCD = new ConcurrentQueue<string>(); // LCD
        private readonly ConcurrentQueue<string> ImagePathQueueRAB = new ConcurrentQueue<string>(); // RAB
        private readonly ConcurrentQueue<string> ImagePathQueueRCD = new ConcurrentQueue<string>(); // RCD

        // 各路径队列锁（确保入队/出队线程安全）
        private readonly object _queueLockLAB = new object();
        private readonly object _queueLockLCD = new object();
        private readonly object _queueLockRAB = new object();
        private readonly object _queueLockRCD = new object();

        // 用于缓存每个路径的文件列表
        private readonly Dictionary<string, List<string>> pathFileBuffers = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, object> pathLocks = new Dictionary<string, object>();
        // 定义线程安全的队列字典：key为"station_条码"，value为该条码的队列
        private ConcurrentDictionary<string, ConcurrentQueue<string>> _barcodeQueues = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private ConcurrentDictionary<string, object> _barcodeQueueLocks = new ConcurrentDictionary<string, object>();
        /// 外层键：工位名称，内层键："Y1"/"Y2"/"Y3"/"Y4"，值：对应的数据数组
        private Dictionary<string, Dictionary<string, double[]>> stationYDataDict = new Dictionary<string, Dictionary<string, double[]>>();
        //线程锁，多工位并行时
        private object stationDataLocker = new object();
        /// <summary>
        /// 是否处于任何检测状态
        /// </summary>
        public bool IsInAnyDetection
        {
            get => !(IsOnlineDetectionEnabledLAB || IsOnlineDetectionEnabledLCD || IsOnlineDetectionEnabledRAB || IsOnlineDetectionEnabledRCD);
        }

        private bool _isThreadTerminatingLAB = false;
        private bool _isThreadTerminatingLCD = false;
        private bool _isThreadTerminatingRAB = false;
        private bool _isThreadTerminatingRCD = false;
        public ICommand SelectMonitorFolderCommandLAB { get; }
        public ICommand SelectMonitorFolderCommandLCD { get; }
        public ICommand SelectMonitorFolderCommandRAB { get; }
        public ICommand SelectMonitorFolderCommandRCD { get; }
        private ImageSource? _currentResultImageLAB;
        public ImageSource? CurrentResultImageLAB
        {
            get => _currentResultImageLAB;
            set { _currentResultImageLAB = value; OnPropertyChanged(); }
        }

        private ImageSource? _currentResultImageLCD;
        public ImageSource? CurrentResultImageLCD
        {
            get => _currentResultImageLCD;
            set { _currentResultImageLCD = value; OnPropertyChanged(); }
        }

        private ImageSource? _currentResultImageRAB;
        public ImageSource? CurrentResultImageRAB
        {
            get => _currentResultImageRAB;
            set { _currentResultImageRAB = value; OnPropertyChanged(); }
        }

        private ImageSource? _currentResultImageRCD;
        public ImageSource? CurrentResultImageRCD
        {
            get => _currentResultImageRCD;
            set { _currentResultImageRCD = value; OnPropertyChanged(); }
        }

        //原始HObject图像
        private HObject? _detectionResultImageLAB;
        private HObject? _detectionResultImageLCD;
        private HObject? _detectionResultImageRAB;
        private HObject? _detectionResultImageRCD;

        //当前在线监控的工位
        private string? _currentMonitoringStation;
        public string? CurrentMonitoringStation
        {
            get => _currentMonitoringStation;
            set { _currentMonitoringStation = value; OnPropertyChanged(); }
        }

        // 文件就绪重试配置
        private const int FileReadyMaxRetry = 30; // 最多重试30次
        private const int FileReadyRetryDelay = 1000; // 每次重试间隔1秒
        private Dictionary<string, HObject> _firstDetectionImageCache = new Dictionary<string, HObject>();
        private object _cacheLock = new object();

        //LAB
        private FileSystemWatcher? _onlineMonitorWatcherLAB;
        private readonly ConcurrentQueue<string> _safeDetectQueueLAB = new ConcurrentQueue<string>();
        private bool _isQueueRunningLAB;
        private readonly SemaphoreSlim _semaphoreLAB = new SemaphoreSlim(MaxConcurrentLAB); // LAB并发控制
        private const int MaxConcurrentLAB = 1; //LAB最大并发数
        // 等待句柄,唤醒线程
        private readonly AutoResetEvent _queueEventLAB = new AutoResetEvent(false);  // 初始为未触发状态

        //LCD
        private FileSystemWatcher? _onlineMonitorWatcherLCD;
        private readonly ConcurrentQueue<string> _safeDetectQueueLCD = new ConcurrentQueue<string>();
        private bool _isQueueRunningLCD;
        private readonly SemaphoreSlim _semaphoreLCD = new SemaphoreSlim(MaxConcurrentLCD);
        private const int MaxConcurrentLCD = 1;
        private readonly AutoResetEvent _queueEventLCD = new AutoResetEvent(false);

        //RAB
        private FileSystemWatcher? _onlineMonitorWatcherRAB;
        private readonly ConcurrentQueue<string> _safeDetectQueueRAB = new ConcurrentQueue<string>();
        private bool _isQueueRunningRAB;
        private readonly SemaphoreSlim _semaphoreRAB = new SemaphoreSlim(MaxConcurrentRAB);
        private const int MaxConcurrentRAB = 1;
        private readonly AutoResetEvent _queueEventRAB = new AutoResetEvent(false);

        //RCD
        private FileSystemWatcher? _onlineMonitorWatcherRCD;
        private readonly ConcurrentQueue<string> _safeDetectQueueRCD = new ConcurrentQueue<string>();
        private bool _isQueueRunningRCD;
        private readonly SemaphoreSlim _semaphoreRCD = new SemaphoreSlim(MaxConcurrentRCD);
        private const int MaxConcurrentRCD = 1;
        private readonly AutoResetEvent _queueEventRCD = new AutoResetEvent(false);
        private readonly object _stationCountLock = new object();
        private int _runningStationsCount = 0;
        // 记录每个条码的最后活动时间
        private readonly Dictionary<string, DateTime> _barcodeLastActivityTime = new Dictionary<string, DateTime>();
        // 保护条码时间字典的锁
        private readonly object _barcodeTimeLock = new object();
        // 定时清理超时条码的Timer
        private System.Timers.Timer _timeoutCleanupTimer;

        #region LAB在线监控参数
        private string? _selectedMonitorFolderPathLAB;
        public string? SelectedMonitorFolderPathLAB
        {
            get => _selectedMonitorFolderPathLAB;
            set
            {
                _selectedMonitorFolderPathLAB = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
        private bool _isOnlineDetectionEnabledLAB;
        public bool IsInAnyDetectionLAB
        {
            get => !(IsOnlineDetectionEnabledLAB);
        }
        public bool IsOnlineDetectionEnabledLAB
        {
            get => _isOnlineDetectionEnabledLAB;
            set
            {

                if (_isOnlineDetectionEnabledLAB == value) return;

                //在线检测开启
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathLAB) || !Directory.Exists(SelectedMonitorFolderPathLAB))
                    {
                        System.Windows.MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledLAB = false;
                        OnPropertyChanged();
                        return;
                    }

                    StartOnlineMonitoringLAB();
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[LAB-在线检测] 开关已开启");
                }
                //在线检测关闭
                else
                {
                    StopOnlineMonitoringLAB();
                    AddLog("[LAB] 已停止监控文件夹");
                }

                _isOnlineDetectionEnabledLAB = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsInAnyDetectionLAB));
            }
        }
        private void StartOnlineMonitoringLAB()
        {
            CurrentMonitoringStation = "LAB";

            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathLAB) || !Directory.Exists(SelectedMonitorFolderPathLAB))
            {
                AddLog("[LAB] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabledLAB = false;
                System.Windows.MessageBox.Show("LAB监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _isThreadTerminatingLAB = false;
            if (_onlineMonitorWatcherLAB != null)
            {
                _onlineMonitorWatcherLAB.Created -= OnOnlineFileCreatedLAB; // 先取消旧绑定
                _onlineMonitorWatcherLAB.Dispose();
                _onlineMonitorWatcherLAB = null;
            }
            _onlineMonitorWatcherLAB = new FileSystemWatcher(SelectedMonitorFolderPathLAB)
            {
                Filter = "*.png",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _onlineMonitorWatcherLAB.Created += OnOnlineFileCreatedLAB; // 绑定LAB文件事件
            AddLog($"[LAB] 已启动监控，路径：{SelectedMonitorFolderPathLAB}");
        }
        private void OnOnlineFileCreatedLAB(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            //检查文件所在目录是否在名为 "OK" 的文件夹之下 
            string? currentDirectory = Path.GetDirectoryName(e.FullPath);
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                bool isUnderOKFolder = false;
                DirectoryInfo dirInfo = new DirectoryInfo(currentDirectory);
                while (dirInfo.Parent != null)
                {
                    if (string.Equals(dirInfo.Name, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        isUnderOKFolder = true;
                        break;
                    }
                    dirInfo = dirInfo.Parent;
                }
                if (!isUnderOKFolder)
                {
                    return;
                }
            }
            //排除系统缩略图缓存文件Thumbs.db
            if (string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[LAB-跳过系统文件] 自动忽略Thumbs.db：{fileName}");
                return;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[LAB-跳过] 非图片格式：{fileName}");
                        return;
                    }
                    //仅处理文件名包含"_origin"的图片
                    if (!fileName.Contains("_origin"))
                    {
                        // AddLog($"[LAB-跳过] 非origin图片：{fileName}");
                        return;
                    }
                    bool isFileReady = false;
                    int retryCount = 0;
                    while (retryCount < FileReadyMaxRetry)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        retryCount++;
                        await Task.Delay(FileReadyRetryDelay);
                        if (!File.Exists(e.FullPath))
                        {
                            AddLog($"[LAB-丢弃] {fileName} 在重试期间被删除");
                            return;
                        }
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[LAB-丢弃] {fileName} 超过最大重试次数（{FileReadyMaxRetry}次）仍未就绪");
                        return;
                    }

                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[LAB-丢弃] {fileName} 已被删除");
                        return;
                    }

                    // 入队处理
                    EnqueueTaskLAB(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[LAB-文件处理异常] {e.FullPath}：{ex.Message}，堆栈：{ex.StackTrace}");
                    // ResetTaskQueueIfNeeded();
                }
            });
        }
        public void EnqueueTaskLAB(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                AddLog("[LAB-入队] 无效路径，跳过入队");
                return;
            }


            _safeDetectQueueLAB.Enqueue(filePath);

            if (!_isQueueRunningLAB)
            {
                //AddLog("[B工位-入队] 线程未运行，强制启动...");
                StartConcurrentProcessingLAB();
            }
            _queueEventLAB.Set(); // 唤醒A工位等待线程
            //AddLog($"[A工位-入队] 新任务入队，发送唤醒信号：{Path.GetFileName(filePath)}");
        }
        private void StartConcurrentProcessingLAB()
        {
            if (!_isQueueRunningLAB)
            {
                _isQueueRunningLAB = true;
                _ = ProcessQueueConcurrentLAB();
            }
        }
        private async Task ProcessQueueConcurrentLAB()
        {
            await _semaphoreLAB.WaitAsync();
            try
            {
                AddLog($"[LAB线程启动] 线程ID：{Thread.CurrentThread.ManagedThreadId}");
                while (!_isThreadTerminatingLAB)
                {
                    while (true)
                    {
                        string? filePath = null;

                        if (_safeDetectQueueLAB.TryDequeue(out filePath))
                        {
                            //AddLog($"[LAB-出队成功] 文件：{Path.GetFileName(filePath)}，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                        }
                        else
                        {
                            break; // 队列空，退出内层循环
                        }

                        if (filePath == null) break;

                        if (!File.Exists(filePath))
                        {
                            AddLog($"[LAB-跳过] 文件已删除：{Path.GetFileName(filePath)}");
                            continue;
                        }
                        HObject? resultImage = null;
                        try
                        {

                            //AddLog($"[LAB-开始检测] 文件：{Path.GetFileName(filePath)}");
                            var (ctCode, tempResultImage) = GapDetection(filePath, "LAB");
                            resultImage = tempResultImage;

                            if (ctCode == "Internal_Error" || resultImage == null)
                            {
                                AddLog($"[LAB-异常跳过] {Path.GetFileName(filePath)} 为算法异常，不进入后续流程");
                                resultImage?.Dispose();
                                continue;
                            }

                            string resultCode = ctCode?.Contains("OK") ?? false ? "OK" : "NG";
                            //AddLog($"[LAB-检测完成] 文件：{Path.GetFileName(filePath)}，结果：{ctCode}");


                            if (resultImage != null && resultImage.IsInitialized())
                            {
                                lock (_cacheLock)
                                {
                                    if (_firstDetectionImageCache.ContainsKey(filePath))
                                    {
                                        _firstDetectionImageCache[filePath]?.Dispose();
                                        _firstDetectionImageCache.Remove(filePath);
                                        //AddLog($"[LAB-缓存更新] 移除旧缓存：{Path.GetFileName(filePath)}");
                                    }
                                }
                                lock (_cacheLock)
                                {
                                    _firstDetectionImageCache[filePath] = resultImage;
                                }
                                await HandlePostDetection(ctCode, resultCode, filePath, "LAB");
                            }
                            else
                            {
                                AddLog($"[LAB-缓存存入失败] 文件：{Path.GetFileName(filePath)}，resultImage无效");
                                resultImage?.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[LAB-检测异常] 文件：{Path.GetFileName(filePath)}，异常：{ex.Message}，堆栈：{ex.StackTrace}");
                            resultImage?.Dispose();
                        }
                        finally
                        {
                            // 强制释放临时资源
                            if (resultImage != null && !resultImage.IsInitialized())
                            {
                                resultImage.Dispose();
                                resultImage = null;
                            }
                        }
                    }

                    // 等待新任务
                    //AddLog($"[LAB-队列空] 等待新任务，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    bool isSignaled = _queueEventLAB.WaitOne(TimeSpan.FromSeconds(30));
                    if (!isSignaled)
                    {
                        //AddLog($"[LAB-等待超时] 继续监听，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //AddLog($"[LAB-唤醒] 收到新任务信号，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                }
            }
            finally
            {
                _semaphoreLAB.Release();
                if (_safeDetectQueueLAB.Count == 0 && _semaphoreLAB.CurrentCount == MaxConcurrentLAB)
                {
                    _isQueueRunningLAB = false;
                    //AddLog("[队列处理] 所有任务完成，线程已释放");
                }
                AddLog($"[LAB-线程终止] 释放信号量，线程ID：{Thread.CurrentThread.ManagedThreadId}");
            }
        }
        private void StopOnlineMonitoringLAB()
        {
            if (_onlineMonitorWatcherLAB != null)
            {
                _onlineMonitorWatcherLAB.EnableRaisingEvents = false;
                _onlineMonitorWatcherLAB.Created -= OnOnlineFileCreatedLAB;
                _onlineMonitorWatcherLAB.Dispose();
                _onlineMonitorWatcherLAB = null;

            }
            _isThreadTerminatingLAB = true;
            _queueEventLAB.Set();
            _safeDetectQueueLAB.Clear();
            _isQueueRunningLAB = false;
            CleanupLabRelatedCacheLAB();
            // 检查边界，避免负数
            lock (_stationCountLock)
            {
                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }
            }
        }
        // 清理LAB工位相关的缓存
        private void CleanupLabRelatedCacheLAB()
        {
            lock (_cacheLock)
            {
                // 筛选出LAB工位的图片缓存
                var labCacheKeys = _firstDetectionImageCache.Keys
                    .Where(key => key.StartsWith(SelectedMonitorFolderPathLAB ?? ""))
                    .ToList();

                foreach (var key in labCacheKeys)
                {
                    _firstDetectionImageCache[key]?.Dispose();
                    _firstDetectionImageCache.Remove(key);
                    AddLog($"[LAB-停止清理] 释放缓存：{Path.GetFileName(key)}");
                }
            }

            lock (_barcodeTimeLock)
            {
                // 移除所有条码的最后活动时间记录
                _barcodeLastActivityTime.Clear();
            }
        }
        private void SelectMonitorFolderLAB()
        {
            // 检查权限
            if (!CheckPermission())
            {
                return;
            }
            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = "选择LAB在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;
            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                SelectedMonitorFolderPathLAB = folderDialog.FolderName;
                AddLog($"[LAB] 已选择在线监控文件夹：{SelectedMonitorFolderPathLAB}");
            }
        }

        #endregion
        #region LCD在线监控参数
        private string? _selectedMonitorFolderPathLCD;
        public string? SelectedMonitorFolderPathLCD
        {
            get => _selectedMonitorFolderPathLCD;
            set
            {
                _selectedMonitorFolderPathLCD = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
        private bool _isOnlineDetectionEnabledLCD;
        public bool IsInAnyDetectionLCD
        {
            get => !IsOnlineDetectionEnabledLCD;
        }
        public bool IsOnlineDetectionEnabledLCD
        {
            get => _isOnlineDetectionEnabledLCD;
            set
            {

                if (_isOnlineDetectionEnabledLCD == value) return;

                //在线检测开启
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathLCD) || !Directory.Exists(SelectedMonitorFolderPathLCD))
                    {
                        System.Windows.MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledLCD = false;
                        OnPropertyChanged();
                        return;
                    }

                    StartOnlineMonitoringLCD();
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[LCD-在线检测] 开关已开启");
                }
                //在线检测关闭
                else
                {
                    StopOnlineMonitoringLCD();
                    AddLog("[LCD] 已停止监控文件夹");
                    //AddLog($"[A工位] 已清空队列（剩余任务数：0）");
                    //AddLog("[A工位-在线检测] 开关已关闭，文件选择功能已恢复");
                }

                _isOnlineDetectionEnabledLCD = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsInAnyDetectionLCD));
            }
        }
        private void StartOnlineMonitoringLCD()
        {
            CurrentMonitoringStation = "LCD";

            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathLCD) || !Directory.Exists(SelectedMonitorFolderPathLCD))
            {
                AddLog("[LCD] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabledLCD = false;
                System.Windows.MessageBox.Show("LCD监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _isThreadTerminatingLCD = false;
            if (_onlineMonitorWatcherLCD != null)
            {
                _onlineMonitorWatcherLCD.Created -= OnOnlineFileCreatedLCD;
                _onlineMonitorWatcherLCD.Dispose();
                _onlineMonitorWatcherLCD = null;
            }
            _onlineMonitorWatcherLCD = new FileSystemWatcher(SelectedMonitorFolderPathLCD)
            {
                Filter = "*.png",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _onlineMonitorWatcherLCD.Created += OnOnlineFileCreatedLCD; // 绑定LCD工位文件事件

            AddLog($"[LCD] 已启动监控，路径：{SelectedMonitorFolderPathLCD}");
        }

        private void OnOnlineFileCreatedLCD(object sender, FileSystemEventArgs e)
        {
            string fileName = Path.GetFileName(e.FullPath);
            //检查文件所在目录是否在名为 "OK" 的文件夹之下
            string? currentDirectory = Path.GetDirectoryName(e.FullPath);
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                bool isUnderOKFolder = false;
                DirectoryInfo dirInfo = new DirectoryInfo(currentDirectory);
                while (dirInfo.Parent != null)
                {
                    if (string.Equals(dirInfo.Name, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        isUnderOKFolder = true;
                        break;
                    }
                    dirInfo = dirInfo.Parent;
                }
                if (!isUnderOKFolder)
                {
                    return;
                }
            }

            // 优先排除系统缩略图缓存文件Thumbs.db
            if (string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[LCD-跳过系统文件] 自动忽略Thumbs.db：{fileName}");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[LCD-跳过] 非图片格式：{fileName}");
                        return;
                    }

                    // 仅处理包含"_origin"的图片
                    if (!fileName.Contains("_origin"))
                    {
                        // AddLog($"[LCD-跳过] 非origin图片：{fileName}");
                        return;
                    }

                    bool isFileReady = false;
                    int retryCount = 0;
                    while (retryCount < FileReadyMaxRetry)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        retryCount++;
                        await Task.Delay(FileReadyRetryDelay);
                        if (!File.Exists(e.FullPath))
                        {
                            AddLog($"[LCD-丢弃] {fileName} 在重试期间被删除");
                            return;
                        }
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[LCD-丢弃] {fileName} 超过最大重试次数（{FileReadyMaxRetry}次）仍未就绪");
                        return;
                    }
                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[LCD-丢弃] {fileName} 已被删除");
                        return;
                    }
                    EnqueueTaskLCD(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[LCD-文件处理异常] {e.FullPath}：{ex.Message}，堆栈：{ex.StackTrace}");
                }
            });
        }

        public void EnqueueTaskLCD(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                AddLog("[LCD-入队] 无效路径，跳过入队");
                return;
            }


            _safeDetectQueueLCD.Enqueue(filePath);

            if (!_isQueueRunningLCD)
            {
                StartConcurrentProcessingLCD();
            }
            _queueEventLCD.Set();
        }

        private void StartConcurrentProcessingLCD()
        {
            if (!_isQueueRunningLCD)
            {
                _isQueueRunningLCD = true;
                _ = ProcessQueueConcurrentLCD();
            }
        }
        //private readonly SemaphoreSlim _lcdProcessingSemaphore = new SemaphoreSlim(2, 2);
        private async Task ProcessQueueConcurrentLCD()
        {
            await _semaphoreLCD.WaitAsync();
            try
            {
                while (!_isThreadTerminatingLCD)
                {
                    while (true)
                    {
                        string? filePath = null;
                        // 从LCD队列中出队文件路径

                        if (_safeDetectQueueLCD.TryDequeue(out filePath))
                        {
                            //AddLog($"[LCD-出队成功] 文件：{Path.GetFileName(filePath)}，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                        }
                        else
                        {
                            break; // 队列空，退出内层循环
                        }

                        if (filePath == null) break;

                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            AddLog($"[LCD-跳过] 文件已删除：{Path.GetFileName(filePath)}");
                            continue;
                        }
                        HObject? resultImage = null;
                        try
                        {
                            var (ctCode, tempResultImage) = GapDetection(filePath, "LCD");
                            resultImage = tempResultImage;
                            if (ctCode == "Internal_Error" || resultImage == null)
                            {
                                AddLog($"[LCD-异常跳过] {Path.GetFileName(filePath)} 为算法异常，不进入后续流程");
                                resultImage?.Dispose();
                                continue;
                            }
                            string resultCode = ctCode?.Contains("OK") ?? false ? "OK" : "NG";

                            // 缓存存入前检查resultImage有效性
                            if (resultImage != null && resultImage.IsInitialized())
                            {
                                lock (_cacheLock)
                                {
                                    if (_firstDetectionImageCache.ContainsKey(filePath))
                                    {
                                        _firstDetectionImageCache[filePath]?.Dispose();
                                        _firstDetectionImageCache.Remove(filePath);
                                        //AddLog($"[LCD-缓存更新] 移除旧缓存：{Path.GetFileName(filePath)}");
                                    }
                                }
                                lock (_cacheLock)
                                {
                                    _firstDetectionImageCache[filePath] = resultImage;
                                }
                                await HandlePostDetection(ctCode, resultCode, filePath, "LCD");
                            }
                            else
                            {
                                AddLog($"[LCD-缓存存入失败] 文件：{Path.GetFileName(filePath)}，resultImage无效");
                                resultImage?.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[LCD-检测异常] 文件：{Path.GetFileName(filePath)}，异常：{ex.Message}，堆栈：{ex.StackTrace}");
                            resultImage?.Dispose();
                        }
                        finally
                        {
                            if (resultImage != null && !resultImage.IsInitialized())
                            {
                                resultImage.Dispose();
                                resultImage = null;
                            }
                        }
                    }

                    // 等待新任务（LCD专属事件）
                    bool isSignaled = _queueEventLCD.WaitOne(TimeSpan.FromSeconds(30));
                    if (!isSignaled)
                    {
                        //AddLog($"[LCD-等待超时] 继续监听，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //AddLog($"[LCD-唤醒] 收到新任务信号，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                }
            }
            finally
            {
                _semaphoreLCD.Release();
                if (_safeDetectQueueLCD.Count == 0 && _semaphoreLCD.CurrentCount == MaxConcurrentLCD)
                {
                    _isQueueRunningLCD = false;
                    //AddLog("[队列处理] 所有任务完成，线程已释放");
                }
                AddLog($"[LCD-线程终止] 释放信号量，线程ID：{Thread.CurrentThread.ManagedThreadId}");
            }
        }

        private void StopOnlineMonitoringLCD()
        {
            if (_onlineMonitorWatcherLCD != null)
            {
                _onlineMonitorWatcherLCD.EnableRaisingEvents = false;
                _onlineMonitorWatcherLCD.Created -= OnOnlineFileCreatedLCD;
                _onlineMonitorWatcherLCD.Dispose();
                _onlineMonitorWatcherLCD = null;
            }
            _isThreadTerminatingLCD = true;
            _queueEventLCD.Set();
            _safeDetectQueueLCD.Clear();
            _isQueueRunningLCD = false;
            CleanupLabRelatedCacheLCD();
            lock (_stationCountLock)
            {
                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }
            }

        }
        // 清理LCD工位相关的缓存
        private void CleanupLabRelatedCacheLCD()
        {
            lock (_cacheLock)
            {
                var labCacheKeys = _firstDetectionImageCache.Keys
                    .Where(key => key.StartsWith(SelectedMonitorFolderPathLCD ?? ""))
                    .ToList();

                foreach (var key in labCacheKeys)
                {
                    _firstDetectionImageCache[key]?.Dispose();
                    _firstDetectionImageCache.Remove(key);
                    AddLog($"[LAB-停止清理] 释放缓存：{Path.GetFileName(key)}");
                }
            }

            lock (_barcodeTimeLock)
            {
                _barcodeLastActivityTime.Clear();
            }
        }
        private void SelectMonitorFolderLCD()
        {
            // 检查权限
            if (!CheckPermission())
            {
                return;
            }

            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = "选择LCD在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;

            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                SelectedMonitorFolderPathLCD = folderDialog.FolderName;
                AddLog($"[LCD] 已选择在线监控文件夹：{SelectedMonitorFolderPathLCD}");
            }
        }
        #endregion
        #region RAB在线监控参数
        private string? _selectedMonitorFolderPathRAB;
        public string? SelectedMonitorFolderPathRAB
        {
            get => _selectedMonitorFolderPathRAB;
            set
            {
                _selectedMonitorFolderPathRAB = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
        private bool _isOnlineDetectionEnabledRAB;
        public bool IsInAnyDetectionRAB
        {
            get => !IsOnlineDetectionEnabledRAB;
        }
        public bool IsOnlineDetectionEnabledRAB
        {
            get => _isOnlineDetectionEnabledRAB;
            set
            {

                if (_isOnlineDetectionEnabledRAB == value) return;

                // 在线检测开启
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathRAB) || !Directory.Exists(SelectedMonitorFolderPathRAB))
                    {
                        System.Windows.MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledRAB = false;
                        OnPropertyChanged();
                        return;
                    }

                    StartOnlineMonitoringRAB();
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[RAB] 开关已开启");
                }
                // 在线检测关闭
                else
                {
                    StopOnlineMonitoringRAB();
                    AddLog("[RAB] 已停止监控文件夹");
                    //AddLog($"[A工位] 已清空队列（剩余任务数：0）");
                    //AddLog("[A工位-在线检测] 开关已关闭，文件选择功能已恢复");
                }

                _isOnlineDetectionEnabledRAB = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsInAnyDetectionRAB));
            }
        }
        private void StartOnlineMonitoringRAB()
        {
            CurrentMonitoringStation = "RAB";

            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathRAB) || !Directory.Exists(SelectedMonitorFolderPathRAB))
            {
                AddLog("[RAB] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabledRAB = false;
                System.Windows.MessageBox.Show("RAB监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _isThreadTerminatingRAB = false;
            if (_onlineMonitorWatcherRAB != null)
            {
                _onlineMonitorWatcherRAB.Created -= OnOnlineFileCreatedRAB;
                _onlineMonitorWatcherRAB.Dispose();
                _onlineMonitorWatcherRAB = null;
            }
            _onlineMonitorWatcherRAB = new FileSystemWatcher(SelectedMonitorFolderPathRAB)
            {
                Filter = "*.png",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _onlineMonitorWatcherRAB.Created += OnOnlineFileCreatedRAB;

            AddLog($"[RAB] 已启动监控，路径：{SelectedMonitorFolderPathRAB}");
        }

        private void OnOnlineFileCreatedRAB(object sender, FileSystemEventArgs e)
        {

            string fileName = Path.GetFileName(e.FullPath);
            //检查文件所在目录是否在名为 "OK" 的文件夹之下
            string? currentDirectory = Path.GetDirectoryName(e.FullPath);

            if (!string.IsNullOrEmpty(currentDirectory))
            {
                bool isUnderOKFolder = false;
                DirectoryInfo dirInfo = new DirectoryInfo(currentDirectory);
                while (dirInfo.Parent != null)
                {
                    if (string.Equals(dirInfo.Name, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        isUnderOKFolder = true;
                        break;
                    }
                    dirInfo = dirInfo.Parent;
                }
                if (!isUnderOKFolder)
                {
                    return;
                }
            }

            // 优先排除系统缩略图缓存文件Thumbs.db
            if (string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[RAB-跳过系统文件] 自动忽略Thumbs.db：{fileName}");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[RAB-跳过] 非图片格式：{fileName}");
                        return;
                    }
                    // 仅处理包含"_origin"的图片
                    if (!fileName.Contains("_origin"))
                    {
                        // AddLog($"[RAB-跳过] 非origin图片：{fileName}");
                        return;
                    }
                    bool isFileReady = false;
                    int retryCount = 0;
                    while (retryCount < FileReadyMaxRetry)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        retryCount++;
                        await Task.Delay(FileReadyRetryDelay);
                        if (!File.Exists(e.FullPath))
                        {
                            AddLog($"[RAB-丢弃] {fileName} 在重试期间被删除");
                            return;
                        }
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[RAB-丢弃] {fileName} 超过最大重试次数（{FileReadyMaxRetry}次）仍未就绪");
                        return;
                    }
                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[RAB-丢弃] {fileName} 已被删除");
                        return;
                    }
                    EnqueueTaskRAB(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[RAB-文件处理异常] {e.FullPath}：{ex.Message}，堆栈：{ex.StackTrace}");
                }
            });
        }

        public void EnqueueTaskRAB(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                AddLog("[RAB-入队] 无效路径，跳过入队");
                return;
            }


            _safeDetectQueueRAB.Enqueue(filePath);
            if (!_isQueueRunningRAB)
            {
                StartConcurrentProcessingRAB();
            }
            _queueEventRAB.Set();
        }

        private void StartConcurrentProcessingRAB()
        {
            if (!_isQueueRunningRAB)
            {
                _isQueueRunningRAB = true;
                _ = ProcessQueueConcurrentRAB();
            }
        }
        private async Task ProcessQueueConcurrentRAB()
        {
            await _semaphoreRAB.WaitAsync();
            try
            {
                while (!_isThreadTerminatingRAB)
                {
                    while (true)
                    {
                        string? filePath = null;

                        if (_safeDetectQueueRAB.TryDequeue(out filePath))
                        {
                            //AddLog($"[RAB-出队成功] 文件：{Path.GetFileName(filePath)}，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                        }
                        else
                        {
                            break;
                        }


                        if (filePath == null) break;

                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            AddLog($"[RAB-跳过] 文件已删除：{Path.GetFileName(filePath)}");
                            continue;
                        }
                        HObject? resultImage = null;
                        try
                        {
                            var (ctCode, tempResultImage) = GapDetection(filePath, "RAB");
                            resultImage = tempResultImage;
                            if (ctCode == "Internal_Error" || resultImage == null)
                            {
                                AddLog($"[RAB-异常跳过] {Path.GetFileName(filePath)} 为算法异常，不进入后续流程");
                                resultImage?.Dispose();
                                continue;
                            }
                            string resultCode = ctCode?.Contains("OK") ?? false ? "OK" : "NG";

                            if (resultImage != null && resultImage.IsInitialized())
                            {
                                lock (_cacheLock)
                                {
                                    // 移除旧缓存
                                    if (_firstDetectionImageCache.ContainsKey(filePath))
                                    {
                                        _firstDetectionImageCache[filePath]?.Dispose();
                                        _firstDetectionImageCache.Remove(filePath);
                                        //AddLog($"[RAB-缓存更新] 移除旧缓存：{Path.GetFileName(filePath)}");
                                    }
                                }
                                // 存入新缓存
                                lock (_cacheLock)
                                {
                                    _firstDetectionImageCache[filePath] = resultImage;
                                }

                                await HandlePostDetection(ctCode, resultCode, filePath, "RAB");
                            }
                            else
                            {
                                AddLog($"[RAB-缓存存入失败] 文件：{Path.GetFileName(filePath)}，resultImage无效");
                                resultImage?.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[RAB-检测异常] 文件：{Path.GetFileName(filePath)}，异常：{ex.Message}，堆栈：{ex.StackTrace}");
                            resultImage?.Dispose();
                        }
                        finally
                        {
                            // 强制释放临时资源
                            if (resultImage != null && !resultImage.IsInitialized())
                            {
                                resultImage.Dispose();
                                resultImage = null;
                            }
                        }
                    }

                    //AddLog($"[RAB-队列空] 等待新任务，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    bool isSignaled = _queueEventRAB.WaitOne(TimeSpan.FromSeconds(30));
                    if (!isSignaled)
                    {
                        //AddLog($"[RAB-等待超时] 继续监听，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //AddLog($"[RAB-唤醒] 收到新任务信号，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                }
            }
            finally
            {
                _semaphoreRAB.Release();
                if (_safeDetectQueueRAB.Count == 0 && _semaphoreRAB.CurrentCount == MaxConcurrentRAB)
                {
                    _isQueueRunningRAB = false;
                    //AddLog("[队列处理] 所有任务完成，线程已释放");
                }
                AddLog($"[RAB-线程终止] 释放信号量，线程ID：{Thread.CurrentThread.ManagedThreadId}");
            }
        }
        private void SelectMonitorFolderRAB()
        {
            // 检查权限
            if (!CheckPermission())
            {
                return;
            }

            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = "选择RAB在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;

            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                SelectedMonitorFolderPathRAB = folderDialog.FolderName;
                AddLog($"[RAB] 已选择在线监控文件夹：{SelectedMonitorFolderPathRAB}");
            }
        }

        private void StopOnlineMonitoringRAB()
        {
            if (_onlineMonitorWatcherRAB != null)
            {
                _onlineMonitorWatcherRAB.EnableRaisingEvents = false;
                _onlineMonitorWatcherRAB.Created -= OnOnlineFileCreatedRAB;
                _onlineMonitorWatcherRAB.Dispose();
                _onlineMonitorWatcherRAB = null;
            }
            _isThreadTerminatingRAB = true;
            _queueEventRAB.Set();
            _safeDetectQueueRAB.Clear();
            _isQueueRunningRAB = false;
            CleanupLabRelatedCacheRAB();
            lock (_stationCountLock)
            {
                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }
            }

        }
        private void CleanupLabRelatedCacheRAB()
        {
            lock (_cacheLock)
            {
                var labCacheKeys = _firstDetectionImageCache.Keys
                    .Where(key => key.StartsWith(SelectedMonitorFolderPathRAB ?? ""))
                    .ToList();

                foreach (var key in labCacheKeys)
                {
                    _firstDetectionImageCache[key]?.Dispose();
                    _firstDetectionImageCache.Remove(key);
                    AddLog($"[LAB-停止清理] 释放缓存：{Path.GetFileName(key)}");
                }
            }

            lock (_barcodeTimeLock)
            {
                // 移除所有条码的最后活动时间记录
                _barcodeLastActivityTime.Clear();
            }
        }
        #endregion
        #region RCD在线监控参数
        private string? _selectedMonitorFolderPathRCD;
        public string? SelectedMonitorFolderPathRCD
        {
            get => _selectedMonitorFolderPathRCD;
            set
            {
                _selectedMonitorFolderPathRCD = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
        private bool _isOnlineDetectionEnabledRCD;
        public bool IsInAnyDetectionRCD
        {
            get => !IsOnlineDetectionEnabledRCD;
        }
        public bool IsOnlineDetectionEnabledRCD
        {
            get => _isOnlineDetectionEnabledRCD;
            set
            {

                if (_isOnlineDetectionEnabledRCD == value) return;

                //在线检测开启
                if (value)
                {
                    if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathRCD) || !Directory.Exists(SelectedMonitorFolderPathRCD))
                    {
                        System.Windows.MessageBox.Show("请先选择有效的在线监控文件夹！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        _isOnlineDetectionEnabledRCD = false;
                        OnPropertyChanged();
                        return;
                    }
                    StartOnlineMonitoringRCD();
                    lock (_stationCountLock)
                    {
                        _runningStationsCount++;
                    }
                    AddLog("[RCD] 开关已开启");
                }
                //在线检测关闭
                else
                {
                    StopOnlineMonitoringRCD();
                    AddLog("[RCD] 已停止监控文件夹");
                    //AddLog($"[A工位] 已清空队列（剩余任务数：0）");
                    //AddLog("[A工位-在线检测] 开关已关闭，文件选择功能已恢复");
                }

                _isOnlineDetectionEnabledRCD = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInAnyDetection));
                OnPropertyChanged(nameof(IsInAnyDetectionRCD));
            }
        }
        private void StartOnlineMonitoringRCD()
        {
            CurrentMonitoringStation = "RCD";

            if (string.IsNullOrWhiteSpace(SelectedMonitorFolderPathRCD) || !Directory.Exists(SelectedMonitorFolderPathRCD))
            {
                AddLog("[RCD] 监控文件夹不存在，请先选择有效文件夹");
                IsOnlineDetectionEnabledRCD = false;
                System.Windows.MessageBox.Show("RCD监控文件夹不存在，请先选择有效文件夹", "在线检测错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _isThreadTerminatingRCD = false;
            if (_onlineMonitorWatcherRCD != null)
            {
                _onlineMonitorWatcherRCD.Created -= OnOnlineFileCreatedRCD;
                _onlineMonitorWatcherRCD.Dispose();
                _onlineMonitorWatcherRCD = null;
            }
            _onlineMonitorWatcherRCD = new FileSystemWatcher(SelectedMonitorFolderPathRCD)
            {
                Filter = "*.png",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
                InternalBufferSize = 65536
            };
            _onlineMonitorWatcherRCD.Created += OnOnlineFileCreatedRCD;

            AddLog($"[RCD] 已启动监控，路径：{SelectedMonitorFolderPathRCD}");
        }

        private void OnOnlineFileCreatedRCD(object sender, FileSystemEventArgs e)
        {

            string fileName = Path.GetFileName(e.FullPath);
            //检查文件所在目录是否在名为 "OK" 的文件夹之下
            string? currentDirectory = Path.GetDirectoryName(e.FullPath);
            if (!string.IsNullOrEmpty(currentDirectory))
            {
                bool isUnderOKFolder = false;
                DirectoryInfo dirInfo = new DirectoryInfo(currentDirectory);
                while (dirInfo.Parent != null)
                {
                    if (string.Equals(dirInfo.Name, "OK", StringComparison.OrdinalIgnoreCase))
                    {
                        isUnderOKFolder = true;
                        break;
                    }
                    dirInfo = dirInfo.Parent;
                }
                if (!isUnderOKFolder)
                {
                    return;
                }
            }

            // 优先排除系统缩略图缓存文件Thumbs.db
            if (string.Equals(fileName, "Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                AddLog($"[RCD-跳过系统文件] 自动忽略Thumbs.db：{fileName}");
                return;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    var allowedExts = new HashSet<string> { ".bmp", ".jpg", ".jpeg", ".png", ".gif", ".tiff", ".webp" };
                    var ext = Path.GetExtension(e.FullPath).ToLower();
                    if (!allowedExts.Contains(ext))
                    {
                        AddLog($"[RCD-跳过] 非图片格式：{fileName}");
                        return;
                    }

                    // 仅处理包含"_origin"的图片
                    if (!fileName.Contains("_origin"))
                    {
                        // AddLog($"[RCD-跳过] 非origin图片：{fileName}");
                        return;
                    }
                    bool isFileReady = false;
                    int retryCount = 0;
                    while (retryCount < FileReadyMaxRetry)
                    {
                        if (IsFileAccessible(e.FullPath))
                        {
                            isFileReady = true;
                            break;
                        }
                        retryCount++;
                        await Task.Delay(FileReadyRetryDelay);

                        if (!File.Exists(e.FullPath))
                        {
                            AddLog($"[RCD-丢弃] {fileName} 在重试期间被删除");
                            return;
                        }
                    }
                    if (!isFileReady)
                    {
                        AddLog($"[RCD-丢弃] {fileName} 超过最大重试次数（{FileReadyMaxRetry}次）仍未就绪");
                        return;
                    }

                    if (!File.Exists(e.FullPath))
                    {
                        AddLog($"[RCD-丢弃] {fileName} 已被删除");
                        return;
                    }
                    EnqueueTaskRCD(e.FullPath);
                }
                catch (Exception ex)
                {
                    AddLog($"[RCD-文件处理异常] {e.FullPath}：{ex.Message}，堆栈：{ex.StackTrace}");
                }
            });
        }

        public void EnqueueTaskRCD(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                AddLog("[RCD-入队] 无效路径，跳过入队");
                return;
            }

            _safeDetectQueueRCD.Enqueue(filePath);

            if (!_isQueueRunningRCD)
            {
                StartConcurrentProcessingRCD();
            }
            _queueEventRCD.Set();
        }

        private void StartConcurrentProcessingRCD()
        {
            if (!_isQueueRunningRCD)
            {
                _isQueueRunningRCD = true;
                _ = ProcessQueueConcurrentRCD();
            }
        }
        private async Task ProcessQueueConcurrentRCD()
        {
            await _semaphoreRCD.WaitAsync();
            try
            {
                while (!_isThreadTerminatingRCD)
                {
                    while (true)
                    {
                        string? filePath = null;


                        if (_safeDetectQueueRCD.TryDequeue(out filePath))
                        {
                            //AddLog($"[RCD-出队成功] 文件：{Path.GetFileName(filePath)}，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                        }
                        else
                        {
                            break;
                        }


                        if (filePath == null) break;

                        if (!File.Exists(filePath))
                        {
                            AddLog($"[RCD-跳过] 文件已删除：{Path.GetFileName(filePath)}");
                            continue;
                        }

                        HObject? resultImage = null;
                        try
                        {
                            var (ctCode, tempResultImage) = GapDetection(filePath, "RCD");
                            resultImage = tempResultImage;
                            if (ctCode == "Internal_Error" || resultImage == null)
                            {
                                AddLog($"[RCD-异常跳过] {Path.GetFileName(filePath)} 为算法异常，不进入后续流程");
                                resultImage?.Dispose();
                                continue;
                            }
                            string resultCode = ctCode?.Contains("OK") ?? false ? "OK" : "NG";

                            if (resultImage != null && resultImage.IsInitialized())
                            {
                                lock (_cacheLock)
                                {
                                    if (_firstDetectionImageCache.ContainsKey(filePath))
                                    {
                                        _firstDetectionImageCache[filePath]?.Dispose();
                                        _firstDetectionImageCache.Remove(filePath);
                                        //AddLog($"[RCD-缓存更新] 移除旧缓存：{Path.GetFileName(filePath)}");
                                    }
                                }
                                lock (_cacheLock)
                                {
                                    _firstDetectionImageCache[filePath] = resultImage;
                                }
                                await HandlePostDetection(ctCode, resultCode, filePath, "RCD");
                            }
                            else
                            {
                                AddLog($"[RCD-缓存存入失败] 文件：{Path.GetFileName(filePath)}，resultImage无效");
                                resultImage?.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"[RCD-检测异常] 文件：{Path.GetFileName(filePath)}，异常：{ex.Message}，堆栈：{ex.StackTrace}");
                            resultImage?.Dispose();
                        }
                        finally
                        {
                            // 强制释放临时资源
                            if (resultImage != null && !resultImage.IsInitialized())
                            {
                                resultImage.Dispose();
                                resultImage = null;
                            }
                        }
                    }

                    bool isSignaled = _queueEventRCD.WaitOne(TimeSpan.FromSeconds(30));
                    if (!isSignaled)
                    {
                        //AddLog($"[RCD-等待超时] 继续监听，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                    else
                    {
                        //AddLog($"[RCD-唤醒] 收到新任务信号，线程ID：{Thread.CurrentThread.ManagedThreadId}");
                    }
                }
            }
            finally
            {
                _semaphoreRCD.Release();
                if (_safeDetectQueueRCD.Count == 0 && _semaphoreRCD.CurrentCount == MaxConcurrentRCD)
                {
                    _isQueueRunningRCD = false;
                    //AddLog("[队列处理] 所有任务完成，线程已释放");
                }
                AddLog($"[RCD-线程终止] 释放信号量，线程ID：{Thread.CurrentThread.ManagedThreadId}");
            }
        }

        private void StopOnlineMonitoringRCD()
        {
            if (_onlineMonitorWatcherRCD != null)
            {
                _onlineMonitorWatcherRCD.EnableRaisingEvents = false;
                _onlineMonitorWatcherRCD.Created -= OnOnlineFileCreatedRCD;
                _onlineMonitorWatcherRCD.Dispose();
                _onlineMonitorWatcherRCD = null;
            }
            _isThreadTerminatingRCD = true;
            _queueEventRCD.Set();
            _safeDetectQueueRCD.Clear();
            _isQueueRunningRCD = false;
            CleanupLabRelatedCacheRCD();
            lock (_stationCountLock)
            {
                if (_runningStationsCount > 0)
                {
                    _runningStationsCount--;
                }
            }
            lock (_cacheLock)
            {
                foreach (var kvp in _firstDetectionImageCache)
                {
                    kvp.Value?.Dispose();
                }
                _firstDetectionImageCache.Clear();
            }
        }
        private void CleanupLabRelatedCacheRCD()
        {
            lock (_cacheLock)
            {
                var labCacheKeys = _firstDetectionImageCache.Keys
                    .Where(key => key.StartsWith(SelectedMonitorFolderPathRCD ?? ""))
                    .ToList();

                foreach (var key in labCacheKeys)
                {
                    _firstDetectionImageCache[key]?.Dispose();
                    _firstDetectionImageCache.Remove(key);
                    AddLog($"[LAB-停止清理] 释放缓存：{Path.GetFileName(key)}");
                }
            }

            lock (_barcodeTimeLock)
            {
                // 移除所有条码的最后活动时间记录
                _barcodeLastActivityTime.Clear();
            }
        }
        private void SelectMonitorFolderRCD()
        {
            // 检查权限
            if (!CheckPermission())
            {
                return;
            }

            var folderDialog = new OpenFolderDialog();
            folderDialog.Title = "选择RCD在线检测监控文件夹";
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderDialog.ValidateNames = true;
            folderDialog.Multiselect = false;

            bool? result = folderDialog.ShowDialog(Application.Current.MainWindow);
            if (result == true)
            {
                SelectedMonitorFolderPathRCD = folderDialog.FolderName;
                AddLog($"[RCD] 已选择在线监控文件夹：{SelectedMonitorFolderPathRCD}");
            }
        }
        #endregion

        private ObservableCollection<Tuple<string, string>> _logTable;
        public ObservableCollection<Tuple<string, string>> LogTable
        {
            get => _logTable;
            set
            {
                _logTable = value;
                OnPropertyChanged();
            }
        }

        private void InitThreshold()
        {
            Gap1Limit = _projectsetting.GetSettingValue("Gap1Limit", "VM_CTAutoDetectionC06Page");
            Gap2Limit = _projectsetting.GetSettingValue("Gap2Limit", "VM_CTAutoDetectionC06Page");
            Gap3Limit = _projectsetting.GetSettingValue("Gap3Limit", "VM_CTAutoDetectionC06Page");
            Gap4Limit = _projectsetting.GetSettingValue("Gap4Limit", "VM_CTAutoDetectionC06Page");
            Gap5Limit = _projectsetting.GetSettingValue("Gap5Limit", "VM_CTAutoDetectionC06Page");
            Gap6Limit = _projectsetting.GetSettingValue("Gap6Limit", "VM_CTAutoDetectionC06Page");
            Gap7Limit = _projectsetting.GetSettingValue("Gap7Limit", "VM_CTAutoDetectionC06Page");
            Gap8Limit = _projectsetting.GetSettingValue("Gap8Limit", "VM_CTAutoDetectionC06Page");
            Gap9Limit = _projectsetting.GetSettingValue("Gap9Limit", "VM_CTAutoDetectionC06Page");
            Gap10Limit = _projectsetting.GetSettingValue("Gap10Limit", "VM_CTAutoDetectionC06Page");
            Gap11Limit = _projectsetting.GetSettingValue("Gap11Limit", "VM_CTAutoDetectionC06Page");
            Gap12Limit = _projectsetting.GetSettingValue("Gap12Limit", "VM_CTAutoDetectionC06Page");
            Gap13Limit = _projectsetting.GetSettingValue("Gap13Limit", "VM_CTAutoDetectionC06Page");
            Gap14Limit = _projectsetting.GetSettingValue("Gap14Limit", "VM_CTAutoDetectionC06Page");
            Gap15Limit = _projectsetting.GetSettingValue("Gap15Limit", "VM_CTAutoDetectionC06Page");
            Gap16Limit = _projectsetting.GetSettingValue("Gap16Limit", "VM_CTAutoDetectionC06Page");
            Gap17Limit = _projectsetting.GetSettingValue("Gap17Limit", "VM_CTAutoDetectionC06Page");
            Gap18Limit = _projectsetting.GetSettingValue("Gap18Limit", "VM_CTAutoDetectionC06Page");
            Gap19Limit = _projectsetting.GetSettingValue("Gap19Limit", "VM_CTAutoDetectionC06Page");
            Gap20Limit = _projectsetting.GetSettingValue("Gap20Limit", "VM_CTAutoDetectionC06Page");
            Gap21Limit = _projectsetting.GetSettingValue("Gap21Limit", "VM_CTAutoDetectionC06Page");
            Gap22Limit = _projectsetting.GetSettingValue("Gap22Limit", "VM_CTAutoDetectionC06Page");
            Gap23Limit = _projectsetting.GetSettingValue("Gap23Limit", "VM_CTAutoDetectionC06Page");
            Gap24Limit = _projectsetting.GetSettingValue("Gap24Limit", "VM_CTAutoDetectionC06Page");
            Gap25Limit = _projectsetting.GetSettingValue("Gap25Limit", "VM_CTAutoDetectionC06Page");
            Gap26Limit = _projectsetting.GetSettingValue("Gap26Limit", "VM_CTAutoDetectionC06Page");
            Gap27Limit = _projectsetting.GetSettingValue("Gap27Limit", "VM_CTAutoDetectionC06Page");
            Gap28Limit = _projectsetting.GetSettingValue("Gap28Limit", "VM_CTAutoDetectionC06Page");
            Gap29Limit = _projectsetting.GetSettingValue("Gap29Limit", "VM_CTAutoDetectionC06Page");
            Gap30Limit = _projectsetting.GetSettingValue("Gap30Limit", "VM_CTAutoDetectionC06Page");
            Gap31Limit = _projectsetting.GetSettingValue("Gap31Limit", "VM_CTAutoDetectionC06Page");
            Gap32Limit = _projectsetting.GetSettingValue("Gap32Limit", "VM_CTAutoDetectionC06Page");
            Gap33Limit = _projectsetting.GetSettingValue("Gap33Limit", "VM_CTAutoDetectionC06Page");
            Gap34Limit = _projectsetting.GetSettingValue("Gap34Limit", "VM_CTAutoDetectionC06Page");
            Gap35Limit = _projectsetting.GetSettingValue("Gap35Limit", "VM_CTAutoDetectionC06Page");
            Gap36Limit = _projectsetting.GetSettingValue("Gap36Limit", "VM_CTAutoDetectionC06Page");
            Gap37Limit = _projectsetting.GetSettingValue("Gap37Limit", "VM_CTAutoDetectionC06Page");
            Gap38Limit = _projectsetting.GetSettingValue("Gap38Limit", "VM_CTAutoDetectionC06Page");
            Gap39Limit = _projectsetting.GetSettingValue("Gap39Limit", "VM_CTAutoDetectionC06Page");
            Gap40Limit = _projectsetting.GetSettingValue("Gap40Limit", "VM_CTAutoDetectionC06Page");
            Gap41Limit = _projectsetting.GetSettingValue("Gap41Limit", "VM_CTAutoDetectionC06Page");
            Gap42Limit = _projectsetting.GetSettingValue("Gap42Limit", "VM_CTAutoDetectionC06Page");
            Gap43Limit = _projectsetting.GetSettingValue("Gap43Limit", "VM_CTAutoDetectionC06Page");
            Gap44Limit = _projectsetting.GetSettingValue("Gap44Limit", "VM_CTAutoDetectionC06Page");
            Gap45Limit = _projectsetting.GetSettingValue("Gap45Limit", "VM_CTAutoDetectionC06Page");
            Gap46Limit = _projectsetting.GetSettingValue("Gap46Limit", "VM_CTAutoDetectionC06Page");
            Gap47Limit = _projectsetting.GetSettingValue("Gap47Limit", "VM_CTAutoDetectionC06Page");
        }
        private void InitParameters()
        {

            SelectedResultImagePath = _projectsetting.GetSettingValue("SelectedResultImagePath", "VM_CTAutoDetectionC06Page");
            ai.Model_init(@"E:\在线CT\1222\C06_best.onnx", true, out LABmodel, out string runinfoLAB);
            ai.Model_init(@"E:\在线CT\1222\C06_best.onnx", true, out LCDmodel, out string runinfoLCD);
            ai.Model_init(@"E:\在线CT\1222\C06_best.onnx", true, out RABmodel, out string runinfoRAB);
            ai.Model_init(@"E:\在线CT\1222\C06_best.onnx", true, out RCDmodel, out string runinfoRCD);
            if (!string.IsNullOrEmpty(runinfoLAB))
            {
                Console.WriteLine($"LAB模型初始化异常：{runinfoLAB}");
                AddLog($"LAB模型初始化异常：{runinfoLAB}");
            }
            if (!string.IsNullOrEmpty(runinfoLCD))
            {
                Console.WriteLine($"LCD模型初始化异常：{runinfoLCD}");
                AddLog($"LCD模型初始化异常：{runinfoLCD}");
            }
            if (!string.IsNullOrEmpty(runinfoRAB))
            {
                Console.WriteLine($"RAB模型初始化异常：{runinfoRAB}");
                AddLog($"RAB模型初始化异常：{runinfoRAB}");
            }
            if (!string.IsNullOrEmpty(runinfoRCD))
            {
                Console.WriteLine($"RCD模型初始化异常：{runinfoRCD}");
                AddLog($"RCD模型初始化异常：{runinfoRCD}");
            }
        }


        public VM_CTAutoDetectionC06Page(INavigationService navigationService, VM_UserCenter userCenterViewModel, ILoggerService<VM_CTAutoDetectionC06Page> logger, IProjectSettingRepository projectsetting)
        {

            _logger = logger;
            _projectsetting = projectsetting;
            _userCenterViewModel = userCenterViewModel ?? throw new ArgumentNullException(nameof(userCenterViewModel));
            _navigationService = navigationService;
            _userCenterViewModel.PropertyChanged += UserCenterViewModel_PropertyChanged;
            LoadConfig();
            InitPieChart();
            UpdatePieChartData();
            InitDatabase();
            InitThreshold();
            SyncTempPropertiesWithFormalProperties();



            _logTable = new ObservableCollection<Tuple<string, string>>();
            AddLog("系统启动，日志功能就绪");
            InitParameters();

            _imeresultRoot = Path.Combine(SelectedResultImagePath ?? @"E:\", "IMEResult");
            // 指定新目标根路径， "\\10.178.5.10\生产数据暂存（如空间不足，请自行删除文件）\C01CT看图（中工序）（A线）（勿删）\CT\IMEResult"
            _archiveTargetRoot = @"\\10.178.5.10\生产数据暂存（如空间不足，请自行删除文件）\C06CT看图（中工序）（A线）勿删\IMEResult";
            // 初始化文件移动服务
            _fileMoverService = new DefectFileMoverService(_imeresultRoot, _archiveTargetRoot);
            _fileMoverService.AddLog = this.AddLog;
            _dailyTaskService = new SimpleDailyTaskService(() => _fileMoverService.MoveYesterdayDefectFilesAsync());
            _dailyTaskService.AddLog = this.AddLog;
            _dailyTaskService.Start();

            // 初始化定时清理任务
            _timeoutCleanupTimer = new System.Timers.Timer(60000); // 60秒
            _timeoutCleanupTimer.Elapsed += CleanupTimeoutBarcodes;
            _timeoutCleanupTimer.Start();

            GPUMonitor();
            //LAB结果图操作--可以优化，代码冗余
            SelectResultImagePath = new RelayCommand(ExecuteSelectResultImagePath);
            ZoomRealTimeImageCommandLAB = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImageLAB);
            ZoomInRealTimeCommandLAB = new RelayCommand(OnZoomInRealTimeLAB);
            ZoomOutRealTimeCommandLAB = new RelayCommand(OnZoomOutRealTimeLAB);
            ResetRealTimeImageZoomCommandLAB = new RelayCommand(OnResetRealTimeZoomLAB);
            ImageMouseDownCommandLAB = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDownLAB);
            ImageMouseMoveCommandLAB = new RelayCommand<MouseEventArgs>(OnImageMouseMoveLAB);
            ImageMouseUpCommandLAB = new RelayCommand<MouseEventArgs>(OnImageMouseUpLAB);
            //LCD结果图操作
            ZoomRealTimeImageCommandLCD = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImageLCD);
            ZoomInRealTimeCommandLCD = new RelayCommand(OnZoomInRealTimeLCD);
            ZoomOutRealTimeCommandLCD = new RelayCommand(OnZoomOutRealTimeLCD);
            ResetRealTimeImageZoomCommandLCD = new RelayCommand(OnResetRealTimeZoomLCD);
            ImageMouseDownCommandLCD = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDownLCD);
            ImageMouseMoveCommandLCD = new RelayCommand<MouseEventArgs>(OnImageMouseMoveLCD);
            ImageMouseUpCommandLCD = new RelayCommand<MouseEventArgs>(OnImageMouseUpLCD);
            //RAB结果图操作
            ZoomRealTimeImageCommandRAB = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImageRAB);
            ZoomInRealTimeCommandRAB = new RelayCommand(OnZoomInRealTimeRAB);
            ZoomOutRealTimeCommandRAB = new RelayCommand(OnZoomOutRealTimeRAB);
            ResetRealTimeImageZoomCommandRAB = new RelayCommand(OnResetRealTimeZoomRAB);
            ImageMouseDownCommandRAB = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDownRAB);
            ImageMouseMoveCommandRAB = new RelayCommand<MouseEventArgs>(OnImageMouseMoveRAB);
            ImageMouseUpCommandRAB = new RelayCommand<MouseEventArgs>(OnImageMouseUpRAB);
            //RCD结果图操作
            ZoomRealTimeImageCommandRCD = new RelayCommand<MouseWheelEventArgs>(OnZoomRealTimeImageRCD);
            ZoomInRealTimeCommandRCD = new RelayCommand(OnZoomInRealTimeRCD);
            ZoomOutRealTimeCommandRCD = new RelayCommand(OnZoomOutRealTimeRCD);
            ResetRealTimeImageZoomCommandRCD = new RelayCommand(OnResetRealTimeZoomRCD);
            ImageMouseDownCommandRCD = new RelayCommand<MouseButtonEventArgs>(OnImageMouseDownRCD);
            ImageMouseMoveCommandRCD = new RelayCommand<MouseEventArgs>(OnImageMouseMoveRCD);
            ImageMouseUpCommandRCD = new RelayCommand<MouseEventArgs>(OnImageMouseUpRCD);

            ClearListCommand = new RelayCommand(ExecuteClearList);
            ClearStatisticsCommand = new RelayCommand(ExecuteClearStatistics);
            DetectionDataList = new ObservableCollection<dynamic>();
            _detectionDataList = new ObservableCollection<dynamic>();
            SelectMonitorFolderCommandLAB = new RelayCommand(SelectMonitorFolderLAB);
            SelectMonitorFolderCommandLCD = new RelayCommand(SelectMonitorFolderLCD);
            SelectMonitorFolderCommandRAB = new RelayCommand(SelectMonitorFolderRAB);
            SelectMonitorFolderCommandRCD = new RelayCommand(SelectMonitorFolderRCD);
            _semaphoreLAB = new SemaphoreSlim(MaxConcurrentLAB, MaxConcurrentLAB);
            _semaphoreLCD = new SemaphoreSlim(MaxConcurrentLCD, MaxConcurrentLCD);
            _semaphoreRAB = new SemaphoreSlim(MaxConcurrentRAB, MaxConcurrentRAB);
            _semaphoreRCD = new SemaphoreSlim(MaxConcurrentRCD, MaxConcurrentRCD);
            QueryCommand = new RelayCommand(async () => await ExecuteQueryAsync());
            ExportCommand = new RelayCommand(async () => await ExecuteExportAsync());
            StartDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0);
            EndDateTime = DateTime.Now.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            PagedAbnormalDataList = new ObservableCollection<Dictionary<string, object>>();
            SelectedAbnormalType = "全部";
            ImagePathQueueLAB = new ConcurrentQueue<string>();
            ImagePathQueueLCD = new ConcurrentQueue<string>();
            ImagePathQueueRAB = new ConcurrentQueue<string>();
            ImagePathQueueRCD = new ConcurrentQueue<string>();
            SaveSettingsCommand = new RelayCommand(SaveThresholdConfig);
            ResetSettingsCommand = new RelayCommand(ResetThresholds);

            PreviousPageCommand = new RelayCommand(ExecutePreviousPage);
            NextPageCommand = new RelayCommand(ExecuteNextPage);
        }
        // 辅助方法：判断文件是否可访问
        private static bool IsFileAccessible(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return stream.CanRead; // 能打开且可读，说明文件就绪
                }
            }
            catch (IOException) // 文件锁定/正在写入
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
        // 监听登录状态变化
        private void UserCenterViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VM_UserCenter.IsLoggedIn))
            {
                if (_userCenterViewModel.IsLoggedIn)
                {
                    // 登录成功，启动15分钟自动退出计时器
                    AutoNavigateBackAfterLogin();
                    StartAutoLogoutTimer();
                    Growl.InfoGlobal("登录成功，15分钟后将自动退出。");
                }
                else
                {
                    // 退出登录，取消计时器
                    CancelAutoLogoutTimer();
                }
            }
        }
        private void AutoNavigateBackAfterLogin()
        {
            // 导航回应用界面
            _navigationService.NavigateTo("VM_CTAutoDetectionC06");
        }

        // 启动自动退出计时器
        private void StartAutoLogoutTimer()
        {
            // 取消旧计时器
            CancelAutoLogoutTimer();
            _autoLogoutCancellationTokenSource = new CancellationTokenSource();
            var token = _autoLogoutCancellationTokenSource.Token;

            Task.Delay(TimeSpan.FromMinutes(15), token)
                .ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (_userCenterViewModel.IsLoggedIn)
                            {
                                AddLog("登录时间已超过15分钟，为保障账户安全，已自动退出登录。");
                                // 调用主框架的退出登录命令
                                _userCenterViewModel.LogoutCommand?.Execute(null);
                            }
                        });
                    }
                }, token);
        }

        /// <summary>
        /// 取消自动退出计时器
        /// </summary>
        private void CancelAutoLogoutTimer()
        {
            // 检查令牌源是否存在且未被取消
            if (_autoLogoutCancellationTokenSource != null && !_autoLogoutCancellationTokenSource.IsCancellationRequested)
            {
                _autoLogoutCancellationTokenSource.Cancel();
                _autoLogoutCancellationTokenSource.Dispose();
                _autoLogoutCancellationTokenSource = null;
            }
        }
        /// <summary>
        /// 执行“浏览路径”命令
        /// </summary>
        private void ExecuteSelectResultImagePath()
        {
            // 检查权限
            if (!CheckPermission())
            {
                return;
            }
            var folderDialog = new OpenFolderDialog
            {
                Title = "请选择结果存储路径",
                InitialDirectory = SelectedResultImagePath,
                Multiselect = false // 禁止多选文件夹
            };

            bool? isSelected = folderDialog.ShowDialog();
            if (isSelected == true)
            {
                SelectedResultImagePath = folderDialog.FolderName;
            }
        }

        /// <summary>
        /// 更新显示集合，仅保留最新的150条记录
        /// </summary>
        private void UpdateDisplayDataList()
        {
            //按时间倒序取最新150条
            var targetNgList = _fullDetectionDataList
                .Where(data => data != null
                            && !string.IsNullOrEmpty(data?.DetectionResult))
                .OrderByDescending(data => data.DetectionTime)
                .Take(150)
                .ToList();

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var toRemove = DetectionDataList
                    .Where(item => !targetNgList.Contains(item))
                    .ToList();

                //移除旧数据
                foreach (var item in toRemove)
                {
                    DetectionDataList.Remove(item);
                }

                //找出需要添加的新数据
                var toAdd = targetNgList
                    .Where(item => !DetectionDataList.Contains(item))
                    .ToList();

                // 插入新数据
                foreach (var item in toAdd)
                {
                    int index = targetNgList.IndexOf(item);
                    if (index < DetectionDataList.Count)
                    {
                        DetectionDataList.Insert(index, item);
                    }
                    else
                    {
                        DetectionDataList.Add(item);
                    }
                }

                // 最终确保列表不超过150条
                while (DetectionDataList.Count > 150)
                {
                    DetectionDataList.RemoveAt(DetectionDataList.Count - 1);
                }
            });
        }

        public static double[] GapMax(string[] Param, string[] Param2)
        {
            double[] GapParam = new double[2];
            double[] doubleArray = ConvertStringArrayToDoubleArray(Param);
            double[] doubleArray2 = ConvertStringArrayToDoubleArray(Param2);
            // 找到最小值和最大值
            double minValue = doubleArray.Min();
            double maxValue = doubleArray.Max();
            double minValue2 = doubleArray2.Min();
            double maxValue2 = doubleArray2.Max();
            if (minValue < minValue2)
            {
                GapParam[0] = minValue;
            }
            else
            {
                GapParam[0] = minValue2;
            }
            if (maxValue > maxValue2)
            {
                GapParam[1] = maxValue2;
            }
            else
            {
                GapParam[1] = maxValue;
            }
            return GapParam;
        }

        public static double[] ConvertStringArrayToDoubleArray(string[] stringArray)
        {
            List<double> doubleList = new List<double>();

            foreach (string str in stringArray)
            {
                if (double.TryParse(str, out double result))
                {
                    doubleList.Add(result);
                }
                else
                {
                    throw new FormatException($"Invalid format for string '{str}'. Cannot convert to double.");
                }
            }
            return doubleList.ToArray();
        }

        /// <summary>
        /// 缺陷判断
        /// </summary>
        /// <param name="aa">输入层数</param>
        /// <param name="bb">输入层数</param>
        /// <param name="aaaa">正极OH值</param>
        /// <param name="bbbb">负极OH值</param>
        /// <param name="cc">负极差阈值</param>
        /// <param name="numbers">正极间隙值</param>
        /// <param name="ImgName">图片名字比如1-X来区分</param>
        public string? NGOK(int aa, int bb, double[] aaaa, double[] bbbb, double cc, string ImgName, double[] gap0MaxArray, int NGflag)
        {
            string Result = "OK";
            //判断OH不良
            if (CheckArrayOutOfRange(ImgName, aaaa, out string? rangeResult))
                return rangeResult;
            //判断多片少片
            if (aa != 76)
                return aa > 76 ? "NG_正极多片" : "NG_正极少片";
            if (bb != 77)
                return bb > 77 ? "NG_负极多片" : "NG_负极少片";
            //判断间隙不良
            if (CheckGapMax(ImgName, gap0MaxArray, out string? gapMaxResult))
                return gapMaxResult;
            //判断负极差不良
            if (CheckCC(ImgName, cc, out string? ccResult))
                return ccResult;
            if (NGflag == 1)
            {
                return "负极压伤";
            }
            ////增加判断Y正极掉片
            if (FindAdjacentDifferencesWithValues(aaaa, Gap45Limit) & ImgName.Contains("Y"))
            {
                Result = "NG_正极Y方向掉片";
                return Result;
            }
            return Result;
        }

        //掉片判断逻辑之一
        public static bool FindAdjacentDifferencesWithValues(double[] array, double threshold)
        {
            if (array == null || array.Length < 2)
            {
                throw new ArgumentException("数组不能为空且长度必须大于等于 2", nameof(array));
            }

            for (int i = 0; i < array.Length - 1; i++)
            {
                double difference = Math.Abs(array[i] - array[i + 1]);
                if (difference > threshold)
                {
                    return true;
                }
            }

            return false;
        }
        //掉片判断逻辑之二三
        public string HandleZJYDP(string station)
        {
            bool FirstBOOL = false;
            bool FirstBOOL2 = false;

            //从嵌套字典中提取当前工位的Y1-Y4
            double[]? Y1 = null, Y2 = null, Y3 = null, Y4 = null;
            lock (stationDataLocker)
            {
                if (stationYDataDict.TryGetValue(station, out var currentStationDict))
                {
                    currentStationDict.TryGetValue("Y1", out Y1);
                    currentStationDict.TryGetValue("Y2", out Y2);
                    currentStationDict.TryGetValue("Y3", out Y3);
                    currentStationDict.TryGetValue("Y4", out Y4);
                }
                else
                {
                    AddLog($"工位 {station} 无数据，无法判断正极掉片！");
                    return "OK";
                }
            }

            //检查数组是否为空
            if (Y1 == null && Y2 == null && Y3 == null && Y4 == null)
            {
                AddLog($"工位 {station} 所有Y数组均为空，无法判断正极掉片！");
                return "OK";
            }

            //初始化结果数组
            double[] result = new double[76];
            double[] result2 = new double[76];
            double[] result3 = new double[76];

            //逐元素计算
            for (int i = 0; i < 76; i++)
            {
                // 获取当前索引的值
                double y1Value = Y1 != null && Y1.Length > i ? Y1[i] : 0;
                double y2Value = Y2 != null && Y2.Length > i ? Y2[i] : 0;
                double y3Value = Y3 != null && Y3.Length > i ? Y3[i] : 0;
                double y4Value = Y4 != null && Y4.Length > i ? Y4[i] : 0;

                // 计算 Y1 + Y3 - Y2 - Y4 并判断是否超0.3
                result[i] = y1Value + y3Value - y2Value - y4Value;
                if (Math.Abs(result[i]) >= Gap46Limit)
                {
                    FirstBOOL = true;
                    //AddLog($"工位 {station} 计算完成，值超过0.3（索引{i}）。");
                }

                // 判断 |Y1-Y3|≥0.5 或 |Y2-Y4|≥0.5
                double absY1MinusY3 = Math.Abs(y1Value - y3Value);
                double absY2MinusY4 = Math.Abs(y2Value - y4Value);
                result2[i] = absY1MinusY3;
                result3[i] = absY2MinusY4;
                if (absY1MinusY3 >= Gap47Limit || absY2MinusY4 >= Gap47Limit)
                {
                    FirstBOOL2 = true;
                    //AddLog($"工位 {station} 满足新增条件（索引{i}）：|Y1-Y3|={absY1MinusY3}, |Y2-Y4|={absY2MinusY4}");
                }

                // 同时满足两个条件时，初步判定为正极掉片
                if (FirstBOOL && FirstBOOL2)
                {
                    // AddLog($"工位 {station} 正极掉片文件路径：{oldFolderPath}");
                    return "NG_正极Y方向掉片";
                }
            }

            // 所有差值均未超阈值
            AddLog($"工位 {station} 计算完成，所有差值均未超过0.3。");
            return "OK";
        }
        // 判断数组越界
        private bool CheckArrayOutOfRange(string ImgName, double[] aaaa, out string? result)
        {
            double upper = 0;
            double lower = 0;

            //处理X类型
            if (ImgName.Contains("1-X"))
            {
                upper = Gap2Limit;    // 1-X 上限：Gap2Limit
                lower = Gap1Limit;    // 1-X 下限：Gap1Limit
            }
            else if (ImgName.Contains("2-X"))
            {
                upper = Gap14Limit;   // 2-X 上限：Gap14Limit
                lower = Gap13Limit;   // 2-X 下限：Gap13Limit
            }
            else if (ImgName.Contains("3-X"))
            {
                upper = Gap24Limit;   // 3-X 上限：Gap24Limit
                lower = Gap23Limit;   // 3-X 下限：Gap23Limit
            }
            else if (ImgName.Contains("4-X"))
            {
                upper = Gap34Limit;   // 4-X 上限：Gap34Limit
                lower = Gap33Limit;   // 4-X 下限：Gap33Limit
            }
            //处理Y类型）
            else if (ImgName.Contains("1-Y"))
            {
                upper = Gap8Limit;    // 1-Y 上限：Gap8Limit
                lower = Gap7Limit;    // 1-Y 下限：Gap7Limit
            }
            else if (ImgName.Contains("2-Y"))
            {
                upper = Gap19Limit;   // 2-Y 上限：Gap19Limit
                lower = Gap18Limit;   // 2-Y 下限：Gap18Limit
            }
            else if (ImgName.Contains("3-Y"))
            {
                upper = Gap29Limit;   // 3-Y 上限：Gap29Limit
                lower = Gap28Limit;   // 3-Y 下限：Gap28Limit
            }
            else if (ImgName.Contains("4-Y"))
            {
                upper = Gap40Limit;   // 4-Y 上限：Gap40Limit
                lower = Gap39Limit;   // 4-Y 下限：Gap39Limit
            }
            else
            {
                result = "未知图片类型";
                return false;
            }

            // 判断数组中是否有元素越界
            bool hasUpperOut = aaaa.Any(num => num > upper);
            bool hasLowerOut = aaaa.Any(num => num < lower);

            if (hasUpperOut || hasLowerOut)
            {
                result = hasUpperOut ? "NG_OH不良偏大" : "NG_OH不良偏小";
                return true;
            }

            result = null;
            return false;
        }
        // 判断负极差不良
        private bool CheckCC(string ImgName, double cc, out string? result)
        {
            result = null;
            double threshold; // 动态阈值，根据图片类型匹配

            // 根据图片名称匹配对应的阈值属性
            if (ImgName.Contains("1-X"))
            {
                threshold = Gap3Limit; // 1-X 对应 Gap3Limit
            }
            else if (ImgName.Contains("1-Y"))
            {
                threshold = Gap9Limit; // 1-Y 对应 Gap9Limit
            }
            else if (ImgName.Contains("4-X"))
            {
                threshold = Gap35Limit; // 4-X 对应 Gap35Limit
            }
            else if (ImgName.Contains("4-Y"))
            {
                threshold = Gap41Limit; // 4-Y 对应 Gap41Limit
            }
            else
            {
                // 不匹配目标类型时，默认不判定为异常
                return false;
            }

            // 判断 cc 是否超过对应阈值
            if (cc > threshold)
            {
                result = "NG_负极差不良";
                return true;
            }

            return false;
        }
        /// <summary>
        /// 检查负极首尾层、正极首尾层、正极中间层间隙最大值是否超差
        /// </summary>
        /// <param name="ImgName">图片名称</param>
        /// <param name="distGapMax">模型输出的间隙最大值数组</param>
        /// <param name="result">异常结果描述</param>
        /// <returns>不良类型</returns>
        private bool CheckGapMax(string ImgName, double[] distGapMax, out string? result)
        {
            result = null;

            // 处理模型异常情况
            if (distGapMax == null || distGapMax.Length == 0)
            {
                result = "NG_正极层数异常（多层或少层）";
                return true;
            }

            // 验证参数格式
            if (distGapMax.Length != 3)
            {
                result = "NG_间隙最大值参数格式错误";
                return true;
            }

            // 提取三个间隙最大值
            double negEndMax = distGapMax[0];       // 负极首尾层间隙最大值
            double posEndMax = distGapMax[1];       // 正极首尾层间隙最大值
            double posMiddleMax = distGapMax[2];    // 正极中间层间隙最大值

            // 根据图片类型匹配对应的阈值
            double negEndThreshold;   // 负极边缘间隙阈值
            double posEndThreshold;   // 正极边缘间隙阈值
            double posMiddleThreshold;// 正极中间间隙阈值

            if (ImgName.Contains("1-X"))
            {
                negEndThreshold = Gap4Limit;     // 1-X 负极边缘间隙阈值
                posEndThreshold = Gap5Limit;     // 1-X 正极边缘间隙阈值
                posMiddleThreshold = Gap6Limit;  // 1-X 正极中间间隙阈值
            }
            else if (ImgName.Contains("1-Y"))
            {
                negEndThreshold = Gap10Limit;    // 1-Y 负极边缘间隙阈值
                posEndThreshold = Gap11Limit;    // 1-Y 正极边缘间隙阈值
                posMiddleThreshold = Gap12Limit; // 1-Y 正极中间间隙阈值
            }
            else if (ImgName.Contains("2-X"))
            {
                negEndThreshold = Gap15Limit;    // 2-X 负极边缘间隙阈值
                posEndThreshold = Gap16Limit;    // 2-X 正极边缘间隙阈值
                posMiddleThreshold = Gap17Limit; // 2-X 正极中间间隙阈值
            }
            else if (ImgName.Contains("2-Y"))
            {
                negEndThreshold = Gap20Limit;    // 2-Y 负极边缘间隙阈值
                posEndThreshold = Gap21Limit;    // 2-Y 正极边缘间隙阈值
                posMiddleThreshold = Gap22Limit; // 2-Y 正极中间间隙阈值
            }
            else if (ImgName.Contains("3-X"))
            {
                negEndThreshold = Gap25Limit;    // 3-X 负极边缘间隙阈值
                posEndThreshold = Gap26Limit;    // 3-X 正极边缘间隙阈值
                posMiddleThreshold = Gap27Limit; // 3-X 正极中间间隙阈值
            }
            else if (ImgName.Contains("3-Y"))
            {
                negEndThreshold = Gap30Limit;    // 3-Y 负极边缘间隙阈值
                posEndThreshold = Gap31Limit;    // 3-Y 正极边缘间隙阈值
                posMiddleThreshold = Gap32Limit; // 3-Y 正极中间间隙阈值
            }
            else if (ImgName.Contains("4-X"))
            {
                negEndThreshold = Gap36Limit;    // 4-X 负极边缘间隙阈值
                posEndThreshold = Gap37Limit;    // 4-X 正极边缘间隙阈值
                posMiddleThreshold = Gap38Limit; // 4-X 正极中间间隙阈值
            }
            else if (ImgName.Contains("4-Y"))
            {
                negEndThreshold = Gap42Limit;    // 4-Y 负极边缘间隙阈值
                posEndThreshold = Gap43Limit;    // 4-Y 正极边缘间隙阈值
                posMiddleThreshold = Gap44Limit; // 4-Y 正极中间间隙阈值
            }
            else
            {
                result = "NG_未知图片类型，无法匹配间隙阈值";
                return true;
            }

            // 按优先级判断超差，返回对应不良类型
            if (negEndMax > negEndThreshold)
            {
                result = "NG_负极边缘间隙不良";
                return true;
            }
            if (posEndMax > posEndThreshold)
            {
                result = "NG_正极边缘间隙不良";
                return true;
            }
            if (posMiddleMax > posMiddleThreshold)
            {
                result = "NG_正极中间间隙不良";
                return true;
            }

            // 未超差
            return false;
        }
        public static bool CountValuesAboveThreshold(double[] array, double threshold, int countThreshold)
        {
            int consecutiveCount = 0;

            foreach (double num in array)
            {
                if (num > threshold)
                {
                    consecutiveCount++;

                    if (consecutiveCount >= countThreshold)
                    {
                        return true; // 找到足够多的连续符合条件的值
                    }
                }
                else
                {
                    consecutiveCount = 0; // 遇到不符合条件的值，重置计数器
                }
            }

            return false; // 没有找到足够的连续符合条件的值
        }

        /// <summary>
        /// 解析缺陷类型（适配统一格式：NG_xxx / OK_xxx）
        /// </summary>
        /// <param name="ctCode">NGOK返回的结果（如"NG_正负极间隙"）</param>
        /// <returns>纯缺陷/状态描述（如"正负极间隙"）</returns>
        private static string GetDefectType(string ctCode)
        {
            if (string.IsNullOrEmpty(ctCode))
                return "未知缺陷";

            // 按"_"分割，取后半部分（核心逻辑）
            string[] parts = ctCode.Split('_');
            if (parts.Length >= 2)
            {
                return parts[1].Trim(); // 取分割后的第二部分（如"NG_正负极间隙"→"正负极间隙"）
            }

            // 特殊情况：无"_"分割（如纯"OK"或异常信息）
            return ctCode.Contains("OK") ? "无" : ctCode.Trim();
        }



        private void UpdateStationImage(string station, ImageSource wpfImage, HObject hImage)
        {
            //检查是否在 UI 线程，不在则调度
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                    UpdateStationImage(station, wpfImage, hImage)));
                return;
            }

            // 检查传入的 HObject 是否有效
            if (hImage == null || !hImage.IsInitialized())
            {
                AddLog($"[更新图像失败] 传入的 HObject 无效（为空或未初始化）。工位: {station}");
                return;
            }

            HObject? newHImage = null;
            try
            {
                // 复制 HObject
                HOperatorSet.CopyImage(hImage, out newHImage);

                //根据工位更新 UI 和保存 HObject
                switch (station)
                {
                    case "LAB":
                        _detectionResultImageLAB?.Dispose();
                        CurrentResultImageLAB = wpfImage;
                        _detectionResultImageLAB = newHImage;
                        break;
                    case "LCD":
                        _detectionResultImageLCD?.Dispose();
                        CurrentResultImageLCD = wpfImage;
                        _detectionResultImageLCD = newHImage;
                        break;
                    case "RAB":
                        _detectionResultImageRAB?.Dispose();
                        CurrentResultImageRAB = wpfImage;
                        _detectionResultImageRAB = newHImage;
                        break;
                    case "RCD":
                        _detectionResultImageRCD?.Dispose();
                        CurrentResultImageRCD = wpfImage;
                        _detectionResultImageRCD = newHImage;
                        break;
                    default:
                        AddLog($"[未知工位] 无法更新{station}的图像");
                        newHImage.Dispose();
                        break;
                }
            }
            catch (HalconException ex)
            {
                AddLog($"[更新图像-HALCON异常] 工位: {station}, 错误: {ex.Message}");
                newHImage?.Dispose();
            }
            catch (Exception ex)
            {
                AddLog($"[更新图像-通用异常] 工位: {station}, 错误: {ex.Message}");
                newHImage?.Dispose();
            }
        }


        // 核心检测逻辑
        public (string? ctCode, HObject? resultImage) GapDetection(string imageFilePath, string station)
        {
            // 检测过程临时变量
            HObject? recNE = null, recPE = null, recMiddlePE = null, originImg = null;
            HTuple? distOHInt = null, distOHOut = null, distSortPEColMiddle = null, distHTPE = null, _gapMax = null;
            HTuple? peRows = null, peCols = null, neRows = null, neCols = null, middlePERows = null, middlePECols = null;
            float hDNe = 0;
            string? ctCode = "OK", resultCode = "OK", errorReport = "";
            int imgWidth = 0, imgHeight = 0, neNGflag = 0;
            List<int> ohAbnormalPositions1 = new List<int>(), ohAbnormalPositions2 = new List<int>();
            double[]? arr1 = null;
            double[]? gapMaxArray = null;
            double[]? oHoutarr = null;
            //double[]? distSortPEColMiddlearr = null;

            // 绘图相关变量
            HObject? resultImage = new HObject();
            Stopwatch? drawStopwatch = null;

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch processingStopwatch = Stopwatch.StartNew();
            Stopwatch gapDetectionTimer = Stopwatch.StartNew();

            //int maxRetries = 5;  // 最大重试次数
            //int retryDelayMs = 200;  // 每次重试间隔（毫秒）
            //bool isFileReady = false;

            AddLog($"[检测开始] 文件：{Path.GetFileName(imageFilePath)}");

            ////等待文件就绪
            //for (int i = 0; i < maxRetries; i++)
            //{
            //    FileStream? fs = null;
            //    try
            //    {
            //        // 尝试以「独占读写」模式打开文件
            //        fs = File.Open(imageFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            //        isFileReady = true;
            //        break; 
            //    }
            //    catch (IOException)
            //    {
            //        // 捕获「文件被占用」异常，等待后重试
            //        AddLog($"[文件等待] 第{i + 1}/{maxRetries}次等待文件就绪... 文件：{Path.GetFileName(imageFilePath)}");
            //        System.Threading.Thread.Sleep(retryDelayMs);
            //    }
            //    finally
            //    {
            //        fs?.Close();  //关闭文件流
            //    }
            //}

            //// 检查文件是否就绪，未就绪则直接返回失败
            //if (!isFileReady)
            //{
            //    AddLog($"[文件读取失败] 重试{maxRetries}次后仍无法获取文件，可能被占用或写入未完成。文件：{Path.GetFileName(imageFilePath)}");
            //    return ("文件未就绪", null);
            //}


            try
            {
                //AddLog($"[检测开始] 文件：{Path.GetFileName(imageFilePath)}");

                //读取并预处理图像
                HOperatorSet.ReadImage(out originImg, imageFilePath);
                originImg = PreprocessImage(originImg);

                // 检查图像有效性
                if (originImg == null || !originImg.IsInitialized())
                {
                    AddLog($"[图像无效] 文件：{Path.GetFileName(imageFilePath)}");
                    return ("图像读取失败", null);
                }


                //获取图像尺寸
                HOperatorSet.GetImageSize(originImg, out HTuple width, out HTuple height);
                imgWidth = (int)width.I;
                imgHeight = (int)height.I;
                AddLog($"[图像尺寸] 宽：{imgWidth}，高：{imgHeight}");

                //执行核心算法
                Stopwatch algorithmStopwatch = new Stopwatch();
                algorithmStopwatch.Start();

                // 初始化算法输出对象
                recNE = new HObject();
                recPE = new HObject();
                recMiddlePE = new HObject();
                distOHInt = new HTuple();
                distOHOut = new HTuple();
                distSortPEColMiddle = new HTuple();
                distHTPE = new HTuple();
                _gapMax = new HTuple();

                // 调用算法模型
                //H246modle?.Dispose();
                switch (station)
                {
                    case "LAB":
                        //H246modle = LABmodel;
                        ai.Algorithm_Action(imageFilePath, LABmodel!, out recNE, out recPE, out recMiddlePE, out distOHInt, out distOHOut, out _gapMax, out hDNe, out neNGflag, out errorReport);
                        break;
                    case "LCD":
                        //H246modle = LCDmodel;
                        ai.Algorithm_Action(imageFilePath, LCDmodel!, out recNE, out recPE, out recMiddlePE, out distOHInt, out distOHOut, out _gapMax, out hDNe, out neNGflag, out errorReport);
                        break;
                    case "RAB":
                        //H246modle = RABmodel;
                        ai.Algorithm_Action(imageFilePath, RABmodel!, out recNE, out recPE, out recMiddlePE, out distOHInt, out distOHOut, out _gapMax, out hDNe, out neNGflag, out errorReport);
                        break;
                    case "RCD":
                        //H246modle = RCDmodel;
                        ai.Algorithm_Action(imageFilePath, RCDmodel!, out recNE, out recPE, out recMiddlePE, out distOHInt, out distOHOut, out _gapMax, out hDNe, out neNGflag, out errorReport);
                        break;
                }
                algorithmStopwatch.Stop();
                //ai.Algorithm_Action(
                //    imageFilePath,
                //    H246modle,
                //    out recNE,
                //    out recPE,
                //    out recMiddlePE,
                //    out distOHInt,
                //    out distOHOut,
                //    out _gapMax,
                //    out hDNe,
                //    out neNGflag,
                //    out errorReport);
                //algorithmStopwatch.Stop();
                AddLog($"[算法耗时] {algorithmStopwatch.ElapsedMilliseconds} ms");

                #region 检测结果判断与数据解析
                if (string.IsNullOrEmpty(errorReport))
                {
                    // 获取区域坐标
                    HOperatorSet.AreaCenter(recPE, out _, out peRows, out peCols);
                    HOperatorSet.AreaCenter(recNE, out _, out neRows, out neCols);
                    HOperatorSet.AreaCenter(recMiddlePE, out _, out middlePERows, out middlePECols);

                    // 解析算法数据为double数组
                    arr1 = ConvertStringArrayToDoubleArray(distOHInt.SArr); // 从distOHInt提取核心数据
                    gapMaxArray = ConvertStringArrayToDoubleArray(_gapMax.SArr);
                    oHoutarr = ConvertStringArrayToDoubleArray(distOHOut.SArr); // bbbb参数（负极OH值）
                    //distSortPEColMiddlearr = ConvertStringArrayToDoubleArray(distSortPEColMiddle.SArr); // numbers参数（正极间隙值）
                    ohAbnormalPositions1 = GetAbnormalPositions(arr1, imageFilePath); // 异常位置标记

                    // 根据文件名给Y1-Y4赋值
                    string fileName = Path.GetFileName(imageFilePath);
                    string namePart = fileName.Split('_')[1];
                    lock (stationDataLocker)
                    {
                        // 确保外层字典包含当前工位
                        if (!stationYDataDict.ContainsKey(station))
                        {
                            stationYDataDict[station] = new Dictionary<string, double[]>();
                        }
                        // 获取当前工位的内层字典（存储该工位的Y1-Y4）
                        Dictionary<string, double[]> currentStationDict = stationYDataDict[station];

                        // 根据图片类型给当前工位的对应Y值赋值
                        if (namePart.Contains("1-Y"))
                        {
                            currentStationDict["Y1"] = arr1;
                        }
                        else if (namePart.Contains("2-Y"))
                        {
                            currentStationDict["Y2"] = arr1;
                        }
                        else if (namePart.Contains("3-Y"))
                        {
                            currentStationDict["Y3"] = arr1;
                        }
                        else if (namePart.Contains("4-Y"))
                        {
                            currentStationDict["Y4"] = arr1;
                        }
                    }

                    // 判断OK/NG
                    string imgNamePart = namePart;
                    ctCode = NGOK(
                        peRows.DArr.Length, neRows.DArr.Length,
                        arr1, // aaaa参数（正极OH值）
                        oHoutarr, // bbbb参数（负极OH值）
                        hDNe, // cc参数（负极差阈值）
                        imgNamePart,
                        gapMaxArray,
                        neNGflag);

                    resultCode = ctCode?.Contains("OK") ?? false ? "OK" : "NG";
                    AddLog($"[检测结果] {fileName} - {ctCode}");

                    //生成独立结果图片
                    drawStopwatch = new Stopwatch();
                    drawStopwatch.Start();
                    resultImage = GenerateResultImage(
                        originImg, imgWidth, imgHeight,
                        recPE, recNE, recMiddlePE,
                        peRows, peCols, neRows, neCols, middlePERows, middlePECols,
                        distOHInt, distOHOut, distSortPEColMiddle,
                        hDNe, ctCode, resultCode,
                        ohAbnormalPositions1, ohAbnormalPositions2,
                        arr1, ConvertStringArrayToDoubleArray(distOHOut.SArr), gapMaxArray,
                        imageFilePath);
                    drawStopwatch.Stop();
                    AddLog($"[绘图耗时] {drawStopwatch.ElapsedMilliseconds} ms");

                    // 更新UI实时预览
                    if (resultImage != null && resultImage.IsInitialized())
                    {
                        ImageSource wpfImage = ConvertHObjectToImageSourceFast(resultImage);
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateStationImage(station, wpfImage, resultImage);
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                        AddLog($"[实时更新] {station}工位 - 直接显示新图：{fileName}");
                    }
                    else
                    {
                        AddLog($"[绘图失败] {fileName} 结果图生成无效");
                        resultImage?.Dispose();
                        resultImage = null;
                    }
                }
                else
                {
                    // 算法异常：归档原图，返回null结果
                    AddLog($"[算法异常] {Path.GetFileName(imageFilePath)}，原因：{errorReport}");
                    ArchiveAbnormalImage(imageFilePath, errorReport);
                    resultImage?.Dispose();
                    return ("Internal_Error", null);
                }
                #endregion

            }
            catch (Exception ex)
            {
                // 其他异常：仅归档原图
                AddLog($"[检测异常] {Path.GetFileName(imageFilePath)}，原因：{ex.Message}");
                ArchiveAbnormalImage(imageFilePath, ex.Message);
                resultImage?.Dispose();
                return ("Internal_Error", null); // 异常标记
            }
            finally
            {
                // 释放所有临时HALCON对象
                originImg?.Dispose();
                recNE?.Dispose();
                recPE?.Dispose();
                recMiddlePE?.Dispose();
                distOHInt?.Dispose();
                distOHOut?.Dispose();
                distSortPEColMiddle?.Dispose();
                distHTPE?.Dispose();
                _gapMax?.Dispose();
                peRows?.Dispose();
                peCols?.Dispose();
                neRows?.Dispose();
                neCols?.Dispose();
                middlePERows?.Dispose();
                middlePECols?.Dispose();

                // 释放算法相关的计时器
                totalStopwatch?.Stop();
                processingStopwatch?.Stop();
                gapDetectionTimer?.Stop();
                drawStopwatch?.Stop();
            }

            //totalStopwatch.Stop();
            AddLog($"[检测完成] {Path.GetFileName(imageFilePath)} 总耗时：{totalStopwatch.ElapsedMilliseconds} ms");
            // 返回检测编码和独立结果图像
            return (ctCode, resultImage);
        }
        private void ArchiveAbnormalImage(string imageFilePath, string reason)
        {
            try
            {
                string fileName = Path.GetFileName(imageFilePath);
                string? originalDir = Path.GetDirectoryName(imageFilePath);
                if (string.IsNullOrEmpty(originalDir))
                {
                    AddLog($"[归档失败] 无效的原图路径：{imageFilePath}");
                    return;
                }

                string? targetDrive = SelectedResultImagePath;
                string baseArchivePath = Path.Combine(targetDrive ?? @"E:\", "IMEResult", "算法异常");
                string? rootDir = Path.GetPathRoot(originalDir);
                string relativePath = originalDir.Substring(rootDir?.Length ?? 0);
                string targetDir = Path.Combine(baseArchivePath, relativePath);
                Directory.CreateDirectory(targetDir);
                string destPath = Path.Combine(targetDir, fileName);
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Copy(imageFilePath, destPath, overwrite: true);
                AddLog($"[异常归档成功] 原图已保存至：{destPath}，原因：{reason}");
            }
            catch (Exception ex)
            {
                AddLog($"[归档失败] 路径：{imageFilePath}，错误：{ex.Message}，堆栈：{ex.StackTrace}");
            }
        }
        private static ImageSource ConvertHObjectToImageSourceFast(HObject halconImage)
        {
            // 获取图像通道信息
            HOperatorSet.CountChannels(halconImage, out HTuple channels);

            if (channels.I == 1)
            {
                // 单通道图像
                return ConvertGrayImage(halconImage);
            }

            else if (channels.I == 3)
            {
                // 三通道图像
                return ConvertColorImage(halconImage);
            }
            else
            {
                return ConvertColorImage(halconImage);
            }

        }

        //转换灰度图像
        private static ImageSource ConvertGrayImage(HObject halconImage)
        {
            HOperatorSet.GetImagePointer1(halconImage, out HTuple pointer, out HTuple type, out HTuple width, out HTuple height);

            int imgWidth = width.I;
            int imgHeight = height.I;

            PixelFormat pixelFormat;
            int bytesPerPixel;

            if (type.S == "byte")
            {
                pixelFormat = PixelFormats.Gray8;
                bytesPerPixel = 1;
            }
            else if (type.S == "uint2")
            {
                pixelFormat = PixelFormats.Gray16;
                bytesPerPixel = 2;
            }
            else
            {
                pixelFormat = PixelFormats.Gray8;
                bytesPerPixel = 1;
            }

            int stride = (imgWidth * bytesPerPixel + 3) & ~3; // 4字节对齐
            byte[] buffer = new byte[stride * imgHeight];
            System.Runtime.InteropServices.Marshal.Copy(pointer, buffer, 0, buffer.Length);

            BitmapSource bitmapSource = BitmapSource.Create(
                imgWidth, imgHeight, 96, 96, pixelFormat, null, buffer, stride);

            bitmapSource.Freeze();
            return bitmapSource;
        }

        //转换彩色图像
        private static ImageSource ConvertColorImage(HObject halconImage)
        {
            // 获取三个通道的指针
            HOperatorSet.GetImagePointer3(halconImage,
                out HTuple pointerRed,
                out HTuple pointerGreen,
                out HTuple pointerBlue,
                out HTuple type,
                out HTuple width,
                out HTuple height);

            int imgWidth = width.I;
            int imgHeight = height.I;

            // 创建 RGB 缓冲区
            int bytesPerPixel = 3;
            int stride = (imgWidth * bytesPerPixel + 3) & ~3;
            byte[] buffer = new byte[stride * imgHeight];

            // 获取各个通道的数据
            byte[] redChannel = new byte[imgWidth * imgHeight];
            byte[] greenChannel = new byte[imgWidth * imgHeight];
            byte[] blueChannel = new byte[imgWidth * imgHeight];

            System.Runtime.InteropServices.Marshal.Copy(pointerRed, redChannel, 0, redChannel.Length);
            System.Runtime.InteropServices.Marshal.Copy(pointerGreen, greenChannel, 0, greenChannel.Length);
            System.Runtime.InteropServices.Marshal.Copy(pointerBlue, blueChannel, 0, blueChannel.Length);

            // 合并通道到 RGB 缓冲区
            for (int y = 0; y < imgHeight; y++)
            {
                for (int x = 0; x < imgWidth; x++)
                {
                    int srcIndex = y * imgWidth + x;
                    int dstIndex = y * stride + x * 3;

                    // WPF 使用 BGR 顺序，所以需要交换 R 和 B
                    buffer[dstIndex] = blueChannel[srcIndex];     // B
                    buffer[dstIndex + 1] = greenChannel[srcIndex]; // G
                    buffer[dstIndex + 2] = redChannel[srcIndex];   // R
                }
            }

            BitmapSource bitmapSource = BitmapSource.Create(
                imgWidth, imgHeight, 96, 96, PixelFormats.Bgr24, null, buffer, stride);

            bitmapSource.Freeze();
            return bitmapSource;
        }

        // 根据station和barcode获取专属队列
        private ConcurrentQueue<string> GetBarcodeQueue(string station, string barcode)
        {
            string key = $"{station}_{barcode}";
            return _barcodeQueues.GetOrAdd(key, _ => new ConcurrentQueue<string>());
        }
        //取当前条码队列的专属锁
        private object GetBarcodeQueueLock(string station, string barcode)
        {
            string key = $"{station}_{barcode}";
            return _barcodeQueueLocks.GetOrAdd(key, _ => new object());
        }

        #region 超时处理核心逻辑
        /// <summary>
        /// 定时清理所有工位的超时条码
        /// </summary>
        private void CleanupTimeoutBarcodes(object? sender, System.Timers.ElapsedEventArgs e)
        {
            // 检查是否有任何工位处于运行状态，如果都没有，则跳过
            if (!IsOnlineDetectionEnabledLAB && !IsOnlineDetectionEnabledLCD && !IsOnlineDetectionEnabledRAB && !IsOnlineDetectionEnabledRCD)
            {
                return;
            }

            List<string> barcodesToCheck = new List<string>();
            lock (_barcodeTimeLock)
            {
                barcodesToCheck.AddRange(_barcodeLastActivityTime.Keys);
            }

            TimeSpan timeoutThreshold = TimeSpan.FromMinutes(10); // 超时阈值：10分钟
            foreach (var barcode in barcodesToCheck)
            {
                DateTime lastActivityTime;
                lock (_barcodeTimeLock)
                {
                    if (!_barcodeLastActivityTime.TryGetValue(barcode, out lastActivityTime))
                    {
                        continue;
                    }
                }

                // 检查是否超时
                if (DateTime.Now - lastActivityTime > timeoutThreshold)
                {
                    // 由于一个条码可能在多个工位出现，找到它属于哪个工位
                    string? station = FindStationForBarcode(barcode);

                    if (station != null)
                    {
                        AddLog($"[{station}-超时处理] 条码{barcode}超过{timeoutThreshold.TotalMinutes}分钟未凑齐8张，开始保存部分结果");
                        _ = ProcessPartialResultsForBarcode(barcode, station);
                    }
                    else
                    {
                        // 如果找不到对应的工位，可能是队列已被清空，但时间记录未被移除
                        AddLog($"[超时处理-警告] 找不到条码{barcode}对应的工位，可能已被处理。将清理时间记录。");
                        lock (_barcodeTimeLock)
                        {
                            _barcodeLastActivityTime.Remove(barcode);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 通过检查各工位的队列，找到条码所属的工位
        /// </summary>
        private string? FindStationForBarcode(string barcode)
        {
            // 检查LAB工位
            if (IsOnlineDetectionEnabledLAB)
            {
                var queue = GetBarcodeQueue("LAB", barcode);
                lock (GetBarcodeQueueLock("LAB", barcode))
                {
                    if (queue.Count > 0)
                    {
                        return "LAB";
                    }
                }
            }
            // 检查LCD工位
            if (IsOnlineDetectionEnabledLCD)
            {
                var queue = GetBarcodeQueue("LCD", barcode);
                lock (GetBarcodeQueueLock("LCD", barcode))
                {
                    if (queue.Count > 0)
                    {
                        return "LCD";
                    }
                }
            }
            // 检查RAB工位
            if (IsOnlineDetectionEnabledRAB)
            {
                var queue = GetBarcodeQueue("RAB", barcode);
                lock (GetBarcodeQueueLock("RAB", barcode))
                {
                    if (queue.Count > 0)
                    {
                        return "RAB";
                    }
                }
            }
            // 检查RCD工位
            if (IsOnlineDetectionEnabledRCD)
            {
                var queue = GetBarcodeQueue("RCD", barcode);
                lock (GetBarcodeQueueLock("RCD", barcode))
                {
                    if (queue.Count > 0)
                    {
                        return "RCD";
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 处理单个条码的部分结果保存
        /// </summary>
        private async Task ProcessPartialResultsForBarcode(string barcode, string station)
        {
            ConcurrentQueue<string> targetQueue = GetBarcodeQueue(station, barcode);
            object targetQueueLock = GetBarcodeQueueLock(station, barcode);
            List<string> itemsToProcess = new List<string>();

            //提取队列中的所有项
            lock (targetQueueLock)
            {
                while (targetQueue.TryDequeue(out string? item))
                {
                    itemsToProcess.Add(item);
                }
            }

            if (itemsToProcess.Count == 0)
            {
                AddLog($"[{station}-超时处理] 条码{barcode}队列已空，无需保存");
                lock (_barcodeTimeLock)
                {
                    _barcodeLastActivityTime.Remove(barcode);
                }
                return;
            }

            //提取缓存中的HObject
            List<(string imgPath, HObject resultImage)> partialResults = new List<(string, HObject)>();
            List<string> cachedImgPaths = new List<string>(); // 记录缓存中存在的图片路径
            lock (_cacheLock)
            {
                foreach (var item in itemsToProcess)
                {
                    string[] parts = item.Split(new[] { "||" }, StringSplitOptions.None);
                    if (parts.Length != 2) continue;

                    string imgPath = parts[0];
                    if (_firstDetectionImageCache.TryGetValue(imgPath, out HObject? resultImage) && resultImage.IsInitialized())
                    {
                        partialResults.Add((imgPath, resultImage));
                        cachedImgPaths.Add(imgPath); // 仅记录缓存中存在的路径
                    }
                }
            }

            if (partialResults.Count > 0)
            {
                try
                {
                    await SavePartialResults(partialResults, barcode, station);
                    AddLog($"[{station}-超时处理] 条码{barcode}成功保存{partialResults.Count}张部分结果");
                }
                finally
                {
                    //确认HObject释放后，再移除缓存
                    lock (_cacheLock)
                    {
                        foreach (var imgPath in cachedImgPaths)
                        {
                            if (_firstDetectionImageCache.ContainsKey(imgPath))
                            {
                                _firstDetectionImageCache.Remove(imgPath);
                            }
                        }
                    }
                }
            }
            else
            {
                AddLog($"[{station}-超时处理] 条码{barcode}未找到有效缓存，无法保存");
            }

            // 移除条码的最后活动时间记录
            lock (_barcodeTimeLock)
            {
                _barcodeLastActivityTime.Remove(barcode);
            }
        }

        /// <summary>
        /// 保存部分结果（仅保存图片到对应工位的文件夹，不进行数据库操作和缺陷分类）
        /// </summary>
        private async Task SavePartialResults(List<(string imgPath, HObject resultImage)> partialResults, string barcode, string station)
        {
            if (partialResults.Count == 0) return;

            await Task.Run(() =>
            {
                try
                {

                    string? rootSavePath = SelectedResultImagePath;

                    if (string.IsNullOrEmpty(rootSavePath))
                    {
                        // 根路径未设置，选择默认路径或记录错误
                        rootSavePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        AddLog($"[{station}-部分结果保存-警告] 结果保存根路径未设置，将使用默认图片文件夹: {rootSavePath}");
                    }
                    else if (!Directory.Exists(rootSavePath))
                    {
                        AddLog($"[{station}-部分结果保存-警告] 结果保存根路径不存在，将尝试创建: {rootSavePath}");
                        Directory.CreateDirectory(rootSavePath);
                    }

                    // 构建目标目录路径：IMEResult\超时未达八张\{station}\{barcode}
                    string targetDir = Path.Combine(
                        rootSavePath,
                        "IMEResult",
                        "超时未达八张",
                        station,
                        barcode
                    );


                    Directory.CreateDirectory(targetDir);
                    AddLog($"[{station}-部分结果保存] 创建目标目录：{targetDir}");

                    foreach (var result in partialResults)
                    {
                        try
                        {
                            // 保持原始文件名
                            string fileName = Path.GetFileName(result.imgPath);
                            string savePath = Path.Combine(targetDir, fileName);
                            HOperatorSet.WriteImage(result.resultImage, "png", 0, savePath);
                            AddLog($"[{station}-部分结果保存] 成功：{savePath}");
                        }
                        finally
                        {
                            if (result.resultImage != null && result.resultImage.IsInitialized())
                            {
                                result.resultImage.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"[{station}-部分结果保存异常] {ex.Message}，堆栈：{ex.StackTrace}");
                }
            });
        }
        #endregion

        /// <summary>
        /// 检测后处理：队列处理、统计更新、结果保存
        /// </summary>
        private async Task HandlePostDetection(string? ctCode, string resultCode, string imageFilePath, string station)
        {
            try
            {
                string fileName = Path.GetFileName(imageFilePath);
                string barcode = "未知条码";
                string[] fileNameParts = fileName.Split('_');
                if (fileName.Contains("auto"))
                {
                    barcode = $"{fileNameParts[0]}_{fileNameParts[1]}";
                }
                else
                {
                    barcode = fileNameParts[0];
                }
                //string barcode = fileName.Split('_')[0];
                bool isOK = resultCode == "OK";

                //获取当前条码的专属队列和锁
                ConcurrentQueue<string> targetQueue = GetBarcodeQueue(station, barcode);
                object targetQueueLock = GetBarcodeQueueLock(station, barcode);
                //当前文件只进入自己条码的队列
                string queueItem = $"{imageFilePath}||{ctCode}";
                DateTime startTime = DateTime.Now;
                lock (targetQueueLock)
                {
                    int previousCount = targetQueue.Count;
                    targetQueue.Enqueue(queueItem);
                    int currentCount = targetQueue.Count;
                    lock (_barcodeTimeLock)
                    {
                        _barcodeLastActivityTime[barcode] = DateTime.Now;
                    }
                    // 如果是第一张图，记录开始时间
                    if (previousCount == 0 && currentCount == 1)
                    {
                        startTime = DateTime.Now;
                        _cellStartTimes.AddOrUpdate((station, barcode), startTime, (key, oldValue) => startTime);
                        AddLog($"[{station}-{barcode}开始计时] 第一张图入队，开始时间：{startTime:yyyy-MM-dd HH:mm:ss.fff}");
                    }
                    else if (_cellStartTimes.TryGetValue((station, barcode), out var existingStartTime))
                    {
                        startTime = existingStartTime;
                    }

                    AddLog($"[{station}-{barcode}入队] 项：{queueItem}，队列长度：{targetQueue.Count}");
                }

                // 检查当前条码的队列是否满8张
                bool needProcess = false;
                lock (targetQueueLock)
                {
                    needProcess = targetQueue.Count >= 8;
                }
                if (needProcess)
                {
                    lock (_barcodeTimeLock)
                    {
                        _barcodeLastActivityTime.Remove(barcode);
                    }
                    AddLog($"[{station}-{barcode}队列满8张] 开始组包处理");
                    List<string> itemsToProcess = new List<string>();
                    bool queueValid = true;

                    // 出队8项
                    lock (targetQueueLock)
                    {
                        if (targetQueue.Count < 8)
                        {
                            queueValid = false;
                            AddLog($"[{station}-{barcode}队列长度变化] 实际长度{targetQueue.Count} < 8，中断处理");
                        }
                        else
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                if (!targetQueue.TryDequeue(out string? item))
                                {
                                    queueValid = false;
                                    AddLog($"[{station}-{barcode}出队失败] 第{i + 1}项：队列突然为空");
                                    break;
                                }
                                itemsToProcess.Add(item);
                            }
                        }
                    }

                    // 组包验证通过
                    if (queueValid && itemsToProcess.Count == 8)
                    {

                        AddLog($"[{station}-{barcode}组包成功] 开始提取缓存");
                        List<(string imgPath, string ctCode, HObject resultImage)> firstDetectionResults = new List<(string, string, HObject)>();

                        foreach (var item in itemsToProcess)
                        {
                            string[] itemParts = item.Split(new[] { "||" }, StringSplitOptions.None);
                            if (itemParts.Length != 2)
                            {
                                queueValid = false;
                                AddLog($"[{station}-{barcode}格式错误] 项：{item}");
                                break;
                            }
                            string imgPath = itemParts[0];
                            string innerCtCode = itemParts[1];

                            // 检查缓存
                            lock (_cacheLock)
                            {
                                if (_firstDetectionImageCache.TryGetValue(imgPath, out HObject? resultImage)
                                    && resultImage != null && resultImage.IsInitialized())
                                {
                                    firstDetectionResults.Add((imgPath, innerCtCode, resultImage));
                                    _firstDetectionImageCache.Remove(imgPath);
                                }
                                else
                                {
                                    queueValid = false;
                                    AddLog($"[{station}-{barcode}缓存缺失] 文件：{Path.GetFileName(imgPath)}");
                                    break;
                                }
                            }
                        }

                        if (queueValid && firstDetectionResults.Count == 8)
                        {
                            DateTime actualStartTime;
                            if (!_cellStartTimes.TryRemove((station, barcode), out actualStartTime))
                            {
                                actualStartTime = startTime; // 如果获取失败，使用当前方法中的startTime
                            }
                            TimeSpan queueWaitTime = DateTime.Now - actualStartTime;
                            // 调用保存逻辑
                            bool finalIsOK = await ProcessBadTypeStatistics(firstDetectionResults, barcode, station, actualStartTime);
                            AddLog($"[{station}-{barcode}保存完成] 结果：{(finalIsOK ? "OK" : "NG")}");

                            lock (_countLock)
                            {
                                if (finalIsOK) OKDetectionNum++;
                                else NGDetectionNum++;
                                OnPropertiesChanged(nameof(OKDetectionNum));
                                OnPropertiesChanged(nameof(NGDetectionNum));
                                OnPropertiesChanged(nameof(TotalDetectionNum));
                            }
                        }
                        else
                        {
                            AddLog($"[{station}-{barcode}缓存提取失败] 有效项{firstDetectionResults.Count}/8");
                            // 回退有效资源
                            foreach (var result in firstDetectionResults)
                            {
                                lock (_cacheLock)
                                {
                                    if (!_firstDetectionImageCache.ContainsKey(result.imgPath))
                                    {
                                        _firstDetectionImageCache[result.imgPath] = result.resultImage;
                                        AddLog($"[{station}-资源回退] 文件：{Path.GetFileName(result.imgPath)}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        AddLog($"[{station}-{barcode}组包失败] 有效项{itemsToProcess.Count}/8，queueValid={queueValid}");
                    }
                }
                else
                {
                    AddLog($"[{station}-{barcode}队列未满] 当前长度{targetQueue.Count}/8");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[{station}-Handle异常] {ex.Message}");
            }
        }
        /// <summary>
        /// 处理不良类型统计与文件移动
        /// </summary>
        private async Task<bool> ProcessBadTypeStatistics(
            List<(string imgPath, string ctCode, HObject resultImage)> firstDetectionResults,
            string barcode,
            string station, DateTime startTime)
        {
            string? savedResultImagePath = null;
            bool finalIsOK = true;
            string finalHighestPriorityDefect = "无缺陷";
            List<string> confirmedDefects = new List<string>();
            DateTime processStartTime = DateTime.Now;
            DateTime endTime = DateTime.Now;

            try
            {

                // 获取文件夹路径
                if (firstDetectionResults.Count == 0)
                {
                    AddLog($"[{station}-处理异常] firstDetectionResults为空，无图可保存");
                    return false;
                }
                string? folderPath = Path.GetDirectoryName(firstDetectionResults[0].imgPath);
                if (string.IsNullOrEmpty(folderPath))
                {
                    AddLog($"[{station}-处理异常] 无效路径（第一张图路径：{firstDetectionResults[0].imgPath}）");
                    return false;
                }
                // 预计算每张图片的最终结果和确认的缺陷列表
                var imageFinalResults = new List<(string imgPath, HObject resultImage, string individualResultName)>();
                foreach (var result in firstDetectionResults)
                {
                    string defectType = GetBadTypeFromItem(result.ctCode);
                    string individualResultName;

                    if (defectType == "正极Y方向掉片")
                    {
                        string zjydpResult = HandleZJYDP(station);
                        if (zjydpResult == "NG_正极Y方向掉片")
                        {
                            individualResultName = "NG_正极Y方向掉片";
                            confirmedDefects.Add(defectType); // 添加到确认缺陷列表
                        }
                        else
                        {
                            individualResultName = "OK";
                        }
                    }
                    else if (!string.IsNullOrEmpty(defectType) && defectType != "未知不良")
                    {
                        individualResultName = $"NG_{defectType}";
                        confirmedDefects.Add(defectType); // 添加到确认缺陷列表
                    }
                    else
                    {
                        individualResultName = "OK";
                    }

                    imageFinalResults.Add((result.imgPath, result.resultImage, individualResultName));
                }
                if (confirmedDefects.Any())
                {
                    finalIsOK = false;

                    // 缺陷优先级排序
                    Dictionary<string, int> badTypePriority = new Dictionary<string, int>
                        {
                            {"负极压伤", 1},{"负极边缘间隙不良", 2}, {"正极边缘间隙不良", 3},  {"正极中间间隙不良", 4},
                            {"负极多片", 5},{"正极Y方向掉片", 6},{"OH不良", 7},{"负极少片", 8}, {"正极少片", 9},
                            {"正极多片", 10}, {"负极差不良", 11}
                        };

                    var topPriorityDefect = confirmedDefects.Distinct()
                        .Select(d => new { Defect = d, Priority = badTypePriority.TryGetValue(d, out int p) ? p : int.MaxValue })
                        .OrderBy(item => item.Priority)
                        .FirstOrDefault();

                    if (topPriorityDefect != null)
                    {
                        finalHighestPriorityDefect = topPriorityDefect.Defect;

                        lock (_countLock)
                        {
                            //仅对最高优先级的缺陷进行计数
                            switch (topPriorityDefect.Defect)
                            {
                                case "OH不良": Count_OHBL++; break;
                                case "负极少片": Count_FJSP++; break;
                                case "负极多片": Count_FJDP++; break;
                                case "正极少片": Count_ZJSP++; break;
                                case "正极多片": Count_ZJDP++; break;
                                case "负极差不良": Count_CPBL++; break;
                                case "负极边缘间隙不良": Count_FJBYJXBL++; break;
                                case "正极边缘间隙不良": Count_ZJBYJXBL++; break;
                                case "正极中间间隙不良": Count_ZJZJJXBL++; break;
                                case "负极压伤": Count_FJYS++; break;
                                case "正极Y方向掉片": Count_ZJYDP++; AddLog($"[{station}-掉片计数] 电芯{barcode}，Count_ZJYDP增加到：{Count_ZJYDP}"); break;
                                default:
                                    AddLog($"[{station}-警告] 未知缺陷：{topPriorityDefect.Defect}");
                                    break;
                            }
                            UpdateRiskRatios();
                        }
                    }
                }

                await Task.Run(async () =>
                {
                    try
                    {
                        //目录名由电芯最高优先级缺陷决定
                        string unifiedDefectType = finalIsOK ? "OK" : finalHighestPriorityDefect;
                        string? targetDir = await GetUnifiedSaveDirectory(firstDetectionResults[0].imgPath, barcode, unifiedDefectType);

                        if (string.IsNullOrEmpty(targetDir))
                        {
                            AddLog($"[{station}-目录创建失败] 无法为电芯{barcode}创建保存目录");
                            finalIsOK = false;
                            return;
                        }

                        AddLog($"[{station}-批量保存] 电芯{barcode}的8张图将保存到：{unifiedDefectType} 文件夹");

                        // 保存所有图片
                        for (int i = 0; i < imageFinalResults.Count; i++)
                        {
                            var processedResult = imageFinalResults[i];
                            try
                            {
                                string? resultPath = await SaveGapDetectionResultBatch(
                                    processedResult.imgPath,
                                    processedResult.individualResultName,
                                    (newPath) => HOperatorSet.WriteImage(processedResult.resultImage, "png", 0, newPath),
                                    unifiedDefectType,
                                    targetDir);

                                if (i == 0 && !string.IsNullOrEmpty(resultPath))
                                {
                                    savedResultImagePath = resultPath;
                                }
                            }
                            finally
                            {
                                processedResult.resultImage.Dispose();
                            }
                        }
                        AddLog($"[{station}-保存完成] 电芯{barcode}的8张图已全部保存。");

                        //保存到数据库
                        var detectionRecord = CreateDetectionRecord(
                            detectionTime: DateTime.Now,
                            cellBarcode: barcode,
                            detectionResult: finalIsOK ? "OK" : "NG",
                            ngDetails: finalIsOK ? "无异常" : finalHighestPriorityDefect,
                            station: station,
                            resultImagePath: savedResultImagePath ?? ""
                        );
                        await SaveToDatabase(detectionRecord);
                        AddLog($"[{station}-数据库] 电芯{barcode}记录已保存。");

                    }
                    catch (Exception ex)
                    {
                        AddLog($"[{station}-异步处理异常] 保存或数据库操作失败：{ex.Message}");
                        finalIsOK = false;
                    }
                });
                // 记录结束时间
                endTime = DateTime.Now;

                // 计算各个阶段耗时
                TimeSpan totalTime = endTime - startTime;
                TimeSpan queueWaitTime = processStartTime - startTime;
                TimeSpan processingTime = endTime - processStartTime;

                // 记录到CSV文件
                LogCellDetectionTimeToCSV(
                    station: station,
                    barcode: barcode,
                    startTime: startTime,
                    endTime: endTime,
                    result: finalIsOK ? "OK" : "NG",
                    defectType: finalHighestPriorityDefect,
                    queueWaitMs: (int)queueWaitTime.TotalMilliseconds,
                    processingMs: (int)processingTime.TotalMilliseconds
                );

                //更新UI列表
                await Application.Current.Dispatcher.InvokeAsync(new Action(() =>
                {
                    dynamic detectionData = new ExpandoObject();
                    detectionData.CellBarcode = barcode;
                    detectionData.DetectionResult = finalIsOK ? "OK" : "NG";
                    detectionData.NgDetails = finalHighestPriorityDefect;
                    detectionData.Station = station;
                    detectionData.DetectionTime = DateTime.Now;
                    detectionData.ResultImagePath = savedResultImagePath ?? "无结果图路径";
                    _fullDetectionDataList.Add(detectionData);
                    UpdateDisplayDataList(); // 刷新UI上的结果列表
                }), System.Windows.Threading.DispatcherPriority.Normal);

                AddLog($"[{station}-处理完成] 电芯{barcode}最终结果：{(!finalIsOK ? "NG" : "OK")}");

                return finalIsOK;
            }
            catch (Exception ex)
            {
                AddLog($"[{station}-保存流程异常] 中断原因：{ex.Message}，堆栈：{ex.StackTrace}");
                return false;
            }
            //finally
            //{
            //    foreach (var result in firstDetectionResults)
            //    {
            //        result.resultImage?.Dispose();
            //    }
            //}
        }

        private async Task<string?> GetUnifiedSaveDirectory(string sampleImagePath, string barcode, string defectType)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string[] stationFolders = new[] { "LAB", "LCD", "RAB", "RCD" };
                    string[] originalPathPartsFull = sampleImagePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                    if (originalPathPartsFull.Length < 4)
                    {
                        AddLog($"[目录创建失败] 路径层级不足：{sampleImagePath}");
                        return null;
                    }

                    string? stationFolder = originalPathPartsFull.FirstOrDefault(part => stationFolders.Contains(part));
                    if (string.IsNullOrEmpty(stationFolder))
                    {
                        AddLog($"[目录创建失败] 未找到工位文件夹：{sampleImagePath}");
                        return null;
                    }

                    int stationIndex = Array.IndexOf(originalPathPartsFull, stationFolder);
                    int dateIndex = stationIndex + 1;
                    var pathToDate = originalPathPartsFull.Skip(stationIndex).Take(dateIndex - stationIndex + 1).ToArray();

                    string? targetDrive = SelectedResultImagePath;
                    string imeresultRoot = Path.Combine($"{targetDrive}", "IMEResult");

                    string targetDir = Path.Combine(
                        imeresultRoot,
                        string.Join(Path.DirectorySeparatorChar.ToString(), pathToDate),
                        defectType,
                        barcode
                    );

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        AddLog($"[创建目录] {targetDir}");
                    }

                    return targetDir;
                }
                catch (Exception ex)
                {
                    AddLog($"[目录创建异常] {ex.Message}");
                    return null;
                }
            });
        }

        private async Task<string?> SaveGapDetectionResultBatch(
            string originalImagePath,
            string detectionResult,
            Action<string> saveImageAction,
            string defectType,
            string targetDir)
        {

            return await Task.Run(() =>
            {
                try
                {
                    string fileName = Path.GetFileName(originalImagePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                    string resultFileNameBase = fileNameWithoutExt;
                    if (resultFileNameBase.EndsWith("_origin"))
                        resultFileNameBase = resultFileNameBase.Substring(0, resultFileNameBase.Length - "_origin".Length);

                    string newFileName = $"{resultFileNameBase}_result_{detectionResult}.png";
                    string targetFilePath = Path.Combine(targetDir, newFileName);

                    // 保存结果图片
                    saveImageAction(targetFilePath);

                    // 复制原图
                    if (File.Exists(originalImagePath))
                    {
                        string originalTargetPath = Path.Combine(targetDir, Path.GetFileName(originalImagePath));
                        if (!File.Exists(originalTargetPath))
                        {
                            File.Copy(originalImagePath, originalTargetPath);
                        }
                    }

                    AddLog($"[批量保存] {Path.GetFileName(originalImagePath)} -> {defectType}/{Path.GetFileName(targetFilePath)}");
                    return targetFilePath;
                }
                catch (Exception ex)
                {
                    AddLog($"[批量保存失败] {Path.GetFileName(originalImagePath)}: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// 从队列项中提取不良类型
        /// </summary>
        private static string GetBadTypeFromItem(string item)
        {
            if (item.Contains("OH不良")) return "OH不良";
            if (item.Contains("负极少片")) return "负极少片";
            if (item.Contains("负极多片")) return "负极多片";
            if (item.Contains("正极少片")) return "正极少片";
            if (item.Contains("正极多片")) return "正极多片";
            if (item.Contains("负极差不良")) return "负极差不良";
            if (item.Contains("正极Y方向掉片")) return "正极Y方向掉片";
            if (item.Contains("分割异常")) return "分割异常";
            if (item.Contains("正极边缘间隙不良")) return "正极边缘间隙不良";
            if (item.Contains("负极边缘间隙不良")) return "负极边缘间隙不良";
            if (item.Contains("正极中间间隙不良")) return "正极中间间隙不良";
            if (item.Contains("负极压伤")) return "负极压伤";
            return "未知不良";

        }


        /// <summary>
        /// 异步保存检测结果图片和原图
        /// </summary>
        /// <param name="originalImagePath">原图路径</param>
        /// <param name="detectionResult">检测结果 (OK/NG)</param>
        /// <param name="saveImageAction">保存图像的委托</param>
        /// <param name="defectType">缺陷类型</param>
        /// <returns>保存的结果图路径，如果保存失败则返回 null。</returns>
        private async Task<string?> SaveGapDetectionResultAsync(string originalImagePath, string detectionResult, Action<string> saveImageAction, string defectType)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string keyName;
                    string fileName = Path.GetFileName(originalImagePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

                    if (fileNameWithoutExt.Contains("auto"))
                    {
                        keyName = fileNameWithoutExt;
                    }
                    else
                    {
                        string[] originalPathParts = originalImagePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                        if (originalPathParts.Length < 2)
                        {
                            AddLog($"[保存失败] 路径层级不足，无法获取上一级目录: {originalImagePath}");
                            return null;
                        }
                        string parentFolderName = originalPathParts[originalPathParts.Length - 2];

                        if (parentFolderName.Contains('_'))
                        {
                            keyName = parentFolderName.Split('_')[0];
                            AddLog($"[路径优化] 从文件夹名 '{parentFolderName}' 中提取电芯码 '{keyName}'");
                        }
                        else
                        {
                            keyName = parentFolderName;
                        }
                    }

                    string resultFileNameBase = fileNameWithoutExt;
                    if (resultFileNameBase.EndsWith("_origin"))
                        resultFileNameBase = resultFileNameBase.Substring(0, resultFileNameBase.Length - "_origin".Length);
                    string newFileName = $"{resultFileNameBase}_result_{detectionResult}.png";

                    string[] stationFolders = new[] { "LAB", "LCD", "RAB", "RCD" };
                    string[] originalPathPartsFull = originalImagePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    if (originalPathPartsFull.Length < 4)
                    {
                        AddLog($"[保存失败] 路径层级不足：{originalImagePath}");
                        return null;
                    }

                    string? station = originalPathPartsFull.FirstOrDefault(part => stationFolders.Contains(part));
                    if (string.IsNullOrEmpty(station))
                    {
                        AddLog($"[保存失败] 未找到工位文件夹：{originalImagePath}");
                        return null;
                    }
                    int stationIndex = Array.IndexOf(originalPathPartsFull, station);
                    int dateIndex = stationIndex + 1;
                    var pathToDate = originalPathPartsFull.Skip(stationIndex).Take(dateIndex - stationIndex + 1).ToArray();

                    string? targetDrive = SelectedResultImagePath;
                    string imeresultRoot = Path.Combine($"{targetDrive}", "IMEResult");

                    string targetDir = Path.Combine(
                        imeresultRoot,
                        string.Join(Path.DirectorySeparatorChar.ToString(), pathToDate),
                        defectType,
                        keyName
                    );

                    // 所有文件操作都在这个委托内部同步执行，整个委托在后台线程运行
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    string targetFilePath = Path.Combine(targetDir, newFileName);
                    saveImageAction(targetFilePath); //图片写入

                    if (File.Exists(originalImagePath))
                    {
                        string originalTargetPath = Path.Combine(targetDir, Path.GetFileName(originalImagePath));
                        if (!File.Exists(originalTargetPath))
                        {
                            File.Copy(originalImagePath, originalTargetPath);
                        }
                        else
                        {
                            AddLog($"[文件已存在] 跳过复制原图: {Path.GetFileName(originalImagePath)}");
                        }
                    }

                    AddLog($"[结果图保存成功] {targetFilePath}");
                    return targetFilePath;
                }
                catch (Exception ex)
                {
                    AddLog($"[保存失败] {ex.Message}，堆栈：{ex.StackTrace}");
                    return null;
                }
            });
        }


        //图像预处理
        private static HObject PreprocessImage(HObject img)
        {
            HOperatorSet.CountChannels(img, out HTuple channels);
            if (channels.TupleEqual(3).TupleOr(channels.TupleEqual(4)))
            {
                HOperatorSet.Rgb1ToGray(img, out HObject grayImg);
                img.Dispose();
                img = grayImg;
            }
            channels.Dispose();
            return img;
        }
        //异常位置计算
        private List<int> GetAbnormalPositions(double[] array, string imageFilePath)
        {
            List<int> positions = new List<int>();
            string imgNamePart = Path.GetFileName(imageFilePath).Split('_')[1];
            double upper;
            double lower;

            // 处理X类型
            if (imgNamePart.Contains("1-X"))
            {
                upper = Gap2Limit;    // 1-X 上限：Gap2Limit
                lower = Gap1Limit;    // 1-X 下限：Gap1Limit
            }
            else if (imgNamePart.Contains("2-X"))
            {
                upper = Gap14Limit;   // 2-X 上限：Gap14Limit
                lower = Gap13Limit;   // 2-X 下限：Gap13Limit
            }
            else if (imgNamePart.Contains("3-X"))
            {
                upper = Gap24Limit;   // 3-X 上限：Gap24Limit
                lower = Gap23Limit;   // 3-X 下限：Gap23Limit
            }
            else if (imgNamePart.Contains("4-X"))
            {
                upper = Gap34Limit;   // 4-X 上限：Gap34Limit
                lower = Gap33Limit;   // 4-X 下限：Gap33Limit
            }
            //处理Y类型
            else if (imgNamePart.Contains("1-Y"))
            {
                upper = Gap8Limit;    // 1-Y 上限：Gap8Limit
                lower = Gap7Limit;    // 1-Y 下限：Gap7Limit
            }
            else if (imgNamePart.Contains("2-Y"))
            {
                upper = Gap19Limit;   // 2-Y 上限：Gap19Limit
                lower = Gap18Limit;   // 2-Y 下限：Gap18Limit
            }
            else if (imgNamePart.Contains("3-Y"))
            {
                upper = Gap29Limit;   // 3-Y 上限：Gap29Limit
                lower = Gap28Limit;   // 3-Y 下限：Gap28Limit
            }
            else if (imgNamePart.Contains("4-Y"))
            {
                upper = Gap40Limit;   // 4-Y 上限：Gap40Limit
                lower = Gap39Limit;   // 4-Y 下限：Gap39Limit
            }
            //未知类型
            else
            {
                return positions;
            }

            // 检查数组中超出阈值范围（>upper 或 <lower）的位置并记录
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > upper || array[i] < lower)
                    positions.Add(i);
            }
            return positions;
        }

        #region 结果图生成
        // 生成结果图片
        private HObject? GenerateResultImage(
            HObject originImg, int imgWidth, int imgHeight,
            HObject recPE, HObject recNE, HObject recMiddlePE,
            HTuple peRows, HTuple peCols, HTuple neRows, HTuple neCols, HTuple middlePERows, HTuple middlePECols,
            HTuple distOHInt, HTuple distOHOut, HTuple distSortPEColMiddle,
            double hDNe, string? ctCode, string resultCode,
            List<int> ohAbnormalPositions1, List<int> ohAbnormalPositions2,
            double[] arr1, double[] arr2, double[] distGapMax,
            string imageFilePath)
        {
            // 验证输入
            if (imgWidth <= 0 || imgHeight <= 0)
            {
                AddLog($"[生成结果图失败] 图像尺寸无效：宽={imgWidth}，高={imgHeight}，文件：{Path.GetFileName(imageFilePath)}");
                return null;
            }
            if (originImg == null || !originImg.IsInitialized())
            {
                AddLog($"[生成结果图失败] 原图未初始化，文件：{Path.GetFileName(imageFilePath)}");
                return null;
            }

            HWindow? tempWindow = null;
            HObject? finalImage = null;

            try
            {
                // 创建隐藏窗口
                tempWindow = new HWindow();
                int windowWidth = Math.Max(imgWidth, 800);
                int windowHeight = imgHeight + 720; // 顶部60+底部540

                tempWindow.OpenWindow(0, 0, windowWidth, windowHeight, new HTuple(0), "invisible", "");

                // ★ 核心：通过 SetPart 偏移显示，让图像从第60行开始
                tempWindow.SetPart(-60, 0, imgHeight + 660, imgWidth - 1);

                // 显示原图
                tempWindow.DispObj(originImg);

                // 绘制顶部信息
                DrawTopInfo(imageFilePath, resultCode, tempWindow);

                //绘制区域标记（标记行坐标+60，与原图位置对齐）
                DrawRegionMarkers(peRows, peCols, ohAbnormalPositions1, tempWindow, "orange", "red");
                DrawRegionMarkers(neRows, neCols, ohAbnormalPositions2, tempWindow, "yellow", "red");
                DrawRegionMarkers(middlePERows, middlePECols, new List<int>(), tempWindow, "blue", "blue");

                //绘制文本信息
                DrawTextInfo(ctCode, imageFilePath, imgHeight,
                    peRows.Length, neRows.Length, hDNe, distOHInt, distOHOut,
                    arr1, arr2, ohAbnormalPositions1, ohAbnormalPositions2, tempWindow, distGapMax);
                // 导出最终图像

                // 备用方案：临时文件
                try
                {
                    string tempFile = Path.GetTempFileName() + ".png";
                    tempWindow.DumpWindow("png", tempFile);
                    HOperatorSet.ReadImage(out finalImage, tempFile);
                    File.Delete(tempFile);
                    AddLog($"[使用临时文件方式导出成功] 文件：{Path.GetFileName(imageFilePath)}");
                }
                catch (Exception tempFileEx)
                {
                    AddLog($"[临时文件方式也失败] {tempFileEx.Message}, 文件：{Path.GetFileName(imageFilePath)}");
                }

                return finalImage;
            }
            catch (HOperatorException ex)
            {
                AddLog($"[HALCON绘图错误] 代码：{ex.GetErrorCode()}，消息：{ex.Message}，文件：{Path.GetFileName(imageFilePath)}");
                if (originImg.IsInitialized())
                {
                    HOperatorSet.CopyImage(originImg, out HObject backupImg);
                    return backupImg;
                }
                return null;
            }
            catch (Exception ex)
            {
                AddLog($"[绘图异常] 消息：{ex.Message}，文件：{Path.GetFileName(imageFilePath)}");
                return null;
            }
            finally
            {
                if (tempWindow != null)
                {
                    try { tempWindow.CloseWindow(); } catch { }
                    tempWindow.Dispose();
                }
            }
        }


        // 绘制顶部信息
        private void DrawTopInfo(string imageFilePath, string resultCode, HWindow window)
        {
            if (window == null)
            {
                AddLog("[顶部信息绘制错误] 窗口对象为空");
                return;
            }
            try
            {
                string fileName = Path.GetFileName(imageFilePath);
                string barcode;
                string imgNum = "未知编号";

                string[] fileNameParts = fileName.Split('_');

                // 如果文件名包含 "auto"，则直接使用整个文件名为条码
                if (fileName.Contains("auto") && fileNameParts.Length > 2)
                {
                    barcode = $"{fileNameParts[0]}_{fileNameParts[1]}";
                    imgNum = fileNameParts[2];

                }
                else
                {
                    barcode = fileNameParts[0];
                    imgNum = fileNameParts[1];
                }

                // 字体：Times New Roman，大小35
                window.SetFont("Times New Roman-35");
                window.SetColor("white");
                if (fileName.Contains("auto"))
                {
                    // 位置：行0、列0
                    window.DispText(
                        $"电芯条码:{barcode}\t\t\t\t\t\t\t\t\t\t\t\t\t图片编号:{imgNum}\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t检测结果:",
                        "window", 0, 0, "white", "box", "false");
                }
                else
                {
                    // 位置：行0、列0
                    window.DispText(
                        $"电芯条码:{barcode}\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t图片编号:{imgNum}\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t检测结果:",
                        "window", 0, 0, "white", "box", "false");
                }

                window.SetColor(resultCode == "OK" ? "green" : "red");
                // 检测结果位置：行5、列1380
                window.DispText(resultCode,
                    "window", 5, 1380, resultCode == "OK" ? "green" : "red", "box", "false");
            }
            catch (Exception ex)
            {
                AddLog($"[顶部信息绘制错误] {ex.Message}");
            }
        }

        //绘制区域标记
        private void DrawRegionMarkers(HTuple rows, HTuple cols, List<int> abnormalPositions,
                          HWindow window, string normalColor, string abnormalColor)
        {
            if (window == null || rows == null || cols == null || rows.Length == 0 || cols.Length == 0 || rows.Length != cols.Length)
            {
                AddLog("[标记绘制错误] 参数无效或窗口对象为空");
                return;
            }

            HObject? normalMark = null;
            HObject? abnormalMark = null;
            List<HObject> tempObjects = new List<HObject>(); // 用于临时存储生成的对象

            try
            {
                double[] rowArr = rows.DArr;
                double[] colArr = cols.DArr;

                for (int i = 0; i < rowArr.Length; i++)
                {
                    int drawRow = (int)rowArr[i];
                    int drawCol = (int)colArr[i];
                    HObject temp = HImageViewerHelper.GenFillCTStar(drawRow, drawCol, 0);
                    if (temp == null || !temp.IsInitialized())
                    {
                        temp?.Dispose();
                        continue;
                    }

                    tempObjects.Add(temp);

                    if (abnormalPositions.Contains(i))
                        abnormalMark = abnormalMark == null ? temp : abnormalMark.ConcatObj(temp);
                    else
                        normalMark = normalMark == null ? temp : normalMark.ConcatObj(temp);
                }

                // 绘制正常标记
                if (normalMark != null && normalMark.IsInitialized())
                {
                    window.SetColor(normalColor);
                    window.DispObj(normalMark);
                }

                // 绘制异常标记
                if (abnormalMark != null && abnormalMark.IsInitialized())
                {
                    window.SetColor(abnormalColor);
                    window.DispObj(abnormalMark);
                }
            }
            catch (Exception ex)
            {
                AddLog($"[标记绘制错误] {ex.Message}");
            }
            finally
            {
                // 释放所有HObject资源
                try
                {
                    normalMark?.Dispose();
                    abnormalMark?.Dispose();

                    // 释放所有临时对象
                    foreach (var tempObj in tempObjects)
                    {
                        if (tempObj != null && tempObj.IsInitialized())
                        {
                            tempObj.Dispose();
                        }
                    }
                }
                catch (Exception disposeEx)
                {
                    AddLog($"[标记资源释放错误] {disposeEx.Message}");
                }
            }
        }
        //绘制文本信息
        private void DrawTextInfo(string? ctCode, string imageFilePath, int imgHeight,
                     int peCount, int neCount, double hDNe,
                     HTuple distOHInt, HTuple distOHOut,
                     double[] arr1, double[] arr2,
                     List<int> ohAbnormalPositions1, List<int> ohAbnormalPositions2,
                     HWindow window, double[] distGapMax)
        {
            if (window == null)
            {
                AddLog("[文本绘制错误] 窗口对象为空");
                return;
            }
            try
            {
                string fileName = Path.GetFileName(imageFilePath);
                string barcode = fileName.Split('_')[0];
                if (barcode.Length > 16) barcode = barcode.Substring(6, 14);
                string imgNum = fileName.Split('_').Length > 1 ?
        (fileName.Split('_')[1].Length < 10 ? fileName.Split('_')[1] : fileName.Split('_')[2]) : "未知编号";

                // 初始化OH标准范围
                string ohBz = "[未知范围]";

                //处理Y类型：按1-Y/2-Y/3-Y/4-Y匹配动态阈值
                if (imgNum.Contains("1-Y"))
                {
                    ohBz = $"[{Gap7Limit:F2},{Gap8Limit:F2}]"; // 1-Y：下限Gap7，上限Gap8
                }
                else if (imgNum.Contains("2-Y"))
                {
                    ohBz = $"[{Gap18Limit:F2},{Gap19Limit:F2}]"; // 2-Y：下限Gap18，上限Gap19
                }
                else if (imgNum.Contains("3-Y"))
                {
                    ohBz = $"[{Gap28Limit:F2},{Gap29Limit:F2}]"; // 3-Y：下限Gap28，上限Gap29
                }
                else if (imgNum.Contains("4-Y"))
                {
                    ohBz = $"[{Gap39Limit:F2},{Gap40Limit:F2}]"; // 4-Y：下限Gap39，上限Gap40
                }
                //处理X类型：按1-X/2-X/3-X/4-X匹配动态阈值
                else if (imgNum.Contains("1-X"))
                {
                    ohBz = $"[{Gap1Limit:F2},{Gap2Limit:F2}]"; // 1-X：下限Gap1，上限Gap2
                }
                else if (imgNum.Contains("2-X"))
                {
                    ohBz = $"[{Gap13Limit:F2},{Gap14Limit:F2}]"; // 2-X：下限Gap13，上限Gap14
                }
                else if (imgNum.Contains("3-X"))
                {
                    ohBz = $"[{Gap23Limit:F2},{Gap24Limit:F2}]"; // 3-X：下限Gap23，上限Gap24
                }
                else if (imgNum.Contains("4-X"))
                {
                    ohBz = $"[{Gap33Limit:F2},{Gap34Limit:F2}]"; // 4-X：下限Gap33，上限Gap34
                }


                double[] ohMaxMin = (distOHInt?.SArr != null && distOHOut?.SArr != null) ?
                    GapMax(distOHInt.SArr, distOHOut.SArr) : new double[] { 0, 0 };

                //处理正极间隙最大值及阈值
                double fjEndMax = 0;       // 正极片边缘间隙最大值（gpa数组第一个元素）
                double posEndMax = 0;       // 正极片边缘间隙最大值（gpa数组第二个元素）
                double posMiddleMax = 0;    // 正极片中间间隙最大值（gpa数组第三个元素）
                double fjEndThreshold = 0;
                double posEndThreshold = 0; // 正极片边缘间隙阈值
                double posMiddleThreshold = 0; // 正极片中间间隙阈值

                // 验证distGapMax数组有效性
                if (distGapMax != null && distGapMax.Length == 3)
                {
                    fjEndMax = distGapMax[0];
                    posEndMax = distGapMax[1];       // 取第二个元素：正极边缘最大值
                    posMiddleMax = distGapMax[2];    // 取第三个元素：正极中间最大值
                }
                else
                {
                    AddLog("[文本绘制] distGapMax数组无效（空或长度不为3），使用默认值");
                }

                // 根据图片类型匹配正极间隙阈值
                if (imgNum.Contains("1-X"))
                {
                    fjEndThreshold = Gap4Limit;
                    posEndThreshold = Gap5Limit;     // 1-X 正极边缘阈值
                    posMiddleThreshold = Gap6Limit;  // 1-X 正极中间阈值
                }
                else if (imgNum.Contains("1-Y"))
                {
                    fjEndThreshold = Gap10Limit;
                    posEndThreshold = Gap11Limit;    // 1-Y 正极边缘阈值
                    posMiddleThreshold = Gap12Limit; // 1-Y 正极中间阈值
                }
                else if (imgNum.Contains("2-X"))
                {
                    fjEndThreshold = Gap15Limit;
                    posEndThreshold = Gap16Limit;    // 2-X 正极边缘阈值
                    posMiddleThreshold = Gap17Limit; // 2-X 正极中间阈值
                }
                else if (imgNum.Contains("2-Y"))
                {
                    fjEndThreshold = Gap20Limit;
                    posEndThreshold = Gap21Limit;    // 2-Y 正极边缘阈值
                    posMiddleThreshold = Gap22Limit; // 2-Y 正极中间阈值
                }
                else if (imgNum.Contains("3-X"))
                {
                    fjEndThreshold = Gap25Limit;
                    posEndThreshold = Gap26Limit;    // 3-X 正极边缘阈值
                    posMiddleThreshold = Gap27Limit; // 3-X 正极中间阈值
                }
                else if (imgNum.Contains("3-Y"))
                {
                    fjEndThreshold = Gap30Limit;
                    posEndThreshold = Gap31Limit;    // 3-Y 正极边缘阈值
                    posMiddleThreshold = Gap32Limit; // 3-Y 正极中间阈值
                }
                else if (imgNum.Contains("4-X"))
                {
                    fjEndThreshold = Gap36Limit;
                    posEndThreshold = Gap37Limit;    // 4-X 正极边缘阈值
                    posMiddleThreshold = Gap38Limit; // 4-X 正极中间阈值
                }
                else if (imgNum.Contains("4-Y"))
                {
                    fjEndThreshold = Gap42Limit;
                    posEndThreshold = Gap43Limit;    // 4-Y 正极边缘阈值
                    posMiddleThreshold = Gap44Limit; // 4-Y 正极中间阈值
                }
                else
                {
                    AddLog("[文本绘制] 未知图片类型，无法匹配正极间隙阈值");
                }

                // 标志位：仅1-X/1-Y/4-X/4-Y显示，2-X/2-Y/3-X/3-Y不显示
                bool showNegativeDiffThreshold = false;
                double negativeDiffThreshold = 0;
                string thresholdDesc = "";

                // 匹配需要显示阈值的类型（1-X/1-Y/4-X/4-Y）
                if (imgNum.Contains("1-X"))
                {
                    showNegativeDiffThreshold = true;
                    negativeDiffThreshold = Gap3Limit;
                    thresholdDesc = $"{Gap3Limit:F3}";
                }
                else if (imgNum.Contains("1-Y"))
                {
                    showNegativeDiffThreshold = true;
                    negativeDiffThreshold = Gap9Limit;
                    thresholdDesc = $"{Gap9Limit:F3}";
                }
                else if (imgNum.Contains("4-X"))
                {
                    showNegativeDiffThreshold = true;
                    negativeDiffThreshold = Gap35Limit;
                    thresholdDesc = $"{Gap35Limit:F3}";
                }
                else if (imgNum.Contains("4-Y"))
                {
                    showNegativeDiffThreshold = true;
                    negativeDiffThreshold = Gap41Limit;
                    thresholdDesc = $"{Gap41Limit:F3}";
                }


                // 字体：Times New Roman，大小23
                window.SetFont("Times New Roman-23");

                // 基础配置
                int titleX = 0; // 标题起始X坐标（统一左对齐）
                int lineHeight = 28; // 行高
                int startHeight = imgHeight + 60;
                //正极层数
                string peTitle = "正极层数: ";
                // 绘制标题
                window.SetColor("orange");
                window.DispText(peTitle,
                    "window", startHeight, titleX, "orange", "box", "false");
                // 绘制数据
                string peData = $"{peCount} (标准76)";
                window.SetColor(peCount != 76 ? "red" : "green");
                window.DispText(peData,
                    "window", startHeight, 100, peCount != 76 ? "red" : "green", "box", "false");


                // 负极层数
                string neTitle = "负极层数: ";
                window.SetColor("orange");
                window.DispText(neTitle,
                    "window", startHeight + lineHeight, titleX, "orange", "box", "false");
                string neData = $"{neCount} (标准77)";
                window.SetColor(neCount != 77 ? "red" : "green");
                window.DispText(neData,
                    "window", startHeight + lineHeight, 100, neCount != 77 ? "red" : "green", "box", "false");


                //OH最大值
                bool isOverSize = ctCode?.Contains("偏大") ?? false;
                string ohMaxTitle = "OH最大值: ";
                window.SetColor("orange");
                window.DispText(ohMaxTitle,
                    "window", startHeight + lineHeight * 2, titleX, "orange", "box", "false");
                string ohMaxData = $"{ohMaxMin[1]:F3} {(isOverSize ? "(异常)" : "")}";
                window.SetColor(isOverSize ? "red" : "green");
                window.DispText(ohMaxData,
                    "window", startHeight + lineHeight * 2, 115, isOverSize ? "red" : "green", "box", "false");


                //OH最小值
                bool isUnderSize = ctCode?.Contains("偏小") ?? false;
                string ohMinTitle = "OH最小值: ";
                window.SetColor("orange");
                window.DispText(ohMinTitle,
                    "window", startHeight + lineHeight * 3, titleX, "orange", "box", "false");
                string ohMinData = $"{ohMaxMin[0]:F3} {(isUnderSize ? "(异常)" : "")}";
                window.SetColor(isUnderSize ? "red" : "green");
                window.DispText(ohMinData,
                    "window", startHeight + lineHeight * 3, 115, isUnderSize ? "red" : "green", "box", "false");

                // 合并文本区域：逐行处理
                int combinedStartRow = startHeight + lineHeight * 4;
                int currentRow = combinedStartRow;


                //OH标准
                string ohBzTitle = "OH标准: ";
                window.SetColor("orange");
                window.DispText(ohBzTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                window.SetColor("orange");
                window.DispText(ohBz,
                    "window", currentRow, 90, "orange", "box", "false");
                currentRow += lineHeight;


                //负极差值
                if (showNegativeDiffThreshold)
                {
                    string negativeDiffTitle = "负极差值: ";
                    window.SetColor("orange");
                    window.DispText(negativeDiffTitle,
                        "window", currentRow, titleX, "orange", "box", "false");
                    bool isNegativeDiffOver = hDNe > negativeDiffThreshold;
                    string negativeDiffData = $"{hDNe:F3} {(isNegativeDiffOver ? "(异常)" : "")}";
                    window.SetColor(isNegativeDiffOver ? "red" : "green");
                    window.DispText(negativeDiffData,
                        "window", currentRow, 100, isNegativeDiffOver ? "red" : "green", "box", "false");
                    currentRow += lineHeight;

                    string negativeThresholdTitle = "负极差阈值: ";
                    window.SetColor("orange");
                    window.DispText(negativeThresholdTitle,
                        "window", currentRow, titleX, "orange", "box", "false");
                    window.SetColor("green");
                    window.DispText(thresholdDesc,
                        "window", currentRow, 130, "green", "box", "false");
                    currentRow += lineHeight;
                }


                //负极边缘间隙最大值
                string fjEndMaxTitle = "负极边缘间隙最大值: ";
                window.SetColor("orange");
                window.DispText(fjEndMaxTitle,
                    "window", currentRow, titleX, "orange", "box", "false");


                bool isFjEndOver = fjEndMax > fjEndThreshold;
                string fjEndMaxData = $"{fjEndMax:F3} {(isFjEndOver ? "(异常)" : "")}";
                window.SetColor(isFjEndOver ? "red" : "green");
                window.DispText(fjEndMaxData,
                    "window", currentRow, 215, isFjEndOver ? "red" : "green", "box", "false");
                currentRow += lineHeight;


                //负极边缘间隙阈值
                string fjEndThresholdTitle = "负极边缘间隙阈值: ";
                window.SetColor("orange");
                window.DispText(fjEndThresholdTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                window.SetColor("green");
                window.DispText($"{fjEndThreshold:F3}",
                    "window", currentRow, 195, "green", "box", "false");
                currentRow += lineHeight;


                //正极边缘间隙最大值
                string posEndMaxTitle = "正极边缘间隙最大值: ";
                window.SetColor("orange");
                window.DispText(posEndMaxTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                bool isPosEndOver = posEndMax > posEndThreshold;
                string posEndMaxData = $"{posEndMax:F3} {(isPosEndOver ? "(异常)" : "")}";
                window.SetColor(isPosEndOver ? "red" : "green");
                window.DispText(posEndMaxData,
                    "window", currentRow, 215, isPosEndOver ? "red" : "green", "box", "false");
                currentRow += lineHeight;


                //正极边缘间隙阈值
                string posEndThresholdTitle = "正极边缘间隙阈值: ";
                window.SetColor("orange");
                window.DispText(posEndThresholdTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                window.SetColor("green");
                window.DispText($"{posEndThreshold:F3}",
                    "window", currentRow, 195, "green", "box", "false");
                currentRow += lineHeight;


                //正极中间间隙最大值
                string posMiddleMaxTitle = "正极中间间隙最大值: ";
                window.SetColor("orange");
                window.DispText(posMiddleMaxTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                bool isPosMiddleOver = posMiddleMax > posMiddleThreshold;
                string posMiddleMaxData = $"{posMiddleMax:F3} {(isPosMiddleOver ? "(异常)" : "")}";
                window.SetColor(isPosMiddleOver ? "red" : "green");
                window.DispText(posMiddleMaxData,
                    "window", currentRow, 215, isPosMiddleOver ? "red" : "green", "box", "false");
                currentRow += lineHeight;


                //正极中间间隙阈值
                string posMiddleThresholdTitle = "正极中间间隙阈值: ";
                window.SetColor("orange");
                window.DispText(posMiddleThresholdTitle,
                    "window", currentRow, titleX, "orange", "box", "false");
                window.SetColor("green");
                window.DispText($"{posMiddleThreshold:F3}",
                    "window", currentRow, 195, "green", "box", "false");


                // 绘制OH参数表格
                if (arr1 != null && arr2 != null)
                {
                    DrawOhValueTable(arr1, arr2, ohAbnormalPositions1, ohAbnormalPositions2, imgHeight, window);
                }
            }
            catch (Exception ex)
            {
                AddLog($"[文本绘制错误] {ex.Message}");
            }
        }

        //绘制OH参数表格
        private void DrawOhValueTable(double[] arr1, double[] arr2, List<int> abnormal1, List<int> abnormal2,
                             int imgHeight, HWindow window)
        {
            if (window == null)
            {
                AddLog("[表格绘制错误] 窗口对象为空");
                return;
            }
            try
            {
                int columns = 5;
                int rowsPerCol = 22;
                window.SetFont("Times New Roman-23");
                int tableStartRow = imgHeight + 60;
                int lineSpacing = 28; // 统一行间距

                for (int col = 0; col < columns; col++)
                {
                    StringBuilder labelText = new StringBuilder();
                    StringBuilder valueText1 = new StringBuilder();
                    StringBuilder valueText2 = new StringBuilder();

                    // 构建文本时，记录每行内容
                    List<string> labelLines = new List<string>();
                    List<string> value1Lines = new List<string>();
                    List<string> value2Lines = new List<string>();

                    for (int row = 0; row < rowsPerCol; row++)
                    {
                        int index = row + col * rowsPerCol;
                        if (index >= arr1.Length || index >= arr2.Length) break;

                        string label = $"L{(index + 1).ToString("D3")}:";
                        string val1 = $"{arr1[index]:F3}";
                        string val2 = $"{arr2[index]:F3}";

                        labelLines.Add(label);
                        value1Lines.Add(val1);
                        value2Lines.Add(val2);
                    }

                    // 逐行绘制：标签 + 数值1 + 数值2
                    int colOffset = 350 + col * 200;
                    for (int i = 0; i < labelLines.Count; i++)
                    {
                        int currentRow = tableStartRow + i * lineSpacing; // 统一行偏移

                        // 绘制标签
                        window.SetColor("orange");
                        window.DispText(labelLines[i],
                            "window", currentRow, colOffset, "orange", "box", "false");

                        // 绘制数值1
                        string color1 = abnormal1.Contains(i + col * rowsPerCol) ? "red" : "green";
                        window.SetColor(color1);
                        window.DispText(value1Lines[i],
                            "window", currentRow, colOffset + 60, color1, "box", "false");

                        // 绘制数值2
                        string color2 = abnormal2.Contains(i + col * rowsPerCol) ? "red" : "green";
                        window.SetColor(color2);
                        window.DispText(value2Lines[i],
                            "window", currentRow, colOffset + 120, color2, "box", "false");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[表格绘制错误] {ex.Message}");
            }
        }


        #endregion

        // 单条检测记录的结构（键为字段名，值为对应数据）
        private static Dictionary<string, object> CreateDetectionRecord(
            DateTime detectionTime,
            string cellBarcode,
            string detectionResult,
            string ngDetails,
            string station,
            string resultImagePath)
        {
            return new Dictionary<string, object>
            {
                {"Id", 0}, // 数据库自增ID，初始为0
                {"DetectionTime", detectionTime}, // 检测时间
                {"CellBarcode", cellBarcode}, // 电芯条码
                {"DetectionResult", detectionResult}, // OK/NG
                {"NgDetails", ngDetails}, // 异常类型
                {"Station", station}, // 工位
                {"ResultImagePath", resultImagePath} // 结果图路径
            };
        }

        private SqliteConnection? _dbConnection;

        private void InitDatabase()
        {
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DetectionData.db");
            _dbConnection = new SqliteConnection($"Data Source={dbPath}");
            _dbConnection.Open();

            // 创建表
            using (var cmd = _dbConnection.CreateCommand())
            {
                cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS DetectionRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DetectionTime DATETIME NOT NULL,
                CellBarcode TEXT NOT NULL,
                DetectionResult TEXT NOT NULL,
                NgDetails TEXT,
                Station TEXT NOT NULL,
                ResultImagePath TEXT
            );
            -- 索引仍基于查询频繁的字段
            CREATE INDEX IF NOT EXISTS IX_DetectionTime ON DetectionRecords(DetectionTime);
            CREATE INDEX IF NOT EXISTS IX_NgDetails ON DetectionRecords(NgDetails);
        ";
                cmd.ExecuteNonQuery();
            }
        }

        // 将 Dictionary 记录写入数据库
        private async Task SaveToDatabase(Dictionary<string, object> record)
        {
            // 检查并初始化数据库连接
            if (_dbConnection == null)
            {
                string connectionString = "Data Source=DetectionData.db;";
                _dbConnection = new SqliteConnection(connectionString);
            }
            try
            {
                using (var cmd = _dbConnection.CreateCommand())
                {
                    cmd.CommandText = @"
                INSERT INTO DetectionRecords (DetectionTime, CellBarcode, DetectionResult, NgDetails, Station, ResultImagePath)
                VALUES (@DetectionTime, @CellBarcode, @DetectionResult, @NgDetails, @Station, @ResultImagePath)
            ";
                    // 从 Dictionary 中取对应键的值，绑定到SQL参数
                    cmd.Parameters.AddWithValue("@DetectionTime", record["DetectionTime"]);
                    cmd.Parameters.AddWithValue("@CellBarcode", record["CellBarcode"]);
                    cmd.Parameters.AddWithValue("@DetectionResult", record["DetectionResult"]);
                    cmd.Parameters.AddWithValue("@NgDetails", record["NgDetails"] ?? DBNull.Value); // 处理空值
                    cmd.Parameters.AddWithValue("@Station", record["Station"]);
                    cmd.Parameters.AddWithValue("@ResultImagePath", record["ResultImagePath"] ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                AddLog($"[数据库存储失败] {ex.Message}");
            }
        }

        // <summary>
        /// 执行查询的完整逻辑：验证参数→查询总数量→加载第一页数据→处理异常
        /// </summary>
        private async Task ExecuteQueryAsync()
        {
            //重置状态（清空表格、更新加载状态）
            PagedAbnormalDataList.Clear();
            IsQuerying = true; // 禁用查询按钮
            AddLog("[查询开始] 用户触发数据查询");

            try
            {
                //验证输入参数
                if (!StartDateTime.HasValue)
                {
                    System.Windows.MessageBox.Show("请选择开始时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AddLog("[查询中断] 未选择开始时间");
                    return;
                }
                if (!EndDateTime.HasValue)
                {
                    System.Windows.MessageBox.Show("请选择结束时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AddLog("[查询中断] 未选择结束时间");
                    return;
                }
                if (StartDateTime.Value > EndDateTime.Value)
                {
                    System.Windows.MessageBox.Show("开始时间不能晚于结束时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    AddLog($"[查询中断] 时间范围无效（开始：{StartDateTime.Value:yyyy-MM-dd HH:mm}，结束：{EndDateTime.Value:yyyy-MM-dd HH:mm}）");
                    return;
                }
                if (string.IsNullOrEmpty(SelectedAbnormalType))
                {
                    SelectedAbnormalType = "全部"; // 默认选中"全部"
                    AddLog("[查询调整] 未选择异常类型，默认使用'全部'");
                }

                //记录查询条件
                AddLog($"[查询条件] 时间范围：{StartDateTime.Value:yyyy-MM-dd HH:mm} 至 {EndDateTime.Value:yyyy-MM-dd HH:mm}，异常类型：{SelectedAbnormalType}");

                //查询符合条件的总数量
                TotalCount = await GetTotalCountAsync();
                AddLog($"[查询结果] 符合条件的总数量：{TotalCount}条");

                //加载第一页数据
                CurrentPage = 1;
                await LoadPageDataAsync(CurrentPage);
                AddLog($"[分页加载] 已加载第1页数据（{PagedAbnormalDataList.Count}/{PageSize}条）");

                //无数据时提示
                if (TotalCount == 0)
                {
                    System.Windows.MessageBox.Show("未查询到符合条件的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (SqliteException ex)
            {
                // 数据库相关异常
                System.Windows.MessageBox.Show($"查询失败：数据库错误（{ex.Message}）", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"[查询异常-数据库] {ex.Message}，堆栈：{ex.StackTrace}");
            }
            catch (Exception ex)
            {
                // 其他未知异常
                System.Windows.MessageBox.Show($"查询失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog($"[查询异常-未知] {ex.Message}，堆栈：{ex.StackTrace}");
            }
            finally
            {
                //恢复状态（启用查询按钮）
                IsQuerying = false;
                AddLog("[查询结束] 流程完成");
            }
        }

        // 执行查询
        private async Task<int> GetTotalCountAsync()
        {
            // 检查并初始化数据库连接
            if (_dbConnection == null)
            {
                string connectionString = "Data Source=DetectionData.db;";
                _dbConnection = new SqliteConnection(connectionString);
            }
            using (var cmd = _dbConnection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM DetectionRecords
                    WHERE DetectionTime BETWEEN @Start AND @End
                    AND (@AbnormalType = '全部' OR NgDetails = @AbnormalType)
                ";
                cmd.Parameters.AddWithValue("@Start", StartDateTime ?? DateTime.MinValue);
                cmd.Parameters.AddWithValue("@End", EndDateTime ?? DateTime.MaxValue);
                cmd.Parameters.AddWithValue("@AbnormalType", SelectedAbnormalType);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
        }

        // 加载分页数据
        private async Task LoadPageDataAsync(int page)
        {
            // 检查并初始化数据库连接
            if (_dbConnection == null)
            {
                string connectionString = "Data Source=DetectionData.db;";
                _dbConnection = new SqliteConnection(connectionString);
            }

            PagedAbnormalDataList.Clear();
            int pageSize = 100;
            int offset = (page - 1) * pageSize;

            using (var cmd = _dbConnection.CreateCommand())
            {
                cmd.CommandText = @"
                        SELECT DetectionTime, CellBarcode, NgDetails 
                        FROM DetectionRecords
                        WHERE DetectionTime BETWEEN @Start AND @End
                        AND (@AbnormalType = '全部' OR NgDetails = @AbnormalType)
                        ORDER BY DetectionTime DESC
                        LIMIT @PageSize OFFSET @Offset
                    ";
                cmd.Parameters.AddWithValue("@Start", StartDateTime ?? DateTime.MinValue);
                cmd.Parameters.AddWithValue("@End", EndDateTime ?? DateTime.MaxValue);
                cmd.Parameters.AddWithValue("@AbnormalType", SelectedAbnormalType);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);
                cmd.Parameters.AddWithValue("@Offset", offset);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // 提取字段值并临时变量保存
                        var timeValue = reader.GetDateTime(0).ToString("yyyy-MM-dd HH:mm:ss");
                        var barcodeValue = reader.GetString(1);
                        var abnormalTypeValue = reader.IsDBNull(2) ? "OK" : reader.GetString(2);

                        // 输出日志，确认值是否有效
                        //AddLog($"[数据行内容] Time: '{timeValue}', CellBarcode: '{barcodeValue}', AbnormalType: '{abnormalTypeValue}'");

                        // 添加到集合
                        var record = new Dictionary<string, object>
                            {
                                {"Time", timeValue},
                                {"CellBarcode", barcodeValue},
                                {"AbnormalType", abnormalTypeValue}
                            };
                        PagedAbnormalDataList.Add(record);
                    }
                }
                //确认数据是否加载
                AddLog($"[DataGrid加载] 第{page}页，实际加载{this.PagedAbnormalDataList.Count}条数据");
            }
        }

        private async Task ExecuteExportAsync()
        {
            if (TotalCount == 0)
            {
                System.Windows.MessageBox.Show("无数据可导出");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV文件 (*.csv)|*.csv",
                FileName = $"{EndDateTime ?? DateTime.MaxValue:yyMMddHH}_{EndDateTime ?? DateTime.MaxValue:yyMMddHH}_{SelectedAbnormalType}.csv"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                AddLog("[导出取消] 用户取消操作");
                return;
            }

            try
            {
                //var progressWindow = new ProgressWindow();
                //progressWindow.Show();

                await Task.Run(async () =>
                {
                    using (var writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                    {
                        await writer.WriteLineAsync("时间,工位,电芯条码,异常类型,检测结果");

                        int batchSize = 1000;
                        int totalExported = 0;

                        while (totalExported < TotalCount)
                        {
                            var batchData = await GetExportBatchDataAsync(totalExported, batchSize);
                            foreach (var item in batchData)
                            {
                                // 从 Dictionary 中取字段值
                                string time = EscapeCsvField(Convert.ToDateTime(item["DetectionTime"]).ToString("yyyy-MM-dd HH:mm:ss"));
                                string station = EscapeCsvField(item["Station"]?.ToString() ?? "");
                                string barcode = EscapeCsvField(item["CellBarcode"]?.ToString() ?? "");
                                string abnormalType = EscapeCsvField(item["NgDetails"]?.ToString() ?? "OK");
                                string detectionResult = item["DetectionResult"]?.ToString() ?? "";

                                //写入行（仅包含时间、条码、异常类型、检测结果）
                                await writer.WriteLineAsync($"{time},{station},{barcode},{abnormalType},{detectionResult}");
                            }

                            totalExported += batchData.Count;
                            //progressWindow.Dispatcher.Invoke(() =>
                            //progressWindow.SetProgress((double)totalExported / TotalCount * 100));
                        }
                    }
                });

                //progressWindow.Close();
                System.Windows.MessageBox.Show($"导出成功：{saveFileDialog.FileName}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}");
            }
        }

        // 分批获取导出数据（返回 Dictionary 集合）
        private async Task<List<Dictionary<string, object>>> GetExportBatchDataAsync(int offset, int batchSize)
        {
            var result = new List<Dictionary<string, object>>();
            using (var cmd = _dbConnection?.CreateCommand())
            {
                if (cmd == null) return result;

                cmd.CommandText = @"
                    SELECT DetectionTime, Station, CellBarcode, DetectionResult, NgDetails 
                    FROM DetectionRecords
                    WHERE DetectionTime BETWEEN @Start AND @End
                    AND (@AbnormalType = '全部' OR NgDetails = @AbnormalType)
                    ORDER BY DetectionTime DESC
                    LIMIT @BatchSize OFFSET @Offset";

                cmd.Parameters.AddWithValue("@Start", StartDateTime ?? DateTime.MinValue);
                cmd.Parameters.AddWithValue("@End", EndDateTime ?? DateTime.MaxValue);
                cmd.Parameters.AddWithValue("@AbnormalType", SelectedAbnormalType);
                cmd.Parameters.AddWithValue("@BatchSize", batchSize);
                cmd.Parameters.AddWithValue("@Offset", offset);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var record = new Dictionary<string, object>
                {
                    {"DetectionTime", reader.GetDateTime(0)},
                    {"Station", reader.IsDBNull(1) ? "" : reader.GetString(1)},
                    {"CellBarcode", reader.GetString(2)},
                    {"DetectionResult", reader.GetString(3)},
                    {"NgDetails", reader.IsDBNull(4) ? "OK" : reader.GetString(4)}
                };
                        result.Add(record);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 处理CSV字段中的特殊字符
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "";

            // 包含逗号、引号或换行符时，需要用双引号包裹
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                // 双引号替换为两个双引号
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }


        public void AddLog(string logContent)
        {
            // 校验日志内容
            string validLogContent = string.IsNullOrWhiteSpace(logContent)
                ? "【空日志内容（可能是异常信息为空导致）】"
                : logContent.Trim();

            _logger.Info(validLogContent);

            // 创建日志项
            var newLog = Tuple.Create(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                validLogContent
            );

            // 仅当内容有效时添加到日志集合
            if (!string.IsNullOrWhiteSpace(validLogContent))
            {
                Application.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        LogTable.Insert(0, newLog);
                        if (LogTable.Count > 300)
                        {
                            LogTable.RemoveAt(LogTable.Count - 1);
                        }
                    })

                );
            }
        }

        //全局查看状态按工位存储
        private readonly Dictionary<string, bool> _isViewingResultDict = new Dictionary<string, bool>
        {
            {"LAB", false}, {"LCD", false}, {"RAB", false}, {"RCD", false}
        };

        // 用于取消延迟任务的控制器
        private readonly Dictionary<string, CancellationTokenSource?> _viewDelayCtsDict = new Dictionary<string, CancellationTokenSource?>
            {
                {"LAB", null}, {"LCD", null}, {"RAB", null}, {"RCD", null}
            };
        private const int ViewDelayMilliseconds = 3000;
        private readonly object _statusLocker = new object(); // 线程安全锁


        // 执行查看结果命令（显示识别结果图，按工位更新对应区域）
        private void ExecuteViewResult(dynamic? data)
        {
            if (data == null)
            {
                System.Windows.MessageBox.Show("未选中任何检测数据，请先在表格中选择一条记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 1. 从数据中提取工位标识（关键：确定要更新哪个区域）
            string? station = null;
            if (data is ExpandoObject expandoData)
            {
                var dataDict = (IDictionary<string, object?>)expandoData;
                // 假设数据中包含"Station"字段（值为"LAB"/"LCD"/"RAB"/"RCD"）
                if (dataDict.TryGetValue("Station", out object? stationObj) && stationObj is string stationStr)
                {
                    station = stationStr;
                }
            }
            // 校验工位有效性
            if (string.IsNullOrEmpty(station) || !_isViewingResultDict.ContainsKey(station))
            {
                AddLog($"[查看失败] 未找到有效工位标识（数据中缺少Station字段或值无效）");
                System.Windows.MessageBox.Show("选中的数据不包含有效工位信息，无法确定显示区域！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 2. 提取结果图路径（保持原有逻辑，增加工位上下文）
            string? resultImagePath = null;
            if (data is ExpandoObject expando)
            {
                var dataDict = (IDictionary<string, object?>)expando;
                if (dataDict.TryGetValue("ResultImagePath", out object? pathObj)
                    && pathObj is string pathStr
                    && !string.IsNullOrWhiteSpace(pathStr))
                {
                    resultImagePath = Path.GetFullPath(pathStr);
                }
            }
            else if (data is string strPath && !string.IsNullOrWhiteSpace(strPath))
            {
                resultImagePath = Path.GetFullPath(strPath);
            }

            // 路径有效性校验（增加工位上下文日志）
            if (string.IsNullOrEmpty(resultImagePath))
            {
                AddLog($"[查看失败] {station}工位结果图路径为空（未找到有效ResultImagePath）");
                System.Windows.MessageBox.Show($"{station}工位结果图路径未生成，请先完成检测", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!File.Exists(resultImagePath))
            {
                AddLog($"[查看失败] {station}工位结果图文件不存在，路径：{resultImagePath}");
                string tips = $"文件可能已被移动/删除，默认存储路径为：\n{Path.GetDirectoryName(resultImagePath)}";
                System.Windows.MessageBox.Show($"{station}工位结果图不存在或路径无效！\n{tips}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // 3. 加载图片（保持原有逻辑，增加工位上下文）
                using (var fileStream = new FileStream(resultImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fileStream.Length == 0)
                    {
                        throw new IOException($"{station}工位结果图文件为空（可能是检测时保存失败）");
                    }

                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 加载后释放文件句柄
                    bitmap.StreamSource = fileStream;
                    bitmap.EndInit();

                    if (!bitmap.IsFrozen)
                    {
                        bitmap.Freeze(); // 允许跨线程访问
                    }

                    // 4. 更新对应工位的UI（核心：按工位更新图像源和查看状态）
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        // 更新图像源（对应工位的专属属性）
                        switch (station)
                        {
                            case "LAB":
                                CurrentResultImageLAB = bitmap;
                                break;
                            case "LCD":
                                CurrentResultImageLCD = bitmap;
                                break;
                            case "RAB":
                                CurrentResultImageRAB = bitmap;
                                break;
                            case "RCD":
                                CurrentResultImageRCD = bitmap;
                                break;
                        }

                        // 更新该工位的查看状态（标记为“正在查看”）
                        lock (_statusLocker)
                        {
                            _isViewingResultDict[station] = true;
                        }
                    });

                    AddLog($"[查看成功] {station}工位结果图已加载：{resultImagePath}（分辨率：{bitmap.PixelWidth}×{bitmap.PixelHeight}）");
                }
            }
            // 细化异常场景（增加工位上下文）
            catch (UnauthorizedAccessException ex)
            {
                AddLog($"[加载失败] {station}工位无权限访问结果图：{ex.Message}，路径：{resultImagePath}");
                System.Windows.MessageBox.Show($"{station}工位：无权限访问图片文件，请检查文件夹权限！\n路径：{resultImagePath}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (FileFormatException ex)
            {
                AddLog($"[加载失败] {station}工位图片格式不支持：{ex.Message}，路径：{resultImagePath}");
                System.Windows.MessageBox.Show($"{station}工位：图片格式无效（仅支持 BMP/JPG/PNG/TIFF 等）！\n路径：{resultImagePath}", "格式错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                AddLog($"[加载失败] {station}工位文件读写异常：{ex.Message}，路径：{resultImagePath}");
                System.Windows.MessageBox.Show($"{station}工位：图片文件被占用或损坏！\n建议：关闭其他可能打开该文件的程序后重试。", "文件异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                AddLog($"[加载失败] {station}工位未知错误：{ex.Message}，堆栈：{ex.StackTrace}，路径：{resultImagePath}");
                System.Windows.MessageBox.Show($"{station}工位：图片加载失败：{ex.Message}\n路径：{resultImagePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        #region 结果图缩放
        #region LAB
        //滚轮缩放逻辑
        private void OnZoomRealTimeImageLAB(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大
                RealTimeImageScaleXLAB = Math.Min(RealTimeImageScaleXLAB + ZoomStepLAB, MaxScaleLAB);
                RealTimeImageScaleYLAB = RealTimeImageScaleXLAB;
            }
            else
            {
                // 缩小
                RealTimeImageScaleXLAB = Math.Max(RealTimeImageScaleXLAB - ZoomStepLAB, MinScaleLAB);
                RealTimeImageScaleYLAB = RealTimeImageScaleXLAB;
            }
            e.Handled = true;
        }



        private void OnZoomInRealTimeLAB()
        {
            RealTimeImageScaleXLAB = Math.Min(RealTimeImageScaleXLAB + ZoomStepLAB, MaxScaleLAB);
            RealTimeImageScaleYLAB = RealTimeImageScaleXLAB;
        }

        private void OnZoomOutRealTimeLAB()
        {
            RealTimeImageScaleXLAB = Math.Max(RealTimeImageScaleXLAB - ZoomStepLAB, MinScaleLAB);
            RealTimeImageScaleYLAB = RealTimeImageScaleXLAB;
        }

        private void OnResetRealTimeZoomLAB()
        {
            RealTimeImageScaleXLAB = 1.0;
            RealTimeImageScaleYLAB = 1.0;
            RealTimeImageTranslateXLAB = 0; // 回到X轴初始位置
            RealTimeImageTranslateYLAB = 0; // 回到Y轴初始位置
        }
        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDownLAB(MouseButtonEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingLAB = true;
                // 记录起始鼠标位置
                _startMousePointLAB = e.GetPosition(image);
                // 记录起始偏移量
                _startTranslateXLAB = RealTimeImageTranslateXLAB;
                _startTranslateYLAB = RealTimeImageTranslateYLAB;
                // 捕获鼠标
                image.CaptureMouse();
            }
            e.Handled = true; // 防止事件冒泡
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMoveLAB(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }

            if (!_isImageDraggingLAB || e.Source is not System.Windows.Controls.Image image)
                return;

            // 计算当前鼠标位置与起始位置的偏移
            System.Windows.Point currentMousePoint = e.GetPosition(image);
            double offsetX = currentMousePoint.X - _startMousePointLAB.X;
            double offsetY = currentMousePoint.Y - _startMousePointLAB.Y;

            // 更新拖动偏移
            RealTimeImageTranslateXLAB = _startTranslateXLAB + offsetX;
            RealTimeImageTranslateYLAB = _startTranslateYLAB + offsetY;
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUpLAB(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingLAB = false;
                image.ReleaseMouseCapture(); // 释放鼠标捕获
            }
            e.Handled = true;
        }
        #endregion
        #region LCD
        //滚轮缩放逻辑
        private void OnZoomRealTimeImageLCD(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大
                RealTimeImageScaleXLCD = Math.Min(RealTimeImageScaleXLCD + ZoomStepLCD, MaxScaleLCD);
                RealTimeImageScaleYLCD = RealTimeImageScaleXLCD;
            }
            else
            {
                // 缩小
                RealTimeImageScaleXLCD = Math.Max(RealTimeImageScaleXLCD - ZoomStepLCD, MinScaleLCD);
                RealTimeImageScaleYLCD = RealTimeImageScaleXLCD;
            }
            e.Handled = true;
        }



        private void OnZoomInRealTimeLCD()
        {
            RealTimeImageScaleXLCD = Math.Min(RealTimeImageScaleXLCD + ZoomStepLCD, MaxScaleLCD);
            RealTimeImageScaleYLCD = RealTimeImageScaleXLCD;
        }

        private void OnZoomOutRealTimeLCD()
        {
            RealTimeImageScaleXLCD = Math.Max(RealTimeImageScaleXLCD - ZoomStepLCD, MinScaleLCD);
            RealTimeImageScaleYLCD = RealTimeImageScaleXLCD;
        }

        private void OnResetRealTimeZoomLCD()
        {
            RealTimeImageScaleXLCD = 1.0;
            RealTimeImageScaleYLCD = 1.0;
            RealTimeImageTranslateXLCD = 0; // 回到X轴初始位置
            RealTimeImageTranslateYLCD = 0; // 回到Y轴初始位置
        }
        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDownLCD(MouseButtonEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingLCD = true;
                // 记录起始鼠标位置
                _startMousePointLCD = e.GetPosition(image);
                // 记录起始偏移量
                _startTranslateXLCD = RealTimeImageTranslateXLCD;
                _startTranslateYLCD = RealTimeImageTranslateYLCD;
                // 捕获鼠标
                image.CaptureMouse();
            }
            e.Handled = true; // 防止事件冒泡
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMoveLCD(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }

            if (!_isImageDraggingLCD || e.Source is not System.Windows.Controls.Image image)
                return;

            // 计算当前鼠标位置与起始位置的偏移
            System.Windows.Point currentMousePoint = e.GetPosition(image);
            double offsetX = currentMousePoint.X - _startMousePointLCD.X;
            double offsetY = currentMousePoint.Y - _startMousePointLCD.Y;

            // 更新拖动偏移
            RealTimeImageTranslateXLCD = _startTranslateXLCD + offsetX;
            RealTimeImageTranslateYLCD = _startTranslateYLCD + offsetY;
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUpLCD(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingLCD = false;
                image.ReleaseMouseCapture(); // 释放鼠标捕获
            }
            e.Handled = true;
        }
        #endregion
        #region RAB
        //滚轮缩放逻辑
        private void OnZoomRealTimeImageRAB(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大
                RealTimeImageScaleXRAB = Math.Min(RealTimeImageScaleXRAB + ZoomStepRAB, MaxScaleRAB);
                RealTimeImageScaleYRAB = RealTimeImageScaleXRAB;
            }
            else
            {
                // 缩小
                RealTimeImageScaleXRAB = Math.Max(RealTimeImageScaleXRAB - ZoomStepRAB, MinScaleRAB);
                RealTimeImageScaleYRAB = RealTimeImageScaleXRAB;
            }
            e.Handled = true;
        }



        private void OnZoomInRealTimeRAB()
        {
            RealTimeImageScaleXRAB = Math.Min(RealTimeImageScaleXRAB + ZoomStepRAB, MaxScaleRAB);
            RealTimeImageScaleYRAB = RealTimeImageScaleXRAB;
        }

        private void OnZoomOutRealTimeRAB()
        {
            RealTimeImageScaleXRAB = Math.Max(RealTimeImageScaleXRAB - ZoomStepRAB, MinScaleRAB);
            RealTimeImageScaleYRAB = RealTimeImageScaleXRAB;
        }

        private void OnResetRealTimeZoomRAB()
        {
            RealTimeImageScaleXRAB = 1.0;
            RealTimeImageScaleYRAB = 1.0;
            RealTimeImageTranslateXRAB = 0; // 回到X轴初始位置
            RealTimeImageTranslateYRAB = 0; // 回到Y轴初始位置
        }
        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDownRAB(MouseButtonEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingRAB = true;
                // 记录起始鼠标位置
                _startMousePointRAB = e.GetPosition(image);
                // 记录起始偏移量
                _startTranslateXRAB = RealTimeImageTranslateXRAB;
                _startTranslateYRAB = RealTimeImageTranslateYRAB;
                // 捕获鼠标
                image.CaptureMouse();
            }
            e.Handled = true; // 防止事件冒泡
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMoveRAB(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }

            if (!_isImageDraggingRAB || e.Source is not System.Windows.Controls.Image image)
                return;

            // 计算当前鼠标位置与起始位置的偏移
            System.Windows.Point currentMousePoint = e.GetPosition(image);
            double offsetX = currentMousePoint.X - _startMousePointRAB.X;
            double offsetY = currentMousePoint.Y - _startMousePointRAB.Y;

            // 更新拖动偏移
            RealTimeImageTranslateXRAB = _startTranslateXRAB + offsetX;
            RealTimeImageTranslateYRAB = _startTranslateYRAB + offsetY;
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUpRAB(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingRAB = false;
                image.ReleaseMouseCapture(); // 释放鼠标捕获
            }
            e.Handled = true;
        }
        #endregion
        #region RCD
        //滚轮缩放逻辑
        private void OnZoomRealTimeImageRCD(MouseWheelEventArgs? e)
        {
            if (e == null) return;

            // e.Delta：滚轮增量（正值=向前滚动=放大，负值=向后滚动=缩小）
            if (e.Delta > 0)
            {
                // 放大
                RealTimeImageScaleXRCD = Math.Min(RealTimeImageScaleXRCD + ZoomStepRCD, MaxScaleRCD);
                RealTimeImageScaleYRCD = RealTimeImageScaleXRCD;
            }
            else
            {
                // 缩小
                RealTimeImageScaleXRCD = Math.Max(RealTimeImageScaleXRCD - ZoomStepRCD, MinScaleRCD);
                RealTimeImageScaleYRCD = RealTimeImageScaleXRCD;
            }
            e.Handled = true;
        }



        private void OnZoomInRealTimeRCD()
        {
            RealTimeImageScaleXRCD = Math.Min(RealTimeImageScaleXRCD + ZoomStepRCD, MaxScaleRCD);
            RealTimeImageScaleYRCD = RealTimeImageScaleXRCD;
        }

        private void OnZoomOutRealTimeRCD()
        {
            RealTimeImageScaleXRCD = Math.Max(RealTimeImageScaleXRCD - ZoomStepRCD, MinScaleRCD);
            RealTimeImageScaleYRCD = RealTimeImageScaleXRCD;
        }

        private void OnResetRealTimeZoomRCD()
        {
            RealTimeImageScaleXRCD = 1.0;
            RealTimeImageScaleYRCD = 1.0;
            RealTimeImageTranslateXRCD = 0; // 回到X轴初始位置
            RealTimeImageTranslateYRCD = 0; // 回到Y轴初始位置
        }
        /// <summary>
        /// 鼠标按下：记录拖动起始状态
        /// </summary>
        private void OnImageMouseDownRCD(MouseButtonEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingRCD = true;
                // 记录起始鼠标位置
                _startMousePointRCD = e.GetPosition(image);
                // 记录起始偏移量
                _startTranslateXRCD = RealTimeImageTranslateXRCD;
                _startTranslateYRCD = RealTimeImageTranslateYRCD;
                // 捕获鼠标
                image.CaptureMouse();
            }
            e.Handled = true; // 防止事件冒泡
        }

        /// <summary>
        /// 鼠标移动：计算拖动偏移
        /// </summary>
        private void OnImageMouseMoveRCD(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }

            if (!_isImageDraggingRCD || e.Source is not System.Windows.Controls.Image image)
                return;

            // 计算当前鼠标位置与起始位置的偏移
            System.Windows.Point currentMousePoint = e.GetPosition(image);
            double offsetX = currentMousePoint.X - _startMousePointRCD.X;
            double offsetY = currentMousePoint.Y - _startMousePointRCD.Y;

            // 更新拖动偏移
            RealTimeImageTranslateXRCD = _startTranslateXRCD + offsetX;
            RealTimeImageTranslateYRCD = _startTranslateYRCD + offsetY;
            e.Handled = true;
        }

        /// <summary>
        /// 鼠标松开/离开：结束拖动
        /// </summary>
        private void OnImageMouseUpRCD(MouseEventArgs? e)
        {
            if (e == null)
            {
                return;
            }
            if (e.Source is System.Windows.Controls.Image image)
            {
                _isImageDraggingRCD = false;
                image.ReleaseMouseCapture(); // 释放鼠标捕获
            }
            e.Handled = true;
        }
        #endregion
        #endregion

        #region 饼状图
        private void UpdateRiskRatios()
        {
            // 总计数
            int totalCount = Count_ZJDP + Count_ZJSP + Count_FJDP + Count_FJSP
                            + Count_OHBL + Count_CPBL + Count_ZJYDP
                           + Count_FJBYJXBL + Count_ZJBYJXBL + Count_ZJZJJXBL + Count_FJYS + OKDetectionNum;

            // 避免除以零
            if (totalCount == 0)
            {
                Ratio_ZJDP = "0.00%";
                Ratio_ZJSP = "0.00%";
                Ratio_FJDP = "0.00%";
                Ratio_FJSP = "0.00%";
                Ratio_OHBL = "0.00%";
                Ratio_CPBL = "0.00%";
                Ratio_ZJYDP = "0.00%";
                Ratio_FJBYJXBL = "0.00%";
                Ratio_ZJBYJXBL = "0.00%";
                Ratio_ZJZJJXBL = "0.00%";
                Ratio_FJYS = "0.00%";
                return;
            }

            // 计算各类型占比
            Ratio_ZJDP = $"{(double)Count_ZJDP / totalCount * 100:F2}%";
            Ratio_ZJSP = $"{(double)Count_ZJSP / totalCount * 100:F2}%";
            Ratio_FJDP = $"{(double)Count_FJDP / totalCount * 100:F2}%";
            Ratio_FJSP = $"{(double)Count_FJSP / totalCount * 100:F2}%";
            Ratio_OHBL = $"{(double)Count_OHBL / totalCount * 100:F2}%";
            Ratio_CPBL = $"{(double)Count_CPBL / totalCount * 100:F2}%";
            Ratio_ZJYDP = $"{(double)Count_ZJYDP / totalCount * 100:F2}%";
            Ratio_FJBYJXBL = $"{(double)Count_FJBYJXBL / totalCount * 100:F2}%";
            Ratio_ZJBYJXBL = $"{(double)Count_ZJBYJXBL / totalCount * 100:F2}%";
            Ratio_ZJZJJXBL = $"{(double)Count_ZJZJJXBL / totalCount * 100:F2}%";
            Ratio_FJYS = $"{(double)Count_FJYS / totalCount * 100:F2}%";
            SaveConfig();
        }

        private readonly OxyColor[] _defectColors = new OxyColor[]
            {
                OxyColor.FromRgb(255, 99, 132),   // #FF6384 - OH不良
                OxyColor.FromRgb(54, 162, 235),   // #36A2EB - 正极少片
                OxyColor.FromRgb(255, 206, 86),   // #FFCE56 - 正极多片
                OxyColor.FromRgb(75, 192, 192),   // #4BC0C0 - 负极多片
                OxyColor.FromRgb(153, 102, 255),  // #9966FF - 负极少片
                OxyColor.FromRgb(255, 159, 64),   // #FF9F40 - 负极差不良
                OxyColor.FromRgb(231, 233, 237),  // #E7E9ED - 负极边缘间隙不良
                OxyColor.FromRgb(255, 87, 34),    // #FF5722 - 正极边缘间隙不良
                OxyColor.FromRgb(67, 160, 71),    // #43A047 - 正极中间间隙不良
                OxyColor.FromRgb(141, 110, 99),   // #2196F3 - 负极压伤
                OxyColor.FromRgb(123, 31, 162)    // #7B1FA2 - 正极Y方向掉片
            };

        // 辅助方法：获取所有缺陷类型及其计数值的列表
        private List<(string Name, int Count)> GetDefectData()
        {
            return new List<(string, int)>
            {
                ("OH不良", Count_OHBL),
                ("正极少片", Count_ZJSP),
                ("正极多片", Count_ZJDP),
                ("负极多片", Count_FJDP),
                ("负极少片", Count_FJSP),
                ("负极差不良", Count_CPBL),
                ("负极边缘间隙不良", Count_FJBYJXBL),
                ("正极边缘间隙不良", Count_ZJBYJXBL),
                ("正极中间间隙不良", Count_ZJZJJXBL),
                ("负极压伤", Count_FJYS),
                ("正极Y方向掉片", Count_ZJYDP)
            };
        }

        private void InitPieChart()
        {
            PieModel = new PlotModel
            {
                //Title = "状态占比",
                IsLegendVisible = false,
                PlotMargins = new OxyThickness(10, 5, 10, 5)
            };

            var pieSeries = new PieSeries
            {
                StrokeThickness = 0,
                InsideLabelPosition = 0.85,
                InsideLabelFormat = "",
                OutsideLabelFormat = "",
                TickRadialLength = 0,
                TickHorizontalLength = 0,
                AngleSpan = 360,
                StartAngle = 45

            };

            // 初始值
            pieSeries.Slices.Add(new PieSlice("OK", 0)
            {
                Fill = OxyColor.FromRgb(47, 128, 237) // #2F80ED - OK颜色
            });

            PieModel.Series.Add(pieSeries);
        }

        // 更新饼图数据
        private void UpdatePieChartData()
        {
            if (PieModel?.Series[0] is PieSeries pieSeries)
            {
                pieSeries.Slices.Clear();

                // 获取所有缺陷数据
                var defectData = GetDefectData();

                // 计算总检测数
                int totalCount = defectData.Sum(d => d.Count) + OKDetectionNum;

                // 处理初始状态或无数据状态
                if (totalCount == 0)
                {
                    pieSeries.Slices.Add(new PieSlice("无检测数据", 1)
                    {
                        Fill = OxyColors.LightGray
                    });
                }
                else
                {
                    //添加OK切片
                    if (OKDetectionNum > 0)
                    {
                        double okPercentage = (double)OKDetectionNum / totalCount * 100;
                        pieSeries.Slices.Add(new PieSlice($"OK ({okPercentage:F1}%)", OKDetectionNum)
                        {
                            Fill = OxyColor.FromRgb(47, 128, 237) // OK 使用固定的蓝色
                        });
                    }

                    //添加各个缺陷类型的切片
                    for (int i = 0; i < defectData.Count; i++)
                    {
                        var (name, count) = defectData[i];
                        // 只添加计数大于0的缺陷类型
                        if (count > 0)
                        {
                            double percentage = (double)count / totalCount * 100;
                            // 使用预定义的颜色，确保与界面图例颜色一致
                            OxyColor color = _defectColors[i];

                            //百分比≤2% 则隐藏标签----没效果？
                            string sliceLabel = percentage > 2
                                ? $"{name} ({percentage:F1}%)"
                                : string.Empty;

                            pieSeries.Slices.Add(new PieSlice(sliceLabel, count)
                            {
                                Fill = color
                            });
                        }
                    }
                }
                // 刷新图表
                PieModel.InvalidatePlot(true);
            }
        }

        #endregion
        // ---------- 清空列表：仅清空图片名表格 ----------
        private void ExecuteClearList()
        {
            if (!IsInAnyDetection)
            {
                System.Windows.MessageBox.Show(
                     "当前正在执行检测任务或处于在线监控模式，\n暂无法清空列表！\n请先结束检测/关闭监控后再尝试。",
                     "操作限制",
                     MessageBoxButton.OK,
                     MessageBoxImage.Warning);

                AddLog("[清空列表] 检测中/监控中，操作终止");
                return;
            }
            // 弹出确认对话框
            MessageBoxResult result = System.Windows.MessageBox.Show(
                "确定要清空图片列表吗？此操作不可恢复。",
                "确认清空",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                DetectionDataList.Clear();
                AddLog("[清空列表] 用户确认，已清空数据");
                _fullDetectionDataList.Clear();
            }
            else
            {
                AddLog("[清空列表] 用户取消，操作终止");
            }
        }
        // ---------- 统计清零：仅清空负极缺陷信息 ----------
        private void ExecuteClearStatistics()
        {
            if (!IsInAnyDetection)
            {
                System.Windows.MessageBox.Show(
                     "当前正在执行检测任务或处于在线监控模式，\n暂无法重置统计数据！\n请先结束检测/关闭监控后再尝试。",
                     "操作限制",
                     MessageBoxButton.OK,
                     MessageBoxImage.Warning);

                AddLog("[统计清零] 检测中/监控中，操作终止");
                return;
            }

            MessageBoxResult result = System.Windows.MessageBox.Show(
                "确定要重置统计数据吗？此操作不可恢复。",
                "确认清零",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.OK)
            {
                _fullDetectionDataList.Clear();
                OKDetectionNum = 0;
                NGDetectionNum = 0;
                Count_OHBL = 0;
                Count_ZJDP = 0;
                Count_ZJSP = 0;
                Count_FJDP = 0;
                Count_FJSP = 0;
                Count_CPBL = 0;
                Count_FJBYJXBL = 0;
                Count_ZJBYJXBL = 0;
                Count_ZJZJJXBL = 0;
                Count_FJYS = 0;
                Count_ZJYDP = 0;

                // 手动触发属性通知，确保UI更新
                OnPropertyChanged(nameof(TotalDetectionNum));
                OnPropertyChanged(nameof(NGDetectionNum));
                OnPropertyChanged(nameof(Count_OHBL));
                OnPropertyChanged(nameof(Count_ZJDP));
                OnPropertyChanged(nameof(Count_ZJSP));
                OnPropertyChanged(nameof(Count_FJDP));
                OnPropertyChanged(nameof(Count_FJSP));
                OnPropertyChanged(nameof(Count_CPBL));
                OnPropertyChanged(nameof(Count_FJBYJXBL));
                OnPropertyChanged(nameof(Count_ZJBYJXBL));
                OnPropertyChanged(nameof(Count_ZJZJJXBL));
                OnPropertyChanged(nameof(Count_FJYS));
                OnPropertyChanged(nameof(Count_ZJYDP));
                UpdatePieChartData();
                AddLog("[统计清零] 用户确认，已重置统计");
            }
            else
            {
                AddLog("[统计清零] 用户取消，操作终止");
            }
        }

        #region 软件重启加载配置
        //一键重置阈值到默认值
        private void ResetThresholds()
        {
            // 检查是否登录
            if (!_userCenterViewModel.IsLoggedIn)
            {
                // 使用 MessageBox 弹出登录提示
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作。\n点击“确定”将跳转到登录页面。",
                    "操作需要登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // 这里只有OK按钮，导航到登录页
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                // 使用 MessageBox 弹出权限不足提示
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            // 确认用户是否要重置
            var result1 = System.Windows.MessageBox.Show(
                "确定要将阈值重置为上一次保存的值吗？\n此操作会覆盖当前未保存的修改。",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result1 != MessageBoxResult.Yes)
                return;

            try
            {
                _projectsetting.UpdateSettingValue("Gap1Limit", "VM_CTAutoDetectionC06Page", 0.50);
                _projectsetting.UpdateSettingValue("Gap2Limit", "VM_CTAutoDetectionC06Page", 2.75);
                _projectsetting.UpdateSettingValue("Gap3Limit", "VM_CTAutoDetectionC06Page", 2.2);
                _projectsetting.UpdateSettingValue("Gap4Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap5Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap6Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap7Limit", "VM_CTAutoDetectionC06Page", 0.50);
                _projectsetting.UpdateSettingValue("Gap8Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap9Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap10Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap11Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap12Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap13Limit", "VM_CTAutoDetectionC06Page", 0.5);
                _projectsetting.UpdateSettingValue("Gap14Limit", "VM_CTAutoDetectionC06Page", 2);
                _projectsetting.UpdateSettingValue("Gap15Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap16Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap17Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap18Limit", "VM_CTAutoDetectionC06Page", 0.50);
                _projectsetting.UpdateSettingValue("Gap19Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap20Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap21Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap22Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap23Limit", "VM_CTAutoDetectionC06Page", 0.5);
                _projectsetting.UpdateSettingValue("Gap24Limit", "VM_CTAutoDetectionC06Page", 2.75);
                _projectsetting.UpdateSettingValue("Gap25Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap26Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap27Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap28Limit", "VM_CTAutoDetectionC06Page", 0.5);
                _projectsetting.UpdateSettingValue("Gap29Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap30Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap31Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap32Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap33Limit", "VM_CTAutoDetectionC06Page", 0.5);
                _projectsetting.UpdateSettingValue("Gap34Limit", "VM_CTAutoDetectionC06Page", 2);
                _projectsetting.UpdateSettingValue("Gap35Limit", "VM_CTAutoDetectionC06Page", 2.2);
                _projectsetting.UpdateSettingValue("Gap36Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap37Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap38Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap39Limit", "VM_CTAutoDetectionC06Page", 0.5);
                _projectsetting.UpdateSettingValue("Gap40Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap41Limit", "VM_CTAutoDetectionC06Page", 2.4);
                _projectsetting.UpdateSettingValue("Gap42Limit", "VM_CTAutoDetectionC06Page", 0.64);
                _projectsetting.UpdateSettingValue("Gap43Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap44Limit", "VM_CTAutoDetectionC06Page", 0.45);
                _projectsetting.UpdateSettingValue("Gap45Limit", "VM_CTAutoDetectionC06Page", 0.3);
                _projectsetting.UpdateSettingValue("Gap46Limit", "VM_CTAutoDetectionC06Page", 0.3);
                _projectsetting.UpdateSettingValue("Gap47Limit", "VM_CTAutoDetectionC06Page", 0.5);
                InitThreshold();
                SyncTempPropertiesWithFormalProperties();
            }
            catch (Exception ex)
            {
                AddLog($"重置失败：{ex.Message}");
            }
        }
        private void SaveThresholdConfig()
        {
            //检查是否登录
            if (!_userCenterViewModel.IsLoggedIn)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作。\n点击“确定”将跳转到登录页面。",
                    "操作需要登录",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                //只有OK按钮导航到登录页
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                // 使用 MessageBox 弹出权限不足提示
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            try
            {
                _projectsetting.UpdateSettingValue("Gap1Limit", "VM_CTAutoDetectionC06Page", TempGap1Limit);
                _projectsetting.UpdateSettingValue("Gap2Limit", "VM_CTAutoDetectionC06Page", TempGap2Limit);
                _projectsetting.UpdateSettingValue("Gap3Limit", "VM_CTAutoDetectionC06Page", TempGap3Limit);
                _projectsetting.UpdateSettingValue("Gap4Limit", "VM_CTAutoDetectionC06Page", TempGap4Limit);
                _projectsetting.UpdateSettingValue("Gap5Limit", "VM_CTAutoDetectionC06Page", TempGap5Limit);
                _projectsetting.UpdateSettingValue("Gap6Limit", "VM_CTAutoDetectionC06Page", TempGap6Limit);
                _projectsetting.UpdateSettingValue("Gap7Limit", "VM_CTAutoDetectionC06Page", TempGap7Limit);
                _projectsetting.UpdateSettingValue("Gap8Limit", "VM_CTAutoDetectionC06Page", TempGap8Limit);
                _projectsetting.UpdateSettingValue("Gap9Limit", "VM_CTAutoDetectionC06Page", TempGap9Limit);
                _projectsetting.UpdateSettingValue("Gap10Limit", "VM_CTAutoDetectionC06Page", TempGap10Limit);
                _projectsetting.UpdateSettingValue("Gap11Limit", "VM_CTAutoDetectionC06Page", TempGap11Limit);
                _projectsetting.UpdateSettingValue("Gap12Limit", "VM_CTAutoDetectionC06Page", TempGap12Limit);
                _projectsetting.UpdateSettingValue("Gap13Limit", "VM_CTAutoDetectionC06Page", TempGap13Limit);
                _projectsetting.UpdateSettingValue("Gap14Limit", "VM_CTAutoDetectionC06Page", TempGap14Limit);
                _projectsetting.UpdateSettingValue("Gap15Limit", "VM_CTAutoDetectionC06Page", TempGap15Limit);
                _projectsetting.UpdateSettingValue("Gap16Limit", "VM_CTAutoDetectionC06Page", TempGap16Limit);
                _projectsetting.UpdateSettingValue("Gap17Limit", "VM_CTAutoDetectionC06Page", TempGap17Limit);
                _projectsetting.UpdateSettingValue("Gap18Limit", "VM_CTAutoDetectionC06Page", TempGap18Limit);
                _projectsetting.UpdateSettingValue("Gap19Limit", "VM_CTAutoDetectionC06Page", TempGap19Limit);
                _projectsetting.UpdateSettingValue("Gap20Limit", "VM_CTAutoDetectionC06Page", TempGap20Limit);
                _projectsetting.UpdateSettingValue("Gap21Limit", "VM_CTAutoDetectionC06Page", TempGap21Limit);
                _projectsetting.UpdateSettingValue("Gap22Limit", "VM_CTAutoDetectionC06Page", TempGap22Limit);
                _projectsetting.UpdateSettingValue("Gap23Limit", "VM_CTAutoDetectionC06Page", TempGap23Limit);
                _projectsetting.UpdateSettingValue("Gap24Limit", "VM_CTAutoDetectionC06Page", TempGap24Limit);
                _projectsetting.UpdateSettingValue("Gap25Limit", "VM_CTAutoDetectionC06Page", TempGap25Limit);
                _projectsetting.UpdateSettingValue("Gap26Limit", "VM_CTAutoDetectionC06Page", TempGap26Limit);
                _projectsetting.UpdateSettingValue("Gap27Limit", "VM_CTAutoDetectionC06Page", TempGap27Limit);
                _projectsetting.UpdateSettingValue("Gap28Limit", "VM_CTAutoDetectionC06Page", TempGap28Limit);
                _projectsetting.UpdateSettingValue("Gap29Limit", "VM_CTAutoDetectionC06Page", TempGap29Limit);
                _projectsetting.UpdateSettingValue("Gap30Limit", "VM_CTAutoDetectionC06Page", TempGap30Limit);
                _projectsetting.UpdateSettingValue("Gap31Limit", "VM_CTAutoDetectionC06Page", TempGap31Limit);
                _projectsetting.UpdateSettingValue("Gap32Limit", "VM_CTAutoDetectionC06Page", TempGap32Limit);
                _projectsetting.UpdateSettingValue("Gap33Limit", "VM_CTAutoDetectionC06Page", TempGap33Limit);
                _projectsetting.UpdateSettingValue("Gap34Limit", "VM_CTAutoDetectionC06Page", TempGap34Limit);
                _projectsetting.UpdateSettingValue("Gap35Limit", "VM_CTAutoDetectionC06Page", TempGap35Limit);
                _projectsetting.UpdateSettingValue("Gap36Limit", "VM_CTAutoDetectionC06Page", TempGap36Limit);
                _projectsetting.UpdateSettingValue("Gap37Limit", "VM_CTAutoDetectionC06Page", TempGap37Limit);
                _projectsetting.UpdateSettingValue("Gap38Limit", "VM_CTAutoDetectionC06Page", TempGap38Limit);
                _projectsetting.UpdateSettingValue("Gap39Limit", "VM_CTAutoDetectionC06Page", TempGap39Limit);
                _projectsetting.UpdateSettingValue("Gap40Limit", "VM_CTAutoDetectionC06Page", TempGap40Limit);
                _projectsetting.UpdateSettingValue("Gap41Limit", "VM_CTAutoDetectionC06Page", TempGap41Limit);
                _projectsetting.UpdateSettingValue("Gap42Limit", "VM_CTAutoDetectionC06Page", TempGap42Limit);
                _projectsetting.UpdateSettingValue("Gap43Limit", "VM_CTAutoDetectionC06Page", TempGap43Limit);
                _projectsetting.UpdateSettingValue("Gap44Limit", "VM_CTAutoDetectionC06Page", TempGap44Limit);
                _projectsetting.UpdateSettingValue("Gap45Limit", "VM_CTAutoDetectionC06Page", TempGap45Limit);
                _projectsetting.UpdateSettingValue("Gap46Limit", "VM_CTAutoDetectionC06Page", TempGap46Limit);
                _projectsetting.UpdateSettingValue("Gap47Limit", "VM_CTAutoDetectionC06Page", TempGap47Limit);
                InitThreshold();
                SyncTempPropertiesWithFormalProperties();
                AddLog("阈值配置保存成功");
                Growl.SuccessGlobal("阈值配置保存成功！");
            }
            catch (Exception ex)
            {
                AddLog("更新设置失败");
                Growl.ErrorGlobal($"更新失败: {ex.Message}");
            }

        }
        /// <summary>
        /// 将所有正式属性的值同步到对应的临时属性
        /// </summary>
        public void SyncTempPropertiesWithFormalProperties()
        {
            TempGap1Limit = Gap1Limit;
            TempGap2Limit = Gap2Limit;
            TempGap3Limit = Gap3Limit;
            TempGap4Limit = Gap4Limit;
            TempGap5Limit = Gap5Limit;
            TempGap6Limit = Gap6Limit;
            TempGap7Limit = Gap7Limit;
            TempGap8Limit = Gap8Limit;
            TempGap9Limit = Gap9Limit;
            TempGap10Limit = Gap10Limit;
            TempGap11Limit = Gap11Limit;
            TempGap12Limit = Gap12Limit;
            TempGap13Limit = Gap13Limit;
            TempGap14Limit = Gap14Limit;
            TempGap15Limit = Gap15Limit;
            TempGap16Limit = Gap16Limit;
            TempGap17Limit = Gap17Limit;
            TempGap18Limit = Gap18Limit;
            TempGap19Limit = Gap19Limit;
            TempGap20Limit = Gap20Limit;
            TempGap21Limit = Gap21Limit;
            TempGap22Limit = Gap22Limit;
            TempGap23Limit = Gap23Limit;
            TempGap24Limit = Gap24Limit;
            TempGap25Limit = Gap25Limit;
            TempGap26Limit = Gap26Limit;
            TempGap27Limit = Gap27Limit;
            TempGap28Limit = Gap28Limit;
            TempGap29Limit = Gap29Limit;
            TempGap30Limit = Gap30Limit;
            TempGap31Limit = Gap31Limit;
            TempGap32Limit = Gap32Limit;
            TempGap33Limit = Gap33Limit;
            TempGap34Limit = Gap34Limit;
            TempGap35Limit = Gap35Limit;
            TempGap36Limit = Gap36Limit;
            TempGap37Limit = Gap37Limit;
            TempGap38Limit = Gap38Limit;
            TempGap39Limit = Gap39Limit;
            TempGap40Limit = Gap40Limit;
            TempGap41Limit = Gap41Limit;
            TempGap42Limit = Gap42Limit;
            TempGap43Limit = Gap43Limit;
            TempGap44Limit = Gap44Limit;
            TempGap45Limit = Gap45Limit;
            TempGap46Limit = Gap46Limit;
            TempGap47Limit = Gap47Limit;

            AddLog("临时属性已从正式属性同步。");
        }

        /// <summary>
        /// 加载设置配置 
        /// </summary>
        private void LoadConfig()
        {
            //SelectedMonitorFolderPathLAB = _projectsetting.GetSettingValue("SelectedMonitorFolderPathLAB", "VM_CTAutoDetectionC06Page");
            //SelectedMonitorFolderPathLCD = _projectsetting.GetSettingValue("SelectedMonitorFolderPathLCD", "VM_CTAutoDetectionC06Page");
            //SelectedMonitorFolderPathRAB = _projectsetting.GetSettingValue("SelectedMonitorFolderPathRAB", "VM_CTAutoDetectionC06Page");
            //SelectedMonitorFolderPathRCD = _projectsetting.GetSettingValue("SelectedMonitorFolderPathRCD", "VM_CTAutoDetectionC06Page");
            //OKDetectionNum = _projectsetting.GetSettingValue("OKDetectionNum", "VM_CTAutoDetectionC06Page");
            //AddLog($"检测值{ OKDetectionNum}");
            //NGDetectionNum = _projectsetting.GetSettingValue("NGDetectionNum", "VM_CTAutoDetectionC06Page");
            //Count_ZJDP = _projectsetting.GetSettingValue("Count_ZJDP", "VM_CTAutoDetectionC06Page");
            //Count_ZJSP = _projectsetting.GetSettingValue("Count_ZJSP", "VM_CTAutoDetectionC06Page");
            //Count_FJDP = _projectsetting.GetSettingValue("Count_FJDP", "VM_CTAutoDetectionC06Page");
            //Count_FJSP = _projectsetting.GetSettingValue("Count_FJSP", "VM_CTAutoDetectionC06Page");
            //Count_OHBL = _projectsetting.GetSettingValue("Count_OHBL", "VM_CTAutoDetectionC06Page");
            //Count_CPBL = _projectsetting.GetSettingValue("Count_CPBL", "VM_CTAutoDetectionC06Page");
            //Count_FJBYJXBL = _projectsetting.GetSettingValue("Count_FJBYJXBL", "VM_CTAutoDetectionC06Page");
            //Count_ZJBYJXBL = _projectsetting.GetSettingValue("Count_ZJBYJXBL", "VM_CTAutoDetectionC06Page");
            //Count_ZJZJJXBL = _projectsetting.GetSettingValue("Count_ZJZJJXBL", "VM_CTAutoDetectionC06Page");
            //Count_ZJYDP = _projectsetting.GetSettingValue("Count_ZJYDP", "VM_CTAutoDetectionC06Page");
            //Count_FJYS = _projectsetting.GetSettingValue("Count_FJYS", "VM_CTAutoDetectionC06Page");
            //UpdateRiskRatios();
            if (!File.Exists(_configFilePath)) return;

            try
            {
                string json = File.ReadAllText(_configFilePath);
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                // 安全地读取路径配置，如果值不存在或为null，则保持原有值
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathLAB", out var pathLAB) && pathLAB != null)
                    SelectedMonitorFolderPathLAB = pathLAB.ToString();
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathLCD", out var pathLCD) && pathLCD != null)
                    SelectedMonitorFolderPathLCD = pathLCD.ToString();
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathRAB", out var pathRAB) && pathRAB != null)
                    SelectedMonitorFolderPathRAB = pathRAB.ToString();
                if (dict != null && dict.TryGetValue("SelectedMonitorFolderPathRCD", out var pathRCD) && pathRCD != null)
                    SelectedMonitorFolderPathRCD = pathRCD.ToString();

                // 安全地读取整数配置，如果值不存在、为null或格式不正确，则保持原有值
                if (dict != null && dict.TryGetValue("OKDetectionNum", out var okDetectionNum) && okDetectionNum != null)
                {
                    int.TryParse(okDetectionNum.ToString(), out _historyOKCount);
                }
                if (dict != null && dict.TryGetValue("NGDetectionNum", out var ngDetectionNum) && ngDetectionNum != null)
                {
                    int.TryParse(ngDetectionNum.ToString(), out _historyNGCount);
                }

                if (dict != null && dict.TryGetValue("Count_ZJDP", out var Count_zjdp) && Count_zjdp != null)
                {
                    int.TryParse(Count_zjdp.ToString(), out _historyCount_ZJDP);
                }
                if (dict != null && dict.TryGetValue("Count_ZJSP", out var Count_zjsp) && Count_zjsp != null)
                {
                    int.TryParse(Count_zjsp.ToString(), out _historyCount_ZJSP);
                }
                if (dict != null && dict.TryGetValue("Count_FJSP", out var Count_fjsp) && Count_fjsp != null)
                {
                    int.TryParse(Count_fjsp.ToString(), out _historyCount_FJSP);
                }
                if (dict != null && dict.TryGetValue("Count_FJDP", out var Count_fjdp) && Count_fjdp != null)
                {
                    int.TryParse(Count_fjdp.ToString(), out _historyCount_FJDP);
                }
                if (dict != null && dict.TryGetValue("Count_OHBL", out var Count_ohbl) && Count_ohbl != null)
                {
                    int.TryParse(Count_ohbl.ToString(), out _historyCount_OHBL);
                }
                if (dict != null && dict.TryGetValue("Count_FJBYJXBL", out var Count_fjbyjxbl) && Count_fjbyjxbl != null)
                {
                    int.TryParse(Count_fjbyjxbl.ToString(), out _historyCount_FJBYJXBL);
                }
                if (dict != null && dict.TryGetValue("Count_ZJBYJXBL", out var Count_zjbyjxbl) && Count_zjbyjxbl != null)
                {
                    int.TryParse(Count_zjbyjxbl.ToString(), out _historyCount_ZJBYJXBL);
                }
                if (dict != null && dict.TryGetValue("Count_ZJZJJXBL", out var Count_zjzjjxbl) && Count_zjzjjxbl != null)
                {
                    int.TryParse(Count_zjzjjxbl.ToString(), out _historyCount_ZJZJJXBL);
                }
                if (dict != null && dict.TryGetValue("Count_CPBL", out var Count_cpbl) && Count_cpbl != null)
                {
                    int.TryParse(Count_cpbl.ToString(), out _historyCount_CPBL);
                }
                if (dict != null && dict.TryGetValue("Count_ZJYDP", out var Count_zjydp) && Count_zjydp != null)
                {
                    int.TryParse(Count_zjydp.ToString(), out _historyCount_ZJYDP);
                }
                if (dict != null && dict.TryGetValue("Count_FJYS", out var Count_fjys) && Count_fjys != null)
                {
                    int.TryParse(Count_fjys.ToString(), out _historyCount_FJYS);
                }

                // 初始计算：加载完成后，先用历史值更新UI
                lock (_statisticsLock)
                {
                    OKDetectionNum = _historyOKCount;
                    NGDetectionNum = _historyNGCount;

                    Count_ZJSP = _historyCount_ZJSP;
                    Count_ZJDP = _historyCount_ZJDP;
                    Count_FJDP = _historyCount_FJDP;
                    Count_FJSP = _historyCount_FJSP;
                    Count_OHBL = _historyCount_OHBL;
                    Count_CPBL = _historyCount_CPBL;
                    Count_ZJYDP = _historyCount_ZJYDP;
                    Count_FJBYJXBL = _historyCount_FJBYJXBL;
                    Count_ZJBYJXBL = _historyCount_ZJBYJXBL;
                    Count_ZJZJJXBL = _historyCount_ZJZJJXBL;
                    Count_FJYS = _historyCount_FJYS;
                }
                UpdateRiskRatios();
                AddLog($"[统计加载] 历史统计数据：总检测数={_historyTotalCount}，OK数={_historyOKCount}，NG数={_historyNGCount}");

            }
            catch (Exception ex)
            {
                AddLog($"加载配置失败：{ex.Message}");
            }
        }
        // 辅助方法：检查是否有权限执行操作
        private bool CheckPermission()
        {
            //检查是否登录
            if (!_userCenterViewModel.IsLoggedIn)
            {
                MessageBoxResult result = System.Windows.MessageBox.Show(
                    "请先登录以执行此操作！\n点击“确定”跳转到登录页。",
                    "操作需要登录",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Information
                );
                if (result == MessageBoxResult.OK)
                {
                    _navigationService.NavigateTo("UserCenter");
                }
                return false;
            }

            //检查权限级别
            if (_userCenterViewModel.RoleID < (int)UserRole.Engineer)
            {
                System.Windows.MessageBox.Show(
                    "您的权限等级不足，无法执行此操作。\n需要【工程师】或以上权限。",
                    "权限不足",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            // 检查通过
            return true;
        }
        /// <summary>
        /// 保存设置配置
        /// </summary>
        private void SaveConfig()
        {
            //if(SelectedMonitorFolderPathLAB != null)
            //{
            //    _projectsetting.UpdateSettingValue("SelectedMonitorFolderPathLAB", "VM_CTAutoDetectionC06Page", SelectedMonitorFolderPathLAB);
            //}
            //if (SelectedMonitorFolderPathLCD != null)
            //{
            //    _projectsetting.UpdateSettingValue("SelectedMonitorFolderPathLCD", "VM_CTAutoDetectionC06Page", SelectedMonitorFolderPathLCD);
            //}
            //if (SelectedMonitorFolderPathRAB != null)
            //{
            //    _projectsetting.UpdateSettingValue("SelectedMonitorFolderPathRAB", "VM_CTAutoDetectionC06Page", SelectedMonitorFolderPathRAB);
            //}
            //if (SelectedMonitorFolderPathRCD != null)
            //{
            //    _projectsetting.UpdateSettingValue("SelectedMonitorFolderPathRCD", "VM_CTAutoDetectionC06Page", SelectedMonitorFolderPathRCD);
            //}
            //if (SelectedResultImagePath != null)
            //{
            //    _projectsetting.UpdateSettingValue("SelectedResultImagePath", "VM_CTAutoDetectionC06Page", SelectedResultImagePath);
            //}
            //_projectsetting.UpdateSettingValue("OKDetectionNum", "VM_CTAutoDetectionC06Page", OKDetectionNum);
            //_projectsetting.UpdateSettingValue("NGDetectionNum", "VM_CTAutoDetectionC06Page", NGDetectionNum);
            //_projectsetting.UpdateSettingValue("Count_ZJDP", "VM_CTAutoDetectionC06Page", Count_ZJDP);
            //_projectsetting.UpdateSettingValue("Count_ZJSP", "VM_CTAutoDetectionC06Page", Count_ZJSP);
            //_projectsetting.UpdateSettingValue("Count_FJDP", "VM_CTAutoDetectionC06Page", Count_FJDP);
            //_projectsetting.UpdateSettingValue("Count_FJSP", "VM_CTAutoDetectionC06Page", Count_FJSP);
            //_projectsetting.UpdateSettingValue("Count_OHBL", "VM_CTAutoDetectionC06Page", Count_OHBL);
            //_projectsetting.UpdateSettingValue("Count_CPBL", "VM_CTAutoDetectionC06Page", Count_CPBL);
            //_projectsetting.UpdateSettingValue("Count_ZJYDP", "VM_CTAutoDetectionC06Page", Count_ZJYDP);
            //_projectsetting.UpdateSettingValue("Count_FJBYJXBL", "VM_CTAutoDetectionC06Page", Count_FJBYJXBL);
            //_projectsetting.UpdateSettingValue("Count_ZJBYJXBL", "VM_CTAutoDetectionC06Page", Count_ZJBYJXBL);
            //_projectsetting.UpdateSettingValue("Count_ZJZJJXBL", "VM_CTAutoDetectionC06Page", Count_ZJZJJXBL);
            //_projectsetting.UpdateSettingValue("Count_FJYS", "VM_CTAutoDetectionC06Page", Count_FJYS);

            try
            {
                var configData = new
                {
                    SelectedMonitorFolderPathLAB,
                    SelectedMonitorFolderPathLCD,
                    SelectedMonitorFolderPathRAB,
                    SelectedMonitorFolderPathRCD,
                    TotalDetectionNum,
                    OKDetectionNum,
                    NGDetectionNum,
                    OKRate,
                    Count_ZJSP,
                    Count_ZJDP,
                    Count_FJDP,
                    Count_FJSP,
                    Count_OHBL,
                    Count_CPBL,
                    Count_ZJYDP,
                    Count_FJBYJXBL,
                    Count_ZJBYJXBL,
                    Count_ZJZJJXBL,
                    Count_FJYS,
                };

                // 序列化为JSON
                string json = System.Text.Json.JsonSerializer.Serialize(configData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                AddLog($"保存配置失败：{ex.Message}");
            }
        }
        #endregion


        private static readonly object _fileLock = new object();
        private void GPUMonitor()
        {
            string processName = $"{Process.GetCurrentProcess().ProcessName}.exe";
            UIAutoMonitorCore.Monitor monitor = new UIAutoMonitorCore.Monitor();
            monitor.WindowProcessMonitorList = new List<string>() { processName };
            monitor.MonitorProcessInfo(true);
            Task.Factory.StartNew(
                async () =>
                {
                    // 创建监控目录
                    string monitorDir = Path.GetFullPath(
                        Path.Combine(System.Environment.CurrentDirectory, "../进程监控信息")
                    );
                    Directory.CreateDirectory(monitorDir); // 确保目录存在

                    string header = "时间,进程Id,主线程Id,CPU占用(%),内存占用(MB),GPU占用(%),I/O读写速度(MB/s),每秒异常数,线程数,等待获取托管锁线程数";

                    // 记录当前日期
                    DateTime currentDate = DateTime.Today;
                    bool isFirstWriteOfDay = true;

                    while (true)
                    {
                        try
                        {
                            // 检查日期是否变化
                            if (DateTime.Today != currentDate)
                            {
                                currentDate = DateTime.Today;
                                isFirstWriteOfDay = true;
                            }

                            // 按天生成文件名
                            string fileName = currentDate.ToString("yyyy_MM_dd");

                            StringBuilder contentBuilder = new StringBuilder();
                            contentBuilder.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].Pid},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].MainThreadId},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].CPULoad},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].RAMUsage},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].GPULoad},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].IODataRate},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].ExceptionCountPerSecond},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].ThreadCount},");
                            contentBuilder.Append($"{monitor.WindowProcessMonitorInfoDic[processName].ThreadCurrentQueueLength}");

                            // 写入文件
                            lock (_fileLock)
                            {
                                SaveCSVFile(
                                    monitorDir,
                                    fileName,
                                    // 每天首次写入时添加表头
                                    isFirstWriteOfDay ? header : string.Empty,
                                    contentBuilder.ToString()
                                );
                            }

                            isFirstWriteOfDay = false;
                            await Task.Delay(10000); // 10000毫秒 = 10秒
                        }
                        catch (Exception ex)
                        {
                            AddLog($"{ex}");
                            //
                        }
                    }
                },
                TaskCreationOptions.LongRunning
            );
        }

        public void SaveCSVFile(string directoryPath, string filename, string header, string content)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 处理CSV后缀
                if (!filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    filename += ".csv";
                }

                // 创建完整文件路径
                string fullPath = System.IO.Path.Combine(directoryPath, filename);

                // 检查并创建文件（如果需要）
                if (!File.Exists(fullPath))
                {
                    // 创建文件并写入表头
                    using (StreamWriter sw = new StreamWriter(fullPath, false, Encoding.UTF8))
                    {
                        sw.WriteLine(header);
                    }
                }
                else
                {
                    // 处理可能存在的只读属性
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(fullPath);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(fullPath, attributes & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch { /* 忽略属性错误 */ }
                }

                // 追加内容
                using (StreamWriter sw = new StreamWriter(fullPath, true, Encoding.UTF8))
                {
                    sw.WriteLine(content);
                }

                // 恢复只读属性
                try
                {
                    File.SetAttributes(fullPath, FileAttributes.ReadOnly);
                }
                catch { /* 忽略 */ }
            }
            catch (Exception ex)
            {

                AddLog($"保存CSV文件异常：{ex}");
            }
        }
        // 记录每个电芯的开始时间
        private readonly ConcurrentDictionary<(string station, string barcode), DateTime> _cellStartTimes = new ConcurrentDictionary<(string, string), DateTime>();

        // 记录电芯检测时间到CSV文件
        private void LogCellDetectionTimeToCSV(string station, string barcode, DateTime startTime, DateTime endTime,
                                             string result, string defectType, int queueWaitMs, int processingMs)
        {
            try
            {
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DetectionTimeLogs");
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                // 按天生成日志文件
                string logPath = Path.Combine(logDirectory, $"CellDetectionTime_{DateTime.Today:yyyyMMdd}.csv");

                // 如果文件不存在，写入表头
                if (!File.Exists(logPath))
                {
                    string header = "时间戳,工位,电芯条码,开始时间,结束时间,总耗时(ms),队列等待(ms),处理耗时(ms),检测结果,缺陷类型\n";
                    File.WriteAllText(logPath, header, Encoding.UTF8);
                }

                // 计算总耗时
                TimeSpan totalTime = endTime - startTime;

                // 构建日志行
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{station},{barcode}," +
                                $"{startTime:yyyy-MM-dd HH:mm:ss.fff},{endTime:yyyy-MM-dd HH:mm:ss.fff}," +
                                $"{totalTime.TotalMilliseconds:F0},{queueWaitMs},{processingMs},{result},{defectType}\n";

                // 写入文件
                File.AppendAllText(logPath, logLine, Encoding.UTF8);

                AddLog($"[{station}-{barcode}时间记录] 已保存到CSV，总耗时：{totalTime.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                AddLog($"[CSV记录异常] {ex.Message}");
            }
        }
    }
}
