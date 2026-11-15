namespace NAPS2.Pdf;

/// <summary>
/// Determines which pages OCR is run for when exporting a PDF with OCR parameters.
/// </summary>
public enum PdfOcrScope
{
    /// <summary>
    /// OCR is run for every page that doesn't already have text. Pages from an imported PDF that have text are
    /// exported as-is. This is the default.
    /// </summary>
    PagesWithoutText,

    /// <summary>
    /// As PagesWithoutText, but OCR is also re-run for scanned pages from an imported PDF that only have a hidden text
    /// layer (i.e. pages that were already OCR'd by something else). Pages with visible text are still exported as-is.
    /// </summary>
    AllScans,

    /// <summary>
    /// No new OCR is run. Text is only included for pages that already have completed OCR results (e.g. from OCR that
    /// was explicitly requested before exporting).
    /// </summary>
    CachedOnly
}
