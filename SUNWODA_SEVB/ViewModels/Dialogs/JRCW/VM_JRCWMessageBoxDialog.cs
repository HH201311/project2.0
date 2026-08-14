using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;

namespace SUNWODA_SEVB.ViewModels.Dialogs.JRCW
{
    public class VM_JRCWMessageBoxDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        private string? messageText;
        private string? caption;
        private double countdownTime = 5.0;
        private double remainTime;
        private DispatcherTimer? timer;

        /// <summary>
        /// 信息
        /// </summary>
        public string? MessageText { get => messageText; set => SetProperty(ref messageText, value); }

        /// <summary>
        /// 标题
        /// </summary>
        public string? Caption { get => caption; set => SetProperty(ref caption, value); }

        /// <summary>
        /// 倒计时
        /// </summary>
        public double CountdownTime { get => countdownTime; set => SetProperty(ref countdownTime, value); }

        /// <summary>
        /// 倒计时显示
        /// </summary>
        public Visibility CountdownTimeVisibility { get => countdownTime > 0 ? Visibility.Visible : Visibility.Collapsed; }

        /// <summary>
        /// 倒计时
        /// </summary>
        public double RemainTime { get => remainTime; private set => SetProperty(ref remainTime, value); }

        /// <summary>
        /// 
        /// </summary>
        public ICommand? ConfirmCommand => new RelayCommand(OnConfirmClicked);

        /// <summary>
        /// 
        /// </summary>
        public ICommand? CancleCommand => new RelayCommand(OnCancleClicked);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg"></param>
        private void OnConfirmClicked(object? arg)
        {
            Result = true;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="arg"></param>
        private void OnCancleClicked(object? arg)
        {
            Result = false;
            CloseAction?.Invoke();
        }

        /// <summary>
        /// 结果
        /// </summary>
        public bool Result { get; set; }

        /// <summary>
        /// 关闭操作
        /// </summary>
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = true;
                CloseAction?.Invoke();
            });

        /// <summary>
        /// 定时器初始化
        /// </summary>
        public void CountdownTimeStart()
        {
            if (CountdownTime <= 0) return;
            RemainTime = CountdownTime;

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Ticked;
            timer?.Start();
        }

        /// <summary>
        /// 定时器任务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Ticked(object? sender, EventArgs e)
        {
            try
            {
                UIElement?.Dispatcher.Invoke(() =>
                {
                    RemainTime--;
                    if (RemainTime >= 0) return;

                    timer?.Stop();
                    // 执行关闭操作
                    Result = true;
                    CloseAction?.Invoke();
                });
            }
            catch
            {
                timer?.Stop();
                // 执行关闭操作
                Result = false;
                CloseAction?.Invoke();
            }
        }
    }
}
