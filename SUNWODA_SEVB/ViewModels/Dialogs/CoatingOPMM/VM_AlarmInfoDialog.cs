using System.Timers;
using System.Windows.Input;
using System.Windows.Media;
using HandyControl.Tools.Extension;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using Timer = System.Timers.Timer;

namespace SUNWODA_SEVB.ViewModels.Dialogs.CoatingOPMM
{
    public class VM_AlarmInfoDialog : DialogViewModelBase, IDialogResultable<bool>
    {
        private string? _alarmLevel;
        private string? _alarmZone;
        private string? _alarmInfo;
        private Brush? _alarmLevelColor;
        private double _remainTime = 100.0;
        private Timer? _timer;
        private double _countdownTime = 5.0;
        private double _mainFontSize = 30;

        private readonly Brush YellowColor = new SolidColorBrush(Color.FromRgb(255, 184, 51));
        private readonly Brush RedColor = new SolidColorBrush(Color.FromRgb(255, 51, 51));

        public string? AlarmLevel
        {
            get => _alarmLevel;
            set
            {
                SetProperty(ref _alarmLevel, value);
                if (value == "严重")
                    AlarmLevelColor = RedColor;
                else
                    AlarmLevelColor = YellowColor;
            }
        }

        public string? AlarmZone
        {
            get => _alarmZone;
            set => SetProperty(ref _alarmZone, value);
        }

        public string? AlarmInfo
        {
            get => _alarmInfo;
            set => SetProperty(ref _alarmInfo, value);
        }

        public Brush? AlarmLevelColor
        {
            get => _alarmLevelColor;
            set => SetProperty(ref _alarmLevelColor, value);
        }

        public double RemainTime
        {
            get => _remainTime;
            set => SetProperty(ref _remainTime, value);
        }

        public double CountdownTime
        {
            get => _countdownTime;
            set => SetProperty(ref _countdownTime, value);
        }

        public double MainFontSize
        {
            get => _mainFontSize;
            set => SetProperty(ref _mainFontSize, value);
        }

        public bool Result { get; set; }
        public Action? CloseAction { get; set; }

        public ICommand CloseCommand =>
            new RelayCommand(() =>
            {
                Result = true;
                CloseAction?.Invoke();
            });

        public VM_AlarmInfoDialog()
        {
            Task.Run(() =>
            {
                // 创建定时器
                _timer = new Timer();
                _timer.Interval = CountdownTime * 10;
                _timer.Elapsed += Timer_Elapsed;
                _timer?.Start();
            });
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                UIElement?.Dispatcher.Invoke(() =>
                {
                    RemainTime--;
                    // 如果倒计时结束
                    if (RemainTime <= 0)
                    {
                        _timer?.Stop();
                        // 执行关闭操作
                        Result = true;
                        CloseAction?.Invoke();
                    }
                });
            }
            catch (Exception)
            {
                _timer?.Stop();
                // 执行关闭操作
                Result = false;
                CloseAction?.Invoke();
            }
        }
    }
}
