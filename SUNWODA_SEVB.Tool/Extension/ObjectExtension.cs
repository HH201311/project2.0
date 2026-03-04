using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Extension
{
    public static class ObjectExtension
    {
        /// <summary>
        /// 通过Json装换复制对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T? CopyByJson<T>(this T? obj) where T : class, new()
        {
            var json = JsonConvert.SerializeObject(obj);
            var newObj = JsonConvert.DeserializeObject<T>(json);
            return newObj;
        }
    }
}
