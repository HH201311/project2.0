using Python.Runtime;
using System.IO;
using System.Windows;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class PythonConfigHelper
    {
        private static Thread? PythonThread;
        private static AutoResetEvent ar = new AutoResetEvent(false);

        private static volatile bool isWorking = false;
        public static bool IsWorking => isWorking;

        private static readonly Queue<(Action<object[]>, object[])> actions = new();
        private static readonly object queueLock = new();

        /// <summary>
        /// 是否已经初始化配置python环境
        /// </summary>
        public static bool IsInitConfig { get; private set; } = false;

        /// <summary>
        /// 初始化Python环境配置
        /// </summary>
        /// <param name="pyInstallPath">python安装路径</param>
        public static void InitPythonConfig(string pyInstallPath)
        {
            if (!IsInitConfig)
            {
                AddAction((InitConfig, [pyInstallPath]));
            }
        }

        /// <summary>
        /// 初始化Python环境配置
        /// </summary>
        /// <param name="pyInstallPath">python安装路径</param>
        /// <param name="pySearchPaths">python模块自定义搜索路径</param>
        public static void InitPythonConfig(string pyInstallPath, params string[] pySearchPaths)
        {
            if (!IsInitConfig)
            {
                AddAction((InitConfig, [pyInstallPath, pySearchPaths]));
            }
        }

        /// <summary>
        /// 初始化Python环境配置
        /// </summary>
        /// <param name="obj">python安装地址(数组第一个)及python自定义搜索路径</param>
        private static void InitConfig(object[] obj)
        {
            try
            {
                string? pythonInstallDir = obj[0] as string;

                // 校验传入的 pythonInstallDir，避免传入 null 或不存在的路径导致 Directory.GetFiles 报 CS8604
                if (string.IsNullOrWhiteSpace(pythonInstallDir))
                {
                    MessageBox.Show("未提供 Python 安装路径", "路径错误");
                    return;
                }
                if (!Directory.Exists(pythonInstallDir))
                {
                    MessageBox.Show("指定的 Python 安装路径不存在：" + pythonInstallDir, "路径错误");
                    return;
                }

                #region python配置<可配置>
                #region python动态链接库

                // 添加 null 检查
                if (string.IsNullOrEmpty(pythonInstallDir))
                {
                    MessageBox.Show("Python 安装路径参数无效或为空", "参数错误");
                    return;
                }

                var files = Directory.GetFiles(pythonInstallDir, "python*.dll");
                if (files.Length == 0)
                {
                    MessageBox.Show("未找到python动态链接库", "路径错误");
                    return;
                }
                string longfile = "";
                foreach (var file in files)
                {
                    string temfile = Path.GetFileName(file);
                    if (temfile.Length > longfile.Length)
                        longfile = temfile;
                }
                string dllPath = Path.Combine(pythonInstallDir, longfile);
                // 检查是否为 64 位 Python
                if (!Is64BitDll(dllPath))
                {
                    MessageBox.Show("Python 版本非 64 位，请安装 64 位 Python 版本。", "系统错误");
                    return;
                }

                Runtime.PythonDLL = dllPath;
                #endregion

                #region python执行文件所在目录
                string pythonHomePath = pythonInstallDir;
                PythonEngine.PythonHome = pythonHomePath;
                #endregion

                #region 设置对应python搜索路径
                string[] pyPaths = { "DLLs", "Lib", "Lib/site-packages" };
                List<string> searchPaths = [pythonInstallDir];
                searchPaths.Add(Path.Combine(pythonInstallDir, "..", "Dlls"));
                foreach (string p in pyPaths)
                {
                    searchPaths.Add(Path.Combine(pythonInstallDir, p));
                }

                // 添加自定义模块的搜索路径
                if (obj.Length > 1)
                {
                    if (obj[1] is string[] customPaths)
                    {
                        searchPaths.AddRange(customPaths);
                    }
                }

                searchPaths.Add("./PythonScripts");
                PythonEngine.PythonPath = string.Join(";", searchPaths);
                #endregion

                // 初始化python运行时
                PythonEngine.Initialize();
            }
            catch (Exception e)
            {
                MessageBox.Show("Python 初始化失败：" + e.Message, "系统错误");
                return;
            }

            IsInitConfig = true;
            #endregion
        }
        /// <summary>
        /// 检查 DLL 文件是否为 64 位
        /// </summary>
        /// <param name="dllPath">DLL 文件路径</param>
        /// <returns>是否为 64 位</returns>
        private static bool Is64BitDll(string dllPath)
        {
            try
            {
                // 使用 PEHeader 检查 DLL 文件位数
                using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);
                // 读取 DOS 头
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peHeaderOffset = br.ReadInt32();
                fs.Seek(peHeaderOffset, SeekOrigin.Begin);

                // 读取 PE 头
                uint peHeader = br.ReadUInt32();
                if (peHeader != 0x00004550) // "PE\0\0"
                {
                    MessageBox.Show("无效的 PE 文件", "系统错误");
                    return false;
                }

                // 读取机器类型
                ushort machine = br.ReadUInt16();
                return machine == 0x8664; // IMAGE_FILE_MACHINE_AMD64
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法检查 DLL 文件位数：" + ex.Message, "系统错误");
                return false;
            }
        }

        /// <summary>
        /// 启动PythonThread
        /// </summary>
        public static void Start()
        {
            if (isWorking)
                return;

            isWorking = true;

            PythonThread = new Thread(DoAction)
            {
                IsBackground = true,
                Name = "PythonThread"
            };

            PythonThread.Start();
        }

        /// <summary>
        /// 关闭任务线程
        /// </summary>
        public static void Stop()
        {
            if (!isWorking)
                return;

            isWorking = false;

            // 唤醒正在 WaitOne() 的线程，让它检查 isWorking 并退出
            ar.Set();

            if (PythonThread is { IsAlive: true })
            {
                bool exited = PythonThread.Join(TimeSpan.FromSeconds(2));

                if (!exited)
                {
                    MessageBox.Show("Python 工作线程未能在指定时间内退出。");
                    return;
                }
            }

            lock (queueLock)
            {
                actions.Clear();
            }

            if (IsInitConfig)
            {
                try
                {
                    PythonEngine.Shutdown();
                    IsInitConfig = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"PythonEngine.Shutdown 失败：{ex.Message}");
                }
            }

            PythonThread = null;
        }

        /// <summary>
        /// 任务队列执行
        /// </summary>
        private static void DoAction()  
        {
            while (true)
            {
                // 等待任务或停止信号
                ar.WaitOne();

                // 被 Stop 唤醒后退出
                if (!isWorking)
                    break;

                while (true)
                {
                    (Action<object[]> action, object[] args)? item = null;

                    lock (queueLock)
                    {
                        if (actions.Count > 0)
                        {
                            item = actions.Dequeue();
                        }
                    }

                    if (!item.HasValue)
                        break;

                    try
                    {
                        item.Value.action(item.Value.args);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Python 任务执行出错：{ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 添加任务到任务队列
        /// </summary>
        /// <param name="action">Python任务</param>
        public static void AddAction((Action<object[]>, object[]) action)
        {
            if (isWorking)
            {
                Task.Run(() =>
                {
                    lock (queueLock)
                    {
                        actions.Enqueue(action);
                        ar.Set(); // 通知工作线程有新任务
                    }
                });
            }
        }
    }
}
