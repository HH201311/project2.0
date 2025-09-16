

namespace SUNWODA_SEVB.Core.Models
{
    /// <summary>
    /// appsettings.json 配置映射类
    /// </summary>
    public class AppSettings
    {
        public ConnectionStrings ConnectionStrings { get; set; } = new();
        public ProjectSettings ProjectSettings { get; set; } = new();
        public LogCleanupSettings LogCleanup { get; set; } = new();
    }

    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; } = string.Empty;
    }

    public class ProjectSettings
    {
        public bool EnableMES { get; set; }
    }
    public class LogCleanupSettings
    {
        public int AppLogRetentionDays { get; set; } = 7;
        public int InterfaceLogRetentionDays { get; set; } = 30;
        public int MaxDatabaseSizeMB { get; set; } = 100;
    }

}
