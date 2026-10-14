namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>现场 12 台相机（不含「定位」）与队列映射。</summary>
    public static class Pack6CameraCatalog
    {
        public sealed record CameraDef(
            string CameraName,
            string QueueName,
            string DiskHint,
            int DesignShotCount,
            string DisplayTitle);

        /// <summary>
        /// 顺序即十二窗布局顺序（4×3，左→右、上→下）：
        /// 上左/上右/前/后 → 长左1/长左2/长右1/长右2 → 底1/底2/底3/底4。
        /// </summary>
        public static IReadOnlyList<CameraDef> Cameras { get; } =
        [
            new("上左", "UpperLeftQueue", "Z", 2, "上左"),
            new("上右", "UpperRightQueue", "Z", 2, "上右"),
            new("前", "FrontQueue", "Y", 4, "前"),
            new("后", "RearQueue", "Y", 4, "后"),
            new("长左1", "LongLeft1Queue", "Z", 4, "长左1"),
            new("长左2", "LongLeft2Queue", "Z", 4, "长左2"),
            new("长右1", "LongRight1Queue", "Z", 4, "长右1"),
            new("长右2", "LongRight2Queue", "Z", 4, "长右2"),
            new("底1", "Bottom1Queue", "Y", 6, "底1"),
            new("底2", "Bottom2Queue", "Y", 6, "底2"),
            new("底3", "Bottom3Queue", "Y", 6, "底3"),
            new("底4", "Bottom4Queue", "Y", 6, "底4")
        ];

        public static IReadOnlyDictionary<string, int> QueueRequirements { get; } =
            Cameras.ToDictionary(c => c.QueueName, c => c.DesignShotCount, StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyDictionary<string, CameraDef> ByCameraName { get; } =
            Cameras.ToDictionary(c => c.CameraName, StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, CameraDef> ByQueueName { get; } =
            Cameras.ToDictionary(c => c.QueueName, StringComparer.OrdinalIgnoreCase);

        public static bool TryGetByCameraName(string? cameraName, out CameraDef def)
        {
            def = null!;
            if (string.IsNullOrWhiteSpace(cameraName))
                return false;
            return ByCameraName.TryGetValue(cameraName.Trim(), out def!);
        }
    }
}
