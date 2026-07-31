using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    public static class ObjectExtension
    {
        private static readonly JsonSerializerOptions _defaultOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 通过Json装换复制对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? CopyByJson<T>(this T? obj) where T : class, new()
        {
            if (obj == null) return null;

            try
            {
                var json = JsonSerializer.Serialize(obj, _defaultOptions);
                var newObj = JsonSerializer.Deserialize<T>(json, _defaultOptions);
                return newObj;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 使用MemberwiseClone进行浅拷贝，然后通过反射实现深拷贝
        /// </summary>
        public static T? DeepCopy<T>(this T? obj) where T : class, new()
        {
            if (obj == null) return null;

            var type = obj.GetType();

            // 如果是值类型或字符串，直接返回
            if (type.IsValueType || type == typeof(string))
                return obj;

            // 创建新实例
            var newObj = Activator.CreateInstance<T>();

            // 复制所有公共属性
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead && property.CanWrite)
                {
                    var value = property.GetValue(obj);

                    // 处理引用类型的深拷贝
                    if (value != null && !property.PropertyType.IsValueType && property.PropertyType != typeof(string))
                    {
                        var method = typeof(ObjectExtension).GetMethod(nameof(DeepCopy),
                            BindingFlags.Public | BindingFlags.Static);
                        var genericMethod = method.MakeGenericMethod(property.PropertyType);
                        value = genericMethod.Invoke(null, new[] { value });
                    }

                    property.SetValue(newObj, value);
                }
            }

            return newObj;
        }

        /// <summary>
        /// 深度比较两个对象是否相等（基于JSON序列化）
        /// </summary>
        public static bool EqualsByJson<T>(this T? obj, T? other) where T : class
        {
            if (ReferenceEquals(obj, other)) return true;
            if (obj == null || other == null) return false;

            var json1 = JsonSerializer.Serialize(obj, _defaultOptions);
            var json2 = JsonSerializer.Serialize(other, _defaultOptions);

            return json1 == json2;
        }
    }
}
