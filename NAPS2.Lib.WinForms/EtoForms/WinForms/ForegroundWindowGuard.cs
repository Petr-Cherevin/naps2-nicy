using System.Runtime.InteropServices;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Avoid moving keyboard focus into the main window when the user
/// has already switched to another application during NAPS2 startup.
/// </summary>
internal static class ForegroundWindowGuard
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    public static bool IsOurProcessForeground()
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0) return false;
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint) Environment.ProcessId;
    }
}
