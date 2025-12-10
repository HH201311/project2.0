using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Enumerations.Component;

namespace SUNWODA_SEVB.Core.Models.Component
{
    /// <summary>
    /// 报警信息模型
    /// </summary>
    public class AlarmInfo : ModelBase
    {
        private DateTime _alarmTime;
        private string? _alarmObject;
        private string? _alarmType;
        private string? _alarmDetail;
        private AlarmLevel _alarmLevel;

        public DateTime AlarmTime
        {
            get => _alarmTime;
            set
            {
                SetProperty(ref _alarmTime, value);
                OnPropertyChanged(nameof(AlarmTimeString));
            }
        }

        public string? AlarmObject
        {
            get => _alarmObject;
            set => SetProperty(ref _alarmObject, value);
        }

        public string? AlarmType
        {
            get => _alarmType;
            set => SetProperty(ref _alarmType, value);
        }

        public string? AlarmDetail
        {
            get => _alarmDetail;
            set => SetProperty(ref _alarmDetail, value);
        }

        public AlarmLevel AlarmLevel
        {
            get => _alarmLevel;
            set
            {
                SetProperty(ref _alarmLevel, value);
                OnPropertyChanged(nameof(LevelConfig));
            }
        }

        // 格式化的时间字符串
        public string AlarmTimeString => AlarmTime.ToString("yyyy-MM-dd HH:mm:ss");

        // 等级配置
        public AlarmLevelConfig LevelConfig => AlarmLevelConfig.GetConfig(AlarmLevel);
    }
}
