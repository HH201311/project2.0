namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
{
    /// <summary>南京现场 10 台逻辑相机与队列映射（合计设计 22 张 OK 图）。</summary>
    public static class Pack6CameraCatalog
    {
        public sealed record CameraDef(
            string CameraName,
            string QueueName,
            string DiskHint,
            int DesignShotCount,
            string DisplayTitle);

        /// <summary>
        /// 十窗布局顺序（建议 5×2）：上盖 → 底部1/2 → 四周1～7。
        /// DiskHint：Top=Z: / Bottom=T: / Side=S:
        /// </summary>
        public static IReadOnlyList<CameraDef> Cameras { get; } =
        [
            new("上盖相机", "TopCoverQueue", "Top", 2, "上盖相机"),
            new("底部相机1", "Bottom1Queue", "Bottom", 3, "底部相机1"),
            new("底部相机2", "Bottom2Queue", "Bottom", 3, "底部相机2"),
            new("四周相机1", "Side1Queue", "Side", 1, "四周相机1"),
            new("四周相机2", "Side2Queue", "Side", 1, "四周相机2"),
            new("四周相机3", "Side3Queue", "Side", 2, "四周相机3"),
            new("四周相机4", "Side4Queue", "Side", 1, "四周相机4"),
            new("四周相机5", "Side5Queue", "Side", 4, "四周相机5"),
            new("四周相机6", "Side6Queue", "Side", 4, "四周相机6"),
            new("四周相机7", "Side7Queue", "Side", 1, "四周相机7")
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

        /// <summary>底/周盘内 Cam 号 → 逻辑相机名。</summary>
        public static bool TryMapLocalCam(string diskRole, int localCam, out CameraDef def)
        {
            def = null!;
            var name = diskRole switch
            {
                "Bottom" when localCam == 1 => "底部相机1",
                "Bottom" when localCam == 2 => "底部相机2",
                "Side" when localCam is >= 1 and <= 7 => $"四周相机{localCam}",
                _ => null
            };
            return name is not null && TryGetByCameraName(name, out def);
        }
    }
}
