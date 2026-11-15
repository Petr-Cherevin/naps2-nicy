using System.Collections.Immutable;
using NAPS2.Pdf;

namespace NAPS2.Ocr;

/// <summary>
/// A word of text on a page, with its position relative to the top-left of the page (in the same units as the page
/// size).
/// </summary>
public record PageTextWord(string Text, float X, float Y, float Width, float Height);

/// <summary>
/// A run of words on the same line of a page, with its position relative to the top-left of the page.
/// </summary>
public record PageTextLine(string Text, float X, float Y, float Width, float Height);

/// <summary>
/// The text of a page, either from OCR or from the text layer of an imported PDF. If word positions are known, the
/// text can be displayed with the same layout as the page; otherwise only the plain text is available.
/// </summary>
public class PageText(float pageWidth, float pageHeight, string plainText, ImmutableList<PageTextWord> words)
{
    public float PageWidth { get; } = pageWidth;

    public float PageHeight { get; } = pageHeight;

    public string PlainText { get; } = plainText;

    public ImmutableList<PageTextWord> Words { get; } = words;

    /// <summary>
    /// The words grouped into lines, for displaying the text with roughly the same layout as the page.
    /// </summary>
    public ImmutableList<PageTextLine> Lines { get; } = GroupIntoLines(words);

    public bool HasLayout => Words.Count > 0 && PageWidth > 0 && PageHeight > 0;

    /// <summary>
    /// Groups words into lines based on their vertical positions. Words on the same line that are far apart (e.g. in
    /// separate columns) are split into separate runs so each keeps its own horizontal position.
    /// </summary>
    public static ImmutableList<PageTextLine> GroupIntoLines(IEnumerable<PageTextWord> words)
    {
        var rows = new List<List<PageTextWord>>();
        float rowTop = 0, rowBottom = 0;
        foreach (var word in words.Where(x => x.Height > 0).OrderBy(x => x.Y + x.Height / 2))
        {
            float center = word.Y + word.Height / 2;
            if (rows.Count > 0 && center >= rowTop && center <= rowBottom)
            {
                rows[rows.Count - 1].Add(word);
                rowTop = Math.Min(rowTop, word.Y);
                rowBottom = Math.Max(rowBottom, word.Y + word.Height);
            }
            else
            {
                rows.Add([word]);
                rowTop = word.Y;
                rowBottom = word.Y + word.Height;
            }
        }

        var lines = ImmutableList.CreateBuilder<PageTextLine>();
        foreach (var row in rows)
        {
            float rowHeight = row.Max(x => x.Y + x.Height) - row.Min(x => x.Y);
            var run = new List<PageTextWord>();
            foreach (var word in row.OrderBy(x => x.X))
            {
                // A gap much wider than a normal space means this is a separate piece of text
                if (run.Count > 0 && word.X - (run[run.Count - 1].X + run[run.Count - 1].Width) > rowHeight * 3)
                {
                    lines.Add(CreateLine(run));
                    run = [];
                }
                run.Add(word);
            }
            lines.Add(CreateLine(run));
        }
        return lines.ToImmutable();
    }

    private static PageTextLine CreateLine(List<PageTextWord> words)
    {
        float left = words.Min(x => x.X);
        float top = words.Min(x => x.Y);
        float right = words.Max(x => x.X + x.Width);
        float bottom = words.Max(x => x.Y + x.Height);
        return new PageTextLine(string.Join(" ", words.Select(x => x.Text)), left, top, right - left, bottom - top);
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(PlainText) && Words.Count == 0;

    public static PageText FromOcrResult(OcrResult result)
    {
        var words = result.Words
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Select(word => new PageTextWord(word.Text, word.Bounds.x - result.PageBounds.x,
                word.Bounds.y - result.PageBounds.y, word.Bounds.w, word.Bounds.h))
            .ToImmutableList();
        var lines = result.Lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToList();
        var plainText = lines.Count > 0
            ? string.Join(Environment.NewLine, lines)
            : string.Join(" ", words.Select(word => word.Text));
        return new PageText(result.PageBounds.w, result.PageBounds.h, plainText, words);
    }

    internal static PageText FromPdfPageText(PdfPageText pdfText)
    {
        var words = pdfText.Words
            .Select(word => new PageTextWord(word.Text, word.Left, word.Top, word.Width, word.Height))
            .ToImmutableList();
        return new PageText(pdfText.PageWidth, pdfText.PageHeight, pdfText.Text.Trim(), words);
    }
}
