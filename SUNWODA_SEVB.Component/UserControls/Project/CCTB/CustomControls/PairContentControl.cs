using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    public class PairContentControl : ContentControl
    {
        #region PairKeyText
        public static readonly DependencyProperty PairKeyTextProperty =
               DependencyProperty.Register("PairKeyText", typeof(string), typeof(PairContentControl),
               new PropertyMetadata(string.Empty, OnPairKeyTextChanged));

        public string PairKeyText
        {
            get { return (string)GetValue(PairKeyTextProperty); }
            set { SetValue(PairKeyTextProperty, value); }
        }

        private static void OnPairKeyTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion

        #region PairKeyWidth
        public static readonly DependencyProperty PairKeyWidthProperty =
               DependencyProperty.Register("PairKeyWidth", typeof(GridLength), typeof(PairContentControl),
               new PropertyMetadata(GridLength.Auto, OnPairKeyWidthChanged));

        public GridLength PairKeyWidth
        {
            get { return (GridLength)GetValue(PairKeyWidthProperty); }
            set { SetValue(PairKeyWidthProperty, value); }
        }

        private static void OnPairKeyWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion

        #region PairKeyOpacity
        public static readonly DependencyProperty PairKeyOpacityProperty =
               DependencyProperty.Register("PairKeyOpacity", typeof(double), typeof(PairContentControl),
               new PropertyMetadata(1d, OnPairKeyOpacityChanged));

        public double PairKeyOpacity
        {
            get { return (double)GetValue(PairKeyOpacityProperty); }
            set { SetValue(PairKeyOpacityProperty, value); }
        }

        private static void OnPairKeyOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion
    }
}
