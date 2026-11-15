using System.Globalization;
using System.Text;

namespace NAPS2.EtoForms;

/// <summary>
/// Opt-in-build diagnostics. Records only app lifecycle and numeric progress; never document text or file paths.
/// The special ChatGPT-0004 build always enables these records, which can be compared across two launches.
/// </summary>
public static class DiagnosticLog
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static bool _initialized;

    public static string? PathToLog { get; private set; }

    public static void Write(string category, string message)
    {
        try
        {
            lock (Sync)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    var folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "NAPS2", "Diagnostics");
                    Directory.CreateDirectory(folder);
                    var filename = $"ChatGPT-0004-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
                    PathToLog = Path.Combine(folder, filename);
                    _writer = new StreamWriter(PathToLog, append: false, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                    _writer.WriteLine("ChatGPT-0004 diagnostic log; timestamps are UTC. Document names and OCR content are excluded.");
                }
                _writer?.WriteLine(
                    $"{DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)} " +
                    $"tid={Environment.CurrentManagedThreadId} [{category}] {message}");
            }
        }
        catch
        {
            // Diagnostics must not change the behavior of the application.
        }
    }
}
