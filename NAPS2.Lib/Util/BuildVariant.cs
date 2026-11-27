namespace NAPS2.Util;

/// <summary>
/// Identifies this as a modified build of NAPS2, so it can't be mistaken for an official release.
/// </summary>
public static class BuildVariant
{
    public const string Name = "Nicy";

    public const string ReleaseNumber = "0020";

    private const string BaseName = "NAPS2";

    /// <summary>
    /// The product name including the variant, e.g. "NAPS2 Nicy".
    /// </summary>
    public static string ProductName => $"{BaseName} {Name}";

    /// <summary>
    /// Adds the variant name to a window title that starts with the product name.
    /// </summary>
    public static string ApplyToTitle(string title)
    {
        return title.StartsWith(BaseName, StringComparison.Ordinal)
            ? ProductName + title.Substring(BaseName.Length)
            : $"{title} ({Name})";
    }
}
