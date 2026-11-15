using System.Runtime;
using NAPS2.EntryPoints;

namespace NAPS2;

static class Program
{
    /// <summary>
    /// The NAPS2.exe main method.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        var profilesPath = Path.Combine(Paths.AppData, "jit");
        Directory.CreateDirectory(profilesPath);
        ProfileOptimization.SetProfileRoot(profilesPath);
        ProfileOptimization.StartProfile("naps2.jit");

        // Record the GUI process before Eto creates its first visible HWND.
        NAPS2.EtoForms.DiagnosticLog.Write("EntryPoint",
            $"pid={Environment.ProcessId} is64bit={Environment.Is64BitProcess} " +
            $"osVersion={Environment.OSVersion.Version} argc={args.Length}");
        NAPS2.EtoForms.WinForms.TaskbarStartupInfoDiagnostics.Log();
        WinFormsEntryPoint.Run(args);
    }
}