using SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls;
using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    /// ArithmeticSettingControl.xaml 的交互逻辑
    /// </summary>
    public partial class ArithmeticSettingControl : UserControl
    {
        public ArithmeticSettingControl()
        {
            InitializeComponent();
        }

        #region SideType
        public ESideType SideType
        {
            get { return (ESideType)GetValue(SideTypeProperty); }
            set { SetValue(SideTypeProperty, value); }
        }

        public static readonly DependencyProperty SideTypeProperty = DependencyProperty.Register(
            "SideType",
            typeof(ESideType),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(ESideType.A, new PropertyChangedCallback(OnSideTypeChanged))
        );

        private static void OnSideTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {

            }
        }
        #endregion

        #region ClosedLoopType
        public EClosedLoopType ClosedLoopType
        {
            get { return (EClosedLoopType)GetValue(ClosedLoopTypeProperty); }
            set { SetValue(ClosedLoopTypeProperty, value); }
        }

        public static readonly DependencyProperty ClosedLoopTypeProperty = DependencyProperty.Register(
            "ClosedLoopType",
            typeof(EClosedLoopType),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(EClosedLoopType.MD, new PropertyChangedCallback(OnClosedLoopTypeChanged))
        );

        private static void OnClosedLoopTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {

            }
        }
        #endregion

        #region Header
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
            "Header",
            typeof(string),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(string.Empty, new PropertyChangedCallback(OnHeaderChanged))
        );

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {
                uc.GB.Header = uc.Header;
            }
        }
        #endregion

        #region ArithmeticName
        public string ArithmeticName
        {
            get { return (string)GetValue(ArithmeticNameProperty); }
            set { SetValue(ArithmeticNameProperty, value); }
        }

        public static readonly DependencyProperty ArithmeticNameProperty = DependencyProperty.Register(
            "ArithmeticName",
            typeof(string),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata("-------------", new PropertyChangedCallback(OnArithmeticNameChanged))
        );

        private static void OnArithmeticNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {
                uc.ArithmeticTB.Text = e.NewValue.ToString();
            }
        }
        #endregion

        #region IsArithmeticEnabled
        public bool IsArithmeticEnabled
        {
            get { return (bool)GetValue(IsArithmeticEnabledProperty); }
            set { SetValue(IsArithmeticEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsArithmeticEnabledProperty = DependencyProperty.Register(
            "IsArithmeticEnabled",
            typeof(bool),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(false, new PropertyChangedCallback(OnIsArithmeticEnabledChanged))
        );

        private static void OnIsArithmeticEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {
                uc.ArithmeticEnable_TB.IsChecked = (bool)e.NewValue;
                uc.EnabledGrid.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        #endregion

        #region ParamSettingClickCommand
        public ICommand ParamSettingClickCommand
        {
            get { return (ICommand)GetValue(ParamSettingClickCommandProperty); }
            set { SetValue(ParamSettingClickCommandProperty, value); }
        }

        public static readonly DependencyProperty ParamSettingClickCommandProperty = DependencyProperty.Register(
            "ParamSettingClickCommand",
            typeof(ICommand),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParamSettingClickCommandChanged))
        );

        private static void OnParamSettingClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {
                uc.ParamSetting_Btn.Command = uc.ParamSettingClickCommand;
                uc.ParamSetting_Btn.CommandParameter = uc;
            }
        }
        #endregion

        #region ArithmeticEnableChangedCommand
        public ICommand ArithmeticEnableChangedCommand
        {
            get { return (ICommand)GetValue(ArithmeticEnableChangedCommandProperty); }
            set { SetValue(ArithmeticEnableChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty ArithmeticEnableChangedCommandProperty = DependencyProperty.Register(
            "ArithmeticEnableChangedCommand",
            typeof(ICommand),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnArithmeticEnableChangedCommandChanged))
        );

        private static void OnArithmeticEnableChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {

            }
        }
        #endregion

        #region ArithmeticVerifyCommand
        public ICommand ArithmeticVerifyCommand
        {
            get { return (ICommand)GetValue(ArithmeticVerifyCommandProperty); }
            set { SetValue(ArithmeticVerifyCommandProperty, value); }
        }

        public static readonly DependencyProperty ArithmeticVerifyCommandProperty = DependencyProperty.Register(
            "ArithmeticVerifyCommand",
            typeof(ICommand),
            typeof(ArithmeticSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnArithmeticVerifyCommandChanged))
        );

        private static void OnArithmeticVerifyCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as ArithmeticSettingControl;
            if (uc != null)
            {

            }
        }
        #endregion

        private string arithmeticNameTemp = string.Empty;
        private void RefreshArithmeticName(string arithmeticName)
        {
            arithmeticNameTemp = arithmeticName;
        }

        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            arithmeticNameTemp = ArithmeticName;
            this.ArithmeticTB.IsReadOnly = false;
            this.ArithmeticTB.Focus();
            this.ArithmeticTB.SelectionStart = 0;
            btn.Visibility = Visibility.Collapsed;
            this.Edit_Bd1.BorderThickness = new Thickness(1);
            this.Edit_Bd2.Visibility = Visibility.Visible;
        }

        private void ArithmeticTB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ArithmeticNameEditEnd();
                return;
            }
            if (e.Key != Key.Enter) return;
            try { ArithmeticNameVerify(); }
            catch (Exception ex) { throw new Exception(ex.Message); }
            finally { ArithmeticNameEditEnd(); }
        }

        private void ArithmeticTB_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (tb.IsFocused) return;
            ArithmeticNameEditEnd();
        }

        private void ArithmeticNameVerify()
        {
            var arithmeticName = this.ArithmeticTB.Text;

            if (arithmeticName == arithmeticNameTemp) return;

            if (ArithmeticVerifyCommand == null)
                throw new Exception($"{nameof(ArithmeticVerifyCommand)} 参数未赋值！");

            var arg = new object[] { SideType, ClosedLoopType, arithmeticName, (Action<string>)RefreshArithmeticName };
            ArithmeticVerifyCommand.Execute(arg);
        }

        private void ArithmeticNameEditEnd()
        {
            this.Edit_Btn.Visibility = Visibility.Visible;
            this.Edit_Bd1.BorderThickness = new Thickness(0);
            this.Edit_Bd2.Visibility = Visibility.Collapsed;
            this.ArithmeticTB.IsReadOnly = true;
            this.ArithmeticName = arithmeticNameTemp;
            this.ArithmeticTB.Text = arithmeticNameTemp;
            Keyboard.ClearFocus();
        }

        private void RefreshArithmeticEnableState(bool isEnable)
        {
            this.IsArithmeticEnabled = isEnable;
            this.EnabledGrid.Visibility = isEnable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ArithmeticEnableTB_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton tb)) return;
            var isCheck = tb.IsChecked ?? false;
            if (ArithmeticEnableChangedCommand == null)
            {
                tb.IsChecked = !isCheck;
                throw new Exception($"{nameof(ArithmeticEnableChangedCommand)} 参数未赋值！");
            }
            var arg = new object[] { SideType, ClosedLoopType, isCheck, (Action<bool>)RefreshArithmeticEnableState };
            ArithmeticEnableChangedCommand.Execute(arg);
        }
    }
}
