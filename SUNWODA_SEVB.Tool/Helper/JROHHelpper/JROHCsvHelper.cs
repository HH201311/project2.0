using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace SUNWODA_SEVB.Tool.Helper.JROHHelper
{
    public class JROHCsvHelper
    {
        public static readonly object WriteLocker = new();

        public static string WriteCSV(string fullPath, DataTable data)
        {
            // 移除路径中的不可见控制字符（如 ASCII 0）
            fullPath = new string(fullPath.Where(ch => !char.IsControl(ch)).ToArray());

            if (!fullPath.ToLower().EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) fullPath = Path.ChangeExtension(fullPath, ".csv");

            if (data == null || data.Rows.Count == 0) return fullPath;

            var filePath = Path.GetDirectoryName(fullPath);
            if (filePath == null) throw new ArgumentNullException(nameof(fullPath), "参数不可为空！");

            // 路径不存在，则创建
            if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

            // 文件超过100MB，则创建新文件
            MoveLargerFileToCache(fullPath);

            // 不存在文件，则直接写入
            if (!File.Exists(fullPath))
            {
                WriteNewFile(fullPath, data);
                return fullPath;
            }

            // 检查是否需要更新列头
            var needAddNewColumn = IsNeedAddNewColumn(fullPath, data, out List<string> newHeaders);
            if (needAddNewColumn)
            {
                // 需要更新列，必须重写文件（流式重写，低内存模式）
                RewriteFileWithNewColumns(fullPath, data, newHeaders);
                return fullPath;
            }

            // 不需要更新列，直接追加（高效模式）
            AppendLines(fullPath, data, newHeaders);
            return fullPath;
        }

        private static bool IsNeedAddNewColumn(string fullPath, DataTable data, out List<string> newHeaders)
        {
            var headerLine = string.Empty;
            using (var fs = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    headerLine = sr.ReadLine();
                }
            }

            // 现有文件表头
            var fileHeaders = headerLine?.Split(',').ToList() ?? new List<string>();
            // 新增数据表头
            var dataHeaders = data.Columns.Cast<DataColumn>().Select(col => col.ColumnName).ToList();
            // 新表头
            newHeaders = new List<string>(fileHeaders);
            var isChanged = false;
            foreach (var header in dataHeaders)
            {
                if (!newHeaders.Contains(header))
                {
                    newHeaders.Add(header);
                    isChanged = true;
                }
            }

            return isChanged;
        }

        private static void WriteNewFile(string fullPath, DataTable data)
        {
            // 获取列名
            var headers = data.Columns.Cast<DataColumn>().Select(col => col.ColumnName).ToList();

            using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Encoding.UTF8))
            {
                // 写表头
                sw.WriteLine(string.Join(",", headers));
                // 写内容
                foreach (DataRow row in data.Rows)
                {
                    var line = BuildCsvLine(row, headers);
                    sw.WriteLine(line);
                }
            }
        }

        private static void AppendLines(string fullPath, DataTable data, List<string> headers)
        {
            using (var fs = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Encoding.UTF8))
            {
                foreach (DataRow row in data.Rows)
                {
                    var line = BuildCsvLine(row, headers);
                    sw.WriteLine(line);
                }
            }
        }

        private static void RewriteFileWithNewColumns(string fullPath, DataTable newData, List<string> newHeaders)
        {
            var tempPath = fullPath + ".tmp";

            // 1. 读取旧文件，转换格式写入临时文件
            using (var fsRead = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fsRead, Encoding.UTF8))
            using (var fsWrite = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fsWrite, Encoding.UTF8))
            {
                // 1.1 读取旧表头
                var oldHeaderLine = sr.ReadLine();
                var oldHeaders = oldHeaderLine?.Split(',').ToList() ?? new List<string>();

                // 1.2 写入新表头
                sw.WriteLine(string.Join(",", newHeaders));

                // 1.3 逐行读取旧数据并映射到新列结构
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    var values = line.Split(',');
                    sw.WriteLine(line + new string(',', newHeaders.Count - oldHeaders.Count));
                }

                // 1.4 写入新数据
                foreach (DataRow row in newData.Rows)
                {
                    var newLine = BuildCsvLine(row, newHeaders);
                    sw.WriteLine(newLine);
                }
            }

            // 2. 替换原文件
            File.Delete(fullPath);
            File.Move(tempPath, fullPath);
        }

        private static string BuildCsvLine(DataRow row, List<string> headers)
        {
            var values = new string[headers.Count];
            for (int i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                var val = row.Table.Columns.Contains(header) ? row[header]?.ToString() : "";
                values[i] = EscapeCsvString(val);
            }
            return string.Join(",", values);
        }

        private static void MoveLargerFileToCache(string sourceFilePath, int maxFileSize = 100 * 1024 * 1024)
        {
            if (!File.Exists(sourceFilePath)) return;
            var sourcelFileInfo = new FileInfo(sourceFilePath);
            if (sourcelFileInfo.Length < maxFileSize) return;

            var index = 0;
            var destFilePath = string.Empty;
            do
            {
                destFilePath = sourceFilePath + $".cache{index++}";

            } while (File.Exists(destFilePath));

            File.Move(sourceFilePath, destFilePath);
            File.Delete(sourceFilePath);
        }

        /// <summary>
        /// CSV 字段转义：若字段包含逗号、引号或换行等，则用双引号包裹，并将内部的引号转义为 ""
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string EscapeCsvString(string? value)
        {
            if (value == null) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) > -1)
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }

        public static DataTable DicToTable(params Dictionary<string, string>[] dics)
        {
            DataTable result = new DataTable();

            // 1. 检查列表是否为空
            if (dics == null || dics.Length == 0)
            {
                return result;
            }

            // 2. 获取所有唯一的 Key (列名)
            // 使用 SelectMany 将所有字典的 Keys 扁平化，然后用 Distinct 去重
            var columnNames = dics.SelectMany(dict => dict.Keys).Distinct().ToList();

            // 3. 创建 DataTable 的列
            foreach (var colName in columnNames)
            {
                // 因为源数据是 string，所以这里列类型设为 typeof(string)
                result.Columns.Add(colName, typeof(string));
            }

            // 4. 填充数据行
            foreach (var dict in dics)
            {
                DataRow row = result.NewRow();

                foreach (var colName in columnNames)
                {
                    // 如果字典包含该列名，则赋值；否则赋值为 DBNull
                    if (dict.TryGetValue(colName, out string? value))
                    {
                        row[colName] = value;
                    }
                    else
                    {
                        row[colName] = DBNull.Value;
                    }
                }

                result.Rows.Add(row);
            }

            return result;
        }

        /// <summary>
        /// 安全读取CSV并按条件查找数据
        /// </summary>
        public static List<Dictionary<string, string>> SearchCsv(string filePath, string searchColumn, string searchValue, params string[] columnsToSelect)
        {
            var results = new List<Dictionary<string, string>>();

            if (!filePath.ToLower().EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) filePath = Path.ChangeExtension(filePath, ".csv");

            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV文件不存在: {filePath}");
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,           // 有表头
                MissingFieldFound = null,         // 忽略缺失字段
                BadDataFound = null,              // 忽略错误数据
                TrimOptions = TrimOptions.Trim,   // 去除空格
                IgnoreBlankLines = true,          // 忽略空行
            };

            // 使用 FileStream 并指定 FileShare.ReadWrite 允许其他进程读写
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fileStream);
            using var csv = new CsvReader(reader, config);

            // 读取所有记录
            var records = csv.GetRecords<dynamic>();

            foreach (var record in records)
            {
                var row = (IDictionary<string, object>)record;

                // 安全获取搜索列的值
                if (row.TryGetValue(searchColumn, out var value))
                {
                    // 按条件匹配（支持模糊搜索）
                    if (value?.ToString() != searchValue) continue;

                    var selectedData = new Dictionary<string, string>();
                    // 提取指定的列
                    foreach (var col in columnsToSelect)
                    {
                        if (row.TryGetValue(col, out var colValue))
                        {
                            selectedData[col] = colValue?.ToString() ?? string.Empty;
                        }
                        else
                        {
                            selectedData[col] = string.Empty;
                        }
                    }

                    results.Add(selectedData);
                }
            }

            return results;
        }
    }
}
