namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    public static class Pack6DefaultConfig
    {
        /// <summary>12 相机设计张数（与存图结构一致）。</summary>
        public static IReadOnlyDictionary<string, int> QueueRequirements =>
            Pack6CameraCatalog.QueueRequirements;

        public static Pack6Config Create() => new();
    }
}
