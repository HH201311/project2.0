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
    /// DataCellTableControl.xaml 的交互逻辑
    /// </summary>
    public partial class DataCellTableControl : UserControl
    {


        public ObservableCollection<ColumnHeader> Columns
        {
            get { return (ObservableCollection<ColumnHeader>)GetValue(ColumnsProperty); }
            set { SetValue(ColumnsProperty, value); }
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register("Columns", typeof(ObservableCollection<ColumnHeader>), typeof(DataCellTableControl), new PropertyMetadata(null));



        public ObservableCollection<RowData> Rows
        {
            get { return (ObservableCollection<RowData>)GetValue(RowsProperty); }
            set { SetValue(RowsProperty, value); }
        }

        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register("Rows", typeof(ObservableCollection<RowData>), typeof(DataCellTableControl), new PropertyMetadata(null));

        public DataCellTableControl()
        {
            InitializeComponent();
        }
    }
}
