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
    private readonly ScanningContext _scanningContext;
    private readonly Naps2Config _config;
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
        _scanningContext = scanningContext;
        _config = config;
        _scanningContext.OcrRequestQueue.ResultsChanged += (_, _) => StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Occurs when the status of some image may have changed due to OCR completing. This can be raised on any thread.
    /// </summary>
    public event EventHandler? StatusChanged;

    public PageTextStatus GetStatus(UiImage uiImage)
    {
        try
        {
            using var image = uiImage.GetClonedImage();
            if (HasOcrResult(image))
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
            var originalKind = ExistingText.GetOriginalKind(image);
            if (originalKind != ExistingTextKind.None)
            {
                if (!image.TransformState.IsEmpty)
                {
                    return PageTextStatus.Lost;
                }
                return originalKind == ExistingTextKind.Visible
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

    private bool HasOcrResult(ProcessedImage image)
    {
        var ocrEngine = _scanningContext.OcrEngine;
        var ocrParams = _config.ManualOcrParams();
        if (ocrEngine == null || string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return false;
        }
        return _scanningContext.OcrRequestQueue.GetCachedResult(ocrEngine, image, ocrParams) != null;
    }
}
