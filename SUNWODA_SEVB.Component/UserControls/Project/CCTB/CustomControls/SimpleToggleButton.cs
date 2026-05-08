using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    public class SimpleToggleButton : ToggleButton
    {
        #region CheckedText
        public static readonly DependencyProperty CheckedTextProperty =
               DependencyProperty.Register("CheckedText", typeof(string), typeof(SimpleToggleButton),
               new PropertyMetadata("开启", OnCheckedTextChanged));

        public string CheckedText
        {
            get { return (string)GetValue(CheckedTextProperty); }
            set { SetValue(CheckedTextProperty, value); }
        }

        private static void OnCheckedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SimpleToggleButton;
        }
        #endregion

        #region UnCheckedText
        public static readonly DependencyProperty UnCheckedTextProperty =
               DependencyProperty.Register("UnCheckedText", typeof(string), typeof(SimpleToggleButton),
               new PropertyMetadata("关闭", OnUnCheckedTextChanged));

        public string UnCheckedText
        {
            get { return (string)GetValue(UnCheckedTextProperty); }
            set { SetValue(UnCheckedTextProperty, value); }
        }

        private static void OnUnCheckedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as SimpleToggleButton;
        }
        #endregion
    }
}
