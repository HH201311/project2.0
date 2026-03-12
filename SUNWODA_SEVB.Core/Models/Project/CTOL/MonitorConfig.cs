namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    /// <summary>全局配置</summary>
    public sealed class MonitorConfig
    {
        public string WatchRoot { get; init; } = @"D:\LAB";
        public string FilePattern { get; init; } = "*_origin.png";
        public int FileStableSeconds { get; init; } = 1;
        public int ScanIntervalSeconds { get; init; } = 60;
        public int HotDirectoryWindowMinutes { get; init; } = 30;
        public int ChannelCapacity { get; init; } = 50_000;
    }

    /// <summary>进入队列的事件</summary>
    /// <param name="FilePath">完整路径</param>
    /// <param name="Source">来源（Watcher / Scanner）</param>
    public sealed record FileEvent(string FilePath, string Source);
}
