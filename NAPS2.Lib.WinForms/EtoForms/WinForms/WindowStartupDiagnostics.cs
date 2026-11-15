using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>
/// Captures taskbar-facing window state, attempts a single post-startup activation
/// and diagnoses UI thread stalls without reading document contents.
/// </summary>
internal static class WindowStartupDiagnostics
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string? windowName);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);

    public static void Attach(Form form)
    {
        void Record(string name)
        {
            try
            {
                var handle = form.IsHandleCreated ? form.Handle : nint.Zero;
                var foreground = GetForegroundWindow();
                int cloak = -1;
                bool nativeVisible = false;
                int exStyle = 0;
                nint owner = 0;
                if (handle != 0)
                {
                    nativeVisible = IsWindowVisible(handle);
                    exStyle = GetWindowLong(handle, -20);
                    owner = GetWindow(handle, 4);
                    DwmGetWindowAttribute(handle, 14, out cloak, sizeof(int));
                }
                var shell = GetShellWindow();
                var taskbar = FindWindow("Shell_TrayWnd", null);
                DiagnosticLog.Write("Window",
                    $"event={name} hwnd=0x{handle.ToInt64():X} foreground=0x{foreground.ToInt64():X} " +
                    $"shell=0x{shell.ToInt64():X} taskbar=0x{taskbar.ToInt64():X} " +
                    $"owner=0x{owner.ToInt64():X} exStyle=0x{exStyle:X} " +
                    $"visible={form.Visible} nativeVisible={nativeVisible} showInTaskbar={form.ShowInTaskbar} " +
                    $"active={Form.ActiveForm == form} containsFocus={form.ContainsFocus} " +
                    $"state={form.WindowState} cloak={cloak}");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write("Window", $"event={name} diagnosticError={ex.GetType().Name}");
            }
        }

        DiagnosticLog.Write("Startup", "begin version=0005 activation=one-shot");
        Record("attach");
        form.HandleCreated += (_, _) => Record("HandleCreated");
        form.HandleDestroyed += (_, _) => Record("HandleDestroyed");
        form.Load += (_, _) => Record("Load");
        form.Shown += (_, _) => Record("Shown");
        form.VisibleChanged += (_, _) => Record("VisibleChanged");
        form.Activated += (_, _) => Record("Activated");
        form.Deactivate += (_, _) => Record("Deactivate");
        form.Resize += (_, _) => Record("Resize");
        form.FormClosed += (_, _) => Record("FormClosed");

        // The requested one-time activation occurs only AFTER initial display.
        // Unlike TopMost, it will not keep NAPS2 above other applications.
        var activationTimer = new System.Windows.Forms.Timer { Interval = 850 };
        activationTimer.Tick += (_, _) =>
        {
            activationTimer.Stop();
            if (!form.IsDisposed && form.IsHandleCreated && form.Visible &&
                form.WindowState != FormWindowState.Minimized)
            {
                var before = GetForegroundWindow();
                form.BringToFront();
                bool accepted = SetForegroundWindow(form.Handle);
                form.Activate();
                DiagnosticLog.Write("Activation",
                    $"requested=True accepted={accepted} previousForeground=0x{before.ToInt64():X} " +
                    $"newForeground=0x{GetForegroundWindow().ToInt64():X} " +
                    $"target=0x{form.Handle.ToInt64():X}");
                Record("PostStartupActivation");
            }
            activationTimer.Dispose();
        };
        form.Shown += (_, _) => activationTimer.Start();
        form.FormClosed += (_, _) =>
        {
            activationTimer.Stop();
            activationTimer.Dispose();
        };

        // Short high-resolution sampling of startup state, even if NAPS2 never gets focus.
        int ticks = 0;
        var timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (_, _) =>
        {
            Record($"sample-{++ticks}");
            if (ticks >= 30) timer.Stop();
        };
        form.Load += (_, _) => timer.Start();
        form.FormClosed += (_, _) => timer.Dispose();

        // Separate worker: if the UI thread blocks, we can measure the missing dispatch.
        _ = Task.Run(async () =>
        {
            int samples = 0;
            while (!form.IsDisposed)
            {
                await Task.Delay(1000);
                if (form.IsDisposed) break;
                if (samples++ % 3 == 0) DiagnosticLog.SampleProcess();
                if (!form.IsHandleCreated) continue;
                var started = Stopwatch.GetTimestamp();
                var ack = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    form.BeginInvoke((Action) (() =>
                    {
                        double delay = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                        if (delay >= 150)
                            DiagnosticLog.Write("UiLatency", $"dispatchDelayMs={delay:F0}");
                        ack.TrySetResult(true);
                    }));
                    if (await Task.WhenAny(ack.Task, Task.Delay(2000)) != ack.Task)
                        DiagnosticLog.Write("UiLatency", "dispatchTimeoutMs=2000");
                }
                catch (Exception ex)
                {
                    if (!form.IsDisposed)
                        DiagnosticLog.Write("UiLatency", $"dispatchError={ex.GetType().Name}");
                }
            }
        });
    }
}
