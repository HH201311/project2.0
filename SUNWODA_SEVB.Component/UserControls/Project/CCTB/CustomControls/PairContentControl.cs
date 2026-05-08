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

        #region PairSeparateWidth
        public static readonly DependencyProperty PairSeparateWidthProperty =
               DependencyProperty.Register("PairSeparateWidth", typeof(GridLength), typeof(PairContentControl),
               new PropertyMetadata(GridLength.Auto, OnPairSeparateWidthChanged));

        public GridLength PairSeparateWidth
        {
            get { return (GridLength)GetValue(PairSeparateWidthProperty); }
            set { SetValue(PairSeparateWidthProperty, value); }
        }

        private static void OnPairSeparateWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion

        #region PairValueWidth
        public static readonly DependencyProperty PairValueWidthProperty =
               DependencyProperty.Register("PairValueWidth", typeof(GridLength), typeof(PairContentControl),
               new PropertyMetadata(new GridLength(1, GridUnitType.Star), OnPairValueWidthChanged));

        public GridLength PairValueWidth
        {
            get { return (GridLength)GetValue(PairValueWidthProperty); }
            set { SetValue(PairValueWidthProperty, value); }
        }

        private static void OnPairValueWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion

        #region PairValueMinWidth
        public static readonly DependencyProperty PairValueMinWidthProperty =
               DependencyProperty.Register("PairValueMinWidth", typeof(double), typeof(PairContentControl),
               new PropertyMetadata(100d, OnPairValueMinWidthChanged));

        public double PairValueMinWidth
        {
            get { return (double)GetValue(PairValueWidthProperty); }
            set { SetValue(PairValueWidthProperty, value); }
        }

        private static void OnPairValueMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion

        #region PairValueMaxWidth
        public static readonly DependencyProperty PairValueMaxWidthProperty =
               DependencyProperty.Register("PairValueMaxWidth", typeof(double), typeof(PairContentControl),
               new PropertyMetadata(130d, OnPairValueMaxWidthChanged));

        public double PairValueMaxWidth
        {
            get { return (double)GetValue(PairValueWidthProperty); }
            set { SetValue(PairValueWidthProperty, value); }
        }

        private static void OnPairValueMaxWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PairContentControl;
        }
        #endregion
    }
}
