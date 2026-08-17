using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Models.Project.JRRY;

namespace SUNWODA_SEVB.ViewModels.Pages.JRRY
{
    /// <summary>
    /// 尺寸标注窗口，管理标注点、线段和绘制模式
    /// </summary>
    public class AnnotationWindowViewModel : ViewModelBase
    {
        #region 字段

        private bool _isDrawingMode;
        private ObservableCollection<Point> _annotationPoints = [];
        private ImageSource? _annotationImage;
        private string _annotationCursor = "Arrow";
        private ObservableCollection<PolylineData> _completedSegments = [];

        #endregion

        #region 属性

        /// <summary>
        /// 是否处于绘制模式
        /// </summary>
        public bool IsDrawingMode
        {
            get => _isDrawingMode;
            set
            {
                AnnotationCursor = value ? "Cross" : "Arrow";
                SetProperty(ref _isDrawingMode, value);
            }
        }

        /// <summary>
        /// 标注点集合
        /// </summary>
        public ObservableCollection<Point> AnnotationPoints
        {
            get => _annotationPoints;
            set => SetProperty(ref _annotationPoints, value);
        }

        /// <summary>
        /// 标注图片
        /// </summary>
        public ImageSource? AnnotationImage
        {
            get => _annotationImage;
            set => SetProperty(ref _annotationImage, value);
        }

        /// <summary>
        /// 标注光标类型，"Arrow" 或 "Cross"
        /// </summary>
        public string AnnotationCursor
        {
            get => _annotationCursor;
            set => SetProperty(ref _annotationCursor, value);
        }

        /// <summary>
        /// 已完成的线段集合，每个线段包含点集合和长度
        /// </summary>
        public ObservableCollection<PolylineData> CompletedSegments
        {
            get => _completedSegments;
            set => SetProperty(ref _completedSegments, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 开始标注命令，进入绘制模式
        /// </summary>
        public ICommand StartAnnotationCommand { get; }

        /// <summary>
        /// 结束标注命令，退出绘制模式并保存当前线段
        /// </summary>
        public ICommand EndAnnotationCommand { get; }

        /// <summary>
        /// 清空标注命令，清空所有标注点和已完成线段
        /// </summary>
        public ICommand ClearAnnotationCommand { get; }

        /// <summary>
        /// 关闭窗口命令，触发请求关闭事件
        /// </summary>
        public ICommand CloseCommand { get; }

        #endregion

        /// <summary>
        /// 构造函数，初始化命令
        /// </summary>
        public AnnotationWindowViewModel()
        {
            StartAnnotationCommand = new RelayCommand(StartAnnotation);
            EndAnnotationCommand = new RelayCommand(EndAnnotation);
            ClearAnnotationCommand = new RelayCommand(ClearAnnotation);
            CloseCommand = new RelayCommand(CloseWindow);
        }

        /// <summary>
        /// 设置标注图片
        /// </summary>
        /// <param name="imageSource"></param>
        public void SetImage(ImageSource imageSource) => AnnotationImage = imageSource;

        /// <summary>
        /// 添加标注点，如果当前处于绘制模式，则将点加入当前标注点集合
        /// </summary>
        /// <param name="point"></param>
        public void AddAnnotationPoint(Point point)
        {
            if (!IsDrawingMode) return;
            AnnotationPoints.Add(point);
        }

        /// <summary>
        /// 开始标注，如果当前处于绘制模式，则先结束当前线段并保存，然后清空当前点集合，进入新的绘制模式
        /// </summary>
        private void StartAnnotation()
        {
            // 如果当前处于绘制模式，则先完成当前线段并保存
            if (IsDrawingMode)
            {
                EndCurrentSegmentAndSave();
            }
            // 开始新的绘制
            AnnotationPoints.Clear();
            IsDrawingMode = true;
        }

        /// <summary>
        /// 结束标注，如果当前处于绘制模式，则结束当前线段并保存，退出绘制模式
        /// </summary>
        private void EndAnnotation()
        {
            if (IsDrawingMode)
            {
                EndCurrentSegmentAndSave();
                IsDrawingMode = false;
            }
        }

        /// <summary>
        /// 清空所有标注点和已完成线段，退出绘制模式
        /// </summary>
        private void ClearAnnotation()
        {
            // 强制退出绘制模式并清空所有
            IsDrawingMode = false;
            AnnotationPoints.Clear();
            CompletedSegments.Clear();
            OnPropertyChanged(nameof(CompletedSegments));
        }

        /// <summary>
        /// 内部：结束当前线段，存入历史记录
        /// </summary>
        private void EndCurrentSegmentAndSave()
        {
            if (AnnotationPoints.Count < 2) return; // 少于2个点不保存

            var points = AnnotationPoints.ToList();
            double length = CalculatePolylineLength(points);
            var segment = new PolylineData { Points = points, Length = length * 0.04 };
            CompletedSegments.Add(segment);

            AnnotationPoints.Clear();
            OnPropertyChanged(nameof(CompletedSegments));
        }

        /// <summary>
        /// 计算折线长度，返回保留两位小数的长度值
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        private double CalculatePolylineLength(List<Point> points)
        {
            double len = 0;
            for (int i = 1; i < points.Count; i++)
                len += (points[i] - points[i - 1]).Length;
            return Math.Round(len, 2);
        }

        /// <summary>
        /// 关闭窗口请求事件触发方法
        /// </summary>
        private void CloseWindow()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 请求关闭窗口的事件，外部可以订阅此事件来处理窗口关闭逻辑
        /// </summary>
        public event EventHandler? RequestClose;
    }
}