using SUNWODA_SEVB.Core.Models.Component;
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

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// DataCellRowControl.xaml 的交互逻辑
    /// </summary>
    public partial class DataCellRowControl : UserControl
    {


        public ObservableCollection<CellData> Cells
        {
            get { return (ObservableCollection<CellData>)GetValue(CellsProperty); }
            set { SetValue(CellsProperty, value); }
        }

        public static readonly DependencyProperty CellsProperty =
            DependencyProperty.Register("Cells", typeof(ObservableCollection<CellData>), typeof(DataCellRowControl), new PropertyMetadata(null));



        public DataCellRowControl()
        {
            InitializeComponent();
        }
    }
}
