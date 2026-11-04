// SPDX-License-Identifier: GPL-3.0-only
// Copyright (c) 2026 Willy19928
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// x64 .NET Framework；asInvoker 沿用呼叫者權限。
// 目標程式需要提升權限時顯示錯誤，由使用者決定是否以系統管理員身分執行。
// 官方啟動器程序保持暫停，不修改檔案或讀取 SDK 紀錄。
static class EndfieldDirect {
    const uint Suspended = 4, ExtendedStartup = 0x80000, Timeout = 258;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Startup {
        public int size; public string reserved, desktop, title;
        public int x, y, width, height, columns, rows, fill, flags;
        public short show, reservedSize; public IntPtr reserved2, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupEx { public Startup startup; public IntPtr attributes; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr process, thread; public uint pid, tid; }
    [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
        public long processTime, jobTime; public uint flags;
        public UIntPtr minWorkingSet, maxWorkingSet; public uint activeProcesses;
        public UIntPtr affinity; public uint priority, scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct JobLimits {
        public BasicLimits basic;
        public ulong reads, writes, others, readBytes, writeBytes, otherBytes;
        public UIntPtr processMemory, jobMemory, peakProcessMemory, peakJobMemory;
    }
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcess(string app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit,
        uint flags, IntPtr environment, string directory, ref StartupEx startup, out ProcessInfo result);
    [DllImport("kernel32", SetLastError = true)] static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32", SetLastError = true)] static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr key, ref IntPtr value, IntPtr size, IntPtr old, IntPtr returned);
    [DllImport("kernel32")] static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32", SetLastError = true)] static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32", SetLastError = true)] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateJobObject(IntPtr security, string name);
    [DllImport("kernel32", SetLastError = true)] static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref JobLimits limits, int size);
    [DllImport("kernel32", SetLastError = true)] static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
    [DllImport("kernel32", SetLastError = true)] static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, out bool member);
    [DllImport("user32", CharSet = CharSet.Unicode)] static extern int MessageBox(IntPtr window, string text, string title, uint flags);
    static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }

    sealed class Child : SafeHandleZeroOrMinusOneIsInvalid {
        IntPtr thread;
        bool suspended = true;
        public Child(ProcessInfo value) : base(true) { SetHandle(value.process); thread = value.thread; }
        public uint Wait(uint milliseconds) {
            uint result = WaitForSingleObject(handle, milliseconds);
            if (result == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            return result;
        }
        public void Run() {
            uint previous = ResumeThread(thread);
            Check(previous != uint.MaxValue);
            if (previous != 1) throw new InvalidOperationException("遊戲執行緒的暫停狀態異常。");
            suspended = false;
            CloseHandle(thread); thread = IntPtr.Zero;
        }
        public void Stop() {
            if (Wait(0) == Timeout) Check(TerminateProcess(handle, 0));
            if (Wait(5000) != 0) throw new TimeoutException("測試程序未在 5 秒內結束。");
        }
        protected override bool ReleaseHandle() {
            if (suspended && WaitForSingleObject(handle, 0) == Timeout) TerminateProcess(handle, 0);
            if (thread != IntPtr.Zero) CloseHandle(thread);
            return CloseHandle(handle);
        }
    }
    static Child Start(string executable, Child parent = null, string args = "") {
        var startup = new StartupEx();
        startup.startup.size = Marshal.SizeOf(parent == null ? typeof(Startup) : typeof(StartupEx));
        IntPtr size = IntPtr.Zero; bool initialized = false;
        try {
            if (parent != null) {
                InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                if (size == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                startup.attributes = Marshal.AllocHGlobal(size);
                Check(InitializeProcThreadAttributeList(startup.attributes, 1, 0, ref size)); initialized = true;
                IntPtr handle = parent.DangerousGetHandle();
                Check(UpdateProcThreadAttribute(startup.attributes, 0, new IntPtr(0x20000), ref handle,
                    new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero));
            }
            ProcessInfo result;
            bool created = CreateProcess(executable, new StringBuilder("\"" + executable + "\"" + args), IntPtr.Zero, IntPtr.Zero,
                false, Suspended | (parent == null ? 0 : ExtendedStartup), IntPtr.Zero,
                Path.GetDirectoryName(executable), ref startup, out result);
            if (!created) {
                int code = Marshal.GetLastWin32Error();
                string detail = code == 740 ? "\n這個程式需要系統管理員權限，請以系統管理員身分執行《明日方舟：終末地》直接啟動工具。" : "";
                throw new Win32Exception(code, "無法建立程序：" + executable + "\n" + new Win32Exception(code).Message + detail);
            }
            return new Child(result);
        } finally {
            if (initialized) DeleteProcThreadAttributeList(startup.attributes);
            if (startup.attributes != IntPtr.Zero) Marshal.FreeHGlobal(startup.attributes);
        }
    }
    static string FindLauncher() {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GRYPHLINK");
        string best = Path.Combine(root, "Games.exe"); Version newest = new Version(0, 0);
        if (Directory.Exists(root)) foreach (string directory in Directory.GetDirectories(root)) {
            Version version; string candidate = Path.Combine(directory, "Games.exe");
            if (Version.TryParse(Path.GetFileName(directory), out version) && version > newest && File.Exists(candidate)) {
                newest = version; best = candidate;
            }
        }
        if (!File.Exists(best)) throw new FileNotFoundException("找不到官方 GRYPHLINK 啟動器。", best);
        return best;
    }
    static bool AlreadyRunning() {
        Process[] processes = Process.GetProcessesByName("Endfield");
        foreach (Process process in processes) process.Dispose();
        return processes.Length != 0;
    }
    [STAThread] static int Main(string[] args) {
        bool test = args.Length == 1 && args[0] == "--self-test";
        bool checkConfig = args.Length == 1 && args[0] == "--check-config";
        try {
            using (var mutex = new Mutex(false, @"Local\EndfieldDirect")) {
                bool owns = false;
                try {
                    try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; }
                    if (!owns) return test || checkConfig ? 3 : 0;
                    if (args.Length != 0 && !test && !checkConfig) throw new ArgumentException("不支援的參數。");
                    if (IntPtr.Size != 8) throw new PlatformNotSupportedException("請使用 x64 版本。");
                    AppConfig config = AppConfig.Load(AppDomain.CurrentDomain.BaseDirectory);
                    string launcher = config.LauncherPath ?? FindLauncher();
                    if (checkConfig) return 0;
                    if (!test && AlreadyRunning()) return 0;
                    string target = test ? Assembly.GetExecutingAssembly().Location : config.GamePath;
                    if (!File.Exists(target)) throw new FileNotFoundException("找不到遊戲：" + target, target);
                    using (var job = CreateJobObject(IntPtr.Zero, null)) {
                        Check(!job.IsInvalid);
                        var limits = new JobLimits(); limits.basic.flags = 0x2000; // KILL_ON_JOB_CLOSE
                        Check(SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf(typeof(JobLimits))));
                        using (var parent = Start(launcher))
                        using (var game = Start(target, parent, test ? "" : " -launcher_lang=zh-tw -launcher_sub_channel=6")) {
                            try {
                                // 確認清理範圍前，兩個程序都保持暫停。
                                Check(AssignProcessToJobObject(job, parent.DangerousGetHandle()));
                                bool member; Check(IsProcessInJob(game.DangerousGetHandle(), job, out member));
                                if (member) throw new InvalidOperationException("遊戲不應屬於清理用 Job。");
                                if (test) {
                                    job.Dispose();
                                    if (parent.Wait(5000) != 0 || game.Wait(0) != Timeout) {
                                        WriteDiagnostic("Job 自測失敗：父程序未結束，或子程序已結束。");
                                        return 2;
                                    }
                                } else { game.Run(); game.Wait(uint.MaxValue); }
                            } finally { if (test) game.Stop(); }
                        }
                    }
                    return 0;
                } finally { if (owns) mutex.ReleaseMutex(); }
            }
        } catch (Exception error) {
            if (test || checkConfig) WriteDiagnostic(error.ToString());
            else MessageBox(IntPtr.Zero, error.Message, "《明日方舟：終末地》直接啟動工具", 0x10);
            return 1;
        }
    }
    static void WriteDiagnostic(string text) {
        try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Endfield-Direct-error.txt"), text, Encoding.UTF8); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
