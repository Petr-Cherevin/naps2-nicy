using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NAPS2.EtoForms;

/// <summary>
/// Detailed diagnostics for ChatGPT-0005. Document paths, page images, and OCR text
/// are never logged. The portable application directory is preferred.
/// </summary>
public static class DiagnosticLog
{
    private const long MaxLogBytes = 4 * 1024 * 1024;
    private const int MaxLogFiles = 20;
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static bool _initialized;
    private static int _part;
    private static string? _folder;
    private static readonly string SessionName =
        $"ChatGPT-0005-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}";

    public static string? PathToLog { get; private set; }

    private static void Initialize()
    {
        _initialized = true;
        var portable = Path.Combine(AppContext.BaseDirectory, "Diagnostics");
        try
        {
            Directory.CreateDirectory(portable);
            _folder = portable;
            OpenNextFile();
            return;
        }
        catch
        {
            // Applications launched from a read-only directory still need diagnostics.
        }
        try
        {
            _folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NAPS2", "Diagnostics");
            Directory.CreateDirectory(_folder);
            OpenNextFile();
        }
        catch
        {
            _writer = null;
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
        _writer.WriteLine("ChatGPT-0005 diagnostics (UTC); no document names, paths, images, or OCR text.");
        _writer.WriteLine(_folder.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase)
            ? "Storage=portable" : "Storage=localappdata-fallback");
        try
        {
            foreach (var old in Directory.EnumerateFiles(_folder, "ChatGPT-0005-*.log")
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
                $"threads={process.Threads.Count} cpuTotalMs={process.TotalProcessorTime.TotalMilliseconds:F0} " +
                $"gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)} gc2={GC.CollectionCount(2)}");
        }
        catch
        {
        }
    }
}
