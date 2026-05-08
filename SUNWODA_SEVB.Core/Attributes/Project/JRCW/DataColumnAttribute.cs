using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Attributes.Project.JRCW
{
    /// <summary>
    /// 标记数据列名，用于保存文件等
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DataColumnAttribute : Attribute
    {
        /// <summary>
        /// 列名
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// 是否忽略
        /// </summary>
        public bool IsIgnore { get; set; }

        /// <summary>
        /// 列表对象的格式化
        /// </summary>
        public string? ColumnNameFormat { get; set; }

        /// <summary>
        /// 时间格式化
        /// </summary>
        public string DataTimeFormat { get; set; } = "yyyyMMdd HH:mm:ss.fff";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="isIgnore"></param>
        public DataColumnAttribute(string columnName, bool isIgnore = false)
        {
            ColumnName = columnName;
            IsIgnore = isIgnore;
        }
    }
}
