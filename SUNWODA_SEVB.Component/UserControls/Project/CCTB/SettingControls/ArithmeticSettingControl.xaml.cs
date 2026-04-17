using SUNWODA_SEVB.Component.UserControls.Project.CCTB.ChartControls;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.SettingControls
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
        }
        #endregion

        private string arithmeticNameTemp = string.Empty;

        private void EditBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn)) return;
            arithmeticNameTemp = ArithmeticName;
            this.ArithmeticName_TB.IsReadOnly = false;
            this.ArithmeticName_TB.Focus();
            this.ArithmeticName_TB.SelectionStart = 0;
            btn.Visibility = Visibility.Collapsed;
            this.Edit_Bd1.BorderThickness = new Thickness(1);
            this.Edit_Bd2.Visibility = Visibility.Visible;
        }

        private void ArithmeticTB_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.ArithmeticName_TB.Text = arithmeticNameTemp;
                ArithmeticNameEditEnd();
                return;
            }
            if (e.Key != Key.Enter) return;
            try
            {
                ArithmeticNameVerify();
                arithmeticNameTemp = this.ArithmeticName;
            }
            catch (Exception ex)
            {
                this.ArithmeticName = arithmeticNameTemp;
                throw new Exception(ex.Message);
            }
            finally { ArithmeticNameEditEnd(); }
        }

        private void ArithmeticTB_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (tb.IsFocused) return;

            this.ArithmeticName = arithmeticNameTemp;
            ArithmeticNameEditEnd();
        }

        private void ArithmeticNameVerify()
        {
            var arithmeticName = this.ArithmeticName_TB.Text;

            if (arithmeticName == arithmeticNameTemp) return;

            if (ArithmeticVerifyCommand == null)
                throw new Exception($"{nameof(ArithmeticVerifyCommand)} 参数未赋值！");

            var arg = new object[] { SideType, ClosedLoopType, arithmeticName, arithmeticNameTemp };
            ArithmeticVerifyCommand.Execute(arg);
        }

        private void ArithmeticNameEditEnd()
        {
            this.Edit_Btn.Visibility = Visibility.Visible;
            this.Edit_Bd1.BorderThickness = new Thickness(0);
            this.Edit_Bd2.Visibility = Visibility.Collapsed;
            this.ArithmeticName_TB.IsReadOnly = true;
            Keyboard.ClearFocus();
        }
    }
}
