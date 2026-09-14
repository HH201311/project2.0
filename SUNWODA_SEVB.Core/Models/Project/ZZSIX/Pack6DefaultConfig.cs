namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    public static class Pack6DefaultConfig
    {
        /// <summary>枣庄八窗队列需求（与旧 VM_PagePack6Surface 一致）。</summary>
        public static IReadOnlyDictionary<string, int> QueueRequirements { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["UpperQueue"] = 1,
                ["Bottom1Queue"] = 3,
                ["Bottom2Queue"] = 3,
                ["Bottom3Queue"] = 3,
                ["FrontQueue"] = 3,
                ["RearQueue"] = 2,
                ["LeftQueue"] = 1,
                ["RightQueue"] = 1
            };

        public static Pack6Config Create() => new();
    }
}
