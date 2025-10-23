using SUNWODA_SEVB.Core.Models.Component;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// DataTableControl.xaml 的交互逻辑
    /// </summary>
    public partial class DataTableControl : UserControl
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(ObservableCollection<ColumnHeader>),
                typeof(DataTableControl), new PropertyMetadata(null, OnColumnsChanged));

        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<RowData>),
                typeof(DataTableControl), new PropertyMetadata(null, OnRowsChanged));

        public ObservableCollection<ColumnHeader> Columns
        {
            get => (ObservableCollection<ColumnHeader>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public ObservableCollection<RowData> Rows
        {
            get => (ObservableCollection<RowData>)GetValue(RowsProperty);
            set => SetValue(RowsProperty, value);
        }

        public DataTableControl()
        {
            InitializeComponent();
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataTableControl control)
            {
                control.RebuildTable();
            }
        }

        private static void OnRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataTableControl control)
            {
                control.RebuildTable();
            }
        }

        private void RebuildTable()
        {
            TableGrid.Children.Clear();
            TableGrid.RowDefinitions.Clear();
            TableGrid.ColumnDefinitions.Clear();

            if (Columns == null || Columns.Count == 0) return;

            // 创建列定义（时间列 + 动态列）
            TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < Columns.Count; i++)
            {
                TableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 创建行定义（标头 + 最多3行数据）
            TableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            int maxRows = Rows != null ? Math.Min(Rows.Count, 3) : 0;
            for (int i = 0; i < maxRows; i++)
            {
                TableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            // 添加标头
            AddHeader("时间", 0, 0);
            for (int i = 0; i < Columns.Count; i++)
            {
                AddHeader(Columns[i].Name, 0, i + 1);
            }

            // 添加数据行
            if (Rows != null)
            {
                for (int i = 0; i < maxRows; i++)
                {
                    AddDataRow(Rows[i], i + 1);
                }
            }
        }

        private void AddHeader(string text, int row, int col)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold,
                FontSize = FontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            TableGrid.Children.Add(border);
        }

        private void AddDataRow(RowData rowData, int row)
        {
            // 添加时间列
            AddCell(rowData.Time, Brushes.White, Brushes.Black, row, 0);

            // 添加数据列
            for (int i = 0; i < rowData.Cells.Count && i < Columns.Count; i++)
            {
                var cell = rowData.Cells[i];
                AddCell(cell.Value, cell.Background, cell.Foreground, row, i + 1);
            }
        }

        private void AddCell(string text, Brush background, Brush foreground, int row, int col)
        {
            var border = new Border
            {
                Background = background,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = FontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = textBlock;
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            TableGrid.Children.Add(border);
        }
    }
}
