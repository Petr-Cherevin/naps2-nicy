using System.Collections.Immutable;
using NAPS2.Pdf;

namespace NAPS2.Ocr;

/// <summary>
/// A word of text on a page, with its position relative to the top-left of the page (in the same units as the page
/// size).
/// </summary>
public record PageTextWord(string Text, float X, float Y, float Width, float Height);

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

    public bool HasLayout => Words.Count > 0 && PageWidth > 0 && PageHeight > 0;

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
