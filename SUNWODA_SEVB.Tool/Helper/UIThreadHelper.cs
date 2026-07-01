using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class UIThreadHelper
    {
        private static Dispatcher? Dispatcher => Application.Current?.Dispatcher;

        /// <summary>
        /// 在 UI 线程执行异步操作
        /// </summary>
        public static async Task<T> RunOnUIThreadAsync<T>(Func<Task<T>> action)
        {
            if (Dispatcher == null) throw new InvalidOperationException("Dispatcher 未初始化");

            if (Dispatcher.CheckAccess())
            {
                return await action();
            }

            return await Dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        /// <summary>
        /// 在 UI 线程执行同步操作
        /// </summary>
        public static void RunOnUIThread(Action action)
        {
            if (Dispatcher == null) throw new InvalidOperationException("Dispatcher 未初始化");

            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }
    }
}
