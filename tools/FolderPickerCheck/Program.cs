using System.Runtime.InteropServices;
using System.Text;
using NAPS2.EtoForms;
using NAPS2.EtoForms.WinForms;

internal static class Program
{
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, WindowCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int size);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr GetDlgItem(IntPtr dialog, int item);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static void Main()
    {
        EtoPlatform.Current = new WinFormsEtoPlatform();
        EtoPlatform.Current.ColorScheme.Config = NAPS2.Config.Naps2Config.Stub();
        using var application = EtoPlatform.Current.CreateApplication();
        foreach (var key in new[] { "profile_simplex", "profile_duplex" })
        {
            foreach (var scale in new[] { 1, 2 })
            {
                using var icon = EtoPlatform.Current.IconProvider.GetIcon(key, IconScenario.SmallToolbar, scale);
                if (icon == null || icon.Width != 16 * scale || icon.Height != 16 * scale)
                    throw new InvalidOperationException($"Missing profile icon: {key} at {scale}x");
            }
        }
        var helper = new EtoDialogHelper(null!, null!, null!);
        string initialFolder = Path.GetFullPath(AppContext.BaseDirectory);
        foreach (bool accept in new[] { false, true })
        {
            bool opened = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 300 };
            timer.Tick += (_, _) => EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
            {
                var name = new StringBuilder(64);
                GetClassName(window, name, name.Capacity);
                if (name.ToString() != "#32770" || !IsWindowVisible(window)) return true;
                var button = GetDlgItem(window, accept ? 1 : 2);
                if (button == IntPtr.Zero) return true;
                opened = true;
                PostMessage(button, 0x00F5 /* BM_CLICK */, IntPtr.Zero, IntPtr.Zero);
                return false;
            }, IntPtr.Zero);
            timer.Start();
            bool result = helper.ChoosePdfFolder(initialFolder, out var selected);
            timer.Stop();
            if (!opened || result != accept ||
                (accept ? !string.Equals(Path.TrimEndingDirectorySeparator(selected!),
                    Path.TrimEndingDirectorySeparator(initialFolder), StringComparison.OrdinalIgnoreCase) : selected != null))
                throw new InvalidOperationException($"Unexpected folder picker result: {result}, {selected}");
            Console.WriteLine(accept ? "Folder selection confirmed" : "Folder selection cancelled");
        }
    }
}
