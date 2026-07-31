
using System.Windows;
using System.Windows.Media;
using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.Project.JRRY
{

    public class DetectionResult
    {
        public string Barcode { get; set; } = "";
        public bool IsOK { get; set; }
        public List<string> DefectTypes { get; set; } = [];
        public List<DetectionItem> GapResults { get; set; } = [];
        public List<DetectionItem> LeftForkResults { get; set; } = [];
        public List<DetectionItem> RightForkResults { get; set; } = [];
    }

    /// <summary>
    /// 检测项模型
    /// </summary>
    public class DetectionItem
    {        
        public string? Name { get; set; }
        public double Value { get; set; }
        public Brush? ValueColor { get; set; }
        // 该测量项是否合格
        public bool IsOK { get; set; } = true;   
    }

    /// <summary>
    /// 缺陷数据统计模型
    /// </summary>
    public class DefectDataStatisticsModel :  ModelBase
    {
        private string? defectType;
        private int count;
        private double ratio;

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string? DefectType 
        {           
            get => defectType; 
            set => SetProperty(ref defectType, value);
        }

        /// <summary>
        /// 缺陷数量
        /// </summary>
        public int Count 
        { 
            get => count; 
            set => SetProperty(ref count, value);
        }

        /// <summary>
        /// 缺陷占比
        /// </summary>
        public double Ratio 
        { 
            get => ratio;
            set => SetProperty(ref ratio, value); 
        }        
    }

    /// <summary>
    /// 测试结果模型
    /// </summary>
    public class TestResultModel
    {
        /// <summary>
        /// 电芯条码
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string? DefectType { get; set; }

        /// <summary>
        /// 检测结果
        /// </summary>
        public string? DetectionResult { get; set; }
    }

    /// <summary>
    /// 检测记录模型
    /// </summary>
    public class InspectionRecord
    {
        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// 电芯条码
        /// </summary>
        public string? Barcode { get; set; }

        /// <summary>
        /// 缺陷类型
        /// </summary>
        public string? DefectType { get; set; }
    }

    /// <summary>
    /// 参数项规格模型
    /// </summary>
    public class ParameterItem : ModelBase
    {
        private string parameterName = string.Empty;
        private double upperLimit;
        private double lowerLimit;

        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName 
        { 
            get => parameterName; 
            set => SetProperty(ref parameterName, value);
        }

        /// <summary>
        /// 阈值上限
        /// </summary>
        public double UpperLimit 
        { 
            get => upperLimit; 
            set => SetProperty(ref upperLimit, value); 
        }

        /// <summary>
        /// 阈值下限
        /// </summary>
        public double LowerLimit 
        { 
            get => lowerLimit; 
            set => SetProperty(ref lowerLimit, value); 
        }        
    }

    /// <summary>
    /// 折线数据模型
    /// </summary>
    public class PolylineData
    {
        /// <summary>
        /// 折线的点集合
        /// </summary>
        public List<Point> Points { get; set; } = [];

        /// <summary>
        /// 折线的长度
        /// </summary>
        public double Length { get; set; }
    }   
}