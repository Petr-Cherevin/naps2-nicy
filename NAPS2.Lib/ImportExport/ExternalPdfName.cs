using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace NAPS2.ImportExport;

internal static class ExternalPdfName
{
    internal const string Token = "$(external)";

    internal static async Task<List<string>> GetNames(PdfSettings settings, IList<ProcessedImage> pages, string? document)
    {
        var names = new List<string>();
        if (!settings.ExternalNameEnabled) return names;
        try
        {
            var inputs = document != null ? new[] { document } : settings.ExternalInput == "page-count"
                ? new[] { pages.Count.ToString(CultureInfo.InvariantCulture) }
                : await Task.Run(() => ReadCodes(pages));
            foreach (var input in inputs)
            {
                if (!Path.IsPathFullyQualified(settings.ExternalProgram ?? "") || !File.Exists(settings.ExternalProgram))
                    throw new FileNotFoundException(settings.ExternalProgram);
                var name = await Run(settings, input);
                if (name != null && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(name);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("External PDF naming failed", ex);
            Eto.Forms.MessageBox.Show(ex.Message);
            return new List<string> { "ОШИБКА!" };
        }
        return names;
    }

    private static string[] ReadCodes(IEnumerable<ProcessedImage> pages)
    {
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            using var rendered = page.Render();
            values.UnionWith(BarcodeDetector.ReadNamingQrCodes(rendered));
        }
        return values.ToArray();
    }

    private static async Task<string?> Run(PdfSettings settings, string input)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(settings.ExternalProgram!, settings.ExternalArguments ?? "")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, StandardErrorEncoding = Encoding.UTF8,
                RedirectStandardInput = true, RedirectStandardOutput = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
            }
        };
        process.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var diagnostic = ReadDiagnostic(process.StandardError, timeout.Token);
            var output = ReadName(process.StandardOutput, timeout.Token);
            await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            var value = await output;
            await process.WaitForExitAsync(timeout.Token);
            var reason = await diagnostic;
            if (process.ExitCode != 0)
                throw new InvalidDataException(reason.Length > 0 ? reason : $"Exit code {process.ExitCode}");
            if (value.Length == 0) return null;
            if (value is "." or ".." ||
                value.EndsWith('.') || value.Any(char.IsControl) ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.IndexOfAny("<>:\"/\\|?*%$".ToCharArray()) >= 0 ||
                Regex.IsMatch(value, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase))
                throw new InvalidDataException("Invalid PDF file name");
            return value;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<string> ReadDiagnostic(StreamReader reader, CancellationToken token)
    {
        var message = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
            message.Append(buffer, 0, Math.Min(count, 2048 - message.Length));
        return message.ToString().Trim();
    }

    private static async Task<string> ReadName(StreamReader output, CancellationToken token)
    {
        var buffer = new char[260];
        int count = await output.ReadBlockAsync(buffer.AsMemory(), token);
        if (count == buffer.Length) throw new InvalidDataException("External file name too long");
        return new string(buffer, 0, count).Trim();
    }
}
