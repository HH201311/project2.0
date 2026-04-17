using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.WindowsAPICodePack.Dialogs;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    /// <summary>
    /// FloderPicker.xaml 的交互逻辑
    /// </summary>
    public partial class FloderPicker : UserControl
    {
        public FloderPicker()
        {
            InitializeComponent();
        }

        #region FloderText
        public string FloderText
        {
            get { return (string)GetValue(FloderTextProperty); }
            set { SetValue(FloderTextProperty, value); }
        }

        public static readonly DependencyProperty FloderTextProperty = DependencyProperty.Register(
            "FloderText",
            typeof(string),
            typeof(FloderPicker),
            new PropertyMetadata(string.Empty, new PropertyChangedCallback(OnFloderTextChanged))
        );

        private static void OnFloderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as FloderPicker;
        }
        #endregion

        #region PickerContent
        public object PickerContent
        {
            get { return (object)GetValue(PickerContentProperty); }
            set { SetValue(PickerContentProperty, value); }
        }

        public static readonly DependencyProperty PickerContentProperty = DependencyProperty.Register(
            "Content",
            typeof(object),
            typeof(FloderPicker),
            new PropertyMetadata("更改", new PropertyChangedCallback(OnPickerContentChanged))
        );

        private static void OnPickerContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var uc = d as FloderPicker;
        }
        #endregion

        /// <summary>
        /// 选择文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FloderPick_Click(object sender, RoutedEventArgs e)
        {
            var currentFloderPath = Path.GetDirectoryName(FloderText);
            if (string.IsNullOrEmpty(currentFloderPath))
                currentFloderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);

            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = false;
            dialog.Title = "请选择文件夹";
            dialog.InitialDirectory = currentFloderPath;

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            var selectedFolderPath = dialog.FileName;

            FloderText = selectedFolderPath;
        }
    }
}
