using System.Runtime.CompilerServices;
using System.Text;
using NAPS2.Pdf.Pdfium;

namespace NAPS2.Pdf;

/// <summary>
/// A word from the text layer of a PDF page. Coordinates are in PDF units relative to the top-left of the page.
/// </summary>
internal record PdfPageWord(string Text, float Left, float Top, float Width, float Height);

internal record PdfPageText(float PageWidth, float PageHeight, string Text, List<PdfPageWord> Words);

/// <summary>
/// Describes the text that a page from an imported PDF already has.
/// </summary>
internal enum PdfPageTextKind
{
    /// <summary>
    /// The page has no text (or isn't a PDF page).
    /// </summary>
    None,

    /// <summary>
    /// The page is a scan (a single full-page image) with a hidden text layer, i.e. it has already been through OCR.
    /// </summary>
    HiddenOverScan,

    /// <summary>
    /// The page has real, visible text (e.g. a PDF generated from a word processor).
    /// </summary>
    Visible
}

/// <summary>
/// Reads the existing text layer (with word positions) from an image that's backed by a single-page PDF.
/// </summary>
internal static class PdfiumPageTextReader
{
    // The kind of text only depends on the stored PDF, so we only need to work it out once per storage object
    private static readonly ConditionalWeakTable<IImageStorage, object> TextKindCache = new();

    /// <summary>
    /// Determines what kind of text the PDF page in the given storage has. This doesn't take into account any
    /// transforms that may have been applied since the page was imported.
    /// </summary>
    public static PdfPageTextKind GetTextKind(IImageStorage storage)
    {
        if (!IsPdfStorage(storage))
        {
            return PdfPageTextKind.None;
        }
        if (TextKindCache.TryGetValue(storage, out var cached))
        {
            return (PdfPageTextKind) cached;
        }
        PdfPageTextKind kind;
        // Pdfium is not thread-safe
        lock (PdfiumNativeLibrary.Instance)
        {
            using var doc = LoadDocument(storage);
            if (doc == null || doc.PageCount == 0)
            {
                return PdfPageTextKind.None;
            }
            using var page = doc.GetPage(0);
            using var text = page.GetText();
            if (text.ReadAll().Trim().Length == 0)
            {
                kind = PdfPageTextKind.None;
            }
            else
            {
                kind = IsScanWithHiddenText(page) ? PdfPageTextKind.HiddenOverScan : PdfPageTextKind.Visible;
            }
        }
        lock (TextKindCache)
        {
            if (!TextKindCache.TryGetValue(storage, out _))
            {
                TextKindCache.Add(storage, kind);
            }
        }
        return kind;
    }

    /// <summary>
    /// The fraction of the page size that a scanned image's size and position can differ from the page's by.
    /// </summary>
    private const float ScanCoverageTolerance = 0.1f;

    // This is like PdfiumImageExtractor.GetSingleImageObject, but more lenient about the image's position. That method
    // is for extracting the image as a replacement for the page, so it needs an exact fit. Here we only need to know
    // if the page is essentially a scan, and PDFs from other applications often have images that are slightly smaller
    // than the page.
    private static bool IsScanWithHiddenText(PdfPage page)
    {
        if (page.AnnotCount > 0)
        {
            return false;
        }
        bool hasImage = false;
        var objectCount = page.ObjectCount;
        for (int i = 0; i < objectCount; i++)
        {
            // Note that objects from PdfPage.GetObject don't need to be disposed
            var pageObj = page.GetObject(i);
            if (pageObj.IsImage && !hasImage && CoversPage(pageObj.Matrix, page.Width, page.Height))
            {
                hasImage = true;
            }
            else if (pageObj.IsText && (!hasImage || PdfiumImageExtractor.IsInvisibleText(pageObj)))
            {
                // Skip invisible text or text that's underneath the image
            }
            else
            {
                // We have other visible objects than a single image
                return false;
            }
        }
        return hasImage;
    }

    /// <summary>
    /// Checks if an image with the given transformation matrix is upright and covers (almost all of) the page.
    /// </summary>
    internal static bool CoversPage(PdfMatrix matrix, float pageWidth, float pageHeight)
    {
        float xTolerance = pageWidth * ScanCoverageTolerance;
        float yTolerance = pageHeight * ScanCoverageTolerance;
        return Math.Abs(matrix.a - pageWidth) <= xTolerance &&
               Math.Abs(matrix.d - pageHeight) <= yTolerance &&
               Math.Abs(matrix.e) <= xTolerance &&
               Math.Abs(matrix.f) <= yTolerance &&
               // No meaningful rotation or skew
               Math.Abs(matrix.b) <= yTolerance / 10 &&
               Math.Abs(matrix.c) <= xTolerance / 10;
    }

    public static bool IsPdfStorage(IImageStorage storage) => storage switch
    {
        ImageFileStorage fileStorage => Path.GetExtension(fileStorage.FullPath).ToLowerInvariant() == ".pdf",
        ImageMemoryStorage memoryStorage => memoryStorage.TypeHint == ".pdf",
        _ => false
    };

    /// <summary>
    /// Reads the text layer for the given storage, or returns null if the storage isn't a PDF.
    /// </summary>
    public static PdfPageText? Read(IImageStorage storage)
    {
        if (!IsPdfStorage(storage))
        {
            return null;
        }
        // Pdfium is not thread-safe
        lock (PdfiumNativeLibrary.Instance)
        {
            using var doc = LoadDocument(storage);
            if (doc == null || doc.PageCount == 0)
            {
                return null;
            }
            using var page = doc.GetPage(0);
            using var text = page.GetText();
            return new PdfPageText(page.Width, page.Height, text.ReadAll(), ReadWords(text, page.Height));
        }
    }

    private static PdfDocument? LoadDocument(IImageStorage storage)
    {
        switch (storage)
        {
            case ImageFileStorage fileStorage:
                return PdfDocument.Load(fileStorage.FullPath);
            case ImageMemoryStorage memoryStorage:
                var buffer = memoryStorage.Stream.ToArray();
                return PdfDocument.Load(buffer, buffer.Length);
            default:
                return null;
        }
    }

    private static List<PdfPageWord> ReadWords(PdfText text, float pageHeight)
    {
        var words = new List<PdfPageWord>();
        var current = new StringBuilder();
        bool hasBox = false;
        float left = 0, top = 0, right = 0, bottom = 0;

        void EndWord()
        {
            if (current.Length > 0 && hasBox)
            {
                // PDF coordinates have the origin at the bottom-left, so we flip the Y axis
                words.Add(new PdfPageWord(current.ToString(), left, pageHeight - top, right - left, top - bottom));
            }
            current.Clear();
            hasBox = false;
        }

        var charCount = text.CharCount;
        for (int i = 0; i < charCount; i++)
        {
            int value = text.ReadUtf32(i);
            // Characters outside the BMP may be returned as separate UTF-16 surrogates
            string str = value is >= 0xD800 and <= 0xDFFF ? ((char) value).ToString() : char.ConvertFromUtf32(value);
            if (str.Length == 1 && (char.IsWhiteSpace(str[0]) || char.IsControl(str[0])))
            {
                EndWord();
                continue;
            }
            current.Append(str);
            PdfRect box;
            try
            {
                box = text.GetCharBox(i);
            }
            catch (Exception)
            {
                continue;
            }
            if (!hasBox)
            {
                (left, top, right, bottom) = (box.Left, box.Top, box.Right, box.Bottom);
                hasBox = true;
            }
            else
            {
                left = Math.Min(left, box.Left);
                right = Math.Max(right, box.Right);
                top = Math.Max(top, box.Top);
                bottom = Math.Min(bottom, box.Bottom);
            }
        }
        EndWord();
        return words;
    }
}
