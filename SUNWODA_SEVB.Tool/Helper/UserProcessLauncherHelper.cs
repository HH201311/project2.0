using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class UserProcessLauncherHelper
    {
        #region 常量定义
        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        // 创建进程标志
        private const uint CREATE_NEW_CONSOLE = 0x00000010;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint NORMAL_PRIORITY_CLASS = 0x00000020;

        // 令牌权限
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_IMPERSONATE = 0x0004;
        private const uint TOKEN_ALL_ACCESS = 0xF01FF;

        // 消息框样式
        private const int MB_OK = 0x00000000;
        private const int MB_ICONINFORMATION = 0x00000040;
        private const int MB_ICONWARNING = 0x00000030;
        private const int MB_ICONERROR = 0x00000010;
        #endregion

        #region 枚举和结构体
        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        private enum TOKEN_INFORMATION_CLASS
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WTS_SESSION_INFO
        {
            public uint SessionID;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }
        #endregion

        #region P/Invoke 声明
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WTSSendMessage(
            IntPtr hServer,
            int SessionId,
            string pTitle,
            int TitleLength,
            string pMessage,
            int MessageLength,
            int Style,
            int Timeout,
            out int pResponse,
            bool bWait);

        [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] uint Reserved,
            [MarshalAs(UnmanagedType.U4)] uint Version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref uint pSessionInfoCount);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string? lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            string? lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
            TOKEN_TYPE TokenType,
            out IntPtr phNewToken);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            IntPtr hToken,
            bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            TOKEN_INFORMATION_CLASS TokenInformationClass,
            IntPtr TokenInformation,
            uint TokenInformationLength,
            out uint ReturnLength);
        #endregion

        #region 公共方法
        /// <summary>
        /// 启动进程配置
        /// </summary>
        public class ProcessStartConfig
        {
            public string? ApplicationPath { get; set; }
            public string Arguments { get; set; } = "";
            public string WorkingDirectory { get; set; } = "";
            public bool CreateNewConsole { get; set; } = false;
            public bool ShowWindow { get; set; } = true;
            public bool UseEnvironmentBlock { get; set; } = true;
            public int TimeoutSeconds { get; set; } = 30;
        }

        /// <summary>
        /// 启动结果
        /// </summary>
        public class ProcessStartResult
        {
            public bool Success { get; set; }
            public int ProcessId { get; set; }
            public uint SessionId { get; set; }
            public string ErrorMessage { get; set; } = "";
            public int Win32ErrorCode { get; set; }
        }

        /// <summary>
        /// 在当前活跃用户会话中启动指定的进程（优化版本）
        /// </summary>
        /// <param name="config">启动配置</param>
        /// <returns>启动结果</returns>
        public static ProcessStartResult CreateProcessInUserSession(ProcessStartConfig config)
        {
            var result = new ProcessStartResult();

            // 参数验证
            if (config == null)
            {
                result.ErrorMessage = "配置不能为空";
                return result;
            }

            if (string.IsNullOrEmpty(config.ApplicationPath))
            {
                result.ErrorMessage = "应用程序路径不能为空";
                return result;
            }

            if (!System.IO.File.Exists(config.ApplicationPath))
            {
                result.ErrorMessage = $"应用程序文件不存在: {config.ApplicationPath}";
                return result;
            }

            try
            {
                return CreateProcessInActiveSession(config);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"启动进程时发生异常: {ex.Message}";
                result.Win32ErrorCode = Marshal.GetLastWin32Error();
                return result;
            }
        }

        /// <summary>
        /// 简化的启动方法
        /// </summary>
        /// <param name="applicationPath">应用程序路径</param>
        /// <param name="arguments">命令行参数</param>
        /// <returns>启动结果</returns>
        public static ProcessStartResult CreateProcess(string applicationPath, string arguments = "")
        {
            var config = new ProcessStartConfig
            {
                ApplicationPath = applicationPath,
                Arguments = arguments
            };
            return CreateProcessInUserSession(config);
        }

        /// <summary>
        /// 向用户会话发送消息（优化版本）
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="title">标题</param>
        /// <param name="style">消息框样式</param>
        /// <param name="timeout">超时时间（秒）</param>
        /// <returns>是否成功</returns>
        public static bool ShowMessageToUser(string message, string title = "系统消息",
            MessageBoxStyle style = MessageBoxStyle.Information, int timeout = 0)
        {
            try
            {
                int sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId == -1) // INVALID_SESSION_ID
                {
                    return false;
                }

                int response = 0;
                int messageStyle = style switch
                {
                    MessageBoxStyle.Information => MB_OK | MB_ICONINFORMATION,
                    MessageBoxStyle.Warning => MB_OK | MB_ICONWARNING,
                    MessageBoxStyle.Error => MB_OK | MB_ICONERROR,
                    _ => MB_OK
                };

                return WTSSendMessage(
                    WTS_CURRENT_SERVER_HANDLE,
                    sessionId,
                    title,
                    title.Length,
                    message,
                    message.Length,
                    messageStyle,
                    timeout,
                    out response,
                    false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有活跃的用户会话信息（排除系统会话）
        /// </summary>
        /// <returns>活跃用户会话列表</returns>
        public static uint[] GetActiveUserSessions()
        {
            var activeSessions = new System.Collections.Generic.List<uint>();
            IntPtr ppSessionInfo = IntPtr.Zero;
            uint sessionCount = 0;

            try
            {
                if (WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref sessionCount))
                {
                    int structSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

                    for (int i = 0; i < sessionCount; i++)
                    {
                        IntPtr sessionPtr = IntPtr.Add(ppSessionInfo, i * structSize);
                        var sessionInfo = Marshal.PtrToStructure<WTS_SESSION_INFO>(sessionPtr);

                        // 只返回有效的用户会话（非系统会话）
                        if (IsValidUserSession(sessionInfo))
                        {
                            activeSessions.Add(sessionInfo.SessionID);
                        }
                    }
                }
            }
            finally
            {
                if (ppSessionInfo != IntPtr.Zero)
                {
                    WTSFreeMemory(ppSessionInfo);
                }
            }

            return activeSessions.ToArray();
        }
        #endregion

        #region 私有方法
        private static ProcessStartResult CreateProcessInActiveSession(ProcessStartConfig config)
        {
            var result = new ProcessStartResult();
            IntPtr ppSessionInfo = IntPtr.Zero;
            uint sessionCount = 0;

            try
            {
                // 枚举所有会话
                if (!WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref sessionCount))
                {
                    result.ErrorMessage = $"枚举会话失败，错误码: {Marshal.GetLastWin32Error()}";
                    result.Win32ErrorCode = Marshal.GetLastWin32Error();
                    return result;
                }

                int structSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                var validUserSessions = new System.Collections.Generic.List<WTS_SESSION_INFO>();

                // 查找有效的用户会话（排除系统会话）
                for (int i = 0; i < sessionCount; i++)
                {
                    IntPtr sessionPtr = IntPtr.Add(ppSessionInfo, i * structSize);
                    var sessionInfo = Marshal.PtrToStructure<WTS_SESSION_INFO>(sessionPtr);

                    // 过滤条件：活跃状态 + 非系统会话 + 用户会话
                    if (IsValidUserSession(sessionInfo))
                    {
                        validUserSessions.Add(sessionInfo);
                    }
                }

                // 如果没有找到有效用户会话
                if (validUserSessions.Count == 0)
                {
                    result.ErrorMessage = "未找到有效的用户会话（所有会话都是系统会话或未激活）";
                    return result;
                }

                // 优先选择控制台会话，然后是RDP会话
                validUserSessions.Sort((s1, s2) =>
                {
                    // Console会话优先级最高
                    bool s1IsConsole = s1.pWinStationName?.Equals("Console", StringComparison.OrdinalIgnoreCase) == true;
                    bool s2IsConsole = s2.pWinStationName?.Equals("Console", StringComparison.OrdinalIgnoreCase) == true;

                    if (s1IsConsole && !s2IsConsole) return -1;
                    if (!s1IsConsole && s2IsConsole) return 1;

                    // 其次按会话ID排序（越小越优先）
                    return s1.SessionID.CompareTo(s2.SessionID);
                });

                // 尝试在用户会话中启动进程
                foreach (var sessionInfo in validUserSessions)
                {
                    var sessionResult = StartProcessInSession(config, sessionInfo.SessionID);
                    if (sessionResult.Success)
                    {
                        return sessionResult;
                    }

                    // 记录失败信息但继续尝试其他会话
                    result.ErrorMessage = sessionResult.ErrorMessage;
                    result.Win32ErrorCode = sessionResult.Win32ErrorCode;
                }

                // 所有用户会话尝试都失败
                result.ErrorMessage = $"在所有 {validUserSessions.Count} 个用户会话中启动进程都失败。最后错误: {result.ErrorMessage}";
            }
            finally
            {
                if (ppSessionInfo != IntPtr.Zero)
                {
                    WTSFreeMemory(ppSessionInfo);
                }
            }

            return result;
        }

        /// <summary>
        /// 判断是否为有效的用户会话（非系统会话）
        /// </summary>
        /// <param name="sessionInfo">会话信息</param>
        /// <returns>是否为有效用户会话</returns>
        private static bool IsValidUserSession(WTS_SESSION_INFO sessionInfo)
        {
            // 必须是活跃状态
            if (sessionInfo.State != WTS_CONNECTSTATE_CLASS.WTSActive)
            {
                return false;
            }

            // 排除系统会话（Session 0）
            if (sessionInfo.SessionID == 0)
            {
                return false;
            }

            // 检查会话名称，过滤掉系统相关的会话
            if (!string.IsNullOrEmpty(sessionInfo.pWinStationName))
            {
                string stationName = sessionInfo.pWinStationName.ToUpperInvariant();

                // 排除系统会话名称
                if (stationName == "SERVICES" ||
                    stationName == "SYSTEM" ||
                    stationName.StartsWith("SERVICE-"))
                {
                    return false;
                }
            }

            // 额外验证：检查会话是否有用户登录
            return HasUserLoggedIn(sessionInfo.SessionID);
        }

        /// <summary>
        /// 检查指定会话是否有用户登录
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <returns>是否有用户登录</returns>
        private static bool HasUserLoggedIn(uint sessionId)
        {
            IntPtr userToken = IntPtr.Zero;
            try
            {
                // 尝试获取用户令牌，如果成功说明有用户登录
                bool hasToken = WTSQueryUserToken(sessionId, out userToken);
                if (hasToken && userToken != IntPtr.Zero)
                {
                    // 进一步验证令牌是否有效（可选）
                    return IsValidUserToken(userToken);
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (userToken != IntPtr.Zero)
                {
                    CloseHandle(userToken);
                }
            }
        }

        /// <summary>
        /// 验证用户令牌是否有效
        /// </summary>
        /// <param name="userToken">用户令牌</param>
        /// <returns>令牌是否有效</returns>
        private static bool IsValidUserToken(IntPtr userToken)
        {
            try
            {
                // 检查令牌类型和用户身份
                uint tokenInfoLength = 0;

                // 先获取需要的缓冲区大小（调用会失败但设置正确的大小）
                GetTokenInformation(userToken, TOKEN_INFORMATION_CLASS.TokenUser,
                    IntPtr.Zero, 0, out tokenInfoLength);

                if (tokenInfoLength > 0)
                {
                    IntPtr tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);
                    try
                    {
                        if (GetTokenInformation(userToken, TOKEN_INFORMATION_CLASS.TokenUser,
                            tokenInfo, tokenInfoLength, out tokenInfoLength))
                        {
                            // 如果能成功获取令牌信息，说明是有效的用户令牌
                            return true;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(tokenInfo);
                    }
                }

                return false;
            }
            catch
            {
                // 出现异常时保守返回true，避免过度限制
                return true;
            }
        }

        private static ProcessStartResult StartProcessInSession(ProcessStartConfig config, uint sessionId)
        {
            var result = new ProcessStartResult { SessionId = sessionId };
            IntPtr userToken = IntPtr.Zero;
            IntPtr duplicateToken = IntPtr.Zero;
            IntPtr environmentBlock = IntPtr.Zero;

            try
            {
                // 获取用户令牌
                if (!WTSQueryUserToken(sessionId, out userToken))
                {
                    result.ErrorMessage = $"获取会话 {sessionId} 的用户令牌失败，错误码: {Marshal.GetLastWin32Error()}";
                    result.Win32ErrorCode = Marshal.GetLastWin32Error();
                    return result;
                }

                // 复制令牌
                if (!DuplicateTokenEx(userToken, TOKEN_ALL_ACCESS, IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary, out duplicateToken))
                {
                    result.ErrorMessage = $"复制令牌失败，错误码: {Marshal.GetLastWin32Error()}";
                    result.Win32ErrorCode = Marshal.GetLastWin32Error();
                    return result;
                }

                // 创建用户环境块
                if (config.UseEnvironmentBlock)
                {
                    CreateEnvironmentBlock(out environmentBlock, duplicateToken, false);
                }

                // 准备启动信息
                var startupInfo = new STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                    lpDesktop = "winsta0\\default",
                    wShowWindow = (short)(config.ShowWindow ? 1 : 0)
                };

                // 设置创建标志
                uint creationFlags = NORMAL_PRIORITY_CLASS;
                if (config.CreateNewConsole)
                    creationFlags |= CREATE_NEW_CONSOLE;
                if (config.UseEnvironmentBlock && environmentBlock != IntPtr.Zero)
                    creationFlags |= 0x00000400; // CREATE_UNICODE_ENVIRONMENT

                // 构建命令行
                string commandLine = string.IsNullOrEmpty(config.Arguments)
                    ? $"\"{config.ApplicationPath}\""
                    : $"\"{config.ApplicationPath}\" {config.Arguments}";

                // 创建进程
                if (CreateProcessAsUser(
                    duplicateToken,
                    null, // 使用命令行中的程序名
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    environmentBlock != IntPtr.Zero ? null : null,
                    string.IsNullOrEmpty(config.WorkingDirectory) ? null : config.WorkingDirectory,
                    ref startupInfo,
                    out PROCESS_INFORMATION processInfo))
                {
                    result.Success = true;
                    result.ProcessId = processInfo.dwProcessId;

                    // 立即关闭进程和线程句柄（进程仍然运行）
                    CloseHandle(processInfo.hProcess);
                    CloseHandle(processInfo.hThread);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    result.ErrorMessage = $"CreateProcessAsUser失败，错误码: {error} ({GetErrorMessage(error)})";
                    result.Win32ErrorCode = error;
                }
            }
            finally
            {
                // 清理资源
                if (environmentBlock != IntPtr.Zero)
                    DestroyEnvironmentBlock(environmentBlock);
                if (duplicateToken != IntPtr.Zero)
                    CloseHandle(duplicateToken);
                if (userToken != IntPtr.Zero)
                    CloseHandle(userToken);
            }

            return result;
        }

        private static string GetErrorMessage(int errorCode)
        {
            try
            {
                return new Win32Exception(errorCode).Message;
            }
            catch
            {
                return $"未知错误 ({errorCode})";
            }
        }
        #endregion

        #region 枚举
        public enum MessageBoxStyle
        {
            Information,
            Warning,
            Error
        }
        #endregion
    }
}
