using FluentFTP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// FTP帮助类（优化版）
    /// </summary>
    public class FTPHelper
    {
        public static void Test()
        {
            using (var client = new FtpClient("ftp://10.215.114.36/", "ftpuser", "123456"))
            //using (var client = new FtpClient("ftp://10.201.88.151/", "test001", "Gaoda123??"))
            {
                // 可选：设置端口、加密等
                client.Port = 21;
                client.ValidateCertificate += (control, e) => { e.Accept = true; }; // 忽略证书错误

                // 连接
                client.Connect();

                // 列出目录（返回详细信息）
                foreach (FtpListItem item in client.GetListing("/"))
                {
                    Console.WriteLine($"{item.Type} - {item.Name} - {item.Size}");
                }

                // 下载文件
                client.DownloadFile(@"C:\local\file.txt", "/remote/path/file.txt");

                // 上传文件
                client.UploadFile(@"C:\local\file.txt", "/remote/path/file.txt");

                // 删除文件
                client.DeleteFile("/remote/path/file.txt");

                // 重命名/移动文件
                client.Rename("/remote/old.txt", "/remote/new.txt");

                // 创建目录
                client.CreateDirectory("/remote/newdir");

                // 删除目录（递归删除需设置参数）
                client.DeleteDirectory("/remote/dir", FtpListOption.Recursive);

                // 判断文件/目录是否存在
                bool exists = client.FileExists("/remote/path/file.txt");
                bool dirExists = client.DirectoryExists("/remote/dir");

                // 断开连接
                client.Disconnect();
            }
        }




        // 基本配置
        public string FtpUser { get; }
        public string FtpPassword { get; }
        public string FtpBaseUri { get; }
        public bool IsUseSSL { get; }

        // 限速相关
        private int _sleepTimeMs;
        private int _chunkSizeBytes;
        private static readonly object SpeedLock = new object();

        /// <summary>
        /// 传输速度限制（如 "1Mbps", "2Mbps", "4Mbps", "8Mbps", "16Mbps"）
        /// </summary>
        public string Speed
        {
            set => SetSpeed(value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="user">FTP用户名</param>
        /// <param name="password">FTP密码</param>
        /// <param name="baseUri">FTP基础地址（如 ftp://example.com/）</param>
        /// <param name="isUseSSL">是否使用SSL</param>
        /// <param name="certificateValidation">自定义证书验证回调（可选，默认忽略所有证书错误）</param>
        public FTPHelper(string user, string password, string baseUri, bool isUseSSL = false,
            RemoteCertificateValidationCallback? certificateValidation = null)
        {
            FtpUser = user ?? throw new ArgumentNullException(nameof(user));
            FtpPassword = password ?? throw new ArgumentNullException(nameof(password));
            FtpBaseUri = baseUri?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUri));
            IsUseSSL = isUseSSL;

            // 设置默认速度（约1Mbps）
            SetSpeed("1Mbps");

            // 配置SSL证书验证回调
            if (isUseSSL && certificateValidation != null)
            {
                ServicePointManager.ServerCertificateValidationCallback = certificateValidation;
            }
            else if (isUseSSL)
            {
                // 默认忽略证书错误（生产环境请使用自定义验证）
                ServicePointManager.ServerCertificateValidationCallback =
                    (sender, cert, chain, errors) => true;
            }
        }

        /// <summary>
        /// 根据速度字符串设置限速参数
        /// </summary>
        private void SetSpeed(string speed)
        {
            lock (SpeedLock)
            {
                switch (speed)
                {
                    case "1Mbps":
                        _chunkSizeBytes = 1024;   // 每次写入1KB
                        _sleepTimeMs = 1;          // 每写一次休眠1ms -> 约1MB/s
                        break;
                    case "2Mbps":
                        _chunkSizeBytes = 2048;
                        _sleepTimeMs = 1;
                        break;
                    case "4Mbps":
                        _chunkSizeBytes = 4096;
                        _sleepTimeMs = 1;
                        break;
                    case "8Mbps":
                        _chunkSizeBytes = 8192;
                        _sleepTimeMs = 1;
                        break;
                    case "16Mbps":
                        _chunkSizeBytes = 16384;
                        _sleepTimeMs = 0;          // 无需休眠，全速
                        break;
                    default:
                        _chunkSizeBytes = 1024;
                        _sleepTimeMs = 1;
                        break;
                }
            }
        }

        /// <summary>
        /// 拼接FTP URI，确保路径格式正确
        /// </summary>
        private Uri BuildUri(string relativePath)
        {
            string fullPath = $"{FtpBaseUri}/{relativePath.TrimStart('/')}";
            return new Uri(fullPath);
        }

        /// <summary>
        /// 应用SSL配置到FtpWebRequest
        /// </summary>
        private void ApplySsl(FtpWebRequest request, bool? useSsl = null)
        {
            if (useSsl ?? IsUseSSL)
            {
                request.EnableSsl = true;
                // 证书验证回调已在构造函数中设置
            }
        }

        /// <summary>
        /// 创建FtpWebRequest并设置公共属性
        /// </summary>
        private FtpWebRequest CreateRequest(Uri uri, string method, bool? useSsl = null)
        {
            var request = (FtpWebRequest)WebRequest.Create(uri);
            request.Method = method;
            request.Credentials = new NetworkCredential(FtpUser, FtpPassword);
            request.KeepAlive = false;
            request.UseBinary = true;
            request.UsePassive = true;
            request.Proxy = null;
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            ApplySsl(request, useSsl);
            return request;
        }

        /// <summary>
        /// 异步上传文件
        /// </summary>
        public async Task<string> FtpFileUploadAsync(string localFilePath, string remoteDirectory, bool? useSsl = null)
        {
            return await Task.Run(() => FtpFileUpload(localFilePath, remoteDirectory, useSsl));
        }

        /// <summary>
        /// 上传文件到FTP
        /// </summary>
        /// <param name="localFilePath">本地文件完整路径</param>
        /// <param name="remoteDirectory">远程目录（相对于FTP根目录）</param>
        /// <param name="useSsl">是否使用SSL，null则使用构造函数设置</param>
        /// <returns>服务器响应信息</returns>
        public string FtpFileUpload(string localFilePath, string remoteDirectory, bool? useSsl = null)
        {
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException($"本地文件不存在: {localFilePath}");

            try
            {
                var fileInfo = new FileInfo(localFilePath);
                // 构建远程文件URI（目录+文件名）
                string remoteFileName = fileInfo.Name;
                string remotePath = $"{remoteDirectory.TrimEnd('/')}/{remoteFileName}";
                Uri serverUri = BuildUri(remotePath);

                var request = CreateRequest(serverUri, WebRequestMethods.Ftp.UploadFile, useSsl);
                request.ContentLength = fileInfo.Length;

                using (var requestStream = request.GetRequestStream())
                using (var fileStream = fileInfo.OpenRead())
                {
                    byte[] buffer = new byte[_chunkSizeBytes];
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        requestStream.Write(buffer, 0, bytesRead);
                        if (_sleepTimeMs > 0)
                        {
                            Thread.Sleep(_sleepTimeMs);
                        }
                    }
                }

                using (var response = (FtpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"[{localFilePath}] 文件上传FTP失败", ex);
            }
        }

        /// <summary>
        /// 创建单个目录
        /// </summary>
        private string MakeOneDir(string remoteDirectory, bool? useSsl = null)
        {
            try
            {
                Uri uri = BuildUri(remoteDirectory);
                var request = CreateRequest(uri, WebRequestMethods.Ftp.MakeDirectory, useSsl);

                using (var response = (FtpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"[{remoteDirectory}] 创建FTP目录失败", ex);
            }
        }

        /// <summary>
        /// 获取指定目录下的所有文件和子目录名称
        /// </summary>
        public List<string> GetAllList(string remoteDirectory, bool? useSsl = null)
        {
            var list = new List<string>();
            try
            {
                var uri = BuildUri(remoteDirectory);
                var request = CreateRequest(uri, WebRequestMethods.Ftp.ListDirectory, useSsl);

                using var response = (FtpWebResponse)request.GetResponse();
                using var reader = new StreamReader(response.GetResponseStream());
                var line = string.Empty;
                while ((line = reader.ReadLine()) != null) list.Add(line);
                return list;
            }
            catch (Exception ex)
            {
                throw new Exception($"[{remoteDirectory}] 获取FTP目录列表失败", ex);
            }
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        public bool FtpFileExist(string remoteDirectory, string fileName, bool? useSsl = null)
        {
            var list = GetAllList(remoteDirectory, useSsl);
            return list.Any(item => item.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 检查目录是否存在
        /// </summary>
        public bool FtpDirExist(string remoteDirectory, string dirName, bool? useSsl = null)
        {
            var list = GetAllList(remoteDirectory, useSsl);
            return list.Any(item => item.Equals(dirName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 递归创建多级目录
        /// </summary>
        public bool MakeDir(string fullDirectoryPath, bool? useSsl = null)
        {
            var currentDir = string.Empty;
            try
            {
                // 分割路径，支持 '/' 和 '\'
                var parts = fullDirectoryPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var dir in parts)
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;

                    if (!FtpDirExist(currentDir, dir, useSsl))
                    {
                        currentDir = string.IsNullOrEmpty(currentDir) ? dir : $"{currentDir}/{dir}";
                        MakeOneDir(currentDir, useSsl);
                    }
                    else
                    {
                        currentDir = string.IsNullOrEmpty(currentDir) ? dir : $"{currentDir}/{dir}";
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"[{fullDirectoryPath}] 创建FTP目录结构失败", ex);
            }
        }

        /// <summary>
        /// 获取远程文件大小（字节）
        /// </summary>
        public long GetFileSize(string remoteFilePath)
        {
            try
            {
                Uri uri = BuildUri(remoteFilePath);
                var request = CreateRequest(uri, WebRequestMethods.Ftp.GetFileSize);

                using (var response = (FtpWebResponse)request.GetResponse())
                {
                    return response.ContentLength;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"[{remoteFilePath}] 获取FTP文件大小失败", ex);
            }
        }
    }
}