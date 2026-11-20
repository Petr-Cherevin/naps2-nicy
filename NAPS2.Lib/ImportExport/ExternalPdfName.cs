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

    internal static async Task<List<string>> GetNames(PdfSettings settings, IList<ProcessedImage> pages)
    {
        var names = new List<string>();
        if (!settings.ExternalNameEnabled || !Path.IsPathFullyQualified(settings.ExternalProgram ?? "") ||
            !File.Exists(settings.ExternalProgram)) return names;
        try
        {
            var inputs = settings.ExternalInput == "page-count"
                ? new[] { pages.Count.ToString(CultureInfo.InvariantCulture) }
                : await Task.Run(() => ReadCodes(pages));
            foreach (var input in inputs)
            {
                var name = await Run(settings, input);
                if (name != null && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(name);
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("External PDF naming failed", ex);
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
                RedirectStandardInput = true, RedirectStandardOutput = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
            }
        };
        process.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var output = ReadName(process.StandardOutput, timeout.Token);
            await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            var value = await output;
            await process.WaitForExitAsync(timeout.Token);
            if (process.ExitCode != 0 || value.Length == 0 || value is "." or ".." ||
                value.EndsWith('.') || value.Any(char.IsControl) ||
                value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                value.IndexOfAny("<>:\"/\\|?*%$".ToCharArray()) >= 0 ||
                Regex.IsMatch(value, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase))
                return null;
            return value;
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<string> ReadName(StreamReader output, CancellationToken token)
    {
        var buffer = new char[260];
        int count = await output.ReadBlockAsync(buffer.AsMemory(), token);
        if (count == buffer.Length) throw new InvalidDataException("External file name too long");
        return new string(buffer, 0, count).Trim();
    }
}
