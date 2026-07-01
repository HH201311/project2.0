using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    /// <summary>
    /// JsonConvert 扩展方法类，提供安全的 JSON 反序列化功能。
    /// </summary>
    public static class JsonConvertExtension
    {
        /// <summary>
        /// 尝试将 JSON 字符串反序列化为指定类型的对象，如果失败则返回 false，并将结果设置为默认值。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static bool TryDeserializeObject<T>(this string json, out T? result)
        {
            try
            {
                if (string.IsNullOrEmpty(json))
                {
                    result = default;
                    return false;
                }
                result = JsonConvert.DeserializeObject<T>(json);
                return true;
            }
            catch (Exception)
            {
                result = default;
                return false;
            }
        }
    }
}
