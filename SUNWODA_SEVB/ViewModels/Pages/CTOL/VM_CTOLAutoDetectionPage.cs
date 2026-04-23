using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Models.Project.CTOL;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    [Module("VM_CTOLAutoDetectionPage", "在线CT AI检测系统v2.0")]
    public class VM_CTOLAutoDetectionPage : ViewModelBase
    {
        #region 字段
        private ImageSource? _currentResultImageLAB;
        private ImageSource? _currentResultImageLCD;
        private ImageSource? _currentResultImageRAB;
        private ImageSource? _currentResultImageRCD;

        private List<double>? _detectionSattisticalData;
        private List<string>? _detectionSattisticalLabel;

        private int _totalDetectionNum;
        private int _ngDetectionNum;
        private int _okDetectionNum;
        private double _okRate;
        #endregion

        #region 属性
        /// <summary>
        /// LAB当前结果图
        /// </summary>
        public ImageSource? CurrentResultImageLAB
        {
            get => _currentResultImageLAB;
            set => SetProperty(ref _currentResultImageLAB, value);
        }

        /// <summary>
        /// LCD当前结果图
        /// </summary>
        public ImageSource? CurrentResultImageLCD
        {
            get => _currentResultImageLCD;
            set => SetProperty(ref _currentResultImageLCD, value);
        }

        /// <summary>
        /// RAB当前结果图
        /// </summary>
        public ImageSource? CurrentResultImageRAB
        {
            get => _currentResultImageRAB;
            set => SetProperty(ref _currentResultImageRAB, value);
        }

        /// <summary>
        /// RCD当前结果图
        /// </summary>
        public ImageSource? CurrentResultImageRCD
        {
            get => _currentResultImageRCD;
            set => SetProperty(ref _currentResultImageRCD, value);
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
        /// 缺陷数据统计列表
        /// </summary>
        public ObservableCollection<DefectData>? DefectDataList { get; set; }

        /// <summary>
        /// 总检测数
        /// </summary>
        public int TotalDetectionNum
        {
            get => _totalDetectionNum;
            set => SetProperty(ref _totalDetectionNum, value);
        }

        /// <summary>
        /// NG数
        /// </summary>
        public int NGDetectionNum
        {
            get => _ngDetectionNum;
            set => SetProperty(ref _ngDetectionNum, value);
        }

        /// <summary>
        /// OK数
        /// </summary>
        public int OKDetectionNum
        {
            get => _okDetectionNum;
            set => SetProperty(ref _okDetectionNum, value);
        }

        /// <summary>
        /// 一次优率
        /// </summary>
        public double OKRate
        {
            get => _okRate;
            set => SetProperty(ref _okRate, value);
        }
        #endregion

        public VM_CTOLAutoDetectionPage() { }

        public override void OnInitialize()
        {
            CurrentResultImageLAB = LoadImageFromFile(
                "E:\\IMEResult\\LAB\\2025-12-12\\负极边缘间隙不良\\355125701G66027\\355125701G66027_1-X_result_NG_负极边缘间隙不良.png"
            );
            CurrentResultImageRAB = LoadImageFromFile(
                "E:\\IMEResult\\LAB\\2025-12-12\\负极边缘间隙不良\\355125701G66027\\355125701G66027_1-X_result_NG_负极边缘间隙不良.png"
            );
            CurrentResultImageLCD = LoadImageFromFile(
                "E:\\IMEResult\\LAB\\2025-12-12\\负极边缘间隙不良\\355125701G66027\\355125701G66027_1-X_result_NG_负极边缘间隙不良.png"
            );
            CurrentResultImageRCD = LoadImageFromFile(
                "E:\\IMEResult\\LAB\\2025-12-12\\负极边缘间隙不良\\355125701G66027\\355125701G66027_1-X_result_NG_负极边缘间隙不良.png"
            );
            DetectionSattisticalData = new List<double>() { 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5 };
            DetectionSattisticalLabel = new List<string>()
            {
                "OK",
                "OH不良",
                "正极少片",
                "正极中间间隙不良",
                "正极多片",
                "正极边缘间隙不良",
                "负极多片",
                "负极边缘间隙不良",
                "负极少片",
                "负极差不良",
                "负极压伤",
                "正极Y方向掉片",
            };
            DefectDataList = new ObservableCollection<DefectData>()
            {
                new DefectData
                {
                    DefectType = "一次优率",
                    Count = 23972,
                    Ratio = 0.735,
                },
                new DefectData
                {
                    DefectType = "OH不良",
                    Count = 10,
                    Ratio = 0.0003,
                },
                new DefectData
                {
                    DefectType = "正极少片",
                    Count = 9,
                    Ratio = 0.0003,
                },
                new DefectData
                {
                    DefectType = "正极中间间隙不良",
                    Count = 96,
                    Ratio = 0.0029,
                },
                new DefectData
                {
                    DefectType = "正极多片",
                    Count = 50,
                    Ratio = 0.0015,
                },
                new DefectData
                {
                    DefectType = "正极边缘间隙不良",
                    Count = 1236,
                    Ratio = 0.0379,
                },
                new DefectData
                {
                    DefectType = "负极多片",
                    Count = 115,
                    Ratio = 0.0035,
                },
                new DefectData
                {
                    DefectType = "负极边缘间隙不良",
                    Count = 6220,
                    Ratio = 0.1907,
                },
                new DefectData
                {
                    DefectType = "负极少片",
                    Count = 310,
                    Ratio = 0.0095,
                },
                new DefectData
                {
                    DefectType = "负极差不良",
                    Count = 0,
                    Ratio = 0.0,
                },
                new DefectData
                {
                    DefectType = "负极压伤",
                    Count = 523,
                    Ratio = 0.016,
                },
                new DefectData
                {
                    DefectType = "正极Y方向掉片",
                    Count = 73,
                    Ratio = 0.0022,
                },
            };

            TotalDetectionNum = 10000;
            NGDetectionNum = 100;
            OKDetectionNum = 9900;
            OKRate = 0.99;
            base.OnInitialize();
        }

        protected override async Task OnInitializeAsync()
        {
            
            await base.OnInitializeAsync();
        }

        public static ImageSource LoadImageFromFile(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad; // 读入内存后释放文件占用
            bmp.EndInit();
            bmp.Freeze(); // 跨线程/性能更好
            return bmp;
        }
    }
}
