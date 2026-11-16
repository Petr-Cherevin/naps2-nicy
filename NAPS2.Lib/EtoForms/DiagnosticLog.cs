using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NAPS2.EtoForms;

/// <summary>
/// Detailed diagnostics for ChatGPT-0011. Document paths, page images, and OCR text
/// are never logged. The portable application directory is preferred.
/// </summary>
public static class DiagnosticLog
{
    private const long MaxLogBytes = 8 * 1024 * 1024;
    private const int MaxLogFiles = 30;
    private static string _storage = "unknown";
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static bool _initialized;
    private static int _part;
    private static string? _folder;
    private static readonly string SessionName =
        $"ChatGPT-0011-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}";

    public static string? PathToLog { get; private set; }

    private static void Initialize()
    {
        _initialized = true;
        // The assembly base directory is App\\lib, not the ZIP root.
        // The launcher supplies the root; direct App\\NAPS2.exe starts work as well.
        var launchRoot = Environment.GetEnvironmentVariable("NAPS2_DIAGNOSTICS_ROOT");
        var executableDir = Path.GetDirectoryName(Environment.ProcessPath);
        var candidateRoot = executableDir == null ? null : Directory.GetParent(executableDir)?.FullName;
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NAPS2", "Diagnostics");
        var candidates = new List<(string Folder, string Source)>();
        if (!string.IsNullOrWhiteSpace(launchRoot))
            candidates.Add((Path.Combine(launchRoot, "Diagnostics"), "portable-launcher"));
        if (candidateRoot != null && File.Exists(Path.Combine(candidateRoot, "NAPS2.Portable.exe")))
            candidates.Add((Path.Combine(candidateRoot, "Diagnostics"), "portable-detected"));
        candidates.Add((Path.Combine(AppContext.BaseDirectory, "Diagnostics"), "application-fallback"));
        candidates.Add((local, "localappdata-fallback"));
        foreach (var (folder, source) in candidates)
        {
            try
            {
                Directory.CreateDirectory(folder);
                _folder = folder;
                _storage = source;
                OpenNextFile();
                if (_writer != null) return;
            }
            catch { _writer = null; }
        }
    }

    private static void OpenNextFile()
    {
        _writer?.Dispose();
        _writer = null;
        if (_folder == null) return;
        PathToLog = Path.Combine(_folder, $"{SessionName}-part{++_part:D2}.log");
        _writer = new StreamWriter(PathToLog, false, new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _writer.WriteLine("ChatGPT-0011 diagnostics (UTC); no document names, paths, images, or OCR text.");
        _writer.WriteLine($"Storage={_storage}; pid={Environment.ProcessId}");
        try
        {
            foreach (var old in Directory.EnumerateFiles(_folder, "ChatGPT-0011-*.log")
                         .OrderByDescending(File.GetCreationTimeUtc).Skip(MaxLogFiles))
            {
                if (!string.Equals(old, PathToLog, StringComparison.OrdinalIgnoreCase))
                    File.Delete(old);
            }
        }
        catch
        {
            // Old log cleanup is best-effort.
        }
    }

    public static void Write(string category, string message)
    {
        try
        {
            lock (Sync)
            {
                if (!_initialized) Initialize();
                if (_writer == null) return;
                if (_writer.BaseStream.Length >= MaxLogBytes) OpenNextFile();
                _writer?.WriteLine(
                    $"{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)} " +
                    $"tid={Environment.CurrentManagedThreadId} [{category}] {message}");
            }
        }
        catch
        {
            // Diagnostics must never break importing, saving, or UI rendering.
        }
    }

    public static void SampleProcess()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            Write("Resources",
                $"workingSetMB={process.WorkingSet64 / 1048576} " +
                $"privateMB={process.PrivateMemorySize64 / 1048576} " +
                $"managedMB={GC.GetTotalMemory(false) / 1048576} " +
                $"threads={process.Threads.Count} handles={process.HandleCount} " +
                $"cpuTotalMs={process.TotalProcessorTime.TotalMilliseconds:F0} " +
                $"gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)}");
        }
        catch
        {
        }
    }
}
