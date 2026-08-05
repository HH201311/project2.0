namespace SUNWODA_SEVB.Core.Models.Project.CTOL
{
    /// <summary>
    /// 主进程发送给 Worker 的请求（每行一条 JSON）。
    /// </summary>
    public sealed class CtWorkerRequest
    {
        /// <summary>
        /// 请求唯一标识，用于把异步返回的结果与请求对应起来。
        /// </summary>
        public string RequestId { get; set; } = "";

        /// <summary>
        /// 目标工位编号；用于工位硬隔离校验。-1 表示不校验。
        /// </summary>
        public int StationId { get; set; } = -1;

        /// <summary>
        /// 待处理图片路径。
        /// </summary>
        public string ImagePath { get; set; } = "";

        /// <summary>
        /// 结果输出目录（Worker 在此写出 recNE/recPE/recMiddlePE.hobj）。
        /// </summary>
        public string OutputDir { get; set; } = "";

        /// <summary>
        /// 控制命令，例如 "exit" 表示请求 Worker 退出。
        /// </summary>
        public string Command { get; set; } = "";
    }
}
