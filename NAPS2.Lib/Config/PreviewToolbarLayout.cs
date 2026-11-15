using System.Collections.Immutable;

namespace NAPS2.Config;

/// <summary>
/// Represents the user-customizable order and visibility of the buttons on the preview window toolbar.
///
/// This is stored in the config as a comma-separated list of item ids in display order, with hidden items prefixed
/// by "-". Items that aren't in the stored list (e.g. buttons added in a newer version) are inserted after the item
/// they follow by default.
/// </summary>
public static class PreviewToolbarLayout
{
    public const string PageNumber = "page";
    public const string Previous = "prev";
    public const string Next = "next";
    public const string ZoomWindow = "zoomWindow";
    public const string ZoomActual = "zoomActual";
    public const string ZoomIn = "zoomIn";
    public const string ZoomOut = "zoomOut";
    public const string ZoomPercent = "zoomPercent";
    public const string Rotate = "rotate";
    public const string Crop = "crop";
    public const string BrightCont = "brightCont";
    public const string HueSat = "hueSat";
    public const string BlackWhite = "blackWhite";
    public const string Sharpen = "sharpen";
    public const string DocumentCorrection = "docCorrection";
    public const string Compress = "compress";
    public const string Redact = "redact";
    public const string RedactColor = "redactColor";
    public const string RedactUndo = "redactUndo";
    public const string RedactApply = "redactApply";
    public const string ShowText = "showText";
    public const string CopyText = "copyText";
    public const string ClearText = "clearText";
    public const string Split = "split";
    public const string Combine = "combine";
    public const string EditWithApp = "editWithApp";
    public const string EditWithPick = "editWithPick";
    public const string SavePdf = "savePdf";
    public const string SaveImages = "saveImages";
    public const string Delete = "delete";

    private const string SeparatorPrefix = "sep";

    public static readonly ImmutableList<string> DefaultOrder =
    [
        PageNumber, Previous, Next,
        "sep1",
        ZoomWindow, ZoomActual, ZoomIn, ZoomOut, ZoomPercent,
        "sep2",
        Rotate, Crop, BrightCont, HueSat, BlackWhite, Sharpen, DocumentCorrection, Compress,
        Redact, RedactColor, RedactUndo, RedactApply,
        "sep3",
        ShowText, CopyText, ClearText,
        "sep4",
        Split, Combine, EditWithApp,
        "sep5",
        SavePdf, SaveImages,
        "sep6",
        Delete
    ];

    public record Item(string Id, bool Visible)
    {
        public bool IsSeparator => PreviewToolbarLayout.IsSeparator(Id);
    }

    public static bool IsSeparator(string id) => id.StartsWith(SeparatorPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Parses the stored layout into the full list of items in display order.
    /// </summary>
    public static List<Item> Parse(string? value)
    {
        var items = new List<Item>();
        foreach (var part in (value ?? "").Split(','))
        {
            var id = part.Trim();
            bool visible = true;
            if (id.StartsWith("-", StringComparison.Ordinal))
            {
                visible = false;
                id = id.Substring(1);
            }
            // Ignore anything unknown (e.g. from a newer version) and duplicates
            if (DefaultOrder.Contains(id) && items.All(x => x.Id != id))
            {
                items.Add(new Item(id, visible));
            }
        }
        // Add in anything missing after the item that precedes it by default
        for (int i = 0; i < DefaultOrder.Count; i++)
        {
            var id = DefaultOrder[i];
            if (items.Any(x => x.Id == id)) continue;
            int insertIndex = 0;
            for (int j = i - 1; j >= 0; j--)
            {
                int index = items.FindIndex(x => x.Id == DefaultOrder[j]);
                if (index != -1)
                {
                    insertIndex = index + 1;
                    break;
                }
            }
            items.Insert(insertIndex, new Item(id, true));
        }
        return items;
    }

    /// <summary>
    /// Converts the items to the stored format. The default layout is stored as an empty string.
    /// </summary>
    public static string Serialize(IEnumerable<Item> items)
    {
        var list = items.ToList();
        if (list.All(x => x.Visible) && list.Select(x => x.Id).SequenceEqual(DefaultOrder))
        {
            return "";
        }
        return string.Join(",", list.Select(x => x.Visible ? x.Id : "-" + x.Id));
    }

    /// <summary>
    /// Gets the ids of the items that should actually be displayed, in order. Separators are dropped if they would be
    /// at the start or end or next to another separator.
    /// </summary>
    public static List<string> GetDisplayedIds(IEnumerable<Item> items, Func<string, bool>? isAvailable = null)
    {
        var result = new List<string>();
        foreach (var item in items)
        {
            if (!item.Visible) continue;
            if (item.IsSeparator)
            {
                if (result.Count > 0 && !IsSeparator(result[result.Count - 1]))
                {
                    result.Add(item.Id);
                }
            }
            else if (isAvailable == null || isAvailable(item.Id))
            {
                result.Add(item.Id);
            }
        }
        if (result.Count > 0 && IsSeparator(result[result.Count - 1]))
        {
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }
}
