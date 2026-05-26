using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// CSV 文件读写帮助类
    /// </summary>
    public static class CsvSaveHelper
    {
        private const char DefaultDelimiter = ',';
        private const char Quote = '"';

        public static object FileObj { get; set; } = new object();

        #region 写入操作

        /// <summary>
        /// 将对象列表写入 CSV 文件
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">数据列表</param>
        /// <param name="includeHeader">是否包含表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="encoding">编码（默认 UTF-8）</param>
        public static void WriteToFile<T>(
            string filePath,
            IEnumerable<T> data,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var content = ConvertToCsv(data, includeHeader, delimiter);
            File.WriteAllText(filePath, content, encoding);
        }

        /// <summary>
        /// 将对象列表异步写入 CSV 文件
        /// </summary>
        public static async Task WriteToFileAsync<T>(
            string filePath,
            IEnumerable<T> data,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null,
            CancellationToken cancellationToken = default)
        {
            encoding ??= Encoding.UTF8;
            var content = ConvertToCsv(data, includeHeader, delimiter);
            await File.WriteAllTextAsync(filePath, content, encoding, cancellationToken);
        }

        /// <summary>
        /// 将 DataTable 写入 CSV 文件
        /// </summary>
        public static void WriteToFile(
            string filePath,
            DataTable dataTable,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var content = ConvertToCsv(dataTable, includeHeader, delimiter);
            File.WriteAllText(filePath, content, encoding);
        }


        public static async Task WriteToFileAsync(
            string filePath,
            DataTable dataTable,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null,
            CancellationToken cancellationToken = default)
        {
            encoding ??= Encoding.UTF8;
            var content = ConvertToCsv(dataTable, includeHeader, delimiter);
            await File.WriteAllTextAsync(filePath, content, encoding, cancellationToken);
        }

        /// <summary>
        /// 将对象列表转换为 CSV 字符串
        /// </summary>
        public static string ConvertToCsv<T>(
            IEnumerable<T> data,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter)
        {
            var sb = new StringBuilder();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 写入表头
            if (includeHeader)
            {
                var headers = properties.Select(p => EscapeField(p.Name, delimiter));
                sb.AppendLine(string.Join(delimiter, headers));
            }

            // 写入数据行
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                {
                    var value = p.GetValue(item);
                    return EscapeField(value?.ToString() ?? string.Empty, delimiter);
                });
                sb.AppendLine(string.Join(delimiter, values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将 DataTable 转换为 CSV 字符串
        /// </summary>
        public static string ConvertToCsv(
            DataTable dataTable,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter)
        {
            var sb = new StringBuilder();

            // 写入表头
            if (includeHeader)
            {
                var headers = dataTable.Columns.Cast<DataColumn>()
                    .Select(c => EscapeField(c.ColumnName, delimiter));
                sb.AppendLine(string.Join(delimiter, headers));
            }

            // 写入数据行
            foreach (DataRow row in dataTable.Rows)
            {
                var values = row.ItemArray.Select(v =>
                    EscapeField(v?.ToString() ?? string.Empty, delimiter));
                sb.AppendLine(string.Join(delimiter, values));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 保存 CSV 文件（适合追加写入，自动处理表头和文件创建）
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <param name="filename"></param>
        /// <param name="header"></param>
        /// <param name="content"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void SaveCSVFile(string directoryPath, string filename, string header, string content)
        {
            if (string.IsNullOrEmpty(filename))
                throw new ArgumentNullException(nameof(filename));

            // 1. 确保目录存在
            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            // 2. 处理 .csv 后缀
            if (!filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                filename += ".csv";

            string fullPath = Path.Combine(directoryPath, filename);

            // 3. 判断是否新文件，一次性写入（避免多次打开文件）
            bool fileExists = File.Exists(fullPath);
            using var sw = new StreamWriter(fullPath, true, Encoding.UTF8);
            // 如果是新建文件，且表头不为空，先写表头
            if (!fileExists && !string.IsNullOrEmpty(header))
            {
                sw.WriteLine(header);
            }

            // 写入正文内容（允许content为null或空，此时写入空行，可根据需求调整）
            sw.WriteLine(content ?? string.Empty);
        }

        #endregion

        #region 读取操作

        /// <summary>
        /// 从 CSV 文件读取到对象列表
        /// </summary>
        /// <typeparam name="T">目标类型（需要有无参构造函数）</typeparam>
        /// <param name="filePath">文件路径</param>
        /// <param name="hasHeader">是否有表头</param>
        /// <param name="delimiter">分隔符</param>
        /// <param name="encoding">编码（默认 UTF-8）</param>
        /// <returns>对象列表</returns>
        public static List<T> ReadFromFile<T>(
            string filePath,
            bool hasHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null) where T : new()
        {
            encoding ??= Encoding.UTF8;
            var content = File.ReadAllText(filePath, encoding);
            return ParseCsv<T>(content, hasHeader, delimiter);
        }

        /// <summary>
        /// 从 CSV 文件异步读取到对象列表
        /// </summary>
        public static async Task<List<T>> ReadFromFileAsync<T>(
            string filePath,
            bool hasHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null,
            CancellationToken cancellationToken = default) where T : new()
        {
            encoding ??= Encoding.UTF8;
            var content = await File.ReadAllTextAsync(filePath, encoding, cancellationToken);
            return ParseCsv<T>(content, hasHeader, delimiter);
        }

        /// <summary>
        /// 从 CSV 文件读取到 DataTable
        /// </summary>
        public static DataTable ReadToDataTable(
            string filePath,
            bool hasHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var content = File.ReadAllText(filePath, encoding);
            return ParseToDataTable(content, hasHeader, delimiter);
        }

        /// <summary>
        /// 从 CSV 字符串解析到对象列表
        /// </summary>
        public static List<T> ParseCsv<T>(
            string csvContent,
            bool hasHeader = true,
            char delimiter = DefaultDelimiter) where T : new()
        {
            var result = new List<T>();
            var lines = ParseLines(csvContent);

            if (lines.Count == 0)
                return result;

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            string[]? headers = null;

            var startIndex = 0;
            if (hasHeader && lines.Count > 0)
            {
                headers = ParseLine(lines[0], delimiter);
                startIndex = 1;
            }

            for (var i = startIndex; i < lines.Count; i++)
            {
                var values = ParseLine(lines[i], delimiter);
                if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                    continue;

                var obj = new T();

                for (var j = 0; j < values.Length; j++)
                {
                    PropertyInfo? prop = null;

                    if (headers != null && j < headers.Length)
                    {
                        // 按表头名称匹配属性
                        prop = properties.FirstOrDefault(p =>
                            p.Name.Equals(headers[j], StringComparison.OrdinalIgnoreCase));
                    }

                    prop ??= j < properties.Length ? properties[j] : null;

                    if (prop != null && prop.CanWrite)
                    {
                        try
                        {
                            var convertedValue = ConvertValue(values[j], prop.PropertyType);
                            prop.SetValue(obj, convertedValue);
                        }
                        catch
                        {
                            // 转换失败时保持默认值
                        }
                    }
                }

                result.Add(obj);
            }

            return result;
        }

        /// <summary>
        /// 从 CSV 字符串解析到 DataTable
        /// </summary>
        public static DataTable ParseToDataTable(
            string csvContent,
            bool hasHeader = true,
            char delimiter = DefaultDelimiter)
        {
            var dataTable = new DataTable();
            var lines = ParseLines(csvContent);

            if (lines.Count == 0)
                return dataTable;

            var firstLine = ParseLine(lines[0], delimiter);
            var columnCount = firstLine.Length;

            // 创建列
            if (hasHeader)
            {
                foreach (var header in firstLine)
                {
                    dataTable.Columns.Add(header);
                }
            }
            else
            {
                for (var i = 0; i < columnCount; i++)
                {
                    dataTable.Columns.Add($"Column{i + 1}");
                }
            }

            // 添加数据行
            var startIndex = hasHeader ? 1 : 0;
            for (var i = startIndex; i < lines.Count; i++)
            {
                var values = ParseLine(lines[i], delimiter);
                if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                    continue;

                var row = dataTable.NewRow();
                for (var j = 0; j < Math.Min(values.Length, columnCount); j++)
                {
                    row[j] = values[j];
                }
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }

        /// <summary>
        /// 读取 CSV 文件到字典列表（每行一个字典，键为列名）
        /// </summary>
        public static List<Dictionary<string, string>> ReadToDictionaries(
            string filePath,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            var content = File.ReadAllText(filePath, encoding);
            return ParseToDictionaries(content, delimiter);
        }

        /// <summary>
        /// 从 CSV 字符串解析到字典列表
        /// </summary>
        public static List<Dictionary<string, string>> ParseToDictionaries(
            string csvContent,
            char delimiter = DefaultDelimiter)
        {
            var result = new List<Dictionary<string, string>>();
            var lines = ParseLines(csvContent);

            if (lines.Count < 2)
                return result;

            var headers = ParseLine(lines[0], delimiter);

            for (var i = 1; i < lines.Count; i++)
            {
                var values = ParseLine(lines[i], delimiter);
                if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                    continue;

                var dict = new Dictionary<string, string>();
                for (var j = 0; j < headers.Length; j++)
                {
                    dict[headers[j]] = j < values.Length ? values[j] : string.Empty;
                }
                result.Add(dict);
            }

            return result;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 转义字段值（处理逗号、引号、换行符等特殊字符）
        /// </summary>
        private static string EscapeField(string field, char delimiter)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            var needsQuoting = field.Contains(delimiter) ||
                              field.Contains(Quote) ||
                              field.Contains('\n') ||
                              field.Contains('\r');

            if (needsQuoting)
            {
                // 将引号替换为两个引号，并用引号包围整个字段
                return $"{Quote}{field.Replace(Quote.ToString(), $"{Quote}{Quote}")}{Quote}";
            }

            return field;
        }

        /// <summary>
        /// 解析 CSV 内容的所有行
        /// </summary>
        private static List<string> ParseLines(string content)
        {
            var lines = new List<string>();
            var currentLine = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                if (c == Quote)
                {
                    inQuotes = !inQuotes;
                    currentLine.Append(c);
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                    // 处理 \r\n
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        /// <summary>
        /// 解析单行 CSV 数据
        /// </summary>
        private static string[] ParseLine(string line, char delimiter)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == Quote)
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == Quote)
                    {
                        // 转义的引号
                        currentField.Append(Quote);
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());

            return fields.ToArray();
        }

        /// <summary>
        /// 将字符串值转换为指定类型
        /// </summary>
        private static object? ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // 处理可空类型
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                targetType = underlyingType;
            }

            // 常见类型转换
            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(int))
                return int.Parse(value);
            if (targetType == typeof(long))
                return long.Parse(value);
            if (targetType == typeof(decimal))
                return decimal.Parse(value);
            if (targetType == typeof(double))
                return double.Parse(value);
            if (targetType == typeof(float))
                return float.Parse(value);
            if (targetType == typeof(bool))
                return bool.Parse(value);
            if (targetType == typeof(DateTime))
                return DateTime.Parse(value);
            if (targetType == typeof(DateOnly))
                return DateOnly.Parse(value);
            if (targetType == typeof(TimeOnly))
                return TimeOnly.Parse(value);
            if (targetType == typeof(Guid))
                return Guid.Parse(value);
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, true);

            // 使用通用转换
            return Convert.ChangeType(value, targetType);
        }

        #endregion

        // 流式写入

        public static void WriteStreaming<T>(
            string filePath,
            IEnumerable<T> data,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;

            using var writer = new StreamWriter(filePath, false, encoding);

            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // 写入表头
            if (includeHeader)
            {
                var headerLine = string.Join(delimiter,
                    properties.Select(p => EscapeField(p.Name, delimiter)));
                writer.WriteLine(headerLine);
            }

            // 缓冲写入（平衡性能和内存）
            var buffer = new StringBuilder();
            const int bufferSize = 1000; // 每1000行写入一次

            var count = 0;
            foreach (var item in data)
            {
                var values = properties.Select(p =>
                    EscapeField(p.GetValue(item)?.ToString() ?? string.Empty, delimiter));
                buffer.AppendLine(string.Join(delimiter, values));

                if (++count % bufferSize == 0)
                {
                    writer.Write(buffer.ToString());
                    buffer.Clear();
                }
            }

            // 写入剩余数据
            if (buffer.Length > 0)
            {
                writer.Write(buffer.ToString());
            }
        }

        // 智能选择方法
        public static void WriteToFileSmart<T>(
            string filePath,
            IEnumerable<T> data,
            bool includeHeader = true,
            char delimiter = DefaultDelimiter,
            Encoding? encoding = null,
            int threshold = 10000) // 默认阈值：10000行
        {
            // 如果是集合类型，可以快速获取数量
            if (data is ICollection<T> collection && collection.Count <= threshold)
            {
                // 使用内存方式
                var content = ConvertToCsv(data, includeHeader, delimiter);
                File.WriteAllText(filePath, content, encoding ?? Encoding.UTF8);
            }
            else
            {
                // 使用流式方式
                WriteStreaming(filePath, data, includeHeader, delimiter, encoding);
            }
        }





    }
}
