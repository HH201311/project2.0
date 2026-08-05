using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Project.PCD
{
    /// <summary>
    /// 将子进程加入一个 "父进程退出即终止" 的 Windows Job 对象。
    /// 主程序崩溃 / 被强杀 / 正常退出时，FFmpeg 等子进程都会被系统连带回收，杜绝孤儿进程。
    /// 注意：进程生命周期内绝不可关闭 s_jobHandle。
    /// </summary>
    public static class ChildProcessTracker
    {
        private static readonly IntPtr s_jobHandle;

        static ChildProcessTracker()
        {
            // 嵌套 Job 需要 Windows 8 / Server 2012 及以上
            if (Environment.OSVersion.Version < new Version(6, 2))
                return;

            string jobName = "FFmpegChildJob_" + Process.GetCurrentProcess().Id;
            s_jobHandle = CreateJobObject(IntPtr.Zero, jobName);

            var extended = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr ptr = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(extended, ptr, false);
                if (!SetInformationJobObject(s_jobHandle,
                        JobObjectInfoType.ExtendedLimitInformation, ptr, (uint)length))
                {
                    Debug.WriteLine("SetInformationJobObject 失败: " + Marshal.GetLastWin32Error());
                }
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        public static bool AddProcess(Process process)
        {
            try
            {
                return process != null
                       && s_jobHandle != IntPtr.Zero
                       && AssignProcessToJobObject(s_jobHandle, process.Handle);
            }
            catch { return false; }
        }

        #region P/Invoke
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(IntPtr job,
            JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
        private enum JobObjectInfoType { ExtendedLimitInformation = 9 }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
        #endregion
    }
}
