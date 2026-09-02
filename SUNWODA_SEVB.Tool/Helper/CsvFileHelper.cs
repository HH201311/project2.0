using System.IO;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class CsvFileHelper
    {
        public static readonly object FileObj = new();

        public static void SaveCSVFile(string filePath, string filename, string header, string content)
        {
            try
            {
                var fullPath = Path.Combine(filePath, $"{filename}.csv");
                var fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Directory!.Exists)
                {
                    fileInfo.Directory.Create();
                }
                if (!File.Exists(fullPath))
                {
                    using var sw = new StreamWriter(fullPath, false, Encoding.UTF8);
                    sw.WriteLine(header);
                }
                File.SetAttributes(fullPath, FileAttributes.Normal);
                using (var sw = new StreamWriter(fullPath, true, Encoding.UTF8))
                {
                    sw.WriteLine(content);
                    File.SetAttributes(fullPath, FileAttributes.ReadOnly);
                }
            }
            catch
            {
                // 进程监控写 CSV 失败时不影响主流程
            }
        }
    }
}
