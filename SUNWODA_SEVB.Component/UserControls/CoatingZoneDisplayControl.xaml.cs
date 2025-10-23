using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SUNWODA_SEVB.Core.Models.Component;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// CoatingZoneDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class CoatingZoneDisplayControl : UserControl
    {
        public ObservableCollection<CoatingSegmentModel>? Segments { get; set; }

        public ObservableCollection<CoatingSegmentModel> RawSegments
        {
            get { return (ObservableCollection<CoatingSegmentModel>)GetValue(RawSegmentsProperty); }
            set { SetValue(RawSegmentsProperty, value); }
        }

        public static readonly DependencyProperty RawSegmentsProperty = DependencyProperty.Register(
            "RawSegments",
            typeof(ObservableCollection<CoatingSegmentModel>),
            typeof(CoatingZoneDisplayControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnRawSegmentsChanged))
        );

        public string FaceALabel
        {
            get { return (string)GetValue(FaceALabelProperty); }
            set { SetValue(FaceALabelProperty, value); }
        }

        public static readonly DependencyProperty FaceALabelProperty =
            DependencyProperty.Register("FaceALabel", typeof(string), typeof(CoatingZoneDisplayControl), new PropertyMetadata("A面", new PropertyChangedCallback(OnFaceALabelChanged)));

        public string FaceBlabel
        {
            get { return (string)GetValue(FaceBlabelProperty); }
            set { SetValue(FaceBlabelProperty, value); }
        }

        public static readonly DependencyProperty FaceBlabelProperty =
            DependencyProperty.Register("FaceBlabel", typeof(string), typeof(CoatingZoneDisplayControl), new PropertyMetadata("B面", new PropertyChangedCallback(OnFaceBLabelChanged)));

        public CoatingZoneDisplayControl()
        {
            InitializeComponent();
            FaceALabelContainer.Text = FaceALabel;
            FaceBLabelContainer.Text = FaceBlabel;
        }

        private static void OnRawSegmentsChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e
        )
        {
            var control = (CoatingZoneDisplayControl)d;
            if (control != null)
            {
                control.SegmentContainer.ItemsSource = SegmentLayoutHelper.LayoutSegments(
                    (ObservableCollection<CoatingSegmentModel>)e.NewValue
                );
                var totalWidth = 0.0;
                foreach (var item in (ObservableCollection<CoatingSegmentModel>)e.NewValue)
                {
                    totalWidth += item.CanvasWidth;
                }
                control.CanvasContainer.Width = totalWidth + 20;
            }
        }

        private static void OnFaceALabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CoatingZoneDisplayControl)d;
            if (control != null)
            {
                control.FaceALabelContainer.Text = (string)e.NewValue;
            }
        }

        private static void OnFaceBLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CoatingZoneDisplayControl)d;
            if (control != null)
            {
                control.FaceBLabelContainer.Text = (string)e.NewValue;
            }
        }
    }

    public static class SegmentLayoutHelper
    {
        /// <summary>
        /// 自动计算 StartX，并返回排好位置的段集合
        /// </summary>
        public static ObservableCollection<CoatingSegmentModel> LayoutSegments(
            ObservableCollection<CoatingSegmentModel> rawSegments,
            double startX = 20
        )
        {
            var result = new ObservableCollection<CoatingSegmentModel>();
            double cursor = startX;

            foreach (var seg in rawSegments)
            {
                seg.StartX = cursor;
                cursor += seg.CanvasWidth;
                result.Add(seg);
            }

            return result;
        }
    }
}
