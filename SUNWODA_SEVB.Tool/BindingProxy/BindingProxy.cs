using System.Windows;

namespace SUNWODA_SEVB.Tool.BindingProxy
{
    /// <summary>
    /// 绑定代理类，用于在WPF中实现跨数据上下文的绑定
    /// BindingProxy 是一个可冻结的类，它能够在DataContext无法直接访问的情况下实现数据绑定，例如在样式或模板中使用时。
    /// </summary>
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
    }
}
