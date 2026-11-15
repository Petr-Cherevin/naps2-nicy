using NAPS2.Ocr;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace NAPS2.Config;

public static class ConfigExtensions
{
    public static OcrParams DefaultOcrParams(this Naps2Config config)
    {
        if (!config.Get(c => c.EnableOcr))
        {
            return OcrParams.Empty;
        }
        return new OcrParams(
            config.Get(c => c.OcrLanguageCode),
            MapOcrMode(config.Get(c => c.OcrMode), config.Get(c => c.OcrPreProcessing)),
            config.Get(c => c.OcrTimeoutInSeconds));
    }

    private static OcrMode MapOcrMode(LocalizedOcrMode ocrMode, bool preProcessing)
    {
        return (ocrMode, preProcessing) switch
        {
            (LocalizedOcrMode.Fast, false) => OcrMode.Fast,
            (LocalizedOcrMode.Fast, true) => OcrMode.FastWithPreProcess,
            (LocalizedOcrMode.Best, false) => OcrMode.Best,
            (LocalizedOcrMode.Best, true) => OcrMode.BestWithPreProcess,
            _ => OcrMode.Default
        };
    }

    /// <summary>
    /// Gets the OCR params to use when OCR is explicitly requested by the user. Unlike DefaultOcrParams, this doesn't
    /// depend on automatic OCR (EnableOcr) being turned on, only on a language being selected.
    /// </summary>
    public static OcrParams ManualOcrParams(this Naps2Config config)
    {
        if (string.IsNullOrEmpty(config.Get(c => c.OcrLanguageCode)))
        {
            return OcrParams.Empty;
        }
        return new OcrParams(
            config.Get(c => c.OcrLanguageCode),
            MapOcrMode(config.Get(c => c.OcrMode), config.Get(c => c.OcrPreProcessing)),
            config.Get(c => c.OcrTimeoutInSeconds));
    }

    /// <summary>
    /// Gets which pages OCR should be run for when saving a PDF from the desktop window. If automatic OCR is off,
    /// only pages the user has already run OCR for get text.
    /// </summary>
    public static PdfOcrScope DesktopPdfOcrScope(this Naps2Config config)
    {
        if (!config.Get(c => c.EnableOcr))
        {
            return PdfOcrScope.CachedOnly;
        }
        return config.Get(c => c.OcrAllScans) ? PdfOcrScope.AllScans : PdfOcrScope.PagesWithoutText;
    }

    public static OcrParams OcrAfterScanningParams(this Naps2Config config)
    {
        if (!config.Get(c => c.OcrAfterScanning))
        {
            return OcrParams.Empty;
        }
        return config.DefaultOcrParams();
    }

    public static ScanProfile DefaultProfileSettings(this Naps2Config config)
    {
        return config.Get(c => c.DefaultProfileSettings) ??
               new ScanProfile { Version = ScanProfile.CURRENT_VERSION };
    }
}