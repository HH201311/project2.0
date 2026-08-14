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
    /// AlgorithmSettingControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmSettingControl : UserControl
    {
        public AlgorithmSettingControl()
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
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(ESideType.A, new PropertyChangedCallback(OnSideTypeChanged))
        );

        private static void OnSideTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
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
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(EClosedLoopType.MD, new PropertyChangedCallback(OnClosedLoopTypeChanged))
        );

        private static void OnClosedLoopTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
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
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(string.Empty, new PropertyChangedCallback(OnHeaderChanged))
        );

        private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region AlgorithmName
        public string AlgorithmName
        {
            get { return (string)GetValue(AlgorithmNameProperty); }
            set { SetValue(AlgorithmNameProperty, value); }
        }

        public static readonly DependencyProperty AlgorithmNameProperty = DependencyProperty.Register(
            "AlgorithmName",
            typeof(string),
            typeof(AlgorithmSettingControl),
            new PropertyMetadata("-------------", new PropertyChangedCallback(OnAlgorithmNameChanged))
        );

        private static void OnAlgorithmNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region AlgorithmName1
        public string AlgorithmName1
        {
            get { return (string)GetValue(AlgorithmName1Property); }
            set { SetValue(AlgorithmName1Property, value); }
        }

        public static readonly DependencyProperty AlgorithmName1Property = DependencyProperty.Register(
            "AlgorithmName1",
            typeof(string),
            typeof(AlgorithmSettingControl),
            new PropertyMetadata("-------------", new PropertyChangedCallback(OnAlgorithmName1Changed))
        );

        private static void OnAlgorithmName1Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region IsAlgorithmEnabled
        public bool IsAlgorithmEnabled
        {
            get { return (bool)GetValue(IsAlgorithmEnabledProperty); }
            set { SetValue(IsAlgorithmEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsAlgorithmEnabledProperty = DependencyProperty.Register(
            "IsAlgorithmEnabled",
            typeof(bool),
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(false, new PropertyChangedCallback(OnIsAlgorithmEnabledChanged))
        );

        private static void OnIsAlgorithmEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
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
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnParamSettingClickCommandChanged))
        );

        private static void OnParamSettingClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region AlgorithmEnableChangedCommand
        public ICommand AlgorithmEnableChangedCommand
        {
            get { return (ICommand)GetValue(AlgorithmEnableChangedCommandProperty); }
            set { SetValue(AlgorithmEnableChangedCommandProperty, value); }
        }

        public static readonly DependencyProperty AlgorithmEnableChangedCommandProperty = DependencyProperty.Register(
            "AlgorithmEnableChangedCommand",
            typeof(ICommand),
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnAlgorithmEnableChangedCommandChanged))
        );

        private static void OnAlgorithmEnableChangedCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region AlgorithmVerifyCommand
        public ICommand AlgorithmVerifyCommand
        {
            get { return (ICommand)GetValue(AlgorithmVerifyCommandProperty); }
            set { SetValue(AlgorithmVerifyCommandProperty, value); }
        }

        public static readonly DependencyProperty AlgorithmVerifyCommandProperty = DependencyProperty.Register(
            "AlgorithmVerifyCommand",
            typeof(ICommand),
            typeof(AlgorithmSettingControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnAlgorithmVerifyCommandChanged))
        );

        private static void OnAlgorithmVerifyCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as AlgorithmSettingControl;
        }
        #endregion

        #region 算法1流程
        private string algorithmNameTemp = string.Empty;

        private void AlgorithmTB_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox tb)) return;

            if (e.Key == Key.Escape)
            {
                tb.Text = algorithmNameTemp;
                tb.IsReadOnly = true;
                Keyboard.ClearFocus();
                return;
            }
            if (e.Key != Key.Enter) return;
            var callback = new Action<bool>((isSuccess) => { if (!isSuccess) tb.Text = algorithmNameTemp; });
            try
            {
                var arg = new object[] { SideType, ClosedLoopType, tb.Text, algorithmNameTemp, 0, callback };
                AlgorithmVerifyCommand?.Execute(arg);
            }
            catch
            {
                tb.Text = algorithmNameTemp;
            }
            finally
            {
                tb.IsReadOnly = true;
                Keyboard.ClearFocus();
            }
        }

        private void AlgorithmTB_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (tb.IsReadOnly) return;
            if (tb.IsFocused) return;

            tb.Text = algorithmNameTemp;
            tb.IsReadOnly = true;
            Keyboard.ClearFocus();
        }

        private void AlgorithmNameTB_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            algorithmNameTemp = AlgorithmName;
            tb.IsReadOnly = false;
            tb.Focus();
        }
        #endregion

        #region 算法2流程
        private string algorithmName1Temp = string.Empty;

        private void AlgorithmTB1_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox tb)) return;

            if (e.Key == Key.Escape)
            {
                tb.Text = algorithmName1Temp;
                tb.IsReadOnly = true;
                Keyboard.ClearFocus();
                return;
            }
            if (e.Key != Key.Enter) return;
            var callback = new Action<bool>((isSuccess) => { if (!isSuccess) tb.Text = algorithmName1Temp; });
            try
            {
                var arg = new object[] { SideType, ClosedLoopType, tb.Text, algorithmName1Temp, 1, callback };
                AlgorithmVerifyCommand?.Execute(arg);
            }
            catch
            {
                tb.Text = algorithmName1Temp;
            }
            finally
            {
                tb.IsReadOnly = true;
                Keyboard.ClearFocus();
            }
        }

        private void AlgorithmTB1_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (tb.IsReadOnly) return;
            if (tb.IsFocused) return;

            tb.Text = algorithmName1Temp;
            tb.IsReadOnly = true;
            Keyboard.ClearFocus();
        }

        private void AlgorithmNameTB1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            algorithmName1Temp = AlgorithmName1;
            tb.IsReadOnly = false;
            tb.Focus();
        }
        #endregion
    }
}
