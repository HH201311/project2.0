using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
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
using HandyControl.Controls;

namespace SUNWODA_SEVB.Views.Pages.CTOL
{
    /// <summary>
    /// CTOLAutoDetectionPage.xaml 的交互逻辑
    /// </summary>
    public partial class CTOLAutoDetectionPage : Page
    {
        public CTOLAutoDetectionPage()
        {
            InitializeComponent();
        }

        private void StatusInfo_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double total = defectListView.ActualWidth;
            if (total <= 0)
                return;

            colType.Width = total * 0.4;
            colCount.Width = total * 0.22;
            colRatio.Width = total * 0.22;

            var spacing =
                (
                    StatusInfo.ActualWidth
                    - clearListBtn.ActualWidth
                    - clearStatisticsBtn.ActualWidth
                ) / 3;
            if (spacing <= 0)
                spacing = 10;
            uniformSpacingPanel.Spacing = spacing;
        }

        private void ParameterSetPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double total = ParameterSetPanel.ActualWidth;
            if (total <= 0)
                return;

            var spacing =
                (
                    ParameterSetPanel.ActualWidth
                    - saveParameterBtn.ActualWidth
                    - resetParameterBtn.ActualWidth
                ) / 3;
            if (spacing <= 0)
                spacing = 10;
            saveAndresetBtnPanel.Spacing = spacing;
        }
    }
}
