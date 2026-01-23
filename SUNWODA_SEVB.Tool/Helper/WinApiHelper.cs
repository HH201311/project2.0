using System.Runtime.InteropServices;
using SUNWODA_SEVB.Core.Enumerations.Tool;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class WinApiHelper
    {
        #region P/Invoke 声明
        /// <summary>
        /// 将窗口设置到前台并激活窗口
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>设置是否成功</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// 设置窗口显示状态
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="cmdShow">显示状态</param>
        /// <returns>显示是否成功</returns>
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

        /// <summary>
        /// 获取当前线程Id
        /// </summary>
        /// <returns>当前线程Id</returns>
        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        /// <summary>
        /// 释放虚拟内存
        /// </summary>
        /// <param name="hProcess">进程句柄</param>
        /// <returns>是否成功</returns>
        /// <example>EmptyWorkingSet(Process.GetCurrentProcess().Handle);</example>
        [DllImport("psapi.dll")]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        /// <summary>
        /// 设置窗口显示状态
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="windowStateFlag">窗口状态标识</param>
        /// <returns>显示是否成功</returns>
        public static bool ShowWindowAsync(IntPtr hWnd, WindowStateFlags windowStateFlag)
        {
            return ShowWindowAsync(hWnd, (int)windowStateFlag);
        }
        #endregion
    }
}
