using System.Reflection;
using System.Threading;

namespace NAPS2.Portable;

class Program
{
    static void Main(string[] args)
    {
        var portableExeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (portableExeDir != null)
        {
            // This launcher lives next to Data and App in portable ZIPs.
            // The child inherits the root so its logging does not accidentally use App\\lib.
            Environment.SetEnvironmentVariable("NAPS2_DIAGNOSTICS_ROOT", portableExeDir);
            WriteLauncherDiagnostic(portableExeDir, "start argc=" + args.Length);
            bool failedUpdate = false;
            bool isUpdate = args.Length == 3 && args[0] == "/Update";
            try
            {
                if (isUpdate)
                {
                    failedUpdate = true;
                    WriteLauncherDiagnostic(portableExeDir, "update-start");
                    UpdatePortableApp(portableExeDir, args[1], args[2]);
                    WriteLauncherDiagnostic(portableExeDir, "update-finished");
                    failedUpdate = false;
                }
            }
            finally
            {
                var portableExePath = Path.Combine(portableExeDir, "App", "NAPS2.exe");
                try
                {
                    Process child = failedUpdate
                        ? Process.Start(portableExePath, "/FailedUpdate")
                        : isUpdate
                            ? Process.Start(portableExePath)
                            : Process.Start(portableExePath, GetRawArgs());
                    WriteLauncherDiagnostic(portableExeDir, "child-started pid=" +
                        (child == null ? "none" : child.Id.ToString()));
                    if (child != null) child.Dispose();
                }
                catch (Exception ex)
                {
                    WriteLauncherDiagnostic(portableExeDir, "child-start-failed type=" + ex.GetType().Name);
                    throw;
                }
            }
        }
    }

    // Gets the original command line without the exe path, so arguments are forwarded exactly as given
    // net462 and newer: no dependency on the GUI or its logging libraries.
    private static void WriteLauncherDiagnostic(string root, string message)
    {
        try
        {
            string folder = Path.Combine(root, "Diagnostics");
            Directory.CreateDirectory(folder);
            // Include UTC day to avoid growing a single file indefinitely.
            string filename = "ChatGPT-0009-launcher-" +
                DateTime.UtcNow.ToString("yyyyMMdd") + "-" +
                Process.GetCurrentProcess().Id + ".log";
            File.AppendAllText(Path.Combine(folder, filename),
                DateTime.UtcNow.ToString("O") + " [PortableLauncher] " + message + Environment.NewLine);
        }
        catch { /* A diagnostics failure must not prevent launching NAPS2. */ }
    }

    private static string GetRawArgs()
    {
        var commandLine = Environment.CommandLine;
        // The exe path is either quoted or ends at the first whitespace (with no escaping)
        int end = commandLine.StartsWith("\"")
            ? commandLine.IndexOf('"', 1)
            : commandLine.IndexOfAny([' ', '\t']);
        return end == -1 ? "" : commandLine.Substring(end + 1).TrimStart();
    }

    private static void UpdatePortableApp(string portableExeDir, string procId, string newAppFolderPath)
    {
        // Wait for the starting process and workers to finish so we don't try to mess with files in use
        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Wait for the starting process to exit
            var mainProc = Process.GetProcessById(int.Parse(procId));
            mainProc.WaitForExit();

            // Assume any process named NAPS2 or NAPS2.Worker could be a worker, although this isn't necessarily true
            // if there are multiple NAPS2 installations.
            var processesToWaitOn =
                Process.GetProcessesByName("NAPS2")
                    .Concat(Process.GetProcessesByName("NAPS2.Worker"))
                    .ToList();

            // Wait at most 10 seconds for them to exit (which is WorkerEntryPoint.ParentCheckInterval)
            const int waitTimeout = 10_000;
            while (stopwatch.ElapsedMilliseconds < waitTimeout && processesToWaitOn.Any(x => !x.HasExited))
            {
                Thread.Sleep(100);
            }
        }
        catch (ArgumentException)
        {
        }

        // Safely replace the old App folder
        AtomicReplaceDirectory(newAppFolderPath, Path.Combine(portableExeDir, "App"));
    }

    private static void AtomicReplaceDirectory(string source, string dest)
    {
        string temp = dest + ".old";
        Directory.Move(dest, temp);
        try
        {
            Directory.Move(source, dest);
            Directory.Delete(temp, true);
        }
        catch (Exception)
        {
            Directory.Move(temp, dest);
            throw;
        }
    }
}