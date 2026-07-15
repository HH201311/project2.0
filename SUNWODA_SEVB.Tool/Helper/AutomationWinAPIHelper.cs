using System.Runtime.InteropServices;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// CT 自动化切片使用的 Win32 API 封装。
    /// </summary>
    public static class WinAPIHelper
    {
        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int cmdShow);

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
