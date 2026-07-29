using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SUNWODA_SEVB.Component.UserControls.Project.CCTB.CustomControls
{
    /// <summary>
    /// DateRangePicker.xaml 的交互逻辑
    /// </summary>
    public partial class DateRangePicker : UserControl
    {
        #region 依赖属性
        /// <summary>
        /// 开始日期
        /// </summary>
        public static readonly DependencyProperty StartDateProperty =
            DependencyProperty.Register("StartDate", typeof(DateTime?), typeof(DateRangePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));

        /// <summary>
        /// 结束日期
        /// </summary>
        public static readonly DependencyProperty EndDateProperty =
            DependencyProperty.Register("EndDate", typeof(DateTime?), typeof(DateRangePicker),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateChanged));

        /// <summary>
        /// 是否显示时间
        /// </summary>
        public static readonly DependencyProperty ShowTimeProperty =
            DependencyProperty.Register("ShowTime", typeof(bool), typeof(DateRangePicker),
                new PropertyMetadata(true));

        /// <summary>
        /// 日期格式
        /// </summary>
        public static readonly DependencyProperty DateFormatProperty =
            DependencyProperty.Register("DateFormat", typeof(string), typeof(DateRangePicker),
                new PropertyMetadata("yyyy-MM-dd HH:mm"));

        /// <summary>
        /// 水印文字
        /// </summary>
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.Register("Watermark", typeof(string), typeof(DateRangePicker),
                new FrameworkPropertyMetadata("全部操作时间", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnWatermarkPropertyChanged));

        /// <summary>
        /// 弹出状态
        /// </summary>
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(DateRangePicker),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        #endregion

        #region 属性
        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartDate { get => (DateTime?)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndDate { get => (DateTime?)GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }

        /// <summary>
        /// 是否显示小时分钟
        /// </summary>
        public bool ShowTime { get => (bool)GetValue(ShowTimeProperty); set => SetValue(ShowTimeProperty, value); }

        /// <summary>
        /// 时间显示格式
        /// </summary>
        public string DateFormat { get => (string)GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

        /// <summary>
        /// 背景字
        /// </summary>
        public string Watermark { get => (string)GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

        /// <summary>
        /// 是否打开
        /// </summary>
        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        #endregion

        #region 属性变更回调
        /// <summary>
        /// 当起止日期修改时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var picker = (DateRangePicker)d;

            picker.startDate = picker.StartDate;
            picker.endDate = picker.EndDate;
            picker.GenerateDays();
        }

        /// <summary>
        /// 当水印文字修改时触发
        /// </summary>
        /// <param name="d"></param>
        /// <param name="e"></param>
        private static void OnWatermarkPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is DateRangePicker picker)) return;
            picker.UpdateDisplayText();
        }
        #endregion

        #region 日历数据
        /// <summary>
        /// 左侧面板日期集合
        /// </summary>
        public ObservableCollection<CalendarDay> LeftDays { get; } = new ObservableCollection<CalendarDay>();

        /// <summary>
        /// 右侧面板日期集合
        /// </summary>
        public ObservableCollection<CalendarDay> RightDays { get; } = new ObservableCollection<CalendarDay>();

        /// <summary>
        /// 小时选项
        /// </summary>
        public ObservableCollection<int> Hours { get; } = new ObservableCollection<int>();

        /// <summary>
        /// 分钟选项
        /// </summary>
        public ObservableCollection<int> Minutes { get; } = new ObservableCollection<int>();

        /// <summary>
        /// 面板时间
        /// </summary>
        private DateTime leftMonth, rightMonth;

        // 是否正在选择开始日期
        private bool isSelectingStart = true;

        // 时间选择
        private DateTime? startDate = null, endDate = null;
        private int startHour = 0, startMinute = 0;
        private int endHour = 23, endMinute = 59;
        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public DateRangePicker()
        {
            InitializeComponent();

            // 初始化时间选项
            for (int i = 0; i < 24; i++) Hours.Add(i);
            for (int i = 0; i < 60; i++) Minutes.Add(i);

            StartHourCB.SelectedIndex = startHour;
            StartMinuteCB.SelectedIndex = startMinute;
            EndHourCB.SelectedIndex = endHour;
            EndMinuteCB.SelectedIndex = endMinute;

            // 默认显示当上月和当月
            leftMonth = DateTime.Now.AddMonths(-1);
            rightMonth = DateTime.Now;
            LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
            RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");

            // 刷新日期
            GenerateDays();
            UpdateDisplayText();
        }

        #region 日期逻辑
        /// <summary>
        /// 生成日期
        /// </summary>
        private void GenerateDays()
        {
            LeftDays.Clear();
            RightDays.Clear();

            GenerateMonthDays(leftMonth, LeftDays);
            GenerateMonthDays(rightMonth, RightDays);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="month"></param>
        /// <param name="days"></param>
        private void GenerateMonthDays(DateTime month, ObservableCollection<CalendarDay> days)
        {
            // 月初
            var firstDay = new DateTime(month.Year, month.Month, 1);
            // 上月需要显示的天数
            var startOffset = (int)firstDay.DayOfWeek;
            if (startOffset == 0) startOffset = 7; // 周日放在最后
            startOffset--; // 调整为0起始

            // 上月的日期
            var prevMonth = firstDay.AddMonths(-1);
            var prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = startOffset - 1; i >= 0; i--)
            {
                var day = new CalendarDay
                {
                    Date = new DateTime(prevMonth.Year, prevMonth.Month, prevMonthDays - i),
                    IsCurrentMonth = false,
                    IsToday = false
                };
                day.IsSelected = IsDateSelected(day.Date);
                day.IsInRange = IsDateInRange(day.Date);
                day.IsRangeStart = IsRangeStart(day.Date);
                day.IsRangeEnd = IsRangeEnd(day.Date);
                days.Add(day);
            }

            // 当月日期
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var today = DateTime.Today;
            for (int i = 1; i <= daysInMonth; i++)
            {
                var date = new DateTime(month.Year, month.Month, i);
                var day = new CalendarDay
                {
                    Date = date,
                    IsCurrentMonth = true,
                    IsToday = date == today
                };
                day.IsSelected = IsDateSelected(date);
                day.IsInRange = IsDateInRange(date);
                day.IsRangeStart = IsRangeStart(date);
                day.IsRangeEnd = IsRangeEnd(date);
                days.Add(day);
            }

            // 下月日期，补齐
            var remaining = 7 - (int)days.Last().Date.DayOfWeek;
            if (remaining == 7) remaining = 0;
            var nextMonth = firstDay.AddMonths(1);
            for (int i = 1; i <= remaining; i++)
            {
                var day = new CalendarDay
                {
                    Date = new DateTime(nextMonth.Year, nextMonth.Month, i),
                    IsCurrentMonth = false,
                    IsToday = false
                };
                day.IsSelected = IsDateSelected(day.Date);
                day.IsInRange = IsDateInRange(day.Date);
                day.IsRangeStart = IsRangeStart(day.Date);
                day.IsRangeEnd = IsRangeEnd(day.Date);
                days.Add(day);
            }
        }

        /// <summary>
        /// 是否是选择的日期
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private bool IsDateSelected(DateTime date)
        {
            if (startDate.HasValue && date.Date == startDate.Value.Date) return true;
            if (endDate.HasValue && date.Date == endDate.Value.Date) return true;
            return false;
        }

        /// <summary>
        /// 是否在选择的日期范围内
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private bool IsDateInRange(DateTime date)
        {
            if (!startDate.HasValue || !endDate.HasValue) return false;
            if (startDate.Value.Date > endDate.Value.Date) return false;
            return date.Date > startDate.Value.Date && date.Date < endDate.Value.Date;
        }

        /// <summary>
        /// 是否是选择范围的开始日期
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private bool IsRangeStart(DateTime date)
        {
            return startDate.HasValue && date.Date == startDate.Value.Date;
        }

        /// <summary>
        /// 是否是选择范围的结束日期
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        private bool IsRangeEnd(DateTime date)
        {
            return endDate.HasValue && date.Date == endDate.Value.Date;
        }

        /// <summary>
        /// 是否是同一天
        /// </summary>
        /// <param name="date"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        private bool IsSameDay(DateTime? date, DateTime? other)
        {
            if (date == null || other == null)
                return false;

            return date.Value.Date == other.Value.Date;
        }
        #endregion

        #region 交互命令
        /// <summary>
        /// 日期点击
        /// </summary>
        public ICommand DayClickCommand => new RelayCommand<CalendarDay>(day =>
        {
            if (day == null) return;

            if (isSelectingStart || !startDate.HasValue)
            {
                startDate = day.Date.Add(new TimeSpan(startHour, startMinute, 0));
                endDate = null;
                isSelectingStart = false;
            }
            else if (!isSelectingStart)
            {
                startDate = startDate.Value.Date.Add(new TimeSpan(startHour, startMinute, 0));
                var newEndDate = day.Date.Add(new TimeSpan(endHour, endMinute, 0));

                if (newEndDate < startDate)
                {
                    if (IsSameDay(startDate, newEndDate))
                    {
                        var tempHour = startHour;
                        startHour = endHour;
                        endHour = tempHour;
                        StartHourCB.SelectedIndex = startHour;
                        EndHourCB.SelectedIndex = endHour;

                        var tempMinute = startMinute;
                        startMinute = endMinute;
                        endMinute = tempMinute;
                        StartMinuteCB.SelectedIndex = startMinute;
                        EndMinuteCB.SelectedIndex = endMinute;

                        startDate = startDate.Value.Date.Add(new TimeSpan(startHour, startMinute, 0));
                        newEndDate = day.Date.Add(new TimeSpan(startHour, startMinute, 0));
                        endDate = newEndDate;
                    }
                    else
                    {
                        endDate = startDate;
                        startDate = newEndDate;
                    }
                }
                else
                {
                    endDate = newEndDate;
                }
                isSelectingStart = true;
            }

            GenerateDays();
        });

        /// <summary>
        /// 月份导航
        /// </summary>
        public ICommand PrevMonthCommand => new RelayCommand((arg) =>
        {
            if (!(arg is string a)) return;

            if (a == "leftDate")
            {
                leftMonth = leftMonth.AddMonths(-1);
                LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
            }
            if (a == "rightDate")
            {
                rightMonth = rightMonth.AddMonths(-1);
                RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");
                if (leftMonth.Date >= rightMonth.Date)
                {
                    leftMonth = rightMonth.AddMonths(-1);
                    LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
                }
            }

            // 刷新选中状态
            GenerateDays();
        });

        /// <summary>
        /// 月份导航
        /// </summary>
        public ICommand NextMonthCommand => new RelayCommand((arg) =>
        {
            if (!(arg is string a)) return;

            if (a == "leftDate")
            {
                leftMonth = leftMonth.AddMonths(1);
                LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
                if (leftMonth.Date >= rightMonth.Date)
                {
                    rightMonth = leftMonth.AddMonths(1);
                    RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");
                }
            }
            if (a == "rightDate")
            {
                rightMonth = rightMonth.AddMonths(1);
                RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");
            }

            // 刷新选中状态
            GenerateDays();
        });

        /// <summary>
        /// 清空
        /// </summary>
        public ICommand ClearCommand => new RelayCommand(() =>
        {
            startDate = null;
            endDate = null;
            StartHourCB.SelectedIndex = 0;
            StartMinuteCB.SelectedIndex = 0;
            EndHourCB.SelectedIndex = 23;
            EndMinuteCB.SelectedIndex = 59;
            isSelectingStart = true;

            StartDate = null;
            EndDate = null;

            IsOpen = false;

            // 默认显示当上月和当月
            leftMonth = DateTime.Now.AddMonths(-1);
            rightMonth = DateTime.Now;
            LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
            RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");
            // 刷新选中状态
            GenerateDays();
            UpdateDisplayText();
        });

        /// <summary>
        /// 确定
        /// </summary>
        public ICommand ConfirmCommand => new RelayCommand(() =>
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                var start = startDate.Value;
                var end = endDate.Value;

                if (start > end)
                {
                    var temp = start;
                    start = end;
                    end = temp;
                    startDate = start;
                    endDate = end;
                    GenerateDays();
                }

                StartDate = start;
                EndDate = end;
            }
            else if (startDate.HasValue)
            {
                StartDate = startDate;
                EndDate = null;
            }
            else if (endDate.HasValue)
            {
                EndDate = endDate;
                StartDate = null;
            }
            else
            {
                StartDate = null;
                EndDate = null;
            }

            IsOpen = false;
            UpdateDisplayText();
        });
        #endregion

        #region 辅助方法
        /// <summary>
        /// 更新水印文本
        /// </summary>
        private void UpdateDisplayText()
        {
            var format = ShowTime ? DateFormat : "yyyy-MM-dd";

            var text = Watermark;
            if (StartDate.HasValue || EndDate.HasValue)
            {
                text = StartDate.HasValue ? $"{StartDate.Value.ToString(format)}" : "最早";
                text += " ` ";
                text += EndDate.HasValue ? $"{EndDate.Value.ToString(format)}" : "至今";
            }

            DisplayTB.Text = text;
        }
        #endregion

        /// <summary>
        /// 当点击时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnInputClick(object sender, MouseButtonEventArgs e)
        {
            // 关键：阻止事件继续传播
            e.Handled = true;
            IsOpen = !IsOpen;

            if (!IsOpen) return;

            if (!StartDate.HasValue && !EndDate.HasValue)
            {
                startDate = null;
                endDate = null;
                leftMonth = DateTime.Now.AddMonths(-1);
                rightMonth = DateTime.Now;
            }
            else if (!StartDate.HasValue)
            {
                leftMonth = rightMonth.AddMonths(-1);
            }
            else if (!EndDate.HasValue)
            {
                rightMonth = leftMonth.AddMonths(1);
            }
            else
            {
                leftMonth = StartDate.Value.Date;
                rightMonth = IsSameDay(StartDate, EndDate) ? leftMonth.AddMonths(1) : EndDate.Value.Date;
            }
            LeftMonthTitle.Text = leftMonth.ToString("yyyy年MM月");
            RightMonthTitle.Text = rightMonth.ToString("yyyy年MM月");

            // 刷新日期
            GenerateDays();
        }

        /// <summary>
        /// 当日期选择时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDayClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Label label && label.DataContext is CalendarDay day)) return;

            // 触发命令
            DayClickCommand?.Execute(day);
        }

        private bool _isUpdatingComboBox = false;
        /// <summary>
        /// 当时间选择时触发
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnTimeClick(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is ComboBox cb)) return;

            if (_isUpdatingComboBox) return;

            var isSamDay = IsSameDay(startDate, endDate);

            if (isSamDay)
            {
                var newStartHour = startHour;
                var newStartMinute = startMinute;
                var newEndHour = endHour;
                var newEndMinute = endMinute;

                if (cb == StartHourCB) newStartHour = (int)cb.SelectedItem;
                if (cb == StartMinuteCB) newStartMinute = (int)cb.SelectedItem;
                if (cb == EndHourCB) newEndHour = (int)cb.SelectedItem;
                if (cb == EndMinuteCB) newEndMinute = (int)cb.SelectedItem;

                var newStartTime = new DateTime().AddHours(newStartHour).AddMinutes(newStartMinute);
                var endStartTime = new DateTime().AddHours(newEndHour).AddMinutes(newEndMinute);

                if (newStartTime > endStartTime)
                {
                    _isUpdatingComboBox = true;
                    if (cb == StartHourCB) cb.SelectedIndex = startHour;
                    if (cb == StartMinuteCB) cb.SelectedIndex = startMinute;
                    if (cb == EndHourCB) cb.SelectedIndex = endHour;
                    if (cb == EndMinuteCB) cb.SelectedIndex = endMinute;
                    _isUpdatingComboBox = false;
                    return;
                }
            }
            if (cb == StartHourCB) startHour = (int)cb.SelectedItem;
            if (cb == StartMinuteCB) startMinute = (int)cb.SelectedItem;
            if (cb == EndHourCB) endHour = (int)cb.SelectedItem;
            if (cb == EndMinuteCB) endMinute = (int)cb.SelectedItem;

            if (startDate.HasValue) startDate = startDate.Value.Date.Add(new TimeSpan(startHour, startMinute, 0));
            if (endDate.HasValue) endDate = endDate.Value.Date.Add(new TimeSpan(endHour, endMinute, 0));
        }
    }

    /// <summary>
    /// 日历日期模型
    /// </summary>
    public class CalendarDay : ModelBase
    {
        private bool _isInRange;
        private bool _isRangeStart;
        private bool _isRangeEnd;
        private bool _isSelected;

        /// <summary>
        /// 
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsCurrentMonth { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsToday { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        /// <summary>
        /// 
        /// </summary>
        public bool IsInRange { get => _isInRange; set => SetProperty(ref _isInRange, value); }

        /// <summary>
        /// 
        /// </summary>
        public bool IsRangeStart { get => _isRangeStart; set => SetProperty(ref _isRangeStart, value); }

        /// <summary>
        /// 
        /// </summary>
        public bool IsRangeEnd { get => _isRangeEnd; set => SetProperty(ref _isRangeEnd, value); }
    }
}