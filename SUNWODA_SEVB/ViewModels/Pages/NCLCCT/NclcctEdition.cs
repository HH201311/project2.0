namespace SUNWODA_SEVB.ViewModels.Pages.NCLCCT
{
    /// <summary>
    /// NCLCCT 编译形态开关（仅本项目内使用，不改 sln/库）。
    /// <c>false</c>=产线生产版；<c>true</c>=客户参观版。
    /// 编客户包时改为 <c>true</c>，打完包后务必改回 <c>false</c>，避免主干默认成参观逻辑。
    /// </summary>
    internal static class NclcctEdition
    {
        /// <summary>当前是否客户参观版编译。</summary>
        public const bool IsVisit = false;
    }
}
