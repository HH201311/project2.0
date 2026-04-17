using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    /// <summary>
    /// 带前缀和后缀的TextBox控件，适用于需要在输入框前后显示单位或其他固定文本的场景
    /// </summary>
    public class AffixedTextBox : TextBox
    {
        #region Prefix 前缀
        public static readonly DependencyProperty PrefixProperty =
               DependencyProperty.Register("Prefix", typeof(string), typeof(AffixedTextBox),
               new PropertyMetadata(string.Empty, OnPrefixChanged));

        public string Prefix
        {
            get { return (string)GetValue(PrefixProperty); }
            set { SetValue(PrefixProperty, value); }
        }

        private static void OnPrefixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as AffixedTextBox;
        }
        #endregion

        #region Suffix 后缀
        public static readonly DependencyProperty SuffixProperty =
               DependencyProperty.Register("Suffix", typeof(string), typeof(AffixedTextBox),
               new PropertyMetadata(string.Empty, OnSuffixChanged));

        public string Suffix
        {
            get { return (string)GetValue(SuffixProperty); }
            set { SetValue(SuffixProperty, value); }
        }

        private static void OnSuffixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as AffixedTextBox;
        }
        #endregion
    }
}
