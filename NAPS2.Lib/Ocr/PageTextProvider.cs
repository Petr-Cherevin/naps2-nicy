using System.Threading;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace NAPS2.Ocr;

/// <summary>
/// Describes where the text for a page can come from.
/// </summary>
public enum PageTextSource
{
    /// <summary>
    /// The page has no text available; OCR needs to be run first.
    /// </summary>
    None,

    /// <summary>
    /// OCR results are already available for the page in its current state.
    /// </summary>
    Ocr,

    /// <summary>
    /// The page is an unmodified page from an imported PDF that has its own text layer.
    /// </summary>
    Pdf,

    /// <summary>
    /// The page is from an imported PDF with a text layer, but it has been changed (e.g. rotated) since being
    /// imported, so the text layer might not match the page any more.
    /// </summary>
    PdfChanged,

    /// <summary>
    /// As PdfChanged, but the changes include redaction, so the original text layer must not be shown.
    /// </summary>
    PdfRedacted
}

/// <summary>
/// Gets the text for a page so it can be displayed, either from cached OCR results, by running OCR, or from the text
/// layer of an imported PDF.
/// </summary>
public class PageTextProvider
{
    private readonly ScanningContext _scanningContext;
    private readonly Naps2Config _config;

    public PageTextProvider(ScanningContext scanningContext, Naps2Config config)
    {
        _scanningContext = scanningContext;
        _config = config;
    }

    private OcrParams OcrParams => _config.ManualOcrParams();

    /// <summary>
    /// Whether OCR has been set up (enabled with a language selected), so that Recognize can be used.
    /// </summary>
    public bool CanRecognize =>
        _scanningContext.OcrEngine != null && !string.IsNullOrEmpty(OcrParams.LanguageCode);

    /// <summary>
    /// Determines where text for the image can come from without running OCR.
    /// </summary>
    public PageTextSource GetSource(ProcessedImage image)
    {
        if (GetCachedOcrText(image) != null)
        {
            return PageTextSource.Ocr;
        }
        if (image.TransformState.Transforms.Any(x => x is RemoveTextTransform))
        {
            // The user has explicitly cleared the original text
            return PageTextSource.None;
        }
        var pdfText = ReadPdfText(image);
        if (pdfText == null || pdfText.IsEmpty)
        {
            return PageTextSource.None;
        }
        if (image.TransformState.IsEmpty)
        {
            return PageTextSource.Pdf;
        }
        return image.TransformState.Transforms.Any(x => x is RedactTransform)
            ? PageTextSource.PdfRedacted
            : PageTextSource.PdfChanged;
    }

    /// <summary>
    /// Gets the text from previously completed OCR for the image in its current state, or null if there isn't any.
    /// </summary>
    public PageText? GetCachedOcrText(ProcessedImage image)
    {
        var ocrEngine = _scanningContext.OcrEngine;
        var ocrParams = OcrParams;
        if (ocrEngine == null || string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return null;
        }
        var result = _scanningContext.OcrRequestQueue.GetCachedResult(ocrEngine, image, ocrParams);
        return result == null ? null : PageText.FromOcrResult(result);
    }

    /// <summary>
    /// Gets the text layer from the original PDF page that the image was imported from. This ignores any changes
    /// made to the image since it was imported, so callers are responsible for checking GetSource first.
    /// </summary>
    public PageText? GetPdfText(ProcessedImage image)
    {
        var pdfText = ReadPdfText(image);
        return pdfText == null || pdfText.IsEmpty ? null : pdfText;
    }

    /// <summary>
    /// Runs OCR on the image in its current state (or uses cached results if available) and returns the text.
    /// </summary>
    public async Task<PageText?> Recognize(ProcessedImage image, CancellationToken cancelToken = default,
        bool force = false)
    {
        var cached = GetCachedOcrText(image);
        if (!force && cached != null)
        {
            return cached;
        }
        var ocrEngine = _scanningContext.OcrEngine;
        var ocrParams = OcrParams;
        if (ocrEngine == null || string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return null;
        }
        // The request queue takes ownership of the temp file and guarantees its eventual deletion
        var tempImageFilePath = await Task.Run(() => _scanningContext.SaveToTempFile(image), cancelToken);
        var result = await _scanningContext.OcrRequestQueue.Enqueue(_scanningContext, ocrEngine, image,
            tempImageFilePath, ocrParams, OcrPriority.Foreground, cancelToken, force);
        return result == null ? null : PageText.FromOcrResult(result);
    }

    /// <summary>
    /// Signals that the text status of some page may have changed, so anything displaying it should be updated.
    /// </summary>
    public void NotifyStatusChanged() => _scanningContext.OcrRequestQueue.NotifyResultsChanged();

    private static PageText? ReadPdfText(ProcessedImage image)
    {
        try
        {
            var pdfText = PdfiumPageTextReader.Read(image.Storage);
            return pdfText == null ? null : PageText.FromPdfPageText(pdfText);
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error reading PDF text", ex);
            return null;
        }
    }
}
