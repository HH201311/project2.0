namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 出胶启动信号配置（出胶N ↔ 工位N）。
    /// </summary>
    public class BoxGluingTriggerConfig
    {
        public string Label { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public int ActiveValue { get; set; } = 1;

        public int Station { get; set; }
    }
}
