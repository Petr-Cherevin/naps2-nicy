using System.Runtime.InteropServices;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Capture STARTUPINFO flags inherited from the launching process.
/// Never log desktop names, parent titles, arguments or document paths.
/// </summary>
public static class TaskbarStartupInfoDiagnostics
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public uint cb;
        public nint lpReserved;
        public nint lpDesktop;
        public nint lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public nint lpReserved2;
        public nint hStdInput;
        public nint hStdOutput;
        public nint hStdError;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern void GetStartupInfoW(ref StartupInfo startupInfo);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    public static void Log()
    {
        try
        {
            StartupInfo startup = new() { cb = (uint) Marshal.SizeOf<StartupInfo>() };
            GetStartupInfoW(ref startup);
            nint foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out uint foregroundPid);
            DiagnosticLog.Write("StartupInfo",
                $"flags=0x{startup.dwFlags:X8} useShowWindow={(startup.dwFlags & 1) != 0} " +
                $"showWindow={startup.wShowWindow} foregroundPid={foregroundPid} " +
                $"foregroundHwnd=0x{foreground.ToInt64():X}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("StartupInfo", $"error={ex.GetType().Name}");
        }
    }
}
