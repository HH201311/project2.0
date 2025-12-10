using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Component;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Models.Component
{
    public class CoatingSegmentModel : ModelBase
    {
        public string Name { get; set; }
        public CoatingSegmentType Type { get; set; }

        /// <summary>
        /// 画线宽度
        /// </summary>
        public double CanvasWidth
        {
            get {
                return Type switch
                {
                    CoatingSegmentType.EdgeTab => 40,
                    CoatingSegmentType.MiddleTab => 80,
                    CoatingSegmentType.CIL => 20,
                    CoatingSegmentType.Coating => 300,
                    _ => 0,
                };
            }
        }
        public Brush Color { get; set; } = Brushes.Black;

        // 坐标（由布局器计算）
        public double StartX { get; set; }
        public double EndX => StartX + CanvasWidth;

        public double LabelStartX => StartX + CanvasWidth/2;
        // 上下 Label
        private double widthA;
        public double WidthA
        {
            get => widthA;
            set => SetProperty(ref widthA, value);
        }

        private double widthB;
        public double WidthB
        {
            get => widthB;
            set => SetProperty(ref widthB, value);
        }

        private Brush foregroundA = new SolidColorBrush(Colors.Black);

        public Brush ForegroundA
        {
            get => foregroundA;
            set => SetProperty(ref foregroundA, value);
        }

        private Brush foregroundB = new SolidColorBrush(Colors.Black);

        public Brush ForegroundB
        {
            get => foregroundB;
            set => SetProperty(ref foregroundB, value);
        }

        public double StrokeThickness
        {
            get
            {
                return Type switch
                {
                    CoatingSegmentType.EdgeTab => 2.0,
                    CoatingSegmentType.MiddleTab => 2.0,
                    CoatingSegmentType.CIL => 4.0,
                    CoatingSegmentType.Coating => 4.0,
                    _ => 0,
                };
            }
        }

        public CoatingSegmentModel(string name, CoatingSegmentType type, Brush color)
        {
            Name = name;
            Type = type;
            Color = color;
        }
    }
}
