using System.Text;
using NAPS2.Pdf.Pdfium;

namespace NAPS2.Pdf;

/// <summary>
/// A word from the text layer of a PDF page. Coordinates are in PDF units relative to the top-left of the page.
/// </summary>
internal record PdfPageWord(string Text, float Left, float Top, float Width, float Height);

internal record PdfPageText(float PageWidth, float PageHeight, string Text, List<PdfPageWord> Words);

/// <summary>
/// Reads the existing text layer (with word positions) from an image that's backed by a single-page PDF.
/// </summary>
internal static class PdfiumPageTextReader
{
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
