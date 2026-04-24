using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Component.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
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

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.SettingControls.FunctionSettingControls
{
    /// <summary>
    /// CLFunctionControl.xaml 的交互逻辑
    /// </summary>
    public partial class CLFunctionControl : UserControl
    {
        public CLFunctionControl()
        {
            InitializeComponent();
        }

        public ObservableCollection<ComboboxItemModel<EWeightDataTypeOfArithmeticParam>> WeightDataTypeOfArithmeticParamSource { get; } =
            new ObservableCollection<ComboboxItemModel<EWeightDataTypeOfArithmeticParam>>(
            Enum.GetValues(typeof(EWeightDataTypeOfArithmeticParam)).Cast<EWeightDataTypeOfArithmeticParam>()
            .Select(v => new ComboboxItemModel<EWeightDataTypeOfArithmeticParam>() { ItemValue = v, DisplayName = v.ToString().Substring(0, v.ToString().Length - 2) }));

        #region ItemSource
        public BasicFunctionSettingModel ItemSource
        {
            get { return (BasicFunctionSettingModel)GetValue(ItemSourceProperty); }
            set { SetValue(ItemSourceProperty, value); }
        }

        public static readonly DependencyProperty ItemSourceProperty = DependencyProperty.Register(
            "ItemSource",
            typeof(BasicFunctionSettingModel),
            typeof(CLFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnItemSourceChanged))
        );

        private static void OnItemSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion

        #region FTPUploadDate
        public DateTime FTPUploadDate
        {
            get { return (DateTime)GetValue(FTPUploadDateProperty); }
            set { SetValue(FTPUploadDateProperty, value); }
        }

        public static readonly DependencyProperty FTPUploadDateProperty = DependencyProperty.Register(
            "FTPUploadDate",
            typeof(DateTime),
            typeof(CLFunctionControl),
            new PropertyMetadata(DateTime.Now, new PropertyChangedCallback(OnFTPUploadDateChanged))
        );

        private static void OnFTPUploadDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion

        #region FTPUploadCommand
        public ICommand FTPUploadCommand
        {
            get { return (ICommand)GetValue(FTPUploadCommandProperty); }
            set { SetValue(FTPUploadCommandProperty, value); }
        }

        public static readonly DependencyProperty FTPUploadCommandProperty = DependencyProperty.Register(
            "FTPUploadCommand",
            typeof(ICommand),
            typeof(CLFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnFTPUploadCommandChanged))
        );

        private static void OnFTPUploadCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion

        #region RollNumPathVerifyCommand
        public ICommand RollNumPathVerifyCommand
        {
            get { return (ICommand)GetValue(RollNumPathVerifyCommandProperty); }
            set { SetValue(RollNumPathVerifyCommandProperty, value); }
        }

        public static readonly DependencyProperty RollNumPathVerifyCommandProperty = DependencyProperty.Register(
            "RollNumPathVerifyCommand",
            typeof(ICommand),
            typeof(CLFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnRollNumPathVerifyCommandChanged))
        );

        private static void OnRollNumPathVerifyCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion

        #region SaveCommand
        public ICommand SaveCommand
        {
            get { return (ICommand)GetValue(SaveCommandProperty); }
            set { SetValue(SaveCommandProperty, value); }
        }

        public static readonly DependencyProperty SaveCommandProperty = DependencyProperty.Register(
            "SaveCommand",
            typeof(ICommand),
            typeof(CLFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnSaveCommandChanged))
        );

        private static void OnSaveCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion

        #region CancleCommand
        public ICommand CancleCommand
        {
            get { return (ICommand)GetValue(CancleCommandProperty); }
            set { SetValue(CancleCommandProperty, value); }
        }

        public static readonly DependencyProperty CancleCommandProperty = DependencyProperty.Register(
            "CancleCommand",
            typeof(ICommand),
            typeof(CLFunctionControl),
            new PropertyMetadata(null, new PropertyChangedCallback(OnCancleCommandChanged))
        );

        private static void OnCancleCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as CLFunctionControl;
        }
        #endregion
    }
}
