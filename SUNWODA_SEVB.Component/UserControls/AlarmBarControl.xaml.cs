using SUNWODA_SEVB.Core.Models.Component;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Component.UserControls
{
    /// <summary>
    /// AlarmBarControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlarmBarControl : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(ObservableCollection<AlarmInfo>),
                typeof(AlarmBarControl),
                new PropertyMetadata(null));

        public ObservableCollection<AlarmInfo> ItemsSource
        {
            get => (ObservableCollection<AlarmInfo>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public AlarmBarControl()
        {
            InitializeComponent();
        }
    }
}
