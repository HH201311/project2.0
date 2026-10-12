namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
{
    public static class Pack6DefaultConfig
    {
        /// <summary>6 相机设计张数（合计 31 张 OK）。</summary>
        public static IReadOnlyDictionary<string, int> QueueRequirements =>
            Pack6CameraCatalog.QueueRequirements;

        public static Pack6Config Create() => new();
    }
}
