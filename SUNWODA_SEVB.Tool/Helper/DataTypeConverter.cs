using Newtonsoft.Json.Linq;
using SUNWODA_SEVB.Tool.Extension;
using System.Globalization;
using System.Windows;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 数据类型转换助手
    /// </summary>
    public static class DataTypeConverter
    {
        public static string TypeToString(Type type)
        {
            return type.Name;
        }

        public static Type StringToType(string type)
        {
            type = type.ToUpper();
            return type switch
            {
                "STRING" => typeof(string),
                "FLOAT" => typeof(float),
                "DOUBLE" => typeof(double),
                "DECIMAL" => typeof(decimal),
                "SBYTE" => typeof(sbyte),
                "BYTE" => typeof(byte),
                "SHORT" => typeof(short),
                "USHORT" => typeof(ushort),
                "INT" or "INTEGER" => typeof(int),
                "UINT" => typeof(uint),
                "LONG" => typeof(long),
                "ULONG" => typeof(ulong),
                "BOOL" or "BOOLEAN" => typeof(bool),
                "DATETIME" => typeof(DateTime),
                "WINDOWSTATE" => typeof(WindowState),
                "HORIZONTALALIGNMENT" => typeof(HorizontalAlignment),
                "VERTICALALIGNMENT" => typeof(VerticalAlignment),
                _ => throw new FormatException($"不支持的数据类型: {type}"),
            };
        }

        /// <summary>
        /// 将字符串转换为指定类型的值
        /// </summary>
        public static dynamic StringToValue(string type, string value)
        {
            type = type.ToUpper();
            return type switch
            {
                "STRING" => value,
                "FLOAT" => value.ToFloat(), // 推荐使用double处理小数（计算机处理float的精度问题）
                "DOUBLE" => value.ToDouble(),
                "DECIMAL" => value.ToDecimal(), // 指针只读
                "SBYTE" => value.ToSByte(),
                "BYTE" => value.ToByte(),
                "SHORT" => value.ToShort(),
                "USHORT" => value.ToUShort(),
                "INT" or "INTEGER" => value.ToInt(),
                "UINT" => value.ToUInt(),
                "LONG" => value.ToLong(),
                "ULONG" => value.ToULong(),
                "BOOL" or "BOOLEAN" => value.ToBool(),
                "DATETIME" => value.ToDateTime("yyyy-MM-dd HH:mm:ss.FFF"),
                "WINDOWSTATE" => value.ToEnum<WindowState>(),
                "HORIZONTALALIGNMENT" => value.ToEnum<HorizontalAlignment>(),
                "VERTICALALIGNMENT" => value.ToEnum<VerticalAlignment>(),
                _ => throw new FormatException($"不支持的数据类型: {type}"),
            };
        }

        /// <summary>
        /// 将值转换为SQL存储的字符串
        /// </summary>
        public static string ValueToString(dynamic value)
        {
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 按类型规范化为数据库字符串（统一格式，避免再次被误判）
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ValueToString(string type, object? value)
        {
            if (value is null) return string.Empty;

            var t = (type ?? string.Empty).ToUpperInvariant();

            switch (t)
            {
                case "STRING":
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

                case "BOOL":
                case "BOOLEAN":
                    {
                        var b = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        return b ? "true" : "false"; // 统一小写
                    }

                case "FLOAT":
                case "DOUBLE":
                    {
                        // 用 Round-trip 格式，避免 1 与 1.0 的抖动，且使用 InvariantCulture
                        double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        return d.ToString("R", CultureInfo.InvariantCulture);
                    }

                case "DECIMAL":
                    {
                        decimal m = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                        return m.ToString(CultureInfo.InvariantCulture);
                    }

                case "SBYTE":
                case "BYTE":
                case "SHORT":
                case "USHORT":
                case "INT":
                case "INTEGER":
                case "UINT":
                case "LONG":
                case "ULONG":
                    {
                        // 统一使用 InvariantCulture，避免本地化影响
                        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                case "DATETIME":
                    {
                        var dt = value is DateTime d
                            ? d
                            : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                        // 保持与 StringToValue 的格式一致，毫秒精度
                        return dt.ToString("yyyy-MM-dd HH:mm:ss.FFF", CultureInfo.InvariantCulture);
                    }

                case "WINDOWSTATE":
                case "HORIZONTALALIGNMENT":
                case "VERTICALALIGNMENT":
                    {
                        // 按名称保存
                        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
            }

            // 默认情况的处理
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// 按类型比较是否相等（防止字符串表示差异导致的伪变化）
        /// </summary>
        /// <param name="type"></param>
        /// <param name="oldValueString"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public static bool AreEqual(string type, string? oldValueString, object? newValue)
        {
            var t = (type ?? string.Empty).ToUpperInvariant();

            // 空判断：新值为空 -> 旧值也必须是空或空白才算相等
            if (newValue is null)
                return string.IsNullOrWhiteSpace(oldValueString);

            object oldObj;
            try
            {
                oldObj = StringToValue(t, oldValueString ?? string.Empty);
            }
            catch
            {
                // 解析失败时，退化为比较“规范化字符串”
                var normNew = ValueToString(t, newValue);
                return string.Equals(oldValueString ?? string.Empty, normNew, StringComparison.Ordinal);
            }

            try
            {
                switch (t)
                {
                    case "BOOL":
                    case "BOOLEAN":
                        return Convert.ToBoolean(oldObj) == Convert.ToBoolean(newValue);

                    case "FLOAT":
                    case "DOUBLE":
                        {
                            double a = Convert.ToDouble(oldObj, CultureInfo.InvariantCulture);
                            double b = Convert.ToDouble(newValue, CultureInfo.InvariantCulture);
                            const double eps = 1e-9;
                            return Math.Abs(a - b) <= eps * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
                        }

                    case "DECIMAL":
                        {
                            decimal a = Convert.ToDecimal(oldObj, CultureInfo.InvariantCulture);
                            decimal b = Convert.ToDecimal(newValue, CultureInfo.InvariantCulture);
                            return a == b;
                        }

                    case "SBYTE":
                    case "BYTE":
                    case "SHORT":
                    case "USHORT":
                    case "INT":
                    case "INTEGER":
                    case "UINT":
                    case "LONG":
                    case "ULONG":
                        {
                            // 用 decimal 比较整数，避免溢出
                            decimal a = Convert.ToDecimal(oldObj, CultureInfo.InvariantCulture);
                            decimal b = Convert.ToDecimal(newValue, CultureInfo.InvariantCulture);
                            return a == b;
                        }

                    case "DATETIME":
                        {
                            DateTime a = (oldObj is DateTime oa)
                                ? oa
                                : Convert.ToDateTime(oldObj, CultureInfo.InvariantCulture);
                            DateTime b = (newValue is DateTime ob)
                                ? ob
                                : Convert.ToDateTime(newValue, CultureInfo.InvariantCulture);

                            // 存储精度为毫秒，比较时对齐到毫秒，避免 Tick 差异
                            a = new DateTime(a.Ticks - (a.Ticks % TimeSpan.TicksPerMillisecond), a.Kind);
                            b = new DateTime(b.Ticks - (b.Ticks % TimeSpan.TicksPerMillisecond), b.Kind);
                            return a == b;
                        }

                    case "WINDOWSTATE":
                    case "HORIZONTALALIGNMENT":
                    case "VERTICALALIGNMENT":
                        // 这些是枚举，按值比较
                        return Equals(oldObj, newValue);

                    case "STRING":
                        return string.Equals(
                            Convert.ToString(oldObj) ?? string.Empty,
                            Convert.ToString(newValue) ?? string.Empty,
                            StringComparison.Ordinal
                        );

                    default:
                        return Equals(oldObj, newValue);
                }
            }
            catch
            {
                // 任意一步失败则回落到“规范化字符串”比较
                var normNew = ValueToString(t, newValue);
                return string.Equals(oldValueString ?? string.Empty, normNew, StringComparison.Ordinal);
            }
        }
    }
}
