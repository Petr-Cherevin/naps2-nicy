using System.Runtime.CompilerServices;
using NAPS2.Scan;

namespace NAPS2.Ocr;

/// <summary>
/// Describes whether a page has text (as far as saving a PDF is concerned) and where it comes from.
/// </summary>
public enum PageTextStatus
{
    /// <summary>
    /// The page has never had text.
    /// </summary>
    None,

    /// <summary>
    /// OCR has been run for the page in its current state.
    /// </summary>
    Recognized,

    /// <summary>
    /// The page is an unchanged scan from an imported PDF that already had (hidden) text from OCR elsewhere.
    /// </summary>
    RecognizedElsewhere,

    /// <summary>
    /// The page is an unchanged page from an imported PDF with real (visible) text.
    /// </summary>
    OriginalText,

    /// <summary>
    /// The page used to have text, but it has since been changed (rotated, redacted, etc.) so the text no longer
    /// applies.
    /// </summary>
    Lost
}

/// <summary>
/// Determines the text status of pages so it can be displayed to the user (e.g. as a badge on thumbnails).
/// </summary>
public class PageTextStatusProvider
{
    private readonly PageTextProvider _pageTextProvider;
    // Remembers which images have had OCR results at some point, so we can tell if the results were lost. This is
    // shared as it's a property of the image, not of whatever is asking about it.
    private static readonly ConditionalWeakTable<UiImage, object> _hadOcrResult = new();

    /// <summary>
    /// Forgets that the image has had OCR results, for when the user has explicitly cleared its text.
    /// </summary>
    public static void Forget(UiImage uiImage)
    {
        lock (_hadOcrResult)
        {
            _hadOcrResult.Remove(uiImage);
        }
    }

    /// <summary>
    /// Whether the image has had OCR results at some point (that haven't since been explicitly cleared).
    /// </summary>
    public static bool HasHadOcrResult(UiImage uiImage)
    {
        lock (_hadOcrResult)
        {
            return _hadOcrResult.TryGetValue(uiImage, out _);
        }
    }

    public PageTextStatusProvider(ScanningContext scanningContext, Naps2Config config)
    {
        _pageTextProvider = new PageTextProvider(scanningContext, config);
        scanningContext.OcrRequestQueue.ResultsChanged += (_, _) => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the status of some image may have changed due to OCR completing. This can be raised on any thread.
    /// </summary>
    public event EventHandler? StatusChanged;

    /// <summary>Reuse a known PDF status unless this page gained or lost an OCR result.</summary>
    public PageTextStatus GetStatusAfterOcrChange(UiImage uiImage, PageTextStatus previous)
    {
        try
        {
            using var image = uiImage.GetClonedImage();
            if (_pageTextProvider.GetCachedOcrText(image) != null)
            {
                lock (_hadOcrResult)
                {
                    if (!_hadOcrResult.TryGetValue(uiImage, out _))
                        _hadOcrResult.Add(uiImage, new object());
                }
                return PageTextStatus.Recognized;
            }
            if (previous != PageTextStatus.Recognized) return previous;
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error checking OCR status", ex);
        }
        return GetStatus(uiImage);
    }

    public PageTextStatus GetStatus(UiImage uiImage)
    {
        try
        {
            using var image = uiImage.GetClonedImage();
            // Match Preview > Show Text: its source detection can find PDF text absent from the old badge path.
            var textSource = _pageTextProvider.GetSource(image);
            if (textSource == PageTextSource.Ocr)
            {
                lock (_hadOcrResult)
                {
                    if (!_hadOcrResult.TryGetValue(uiImage, out _))
                    {
                        _hadOcrResult.Add(uiImage, new object());
                    }
                }
                return PageTextStatus.Recognized;
            }
            if (image.TransformState.Transforms.Any(x => x is RemoveTextTransform))
            {
                // The user has explicitly cleared the text, so the page is now just a page without text
                return PageTextStatus.None;
            }
            if (textSource is PageTextSource.PdfChanged or PageTextSource.PdfRedacted)
            {
                return PageTextStatus.Lost;
            }
            if (textSource == PageTextSource.Pdf)
            {
                return ExistingText.GetOriginalKind(image) == ExistingTextKind.Visible
                    ? PageTextStatus.OriginalText
                    : PageTextStatus.RecognizedElsewhere;
            }
            lock (_hadOcrResult)
            {
                return _hadOcrResult.TryGetValue(uiImage, out _) ? PageTextStatus.Lost : PageTextStatus.None;
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error getting page text status", ex);
            return PageTextStatus.None;
        }
    }

}
