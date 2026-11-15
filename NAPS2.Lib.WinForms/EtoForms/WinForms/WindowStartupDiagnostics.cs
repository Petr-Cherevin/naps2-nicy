using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAPS2.EtoForms;

namespace NAPS2.EtoForms.WinForms;

/// <summary>Captures the real sequence of Windows taskbar, focus, handle and visibility events.</summary>
internal static class WindowStartupDiagnostics
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hwnd, uint command);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hwnd, int index);

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
                DiagnosticLog.Write("Window",
                    $"event={name} hwnd=0x{handle.ToInt64():X} foreground=0x{foreground.ToInt64():X} " +
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

        // Sample after the form first appears even if it is covered by another application.
        // This distinguishes a window missing from the taskbar from one that has lost focus.
        var ticks = 0;
        var timer = new Timer { Interval = 500 };
        timer.Tick += (_, _) =>
        {
            Record($"sample-{++ticks}");
            if (ticks >= 30) timer.Stop();
        };
        form.Load += (_, _) => timer.Start();
        form.FormClosed += (_, _) => timer.Dispose();
    }
}
