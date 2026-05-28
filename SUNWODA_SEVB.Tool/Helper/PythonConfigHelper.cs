using Python.Runtime;
using System.IO;
using System.Windows;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class PythonConfigHelper
    {
        private static Thread? PythonThread;
        
        private static volatile bool isWorking = false;          
        public static bool IsWorking => isWorking;

        private static readonly Queue<(Action<object[]>, object[])> actions =new();
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
            if (!isWorking)
            {
                isWorking = true;
                PythonThread = new Thread(DoAction)
                {
                    IsBackground = true,
                    Name = "PythonThread"
                };
                PythonThread.Start();
            }
        }

        /// <summary>
        /// 关闭任务线程
        /// </summary>
        public static void Stop()
        {
            if (!isWorking) return;

            // 1. 通知工作线程退出
            isWorking = false;

            // 2. 等待线程自然结束（避免强制中止）
            if (PythonThread != null && PythonThread.IsAlive)
            {
                // 给线程一些时间完成当前任务和清理（例如 2 秒）
                bool exited = PythonThread.Join(TimeSpan.FromSeconds(2));
                if (!exited)
                {
                    // 理论上工作线程会很快退出；如果未退出，记录日志或做额外处理（不建议强制中止）
                    // 这里可以设置一个超时后的备用逻辑，例如置空线程引用，但保留 Python 资源不释放
                }
            }

            // 3. 清空未执行的任务队列（安全清空，因为工作线程已停止或即将停止）
            lock (queueLock)
            {
                actions.Clear();
            }

            // 4. 关闭 Python 运行时并重置状态
            if (IsInitConfig)
            {
                try
                {
                    // 释放 Python 资源
                    PythonEngine.Shutdown();   
                }
                catch (Exception ex)
                {                    
                    MessageBox.Show($"PythonEngine.Shutdown 失败: {ex.Message}");
                }
                IsInitConfig = false;
            }

            PythonThread = null;
        }

        /// <summary>
        /// 任务队列执行
        /// </summary>
        private static void DoAction()
        {
            // 每次循环检查标志
            while (isWorking)   
            {
                (Action<object[]> action, object[] args)? item = null;
                try
                {
                    // 安全地从队列中取出任务
                    lock (queueLock)
                    {
                        if (actions.Count > 0)
                        {
                            item = actions.Dequeue();
                        }
                    }

                    if (item.HasValue)
                    {
                        item.Value.action(item.Value.args);
                    }
                    else
                    {
                        // 队列为空，短暂休眠避免 CPU 空转
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Python 任务执行出错: {ex.Message}");
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
                    }
                });
            }
        }               
    }
}
