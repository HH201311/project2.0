using SUNWODA_SEVB.ViewModels.Pages.STC;
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

namespace SUNWODA_SEVB.Views.Pages.STC
{
    /// <summary>
    /// P230ConnectorInstallerPage.xaml 的交互逻辑
    /// </summary>
    public partial class P230ConnectorInstallerPage : Page
    {
        public P230ConnectorInstallerPage()
        {
            InitializeComponent();
            // 订阅容器大小变化事件
            ScrewPositionContainer.SizeChanged += OnContainerSizeChanged;

            // 订阅 Loaded 事件，确保初始尺寸正确
            this.Loaded += OnPageLoaded;
        }
        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // 页面加载完成后，触发一次尺寸更新
            UpdateScrewPositions();
        }

        private void OnContainerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateScrewPositions();
        }

        private void UpdateScrewPositions()
        {
            if (DataContext is VM_P230ConnectorInstallerPage viewModel)
            {
                // 获取图片的实际显示尺寸（考虑 Uniform 拉伸）
                var imageActualWidth = ProcessBackgroundImage.ActualWidth;
                var imageActualHeight = ProcessBackgroundImage.ActualHeight;

                // 如果图片尚未加载，使用容器尺寸
                if (imageActualWidth <= 0 || imageActualHeight <= 0)
                {
                    imageActualWidth = ScrewPositionContainer.ActualWidth;
                    imageActualHeight = ScrewPositionContainer.ActualHeight;
                }

                if (imageActualWidth > 0 && imageActualHeight > 0)
                {
                    viewModel.UpdateContainerSize(imageActualWidth, imageActualHeight);
                }
            }
        }
    }
}
