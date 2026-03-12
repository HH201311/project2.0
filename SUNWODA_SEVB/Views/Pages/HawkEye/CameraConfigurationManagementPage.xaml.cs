using System;
using System.Collections.Generic;
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

namespace SUNWODA_SEVB.Views.Pages.HawkEye
{
    /// <summary>
    /// CameraConfigurationManagementPage.xaml 的交互逻辑
    /// </summary>
    public partial class CameraConfigurationManagementPage : Page
    {
        public CameraConfigurationManagementPage()
        {
            InitializeComponent();
        }

        private bool _dragging;
        private Point _lastPoint;

        // 确保 Image 有可用的 Scale + Translate
        private void EnsureTransforms(Image img)
        {
            if (img.RenderTransform is not TransformGroup group || group.Children.Count < 2)
            {
                var newGroup = new TransformGroup();
                newGroup.Children.Add(new ScaleTransform(1, 1));      // ScaleTransform
                newGroup.Children.Add(new TranslateTransform(0, 0));  // TranslateTransform
                img.RenderTransform = newGroup;
            }
        }

        // 获取 ScaleTransform 和 TranslateTransform
        private (ScaleTransform scale, TranslateTransform translate) GetTransforms(Image img)
        {
            EnsureTransforms(img);

            var group = img.RenderTransform as TransformGroup;

            var scale = group.Children[0] as ScaleTransform;
            var translate = group.Children[1] as TranslateTransform;

            return (scale, translate);
        }

        // 鼠标滚轮缩放
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (sv.Content is not Image img) return;

            var (scale, _) = GetTransforms(img);

            double zoom = e.Delta > 0 ? 1.1 : 0.9;
            scale.ScaleX *= zoom;
            scale.ScaleY *= zoom;
        }

        // 左键拖动
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (sv.Content is not Image img) return;

            _dragging = true;
            _lastPoint = e.GetPosition(sv);
            img.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            if (sender is not ScrollViewer sv) return;
            if (sv.Content is not Image img) return;

            var (_, translate) = GetTransforms(img);

            Point current = e.GetPosition(sv);
            translate.X += current.X - _lastPoint.X;
            translate.Y += current.Y - _lastPoint.Y;

            _lastPoint = current;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (sv.Content is not Image img) return;

            _dragging = false;
            img.ReleaseMouseCapture();
        }


    }
}
