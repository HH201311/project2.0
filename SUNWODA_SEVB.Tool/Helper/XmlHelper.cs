using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using BigInteger = Org.BouncyCastle.Math.BigInteger;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// XML帮助类
    /// </summary>
    public class XmlHelper
    {
        private static Dictionary<string, object> lockerDic = new Dictionary<string, object>();

        private static object checkLoker = new object();
        private static object checkLockerDic(string fullPath)
        {
            lock (checkLoker)
            {
                if (!lockerDic.ContainsKey(fullPath))
                    lockerDic.Add(fullPath, new object());

                return lockerDic[fullPath];
            }
        }
        #region 序列化
        /// <summary>
        /// 读取XML文件，反序列化为Model
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        public static T? XmlToModel<T>(string fullPath) where T : class, new()
        {
            fullPath += fullPath.ToLower().EndsWith(".xml") ? string.Empty : ".xml";
            var path = Path.GetDirectoryName(fullPath) ?? "";
            var fileName = Path.GetFileName(fullPath);
            return XmlToModel<T>(path, fileName);
        }

        /// <summary>
        /// 读取XML文件，反序列化为Model
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T? XmlToModel<T>(string path, string fileName) where T : class, new()
        {
            try
            {
                fileName += fileName.ToLower().EndsWith(".xml") ? string.Empty : ".xml";
                var fullPath = Path.Combine(path, fileName);
                var locker = checkLockerDic(fullPath);
                lock (locker)
                {
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    if (!File.Exists(fullPath))
                    {
                        try
                        {
                            using (var fs = File.Open(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
                            {
                                var xs = new XmlSerializer(typeof(T));
                                xs.Serialize(fs, new T());
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("创建XML文件异常！", ex);
                        }
                    }

                    using (var fs = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs))
                    {
                        var xs = new XmlSerializer(typeof(T));
                        return xs.Deserialize(sr) as T;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"读取XML文件[{path}]-[{fileName}]异常！", ex);
            }
        }

        /// <summary>
        /// Model序列化为XML文件并保存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="fullPath"></param>
        public static void ModelToXml<T>(T? obj, string fullPath) where T : class, new()
        {
            fullPath += fullPath.ToLower().EndsWith(".xml") ? string.Empty : ".xml";
            var path = Path.GetDirectoryName(fullPath) ?? "";
            var fileName = Path.GetFileName(fullPath);
            ModelToXml(obj, path, fileName);
        }

        /// <summary>
        /// Model序列化为XML文件并保存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        /// <exception cref="Exception"></exception>
        public static void ModelToXml<T>(T? obj, string path, string fileName) where T : class, new()
        {
            try
            {
                fileName += fileName.ToLower().EndsWith(".xml") ? string.Empty : ".xml";
                var fullPath = Path.Combine(path, fileName);
                var locker = checkLockerDic(fullPath);
                lock (locker)
                {
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                    // 如果使用OpenOrCreate则会在原文件基础上覆盖，因文本长度不一致导致文件格式异常
                    using (var fs = File.Open(fullPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        var xs = new XmlSerializer(typeof(T));
                        xs.Serialize(fs, obj);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"保存XML文件[{path}]-[{fileName}]异常！", ex);
            }
        }
        #endregion
    }
}
