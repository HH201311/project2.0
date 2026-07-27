using System.Runtime.InteropServices;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// CT 自动化切片使用的 Win32 API 封装。
    /// </summary>
    public static class WinAPIHelper
    {
        public const int SwRestore = 9;
        public const int SwShow = 5;

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void BlockInput(bool block);

        [DllImport("psapi.dll")]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        public const uint MouseEventLeftDown = 0x0002;
        public const uint MouseEventLeftUp = 0x0004;

        /// <summary>
        /// 强制将指定窗口置前（含最小化恢复；用 AttachThreadInput 绕过前台限制）。
        /// 用于人工干预时把自动化软件从 VG 后面拉到最前。
        /// </summary>
        public static bool ForceForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SwRestore);
                }
                else
                {
                    ShowWindow(hWnd, SwShow);
                }

                var foreground = GetForegroundWindow();
                if (foreground == hWnd)
                {
                    return true;
                }

                var targetThread = GetWindowThreadProcessId(hWnd, IntPtr.Zero);
                var foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
                var currentThread = GetCurrentThreadId();

                var attachedFg = false;
                var attachedTarget = false;
                try
                {
                    if (foregroundThread != 0 && foregroundThread != currentThread)
                    {
                        attachedFg = AttachThreadInput(currentThread, foregroundThread, true);
                    }

                    if (targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread)
                    {
                        attachedTarget = AttachThreadInput(currentThread, targetThread, true);
                    }

                    BringWindowToTop(hWnd);
                    return SetForegroundWindow(hWnd);
                }
                finally
                {
                    if (attachedTarget)
                    {
                        AttachThreadInput(currentThread, targetThread, false);
                    }

                    if (attachedFg)
                    {
                        AttachThreadInput(currentThread, foregroundThread, false);
                    }
                }
            }
            catch
            {
                try
                {
                    return SetForegroundWindow(hWnd);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 使用 Win32 在屏幕坐标执行左键点击（比部分 Hook 模拟更可靠）。
        /// </summary>
        public static void ClickScreen(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(80);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        /// <summary>
        /// Ctrl + 左键单击（用于 VG「简单 3-2-1 对齐」选点）。调用方负责按下/释放 Ctrl。
        /// </summary>
        public static void LeftClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(50);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(40);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        public static void DoubleClickScreen(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(80);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        }
    }
}
