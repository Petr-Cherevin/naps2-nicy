using NAPS2.Pdf;

namespace NAPS2.Ocr;

/// <summary>
/// Describes the text that a page already has from the PDF it was imported from.
/// </summary>
public enum ExistingTextKind
{
    /// <summary>
    /// The page has no text of its own (e.g. it was scanned, or imported from an image or an image-only PDF).
    /// </summary>
    None,

    /// <summary>
    /// The page is a scan with a hidden text layer, i.e. it has already been through OCR in another application.
    /// </summary>
    HiddenOverScan,

    /// <summary>
    /// The page has real, visible text (e.g. a PDF generated from a word processor). OCR is never run for these.
    /// </summary>
    Visible
}

public static class ExistingText
{
    /// <summary>
    /// Gets the kind of text the original PDF page for the image has, ignoring any changes made since importing.
    /// </summary>
    public static ExistingTextKind GetOriginalKind(ProcessedImage image)
    {
        try
        {
            return PdfiumPageTextReader.GetTextKind(image.Storage) switch
            {
                PdfPageTextKind.HiddenOverScan => ExistingTextKind.HiddenOverScan,
                PdfPageTextKind.Visible => ExistingTextKind.Visible,
                _ => ExistingTextKind.None
            };
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error reading PDF page text", ex);
            // If we can't tell, the safe assumption is that the page has text we shouldn't replace
            return ExistingTextKind.Visible;
        }
    }

    /// <summary>
    /// Gets the kind of text the image currently has. Once an image has been changed (rotated, cropped, etc.) it's no
    /// longer exported as its original PDF page, so its original text no longer applies.
    /// </summary>
    public static ExistingTextKind GetKind(ProcessedImage image)
    {
        return image.TransformState.IsEmpty ? GetOriginalKind(image) : ExistingTextKind.None;
    }
}
