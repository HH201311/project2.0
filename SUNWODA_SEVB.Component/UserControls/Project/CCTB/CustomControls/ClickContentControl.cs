using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    public class ClickContentControl : ContentControl
    {
        #region ClickContent
        public static readonly DependencyProperty ClickContentProperty =
               DependencyProperty.Register("ClickContent", typeof(object), typeof(ClickContentControl),
               new PropertyMetadata("点击", OnClickContentChanged));

        public object ClickContent
        {
            get { return (object)GetValue(ClickContentProperty); }
            set { SetValue(ClickContentProperty, value); }
        }

        private static void OnClickContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ClickContentControl;
        }
        #endregion

        #region ClickCommand
        public static readonly DependencyProperty ClickCommandProperty =
               DependencyProperty.Register("ClickCommand", typeof(ICommand), typeof(ClickContentControl),
               new PropertyMetadata(null, OnClickCommandChanged));

        public ICommand ClickCommand
        {
            get { return (ICommand)GetValue(ClickCommandProperty); }
            set { SetValue(ClickCommandProperty, value); }
        }

        private static void OnClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ClickContentControl;
        }
        #endregion

        #region ClickCommandParameter
        public static readonly DependencyProperty ClickCommandParameterProperty =
               DependencyProperty.Register("ClickCommandParameter", typeof(object), typeof(ClickContentControl),
               new PropertyMetadata(null, OnClickCommandParameterChanged));

        public object ClickCommandParameter
        {
            get { return (object)GetValue(ClickCommandParameterProperty); }
            set { SetValue(ClickCommandParameterProperty, value); }
        }

        private static void OnClickCommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as ClickContentControl;
        }
        #endregion
    }
}
