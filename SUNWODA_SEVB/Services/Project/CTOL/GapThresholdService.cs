namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 间隙判断阈值注册表
    ///
    /// 以 monitorId 为键，存储各通道的运行时阈值覆盖字典。
    /// 在用户点击"启动"时写入；
    /// 在每次处理图片前读取。
    /// </summary>
    public sealed class GapThresholdService
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, double>> _store =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyDictionary<string, double>? _sharedThresholds;

        /// <summary>
        /// 是否启用四路共享模式。
        /// 为 true 时，<see cref="Get"/> 对所有 monitorId 均返回共享阈值字典；
        /// 为 false 时，返回各路独立阈值字典。
        /// </summary>
        public bool IsSharedMode { get; set; } = false;

        /// <summary>
        /// 获取指定通道的阈值字典。
        /// 共享模式下返回共享字典；独立模式下返回该通道的独立字典，未注册则返回 null。
        /// </summary>
        public IReadOnlyDictionary<string, double>? Get(string monitorId) =>
            IsSharedMode
            ? _sharedThresholds
            : (_store.TryGetValue(monitorId, out var d) ? d : null);

        /// <summary>
        /// 写入或更新指定通道的独立阈值字典（在 UI 线程调用，无需加锁）。
        /// </summary>
        public void Set(string monitorId, Dictionary<string, double> thresholds) =>
            _store[monitorId] = thresholds;

        /// <summary>
        /// 写入四路共享阈值字典（共享模式下所有通道均使用此字典）。
        /// </summary>
        public void SetShared(Dictionary<string, double> thresholds) =>
            _sharedThresholds = thresholds;
    }
}
