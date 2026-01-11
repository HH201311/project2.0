
namespace SUNWODA_SEVB.Core.Models.Project.STC
{
    /// <summary>
    /// 控制器详细信息
    /// </summary>
    public class ScrewControllerDetail
    {
        /// <summary>
        /// 控制器名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 控制器IP地址
        /// </summary>
        public string IP { get; set; } = string.Empty;

        /// <summary>
        /// 配置来源
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 是否在线
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// 关联的螺丝枪列表
        /// </summary>
        public string TightenGuns { get; set; } = string.Empty;
    }

}
