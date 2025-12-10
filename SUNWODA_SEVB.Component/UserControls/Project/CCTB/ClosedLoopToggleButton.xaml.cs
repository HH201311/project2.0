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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB
{
    /// <summary>
    /// ClosedLoopToggleButton.xaml 的交互逻辑
    /// </summary>
    public partial class ClosedLoopToggleButton : UserControl
    {
        public ClosedLoopToggleButton()
        {
            InitializeComponent();
        }

        #region Text
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(ClosedLoopToggleButton),
            new PropertyMetadata("", new PropertyChangedCallback(OnTextChanged))
        );

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ClosedLoopToggleButton;
            if (uc != null)
            {
                uc.ToggleButtonTB.Text = uc.Text;
            }
        }
        #endregion

        #region IsChecked
        public bool? IsChecked
        {
            get { return (bool?)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
            "IsChecked",
            typeof(bool?),
            typeof(ClosedLoopToggleButton),
            new PropertyMetadata(false, new PropertyChangedCallback(OnIsCheckedChanged))
        );

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ClosedLoopToggleButton;
            if (uc != null)
            {
                uc.ToggleButton.IsChecked = uc.IsChecked;
            }
        }
        #endregion

        #region Command
        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
            "Command",
            typeof(ICommand),
            typeof(ClosedLoopToggleButton),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCommandChanged))
        );

        private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ClosedLoopToggleButton;
            if (uc != null)
            {
                uc.ToggleButton.Command = uc.Command;
            }
        }
        #endregion

        #region CommandParameter
        public object CommandParameter
        {
            get { return (object)GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
            "CommandParameter",
            typeof(object),
            typeof(ClosedLoopToggleButton),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCommandParameterChanged))
        );

        private static void OnCommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ClosedLoopToggleButton;
            if (uc != null)
            {
                uc.ToggleButton.CommandParameter = uc.CommandParameter;
            }
        }
        #endregion
    }
}
