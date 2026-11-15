using System.Globalization;

namespace NAPS2.ImportExport;

/// <summary>
/// Generates the file name suffix used when extracting pages to a new file, based on the page numbers.
/// </summary>
public static class ExtractFileNamer
{
    /// <summary>
    /// The longest suffix of page numbers we'll use before falling back to a random code.
    /// </summary>
    public const int MaxSuffixLength = 30;

    private const string RandomCodeChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int RandomCodeLength = 6;

    /// <summary>
    /// Gets a suffix that identifies the given (1-based) page numbers, e.g. "07" or "03-05,09,12". Page numbers are
    /// padded with leading zeros to the number of digits in the total page count so that file names sort correctly.
    /// Consecutive pages are collapsed into ranges. If the result would be too long, a random alphanumeric code is
    /// used instead.
    /// </summary>
    public static string GetPageSuffix(IEnumerable<int> pageNumbers, int totalPages, Random? random = null)
    {
        var sorted = pageNumbers.Distinct().OrderBy(x => x).ToList();
        if (sorted.Count == 0)
        {
            return GetRandomCode(random);
        }
        int digits = Math.Max(Math.Max(totalPages, sorted[sorted.Count - 1]), 1)
            .ToString(CultureInfo.InvariantCulture).Length;
        string Format(int pageNumber) => pageNumber.ToString("D" + digits, CultureInfo.InvariantCulture);
        string FormatRange(int first, int last) => first == last ? Format(first) : $"{Format(first)}-{Format(last)}";

        var parts = new List<string>();
        int rangeStart = sorted[0];
        int previous = sorted[0];
        foreach (var pageNumber in sorted.Skip(1))
        {
            if (pageNumber != previous + 1)
            {
                parts.Add(FormatRange(rangeStart, previous));
                rangeStart = pageNumber;
            }
            previous = pageNumber;
        }
        parts.Add(FormatRange(rangeStart, previous));

        var suffix = string.Join(",", parts);
        return suffix.Length <= MaxSuffixLength ? suffix : GetRandomCode(random);
    }

    private static string GetRandomCode(Random? random)
    {
        random ??= new Random();
        var chars = new char[RandomCodeLength];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = RandomCodeChars[random.Next(RandomCodeChars.Length)];
        }
        return new string(chars);
    }
}
