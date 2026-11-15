using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Non-invasive taskbar forensics. Hook the actual top-level Win32 window messages
/// and accessibility WinEvents that the shell can observe. These events alone do
/// not prove an Explorer taskbar button exists; do not infer one from ShowInTaskbar.
/// No focus requests, SetWindowLong, or taskbar registration calls are made.
/// </summary>
internal static class TaskbarForensics
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectCreate = 0x8000;
    private const uint EventObjectHide = 0x8003;
    private const uint WinEventOutOfContext = 0x0000;

    private delegate void WinEventCallback(nint hook, uint eventType, nint hwnd,
        int idObject, int idChild, uint eventThread, uint timestamp);
    private delegate bool EnumWindowsCallback(nint hwnd, nint lParam);

    [DllImport("user32.dll")] private static extern nint SetWinEventHook(
        uint eventMin, uint eventMax, nint module, WinEventCallback callback,
        uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint code);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback cb, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder buffer, int maxCount);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("shell32.dll")]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out nint appId);

    private static string Hex(nint hwnd) => "0x" + hwnd.ToInt64().ToString("X");

    public static void Attach(Form form)
    {
        // Window messages: the exact show/style/activation ordering is normally
        // hidden by the Eto/WinForms abstraction.
        var tap = new MessageTap();
        void ConnectHandle()
        {
            if (form.IsHandleCreated)
            {
                tap.ReleaseHandle();
                tap.AssignHandle(form.Handle);
                DiagnosticLog.Write("TaskbarMsg", $"tapAttached hwnd={Hex(form.Handle)}");
            }
        }
        form.HandleCreated += (_, _) => ConnectHandle();
        form.HandleDestroyed += (_, _) => tap.ReleaseHandle();
        if (form.IsHandleCreated) ConnectHandle();

        var hooks = new List<nint>();
        WinEventCallback callback = (_, ev, hwnd, obj, child, eventThread, timestamp) =>
        {
            try
            {
                if (obj != 0 || child != 0 || hwnd == nint.Zero) return;
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                var taskbar = FindWindow("Shell_TrayWnd", null);
                // Limit the event stream to this process, its primary window and the
                // taskbar shell HWND. Other application titles and paths are never read.
                if (pid != Environment.ProcessId && hwnd != taskbar &&
                    GetAncestor(hwnd, 2) != taskbar && ev != EventSystemForeground) return;
                DiagnosticLog.Write("WinEvent",
                    $"event=0x{ev:X} hwnd={Hex(hwnd)} pid={pid} thread={eventThread} " +
                    $"obj={obj} child={child} shellTaskbar={hwnd == taskbar} ms={timestamp}");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("WinEvent", $"callbackError={ex.GetType().Name}");
            }
        };

        void InstallHooks()
        {
            if (hooks.Count != 0) return;
            // Out-of-context callbacks require the WinForms message loop on this thread.
            // They are unhooked with the form, and callback is kept strongly rooted.
            foreach (var (low, high) in new[]
                     {
                         (EventSystemForeground, EventSystemForeground),
                         (EventObjectCreate, EventObjectHide)
                     })
            {
                var hook = SetWinEventHook(low, high, nint.Zero, callback, 0, 0, WinEventOutOfContext);
                if (hook != 0) hooks.Add(hook);
                else DiagnosticLog.Write("WinEvent", $"hookFailed range=0x{low:X}-0x{high:X}");
            }
            Snapshot(form, "hooks-installed");
        }

        // Attach before the HWND is created. Installing only at Load misses the
        // initial CREATE/SHOW messages that are central to this taskbar bug.
        InstallHooks();

        int tick = 0;
        var sampling = new System.Windows.Forms.Timer { Interval = 250 };
        sampling.Tick += (_, _) =>
        {
            if (form.IsDisposed || !form.IsHandleCreated) return;
            tick++;
            // First 5 seconds at 250 ms, then 1-second samples up to 35 seconds.
            if (tick > 20) sampling.Interval = 1000;
            Snapshot(form, $"sample-{tick}");
            if (tick >= 50) sampling.Stop();
        };
        form.Shown += (_, _) =>
        {
            Snapshot(form, "shown");
            sampling.Start();
        };
        form.Activated += (_, _) => Snapshot(form, "activated");
        form.Deactivate += (_, _) => Snapshot(form, "deactivated");
        form.VisibleChanged += (_, _) => Snapshot(form, "visible-change");
        form.FormClosed += (_, _) =>
        {
            sampling.Stop();
            sampling.Dispose();
            Snapshot(form, "closed");
            foreach (var hook in hooks) UnhookWinEvent(hook);
            hooks.Clear();
            GC.KeepAlive(callback);
            tap.ReleaseHandle();
        };
    }

    private static void Snapshot(Form form, string stage)
    {
        try
        {
            var hwnd = form.IsHandleCreated ? form.Handle : nint.Zero;
            var foreground = GetForegroundWindow();
            var shell = FindWindow("Shell_TrayWnd", null);
            uint foregroundPid, shellPid, ownPid;
            GetWindowThreadProcessId(foreground, out foregroundPid);
            GetWindowThreadProcessId(shell, out shellPid);
            GetWindowThreadProcessId(hwnd, out ownPid);
            int cloak = -1;
            int dwmStatus = hwnd == 0 ? -1 :
                DwmGetWindowAttribute(hwnd, 14, out cloak, sizeof(int));
            var cls = new StringBuilder(180);
            if (hwnd != 0) GetClassName(hwnd, cls, cls.Capacity);
            int topLevel = 0, visible = 0;
            EnumWindows((candidate, _) =>
            {
                GetWindowThreadProcessId(candidate, out uint pid);
                if (pid != Environment.ProcessId) return true;
                topLevel++;
                if (IsWindowVisible(candidate)) visible++;
                return true;
            }, nint.Zero);

            int appIdPresent = -1;
            nint appIdPointer = 0;
            try
            {
                int hr = GetCurrentProcessExplicitAppUserModelID(out appIdPointer);
                appIdPresent = hr == 0 && appIdPointer != 0 ? 1 : 0;
            }
            finally
            {
                if (appIdPointer != 0) Marshal.FreeCoTaskMem(appIdPointer);
            }

            DiagnosticLog.Write("TaskbarSnapshot",
                $"stage={stage} hwnd={Hex(hwnd)} class={cls} " +
                $"pid={ownPid} foreground={Hex(foreground)} foregroundPid={foregroundPid} " +
                $"taskbar={Hex(shell)} taskbarPid={shellPid} " +
                $"os={Environment.OSVersion.Version} is64bit={Environment.Is64BitProcess} " +
                $"owner={Hex(GetWindow(hwnd, 4))} parent={Hex(GetParent(hwnd))} " +
                $"root={Hex(GetAncestor(hwnd, 2))} rootOwner={Hex(GetAncestor(hwnd, 3))} " +
                $"style=0x{GetWindowLong(hwnd, -16):X} exStyle=0x{GetWindowLong(hwnd, -20):X} " +
                $"visible={IsWindowVisible(hwnd)} showInTaskbar={form.ShowInTaskbar} " +
                $"cloaked={cloak} dwmHr={dwmStatus} state={form.WindowState} " +
                $"topLevelWindows={topLevel} visibleTopLevel={visible} explicitAppId={appIdPresent}");
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("TaskbarSnapshot", $"stage={stage} error={ex.GetType().Name}");
        }
    }

    private sealed class MessageTap : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            // A small list of messages important to shell enumeration/activation.
            string? name = m.Msg switch
            {
                0x18 => "WM_SHOWWINDOW",
                0x46 => "WM_WINDOWPOSCHANGING",
                0x47 => "WM_WINDOWPOSCHANGED",
                0x7D => "WM_STYLECHANGED",
                0x06 => "WM_ACTIVATE",
                0x1C => "WM_ACTIVATEAPP",
                0x86 => "WM_NCACTIVATE",
                0x07 => "WM_SETFOCUS",
                0x08 => "WM_KILLFOCUS",
                0x02 => "WM_DESTROY",
                0x112 => "WM_SYSCOMMAND",
                _ => null
            };
            if (name != null)
                DiagnosticLog.Write("TaskbarMsg",
                    $"event={name} hwnd={Hex(m.HWnd)} wp=0x{m.WParam.ToInt64():X} " +
                    $"lp=0x{m.LParam.ToInt64():X} " +
                    $"style=0x{GetWindowLong(m.HWnd, -16):X} exStyle=0x{GetWindowLong(m.HWnd, -20):X} " +
                    $"owner={Hex(GetWindow(m.HWnd, 4))} visible={IsWindowVisible(m.HWnd)}");
            base.WndProc(ref m);
        }
    }
}
