using SUNWODA_SEVB.Core.Enumerations.Component;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Models.Component
{
    /// <summary>
    /// 报警等级配置
    /// </summary>
    public class AlarmLevelConfig
    {
        public AlarmLevel Level { get; set; }
        public Brush? Background { get; set; }
        public Brush? Foreground { get; set; }
        public string? DisplayName { get; set; }

        public static AlarmLevelConfig GetConfig(AlarmLevel level)
        {
            return level switch
            {
                AlarmLevel.Info => new AlarmLevelConfig
                {
                    Level = AlarmLevel.Info,
                    Background = new SolidColorBrush(Color.FromRgb(206, 255, 255)),
                    Foreground = new SolidColorBrush(Color.FromRgb(106, 255, 255)),
                    DisplayName = "信息"
                },
                AlarmLevel.Warning => new AlarmLevelConfig
                {
                    Level = AlarmLevel.Warning,
                    Background = new SolidColorBrush(Color.FromRgb(255, 248, 234)),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 202, 96)),
                    DisplayName = "警告"
                },
                AlarmLevel.Error => new AlarmLevelConfig
                {
                    Level = AlarmLevel.Error,
                    Background = new SolidColorBrush(Color.FromRgb(244, 136, 142)),
                    Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    DisplayName = "错误"
                },
                AlarmLevel.Critical => new AlarmLevelConfig
                {
                    Level = AlarmLevel.Critical,
                    Background = new SolidColorBrush(Color.FromRgb(255, 234, 234)),
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                    DisplayName = "严重"
                },
                _ => new AlarmLevelConfig
                {
                    Level = AlarmLevel.Info,
                    Background = Brushes.Gray,
                    Foreground = Brushes.White,
                    DisplayName = "未知"
                }
            };
        }
    }
}
