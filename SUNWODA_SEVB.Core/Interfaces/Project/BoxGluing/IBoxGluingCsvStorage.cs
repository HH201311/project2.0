using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing
{
    public interface IBoxGluingCsvStorage
    {
        /// <summary>按出胶工位入队，写入对应 CSV 文件。</summary>
        Task EnqueueAsync(int station, BoxGluingSampleRecord record, IReadOnlyList<string> headerLabels);

        Task FlushAsync();

        /// <summary>
        /// 指定出胶一段结束后先刷盘，再在该文件末尾追加空行。
        /// </summary>
        Task WriteSegmentBreakAsync(int station);
    }
}
