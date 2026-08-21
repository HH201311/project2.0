using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// Windows 快捷方式（.lnk）解析。
    /// </summary>
    public static class ShellLinkHelper
    {
        /// <summary>
        /// 解析 .lnk 的目标路径。失败返回 false（损坏、非文件快捷方式、或 COM 不可用）。
        /// </summary>
        public static bool TryGetShortcutTarget(string shortcutPath, out string targetPath)
        {
            targetPath = string.Empty;
            if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath))
            {
                return false;
            }

            // 不用 Path.GetExtension：文件名形如「a.vgi - 快捷方式.lnk」时虽通常仍返回 .lnk，
            // 但用 EndsWith 更直观稳妥。
            if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fullPath = Path.GetFullPath(shortcutPath);
            if (TryGetShortcutTargetViaShellLink(fullPath, out targetPath))
            {
                return true;
            }

            return TryGetShortcutTargetViaWScript(fullPath, out targetPath);
        }

        private static bool TryGetShortcutTargetViaShellLink(string fullPath, out string targetPath)
        {
            targetPath = string.Empty;
            object? linkObj = null;
            try
            {
                linkObj = new ShellLinkCoClass();
                var link = (IShellLinkW)linkObj;
                var persist = (IPersistFile)linkObj;
                persist.Load(fullPath, 0);

                var buffer = new StringBuilder(260);
                var data = new WIN32_FIND_DATAW();
                // SLGP_UNCPRIORITY = 2：优先可用路径
                link.GetPath(buffer, buffer.Capacity, ref data, 2);
                var path = buffer.ToString().Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    buffer.Clear();
                    link.GetPath(buffer, buffer.Capacity, ref data, 0);
                    path = buffer.ToString().Trim();
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                targetPath = path;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (linkObj != null && Marshal.IsComObject(linkObj))
                {
                    Marshal.FinalReleaseComObject(linkObj);
                }
            }
        }

        private static bool TryGetShortcutTargetViaWScript(string fullPath, out string targetPath)
        {
            targetPath = string.Empty;
            object? shell = null;
            object? shortcut = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return false;
                }

                shell = Activator.CreateInstance(shellType);
                if (shell == null)
                {
                    return false;
                }

                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    binder: null,
                    target: shell,
                    args: new object[] { fullPath });

                if (shortcut == null)
                {
                    return false;
                }

                var target = shortcut.GetType().InvokeMember(
                    "TargetPath",
                    System.Reflection.BindingFlags.GetProperty,
                    binder: null,
                    target: shortcut,
                    args: Array.Empty<object>()) as string;

                if (string.IsNullOrWhiteSpace(target))
                {
                    return false;
                }

                targetPath = target;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }

                if (shell != null && Marshal.IsComObject(shell))
                {
                    Marshal.FinalReleaseComObject(shell);
                }
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath(
                [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                int cchMaxPath,
                ref WIN32_FIND_DATAW pfd,
                int fFlags);

            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, int fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WIN32_FIND_DATAW
        {
            public int dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public int nFileSizeHigh;
            public int nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }
    }
}
