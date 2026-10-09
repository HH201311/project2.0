namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
{
    public static class Pack6DefaultConfig
    {
        /// <summary>10 相机设计张数（合计 22 张 OK）。</summary>
        public static IReadOnlyDictionary<string, int> QueueRequirements =>
            Pack6CameraCatalog.QueueRequirements;

        public static Pack6Config Create() => new();
    }
}
