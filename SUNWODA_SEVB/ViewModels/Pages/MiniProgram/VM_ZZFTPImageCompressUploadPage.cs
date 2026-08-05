using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FluentFTP;
using Microsoft.WindowsAPICodePack.Dialogs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Enumerations.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Models.Data;

namespace SUNWODA_SEVB.ViewModels.Pages.MiniProgram
{
    [Module("VM_ZZFTPImageCompressUploadPage", "图片压缩上传", Order = 0)]
    internal class VM_ZZFTPImageCompressUploadPage : ViewModelBase
    {
        private readonly ILoggerService<VM_ZZFTPImageCompressUploadPage> logger;
        private readonly IGlobalSettingRepository globalSettingRepository;
        private readonly IProjectSettingRepository projectSettingRepository;
        private readonly IModuleManager moduleManager;

        /// <summary>
        /// 最多同时处理3个目录，过多可能会导致FTP连接数过多而被服务器拒绝
        /// </summary>
        private readonly int maxConcurrency = 3;

        /// <summary>
        /// 断线或其他异常时的重试次数，过多可能会导致长时间卡在某个目录
        /// </summary>
        private readonly int retryCount = 5;

        /// <summary>
        /// 全局连接阀门---最多5个FTP连接
        /// </summary>
        private readonly SemaphoreSlim ftpConnectionSemaphore = new(5);

        private SemaphoreSlim? semaphore;

        // 日志集合：用Tuple<string, string>存储（Item1=时间戳，Item2=内容）        
        public ObservableCollection<Tuple<string, string>> LogTable { get; set; } = [];

        /// <summary>
        /// 模式枚举：FTP或本地
        /// </summary>
        public enum CompressMode
        {
            None,
            Ftp,
            Local
        }

        private CompressMode currentMode = CompressMode.None;
        /// <summary>
        /// 当前选择的模式：FTP或本地
        /// </summary>
        public CompressMode CurrentMode
        {
            get => currentMode;
            set
            {
                if (SetProperty(ref currentMode, value))
                {
                    OnPropertyChanged(nameof(IsFtpMode));
                    OnPropertyChanged(nameof(IsLocalMode));
                    OnPropertyChanged(nameof(IsModeSelected));
                }
            }
        }

        // 是否选中FTP
        public bool IsFtpMode => CurrentMode == CompressMode.Ftp;

        // 是否选中本地
        public bool IsLocalMode => CurrentMode == CompressMode.Local;

        // 是否已选择模式（用于按钮启用）
        public bool IsModeSelected => CurrentMode != CompressMode.None;

        private bool isCompressTif = false;
        /// <summary>
        /// 是否压缩TIF文件
        /// </summary>
        public bool IsCompressTif
        {
            get => isCompressTif;
            set => SetProperty(ref isCompressTif, value);
        }

        private bool isFileSizeLimit = false;
        /// <summary>
        /// 是否限制文件大小
        /// </summary>
        public bool IsFileSizeLimit
        {
            get => isFileSizeLimit;
            set
            {
                if (SetProperty(ref isFileSizeLimit, value))
                {
                    if (value && !IsCompressTif)
                    {
                        // 自动纠正
                        IsCompressTif = true;
                    }
                }
            }
        }

        private int fileSizeLimit = 2;
        /// <summary>
        /// 文件大小限制（单位：M）
        /// </summary>
        public int FileSizeLimit
        {
            get => fileSizeLimit;
            set
            {
                if (value <= 0)
                    value = 2;

                SetProperty(ref fileSizeLimit, value);
            }
        }

        private string ftpPath = string.Empty;
        /// <summary>
        /// FTP路径
        /// </summary>
        public string FtpPath
        {
            get => ftpPath;
            set => SetProperty(ref ftpPath, value);
        }

        private bool isDeleteImage = false;
        /// <summary>
        /// 是否删除图
        /// </summary>
        public bool IsDeleteImage
        {
            get => isDeleteImage;
            set => SetProperty(ref isDeleteImage, value);
        }

        public ObservableCollection<FtpNode> FtpTree { get; set; } = [];

        private int totalFiles = 0;

        public int TotalFiles
        {
            get => totalFiles;
            set => SetProperty(ref totalFiles, value);
        }

        private int processedFiles = 0;
        public int ProcessedFiles
        {
            get => processedFiles;
            set => SetProperty(ref processedFiles, value);
        }

        // 进度百分比
        private double progress = 0;
        public double Progress
        {
            get => progress;
            set => SetProperty(ref progress, value);
        }


        private string exportFolderPath = string.Empty;
        /// <summary>
        /// 导出文件夹路径（本地模式）
        /// </summary>
        public string ExportFolderPath
        {
            get => exportFolderPath;
            set => SetProperty(ref exportFolderPath, value);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public VM_ZZFTPImageCompressUploadPage
        (
            ILoggerService<VM_ZZFTPImageCompressUploadPage> logger,
            IGlobalSettingRepository globalSettingRepository,
            IProjectSettingRepository projectSettingRepository,
            IModuleManager moduleManager
        )
        {
            this.logger = logger;
            this.globalSettingRepository = globalSettingRepository;
            this.projectSettingRepository = projectSettingRepository;
            this.moduleManager = moduleManager;
        }

        /// <summary>
        /// 初始化项目字段
        /// </summary>
        public override void OnInitialize()
        {
            EnsureSetting("FTPHost", "ftp://10.182.5.249/", "FTP路径");
            EnsureSetting("FTPUser", "ftpadmin", "FTP用户名");
            EnsureSetting("FTPPassWord", "dJE230kfco540wdkiadmin", "FTP密码");

            base.OnInitialize();
        }

        /// <summary>
        /// 模式选择命令：FTP
        /// </summary>
        public ICommand SelectFtpModeCommand => new RelayCommand(async () =>
        {
            CurrentMode = CompressMode.Ftp;
            AddLog("已选择：FTP模式");
            await InitFtpTreeAsync();
        });

        /// <summary>
        /// 模式选择命令：本地
        /// </summary>
        public ICommand SelectLocalModeCommand => new RelayCommand(() =>
        {
            CurrentMode = CompressMode.Local;
            AddLog("已选择：本地模式");
        });

        /// <summary>
        /// 开始处理命令
        /// </summary>
        public ICommand StartCommand => new RelayCommand(async () =>
        {
            if (CurrentMode == CompressMode.None)
            {
                AddLog("请先选择模式");
                return;
            }

            if (CurrentMode == CompressMode.Ftp)
            {
                if (string.IsNullOrWhiteSpace(FtpPath))
                {
                    AddLog("FTP路径不能为空");
                    return;
                }

                AddLog($"开始处理FTP路径: {FtpPath}");
                await Run(FtpPath);
            }
            else if (CurrentMode == CompressMode.Local)
            {
                if (string.IsNullOrWhiteSpace(ExportFolderPath))
                {
                    AddLog("未选择导出文件夹路径", CoreLogLevel.Warn);
                    return;
                }
                await RunLocalMode();
            }
        });

        /// <summary>
        /// 选择导出文件夹命令（本地模式）
        /// </summary>
        public ICommand ExportFolder => new RelayCommand(() =>
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                ExportFolderPath = dialog.FileName;
                AddLog($"选择导出文件夹路径: {ExportFolderPath}");
            }
            else
            {
                AddLog("取消选择导出文件夹路径");
            }
        });

        private async Task RunLocalMode()
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                AddLog("取消选择目录");
                return;
            }

            string selectedPath = dialog.FileName;
            FtpPath = selectedPath;

            AddLog($"本地路径：{selectedPath}");
            AddLog($"模式: {(IsCompressTif ? "TIF压缩" : "PNG压缩")}");
            AddLog($"大小过滤: {(IsFileSizeLimit ? $"≥{FileSizeLimit}MB" : "关闭")}");

            // ===== 获取所有子目录（包含自身）=====
            var allDirs = Directory
                .EnumerateDirectories(selectedPath, "*", SearchOption.AllDirectories)
                .Prepend(selectedPath)
                .ToList();

            // ===== 统计总目录数 =====          
            TotalFiles = allDirs.Count;
            ProcessedFiles = 0;
            Progress = 0;

            // ===== 按目录顺序处理 =====
            foreach (var dir in allDirs)
            {
                await ProcessLocalDirectory(dir);
            }

            AddLog("本地处理完成！");
        }


        private async Task ProcessLocalDirectory(string dirPath)
        {
            var files = Directory.EnumerateFiles(dirPath)
                .Where(f => IsTargetFile(f))
                .ToList();

            if (files.Count == 0)
                return;

            AddLog($"开始处理目录: {dirPath}，文件数: {files.Count}");

            foreach (var file in files)
            {
                try
                {
                    await ProcessLocalFile(file);
                }
                catch (Exception ex)
                {
                    AddLog($"处理失败: {file} - {ex.Message}");
                }

                await RunOnUIThreadAsync(() =>
                {
                    ProcessedFiles++;
                    Progress = TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
                });
            }

            AddLog($"目录处理完成: {dirPath}");

            // ===== ⭐ 关键：处理完立即移动该目录 =====
            if (!string.IsNullOrWhiteSpace(ExportFolderPath))
            {
                MoveDirectoryToExport(dirPath);
            }
        }

        private bool IsTargetFile(string file)
        {
            string ext = Path.GetExtension(file);

            // ===== 格式过滤 =====
            if (IsCompressTif)
            {
                if (!ext.Equals(".tif", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // ===== 大小过滤 =====
            if (IsFileSizeLimit)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    double sizeMB = fileInfo.Length / 1024.0 / 1024.0;

                    if (sizeMB < FileSizeLimit)
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private void MoveDirectoryToExport(string sourceDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ExportFolderPath))
                    return;

                if (!Directory.Exists(sourceDir))
                    return;

                string dirName = Path.GetFileName(sourceDir);

                string targetDir = Path.Combine(ExportFolderPath, dirName);

                // ===== 防止重名覆盖 =====
                int index = 1;
                while (Directory.Exists(targetDir))
                {
                    targetDir = Path.Combine(ExportFolderPath, $"{dirName}_{index++}");
                }

                Directory.Move(sourceDir, targetDir);

                AddLog($"目录已移动 → {targetDir}");
            }
            catch (Exception ex)
            {
                AddLog($"目录移动失败: {sourceDir} - {ex.Message}");
            }
        }

        private async Task ProcessLocalFile(string filePath)
        {
            string jpgPath = Path.Combine(
                Path.GetDirectoryName(filePath)!,
                Path.GetFileNameWithoutExtension(filePath) + "_已处理.jpg");

            if (File.Exists(jpgPath))
            {
                AddLog($"跳过: {filePath}");
                return;
            }

            AddLog($"[开始压缩]\t{filePath}");
            var sw = System.Diagnostics.Stopwatch.StartNew();

            //using (var image = new HImage(filePath))
            //{
            //    image.WriteImage("jpg", 80, jpgPath);
            //}

            using (var image = Image.Load(filePath))
            {
                var encoder = new JpegEncoder { Quality = 80 };
                image.Save(jpgPath, encoder);
            }

            sw.Stop();
            AddLog($"[完成压缩]\t{jpgPath}\t{sw.ElapsedMilliseconds}ms");

            if (IsDeleteImage)
            {
                File.Delete(filePath);
            }

            AddLog($"完成: {Path.GetFileName(filePath)}");
        }

        public async Task Run(string basePath)
        {
            semaphore = new SemaphoreSlim(maxConcurrency);

            var dirs = await UseFtpClientAsync(client =>
                client.GetListing(basePath));

            var barcodeDirs = dirs.Where(x => x.Type == FtpObjectType.Directory).ToList();

            TotalFiles = barcodeDirs.Count;
            ProcessedFiles = 0;
            Progress = 0;

            var tasks = new List<Task>();

            foreach (var dir in barcodeDirs)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    string traceId = Guid.NewGuid().ToString("N")[..6];

                    try
                    {
                        await RetryAsync(async () =>
                        {
                            await ProcessDirectory(dir.FullName);
                            return true;
                        }, context: dir.FullName, traceId: traceId);
                    }
                    finally
                    {
                        await RunOnUIThreadAsync(() =>
                        {
                            ProcessedFiles++;
                            Progress = TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
                        });

                        AddLog($"目录完成：{dir.FullName}，进度：{ProcessedFiles}");

                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            AddLog("全部处理完成！");
        }

        // ===== 单目录处理 =====       
        private async Task ProcessDirectory(string dirPath)
        {
            string traceId = Guid.NewGuid().ToString("N")[..6];

            try
            {
                AddLog($"[{traceId}] 目录开始 {dirPath}");

                var files = await UseFtpClientAsync(client =>
                    client.GetListing(dirPath));

                var targetFiles = files
                     .Where(f => f.Type == FtpObjectType.File)
                     .Where(f =>
                     {
                         string ext = Path.GetExtension(f.Name).ToLower();

                         // ===== 格式过滤 =====
                         if (IsCompressTif)
                         {
                             if (ext != ".tif" && ext != ".tiff")
                                 return false;
                         }
                         else
                         {
                             if (ext != ".png")
                                 return false;
                         }

                         // ===== 大小过滤（FTP文件 Size 单位：字节）=====
                         if (IsFileSizeLimit && f.Size >= 0)
                         {
                             double sizeMB = f.Size / 1024.0 / 1024.0;

                             AddLog($"[{traceId}] 文件大小: {Math.Round(sizeMB, 2)}MB (限制:{FileSizeLimit}MB)");

                             if (sizeMB < FileSizeLimit)
                                 return false;
                         }

                         return true;
                     }).ToList();

                if (targetFiles.Count == 0)
                    return;

                //  一次性获取已有文件（避免多次判断FileExists）
                var existingFiles = files
                    .Where(f => f.Type == FtpObjectType.File)
                    .Select(f => f.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var file in targetFiles)
                {
                    string jpgName = Path.GetFileNameWithoutExtension(file.Name) + "_已处理.jpg";

                    if (existingFiles.Contains(jpgName))
                    {
                        AddLog($"[{traceId}] 跳过 {file.Name}");
                        continue;
                    }

                    bool ok = await RetryAsync(async () =>
                    {
                        return await UseFtpClientAsync(client =>
                            ProcessSingleFile(client, dirPath, file, traceId));
                    }, context: file.Name, traceId: traceId);

                    if (!ok)
                        AddLog($"[{traceId}] 失败 {file.Name}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"[{traceId}] 异常 {ex.Message}");
            }
        }

        // ===== 单文件处理 =====
        private async Task<bool> ProcessSingleFile(AsyncFtpClient client, string dirPath, FtpListItem pngFile, string traceId)
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();

            string tempPng = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
            string tempJpg = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");

            try
            {
                AddLog($"[{traceId}]\t[开始处理]\t{pngFile.Name}");

                // ===== 下载（断点续传）=====
                var sw = System.Diagnostics.Stopwatch.StartNew();
                AddLog($"[{traceId}] ↓下载开始 {pngFile.Name}");

                var dlStatus = await WithTimeout(client.DownloadFile(tempPng, pngFile.FullName, FtpLocalExists.Resume), 15000, traceId);

                sw.Stop();
                AddLog($"[{traceId}]\t[下载完成]\t{pngFile.Name}\t{sw.ElapsedMilliseconds}ms");

                if (dlStatus != FtpStatus.Success)
                    return false;

                // ===== Halcon压缩 =====
                sw.Restart();

                //using (var image = new HImage(tempPng))
                //{
                //    image.WriteImage("jpg", 80, tempJpg);
                //}

                using (var image = Image.Load(tempPng))
                {
                    var encoder = new JpegEncoder { Quality = 80 };
                    image.Save(tempJpg, encoder);
                }

                sw.Stop();
                AddLog($"[{traceId}]\t[压缩完成]\t{pngFile.Name}\t{sw.ElapsedMilliseconds}ms");

                // ===== 上传（断点续传）=====
                string remoteJpg = $"{dirPath}/{Path.GetFileNameWithoutExtension(pngFile.Name)}_已处理.jpg";

                sw.Restart();
                AddLog($"[{traceId}] ↑上传开始 {pngFile.Name}");

                var upStatus = await WithTimeout(client.UploadFile(tempJpg, remoteJpg, FtpRemoteExists.Resume), 15000, traceId: traceId);

                sw.Stop();
                AddLog($"[{traceId}]\t[上传完成]\t{pngFile.Name}\t{sw.ElapsedMilliseconds}ms");

                if (upStatus != FtpStatus.Success)
                    return false;

                // ===== 删除 =====
                if (IsDeleteImage)
                {
                    try
                    {
                        await client.DeleteFile(pngFile.FullName);
                        AddLog($"[{traceId}]\t[删除]\t{pngFile.Name}");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"[{traceId}]\t[删除失败]\t{ex.Message}");
                    }
                }

                swTotal.Stop();
                AddLog($"[{traceId}]\t[完成]\t{pngFile.Name}总耗时{swTotal.ElapsedMilliseconds}ms");

                return true;
            }
            catch (Exception ex)
            {
                AddLog($"[{traceId}]\t[异常]\t{ex.Message}");
                return false;
            }
            finally
            {
                SafeDelete(tempPng);
                SafeDelete(tempJpg);
            }
        }

        // ===== 重试机制 =====
        private async Task<bool> RetryAsync(Func<Task<bool>> action, string context = "", string traceId = "")
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    if (await action())
                    {
                        if (i > 0)
                        {
                            AddLog($"[{traceId}]\t[重试成功]\t{context} 第{i + 1}次");
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    // ===== 判断是否值得重试 =====
                    if (!IsRetryable(ex))
                    {
                        AddLog($"[{traceId}]\t[致命错误]\t{context} - {ex.Message}");
                        return false;
                    }

                    AddLog($"[{traceId}]\t[重试{i + 1}]\t{context} - {ex.Message}");
                }

                // ===== 指数退避 =====
                int delay = Math.Min((int)Math.Pow(2, i) * 1000, 10000);
                await Task.Delay(delay);
            }

            // ===== 最终失败 =====           
            AddLog($"[{traceId}]\t[最终失败]\t{context}");

            return false;
        }

        private bool IsRetryable(Exception ex)
        {
            string msg = ex.Message.ToLower();

            // 网络类问题 → 可重试
            if (msg.Contains("timeout") ||
                msg.Contains("timed out") ||
                msg.Contains("connection") ||
                msg.Contains("closed") ||
                msg.Contains("reset"))
                return true;

            // FTP状态异常
            if (ex is IOException)
                return true;

            // Halcon偶发异常（可选）
            if (msg.Contains("halcon"))
                return true;

            // 其他默认不重试
            return false;
        }

        // ===== 创建客户端 =====
        private AsyncFtpClient CreateClient()
        {
            var host = projectSettingRepository.GetByNameAndVM("FTPHost", "VM_ZZFTPImageCompressUploadPage")?.Value ?? "ftp://10.182.5.249/"; ;
            var user = projectSettingRepository.GetByNameAndVM("FTPUser", "VM_ZZFTPImageCompressUploadPage")?.Value ?? "ftpadmin"; ;
            var pass = projectSettingRepository.GetByNameAndVM("FTPPassWord", "VM_ZZFTPImageCompressUploadPage")?.Value ?? "dJE230kfco540wdkiadmin"; ;

            var client = new AsyncFtpClient(host, 21)
            {
                Credentials = new NetworkCredential(user, pass)
            };

            client.Config.ConnectTimeout = 5000;
            client.Config.ReadTimeout = 5000;
            client.Config.DataConnectionConnectTimeout = 5000;
            client.Config.DataConnectionReadTimeout = 5000;

            client.Config.RetryAttempts = 1; // 重试功能自己写
            client.Config.InternetProtocolVersions = FtpIpVersion.IPv4;

            return client;
        }

        private async Task<T> UseFtpClientAsync<T>(Func<AsyncFtpClient, Task<T>> func)
        {
            AsyncFtpClient? client = null;

            try
            {
                client = CreateClient();

                // ===== 连接超时 =====
                var connectTask = client.Connect();
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
                {
                    AddLog("FTP连接超时");
                    throw new TimeoutException("FTP连接超时");
                }
                await connectTask;

                return await func(client);
            }
            finally
            {
                if (client != null)
                    await client.DisposeAsync();

                ftpConnectionSemaphore.Release();
            }
        }

        private async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs = 10000, string traceId = "")
        {
            var delayTask = Task.Delay(timeoutMs);

            if (await Task.WhenAny(task, delayTask) != task)
            {
                AddLog($"[{traceId}] 操作超时（{timeoutMs}ms）");
                throw new TimeoutException("FTP操作超时");
            }

            return await task;
        }

        private void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        public void AddLog
        (
            string message, CoreLogLevel level = CoreLogLevel.Info, bool isToDatabase = false,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0
        )
        {
            // 校验日志内容
            if (string.IsNullOrWhiteSpace(message))
                return;

            try
            {
                switch (level)
                {
                    case CoreLogLevel.Info:
                        logger?.Info(message, isToDatabase, memberName, filePath, lineNumber);
                        break;
                    case CoreLogLevel.Warn:
                        logger?.Warn(message, isToDatabase, memberName, filePath, lineNumber);
                        break;
                    case CoreLogLevel.Error:
                        logger?.Error(message, isToDatabase, memberName, filePath, lineNumber);
                        break;
                    default:
                        logger?.Log(level, message, isToDatabase, memberName, filePath, lineNumber);
                        break;
                }
            }
            catch (Exception)
            {

            }

            try
            {
                string uiMessage = $"[{level}]{message}";
                // 创建日志项
                var newLog = Tuple.Create(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), uiMessage);

                // 仅当内容有效时添加到日志集合
                _ = RunOnUIThreadAsync(() =>
                {
                    LogTable.Insert(0, newLog);
                    if (LogTable.Count > 300)
                    {
                        LogTable.RemoveAt(LogTable.Count - 1);
                    }
                });
            }
            catch (Exception)
            {

            }
        }

        public void OnNodeSelected(FtpNode node)
        {
            if (node == null) return;

            FtpPath = node.FullPath;

            Console.WriteLine($"选中路径: {FtpPath}");
        }

        public class FtpNode : ViewModelBase
        {
            public required string Name { get; set; }
            public required string FullPath { get; set; }

            public ObservableCollection<FtpNode?> Children { get; set; } = [];

            public bool IsLoaded { get; set; } = false;

            public required Func<string, Task<List<FtpNode>>> LoadFunc { get; set; }

            public FtpNode()
            {
                // ⭐ 关键：加一个占位，让TreeView显示展开箭头
                Children.Add(null);
            }

            public async Task LoadChildrenAsync()
            {
                if (IsLoaded || LoadFunc == null) return;

                var list = await LoadFunc(FullPath);

                Children.Clear();

                foreach (var item in list)
                {
                    Children.Add(item);
                }

                IsLoaded = true;
            }
        }

        public async Task InitFtpTreeAsync()
        {
            FtpTree.Clear(); // 清空已有节点

            AddLog("FTP图片压缩软件已开启");

            using var client = CreateClient();
            await client.Connect();

            AddLog("FTP已连接");

            // 获取根目录下所有一级目录
            var rootDirs = await client.GetListing("/");

            foreach (var d in rootDirs.Where(x => x.Type == FtpObjectType.Directory))
            {
                var node = new FtpNode
                {
                    Name = d.Name,
                    FullPath = d.FullName,
                    LoadFunc = LoadFtpDirectories  // 递归加载子目录
                };

                FtpTree.Add(node);
            }
        }

        private async Task<List<FtpNode>> LoadFtpDirectories(string path)
        {
            var list = new List<FtpNode>();

            // ===== ⭐ 如果已经是 OK/NG，直接返回空（终止展开）=====
            if (path.EndsWith("/OK", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/NG", StringComparison.OrdinalIgnoreCase))
            {
                return list; // 不再往下加载
            }

            try
            {
                var dirs = await UseFtpClientAsync(async client =>
                {
                    return await client.GetListing(path);
                });

                foreach (var d in dirs.Where(x => x.Type == FtpObjectType.Directory))
                {
                    list.Add(new FtpNode
                    {
                        Name = d.Name,
                        FullPath = d.FullName,
                        LoadFunc = LoadFtpDirectories
                    });
                }
            }
            finally
            {

            }

            return list;
        }

        private void EnsureSetting(string name, string defaultValue, string remark)
        {
            const string vmName = "VM_ZZFTPImageCompressUploadPage";

            if (!projectSettingRepository.Exists(m => m.BelongToVM == vmName && m.Name == name))
            {
                var psm = new ProjectSettingModel(name, vmName, defaultValue, "string", 0) { Remark = remark };

                projectSettingRepository.Add(psm);
            }
        }
    }
}
