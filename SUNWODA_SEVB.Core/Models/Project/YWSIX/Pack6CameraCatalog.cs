namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
{
    /// <summary>
    /// 义乌现场 6 台逻辑相机与队列映射（合计设计 31 张 OK 图）。
    /// 目录：{根}\{日期}\{SN}\{相机名}\原始图像\OK-{相机名}-{张序}-{时间戳}-{SN}.jpg
    /// </summary>
    public static class Pack6CameraCatalog
    {
        public sealed record CameraDef(
            string CameraName,
            string QueueName,
            string DiskHint,
            int DesignShotCount,
            string DisplayTitle);

        /// <summary>
        /// 六窗布局顺序（3×2）：顶面 → 前/后 → 左/右 → 底部。
        /// </summary>
        public static IReadOnlyList<CameraDef> Cameras { get; } =
        [
            new("顶面相机", "TopQueue", "Top", 6, "顶面相机"),
            new("前侧相机", "FrontQueue", "Side", 7, "前侧相机"),
            new("后侧相机", "RearQueue", "Side", 6, "后侧相机"),
            new("左侧相机", "LeftQueue", "Side", 3, "左侧相机"),
            new("右侧相机", "RightQueue", "Side", 4, "右侧相机"),
            new("底部相机", "BottomQueue", "Bottom", 5, "底部相机")
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
