using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class PythonHelper
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        private PythonHelper()
        {
            // 初始化Python线程
            PythonThread = new Thread(DoPyhtonAction);
            PythonThread.IsBackground = true;
            PythonThread.Name = "PythonThread";
        }

        private static PythonHelper? instance;

        /// <summary>
        /// 此类的单例对象
        /// </summary>
        public static PythonHelper Instance
        {
            get
            {
                if (instance == null) instance = new PythonHelper();
                return instance;
            }
        }

        /// <summary>
        /// Python任务线程
        /// </summary>
        private Thread PythonThread;

        /// <summary>
        /// Python任务线程开关
        /// </summary>
        public bool IsWorking { get; private set; } = false;

        /// <summary>
        /// Python任务队列
        /// </summary>
        private Queue<(Action<object[]>, object[])> actions = new Queue<(Action<object[]>, object[])>();

        /// <summary>
        /// 是否已经初始化配置python环境
        /// </summary>
        public bool IsInitConfig { get; private set; } = false;

        /// <summary>
        /// 初始化Python环境配置
        /// </summary>
        /// <param name="pyInstallPath">python安装路径</param>
        /// <param name="pySearchPaths">python模块搜索路径</param>
        public void InitPythonConfig(string pyInstallPath, params string[] pySearchPaths)
        {
            if (!IsInitConfig)
                AddPyhtonAction((InitPythonConfig, new object[] { pyInstallPath, pySearchPaths }));
        }

        /// <summary>
        /// 初始化Python环境配置
        /// </summary>
        /// <param name="obj"></param>
        private void InitPythonConfig(object[] obj)
        {
            try
            {
                var pythonInstallDir = obj[0] as string;
                #region python配置<可配置>
                #region python动态链接库
                if (string.IsNullOrEmpty(pythonInstallDir)) throw new ArgumentException("Python路径不可为空！");
                var files = Directory.GetFiles(pythonInstallDir, "python*.dll");
                if (files.Length == 0)
                {
                    throw new Exception("路径错误-未找到python动态链接库");
                }
                var longfile = string.Empty;
                foreach (var file in files)
                {
                    var temfile = Path.GetFileName(file);
                    if (temfile.Length > longfile.Length)
                        longfile = temfile;
                }
                var dllPath = $"{pythonInstallDir}/{longfile}";
                //var dllPath = Path.Combine(pythonInstallDir, longfile);
                Runtime.PythonDLL = dllPath;
                #endregion

                #region python执行文件所在目录
                var pythonHomePath = pythonInstallDir;
                PythonEngine.PythonHome = pythonHomePath;
                #endregion

                #region 设置对应python搜索路径
                var py_paths = new[] { "Dlls", "DLLs", "Lib", "Lib/site-packages" };
                var pySearchPath = $"{pythonHomePath};";
                foreach (var p in py_paths)
                {
                    pySearchPath += $"{pythonHomePath}/{p};";
                    //pySearchPath += $"{Path.Combine(pythonHomePath, p)};";
                }
                // 添加自定义模块的搜索路径
                if (obj[1] is string[] pySearchPaths)
                {
                    for (int i = 0; i < pySearchPaths.Length; i++)
                        pySearchPath += $"{pySearchPaths[i]};";
                }
                PythonEngine.PythonPath = pySearchPath;
                #endregion

                // 初始化python运行时
                PythonEngine.Initialize();
            }
            catch (Exception ex)
            {
                throw new Exception("初始化Python环境异常！", ex);
            }

            IsInitConfig = true;
            #endregion
        }

        /// <summary>
        /// 启动PythonThread
        /// </summary>
        public void PyhtonThreadStart()
        {
            if (!IsWorking)
            {
                IsWorking = true;
                PythonThread.Start();
            }
        }

        /// <summary>
        /// 关闭PythonThread
        /// </summary>
        public void PyhtonThreadStop()
        {
            IsWorking = false;
        }

        /// <summary>
        /// 任务队列执行
        /// </summary>
        private void DoPyhtonAction()
        {
            while (IsWorking)
            {
                try
                {
                    Monitor.Enter(actions);
                    while (actions.Count > 0)
                    {
                        (Action<object[]> action, object[] args) = actions.Dequeue();
                        action?.Invoke(args);
                    }
                }
                finally
                {
                    Monitor.Exit(actions);
                }

                Thread.Sleep(500);
            }
        }

        /// <summary>
        /// 添加任务到Python线程任务队列
        /// </summary>
        /// <param name="action"></param>
        public async Task<bool> AddPyhtonActionAsync((Action<object[]>, object[]) action)
        {
            var t = Task.Factory.StartNew((obj) =>
            {
                if (obj == null) return false;
                var reBool = AddPyhtonAction(((Action<object[]>, object[]))obj);
                return reBool;
            }, action);
            await t;
            return t.Result;
        }

        /// <summary>
        /// 添加任务到Python线程任务队列
        /// </summary>
        /// <param name="action"></param>
        public bool AddPyhtonAction((Action<object[]>, object[]) action)
        {
            if (IsWorking)
            {
                lock (actions)
                {
                    actions.Enqueue(action);
                }
            }

            return IsWorking;
        }
    }
}
