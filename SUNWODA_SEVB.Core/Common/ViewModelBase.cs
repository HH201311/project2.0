using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace SUNWODA_SEVB.Core.Common
{
    public abstract class ViewModelBase : ModelBase
    {
        private readonly Dispatcher _dispatcher;

        public ViewModelBase()
        {
            // 保存UI的Dispatcher，用于后续的UI更新
            _dispatcher = Application.Current.Dispatcher;
        }

        // 生命周期方法
        /// <summary>
        /// 导航完成后
        /// </summary>
        /// <param name="parameter"></param>
        public virtual void OnNavigatedTo(object? parameter)
        {
            // 默认调用异步版本
            Task.Run(async () => await OnNavigatedToAsync(parameter));
        }

        /// <summary>
        /// 导航离开前
        /// </summary>
        public virtual void OnNavigatedFrom()
        {
            // 默认调用异步版本
            Task.Run(async () => await OnNavigatedFromAsync());
        }

        /// <summary>
        /// 是否可以导航
        /// </summary>
        /// <returns></returns>
        public virtual bool CanNavigateFrom() => true;

        /// <summary>
        /// ViewModel初始化完成
        /// </summary>
        public virtual void OnInitialize()
        {
            // 默认调用异步版本
            Task.Run(async () => await OnInitializeAsync());
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public virtual void OnCleanup()
        {
            // 默认调用异步版本
            Task.Run(async () => await OnCleanupAsync());
        }

        // 新增异步生命周期方法
        /// <summary>
        /// 导航完成后
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        protected virtual async Task OnNavigatedToAsync(object? parameter)
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// 导航离开前
        /// </summary>
        /// <returns></returns>
        protected virtual async Task OnNavigatedFromAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// ViewModel初始化完成
        /// </summary>
        /// <returns></returns>
        protected virtual async Task OnInitializeAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        /// <returns></returns>
        protected virtual async Task OnCleanupAsync()
        {
            await Task.CompletedTask;
        }

        // 线程辅助方法

        /// <summary>
        /// UI线程辅助方法
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        protected async Task RunOnUIThreadAsync(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                await _dispatcher.InvokeAsync(action);
            }
        }

        /// <summary>
        /// UI线程辅助方法
        /// </summary>
        /// <param name="action"></param>
        protected void RunOnUIThread(Action action)
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// 后台任务辅助方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        protected async Task<T> RunInBackgroundAsync<T>(Func<Task<T>> func)
        {
            return await Task.Run(func);
        }

        /// <summary>
        /// 后台任务辅助方法
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        protected async Task RunInBackgroundAsync(Func<Task> func)
        {
            await Task.Run(func);
        }

        /// <summary>
        /// 延迟执行UI线程辅助方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="delayMilliseconds"></param>
        /// <returns></returns>
        protected async Task DelayedExecuteAsync(Action action, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds);
            await RunOnUIThreadAsync(action);
        }
    }
}
