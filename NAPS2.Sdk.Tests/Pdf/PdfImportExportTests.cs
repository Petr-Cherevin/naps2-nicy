using System.Threading;
using NAPS2.ImportExport;
using NAPS2.Ocr;
using NAPS2.Pdf;
using NAPS2.Sdk.Tests.Asserts;
using Xunit;

namespace NAPS2.Sdk.Tests.Pdf;

public class PdfImportExportTests : ContextualTests
{
    private readonly PdfImporter _importer;
    private readonly PdfExporter _exporter;
    private readonly string _importPath;
    private readonly string _exportPath;

    public PdfImportExportTests(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        _importer = new PdfImporter(ScanningContext);
        _exporter = new PdfExporter(ScanningContext);
        _importPath = CopyResourceToFile(PdfResources.word_generated_pdf, "import.pdf");
        _exportPath = P("export.pdf");
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExport(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);
        await _exporter.Export(_exportPath, images, ocrParams: config.OcrParams);

        PdfAsserts.AssertImages(_exportPath, PdfResources.word_p1, PdfResources.word_p2);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportToStream(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var fileStream = File.OpenWrite(_exportPath);
        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);
        await _exporter.Export(fileStream, images, ocrParams: config.OcrParams);
        fileStream.Close();

        PdfAsserts.AssertImages(_exportPath, PdfResources.word_p1, PdfResources.word_p2);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportToNonexistentFolder(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var exportPath = P("blah", "export.pdf");
        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);
        await _exporter.Export(exportPath, images, ocrParams: config.OcrParams);

        PdfAsserts.AssertImages(exportPath, PdfResources.word_p1, PdfResources.word_p2);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportInsertExport(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);

        var toInsert = ScanningContext.CreateProcessedImage(LoadImage(ImageResources.dog));
        var newImages = new List<ProcessedImage>
        {
            images[0],
            toInsert,
            images[1]
        };
        await _exporter.Export(_exportPath, newImages, ocrParams: config.OcrParams);

        PdfAsserts.AssertImages(_exportPath, PdfResources.word_p1, ImageResources.dog, PdfResources.word_p2);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportTransformExport(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);

        var newImages = new List<ProcessedImage>
        {
            images[0].WithTransform(new RotationTransform(90)),
            images[1].WithTransform(new BlackWhiteTransform())
        };
        ImageAsserts.Similar(PdfResources.word_p1_rotated, newImages[0], ignoreResolution: true);
        ImageAsserts.Similar(PdfResources.word_p2_bw, newImages[1], ignoreResolution: true);

        await _exporter.Export(_exportPath, newImages, ocrParams: config.OcrParams);
        PdfAsserts.AssertImages(_exportPath, PdfResources.word_p1_rotated, PdfResources.word_p2_bw);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportRedactExport(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr(new()
        {
            { LoadImage(PdfResources.word_p1), "Page one." },
            { LoadImage(PdfResources.word_p2), "Page two." },
            { LoadImage(PdfResources.word_patcht_p1), "Sized for printing unscaled" }
        });

        var importPathForOcr = P("import_ocr.pdf");
        File.WriteAllBytes(importPathForOcr, PdfResources.word_patcht_pdf);
        var images = await _importer.Import(_importPath).ToListAsync();
        images.AddRange(await _importer.Import(importPathForOcr).ToListAsync());
        Assert.Equal(3, images.Count);

        // Export before redacting, which also means any OCR results for the original pages are cached
        await _exporter.Export(_exportPath, images, ocrParams: config.OcrParams);
        PdfAsserts.AssertContainsTextOnce("Page one.", _exportPath);
        PdfAsserts.AssertContainsTextOnce("Page two.", _exportPath);
        if (config.OcrParams != null)
        {
            PdfAsserts.AssertContainsTextOnce("Sized for printing unscaled", _exportPath);
        }

        // Redact the whole of the first page (which has text in the original PDF) and the third page (which has text
        // from OCR), leaving the second page as-is
        var redactedImages = new List<ProcessedImage>
        {
            RedactAll(images[0]),
            images[1],
            RedactAll(images[2])
        };
        var redactedExportPath = P("export_redacted.pdf");
        await _exporter.Export(redactedExportPath, redactedImages, ocrParams: config.OcrParams);

        PdfAsserts.AssertPageCount(3, redactedExportPath);
        // Neither the text from the original PDF nor the previous OCR results should be in the redacted PDF
        PdfAsserts.AssertDoesNotContainText("Page one.", redactedExportPath);
        PdfAsserts.AssertDoesNotContainText("Sized for printing unscaled", redactedExportPath);
        PdfAsserts.AssertContainsTextOnce("Page two.", redactedExportPath);
    }

    [Fact]
    public async Task ExportWithCachedOcrOnly()
    {
        SetUpFakeOcr(new()
        {
            { LoadImage(PdfResources.word_patcht_p1), "Sized for printing unscaled" }
        });
        var ocrParams = new OcrParams("eng", OcrMode.Fast, 0);
        var exportParams = new PdfExportParams { OcrScope = PdfOcrScope.CachedOnly };
        var importPathForOcr = P("import_ocr.pdf");
        File.WriteAllBytes(importPathForOcr, PdfResources.word_patcht_pdf);
        var images = await _importer.Import(importPathForOcr).ToListAsync();

        // Nothing has been through OCR yet, so there's no text to include
        await _exporter.Export(_exportPath, images, exportParams, ocrParams);
        PdfAsserts.AssertDoesNotContainText("Sized for printing unscaled", _exportPath);

        // Once OCR has been run for the page, the result is included
        var tempPath = ScanningContext.SaveToTempFile(images[0]);
        await ScanningContext.OcrRequestQueue.Enqueue(ScanningContext, ScanningContext.OcrEngine!, images[0],
            tempPath, ocrParams, OcrPriority.Foreground, CancellationToken.None);
        var secondExportPath = P("export2.pdf");
        await _exporter.Export(secondExportPath, images, exportParams, ocrParams);
        PdfAsserts.AssertContainsTextOnce("Sized for printing unscaled", secondExportPath);
    }

    [Fact]
    public async Task ExportScanWithExistingHiddenText()
    {
        SetUpFakeOcr(new()
        {
            { LoadImage(ImageResources.ocr_test), "REPLACEMENT." }
        });
        var ocrParams = new OcrParams("eng", OcrMode.Fast, 0);
        File.WriteAllBytes(_importPath, PdfResources.ocr_test_output);
        var images = await _importer.Import(_importPath).ToListAsync();

        Assert.Equal(PdfPageTextKind.HiddenOverScan, PdfiumPageTextReader.GetTextKind(images[0].Storage));

        // By default the page keeps the text it already has
        await _exporter.Export(_exportPath, images, new PdfExportParams(), ocrParams);
        PdfAsserts.AssertContainsTextOnce("ADVERTISEMENT.", _exportPath);
        PdfAsserts.AssertDoesNotContainText("REPLACEMENT.", _exportPath);

        // With AllScans the existing text is replaced by our own OCR
        var secondExportPath = P("export2.pdf");
        await _exporter.Export(secondExportPath, images, new PdfExportParams { OcrScope = PdfOcrScope.AllScans },
            ocrParams);
        PdfAsserts.AssertContainsTextOnce("REPLACEMENT.", secondExportPath);
        PdfAsserts.AssertDoesNotContainText("ADVERTISEMENT.", secondExportPath);
    }

    [Fact]
    public async Task VisibleTextIsNeverReplaced()
    {
        SetUpFakeOcr(new()
        {
            { LoadImage(PdfResources.word_p1), "REPLACEMENT." }
        });
        var ocrParams = new OcrParams("eng", OcrMode.Fast, 0);
        var images = await _importer.Import(_importPath).ToListAsync();

        Assert.Equal(PdfPageTextKind.Visible, PdfiumPageTextReader.GetTextKind(images[0].Storage));

        await _exporter.Export(_exportPath, images, new PdfExportParams { OcrScope = PdfOcrScope.AllScans },
            ocrParams);
        PdfAsserts.AssertContainsTextOnce("Page one.", _exportPath);
        PdfAsserts.AssertDoesNotContainText("REPLACEMENT.", _exportPath);
    }

    [Theory]
    // Exactly the page size
    [InlineData(595.276f, 0, 0, 841.89f, 0, 0, true)]
    // Slightly smaller than the page with a small offset, as produced by some other applications
    [InlineData(595.2f, 0, 0, 841.68f, 0, 0.20977f, true)]
    // Within 10% of the page size
    [InlineData(550f, 0, 0, 780f, 20f, 30f, true)]
    // Only covers half the page
    [InlineData(595.276f, 0, 0, 420f, 0, 0, false)]
    // Offset too far
    [InlineData(595.276f, 0, 0, 841.89f, 100f, 0, false)]
    // Rotated
    [InlineData(0, 841.89f, -595.276f, 0, 595.276f, 0, false)]
    public void ScanImageCoversPage(float a, float b, float c, float d, float e, float f, bool expected)
    {
        var matrix = new NAPS2.Pdf.Pdfium.PdfMatrix(a, b, c, d, e, f);

        Assert.Equal(expected, PdfiumPageTextReader.CoversPage(matrix, 595.276f, 841.89f));
    }

    [Fact]
    public async Task ReadPageTextWithPositions()
    {
        var images = await _importer.Import(_importPath).ToListAsync();

        var text = PdfiumPageTextReader.Read(images[0].Storage);

        Assert.NotNull(text);
        Assert.Contains("Page one.", text.Text);
        Assert.Contains(text.Words, word => word.Text == "Page");
        Assert.All(text.Words, word =>
        {
            Assert.InRange(word.Left, 0, text.PageWidth);
            Assert.InRange(word.Top, 0, text.PageHeight);
            Assert.True(word.Width > 0 && word.Height > 0);
        });
    }

    private static ProcessedImage RedactAll(ProcessedImage image)
    {
        using var rendered = image.Render();
        return image.WithTransform(
            new RedactTransform(0, 0, rendered.Width, rendered.Height, rendered.Width, rendered.Height));
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportOcrablePdf(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr(new()
        {
            { LoadImage(PdfResources.word_p1), "Page one."},
            { LoadImage(PdfResources.word_p2), "Page two."},
            { LoadImage(PdfResources.word_patcht_p1), "Sized for printing unscaled"}
        });

        var importPathForOcr = P("import_ocr.pdf");
        File.WriteAllBytes(importPathForOcr, PdfResources.word_patcht_pdf);
        var images = await _importer.Import(_importPath).ToListAsync();
        var imagesForOcr = await _importer.Import(importPathForOcr).ToListAsync();

        Assert.Equal(2, images.Count);
        Assert.Single(imagesForOcr);
        ImageAsserts.Similar(PdfResources.word_patcht_p1, imagesForOcr[0], ignoreResolution: true);

        var allImages = images.Concat(imagesForOcr).ToList();

        await _exporter.Export(_exportPath, allImages, ocrParams: config.OcrParams);
        PdfAsserts.AssertImages(_exportPath, PdfResources.word_p1, PdfResources.word_p2, PdfResources.word_patcht_p1);
        PdfAsserts.AssertContainsTextOnce("Page one.", _exportPath);
        if (config.OcrParams != null)
        {
            PdfAsserts.AssertContainsTextOnce("Sized for printing unscaled", _exportPath);
        }
        else
        {
            PdfAsserts.AssertDoesNotContainText("Sized for printing unscaled", _exportPath);
        }
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportPdfWithOcrText(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr(new()
        {
            { LoadImage(ImageResources.ocr_test), "ADVERTISEMENT."}
        });

        File.WriteAllBytes(_importPath, PdfResources.ocr_test_output);

        var images = await _importer.Import(_importPath).ToListAsync();
        await _exporter.Export(_exportPath, images, ocrParams: config.OcrParams);

        PdfAsserts.AssertContainsTextOnce("ADVERTISEMENT.", _exportPath);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportEncrypted(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        var images = await _importer.Import(_importPath).ToListAsync();
        Assert.Equal(2, images.Count);
        await _exporter.Export(_exportPath, images, new PdfExportParams
        {
            Encryption = new()
            {
                EncryptPdf = true,
                OwnerPassword = "hello",
                UserPassword = "world"
            }
        }, config.OcrParams);

        PdfAsserts.AssertEncrypted(_exportPath, "hello", "world");
        PdfAsserts.AssertImages(_exportPath, "world", PdfResources.word_p1, PdfResources.word_p2);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportExportAnnotationPopups(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);
        SetUpFakeOcr();

        // The popup annotations reference their parent annotations, which Pdfium doesn't remap when copying pages
        var importPath = CopyResourceToFile(PdfResources.filled_form_annotated, "annotated.pdf");
        var images = await _importer.Import(importPath).ToListAsync();
        images.AddRange(await _importer.Import(importPath).ToListAsync());
        Assert.Equal(2, images.Count);
        await _exporter.Export(_exportPath, images, ocrParams: config.OcrParams);

        PdfAsserts.AssertImages(_exportPath, ImageAsserts.XPLAT_RMSE_THRESHOLD,
            ImageResources.filled_form_annotated, ImageResources.filled_form_annotated);
    }

    [Theory]
    [ClassData(typeof(OcrTestData))]
    public async Task ImportVariousAndExport(OcrTestConfig config)
    {
        config.StorageConfig.Apply(this);

        var f1 = CopyResourceToFile(PdfResources.word_generated_pdf, "word.pdf");
        var f2 = CopyResourceToFile(PdfResources.word_patcht_pdf, "patcht.pdf");
        var f3 = CopyResourceToFile(PdfResources.image_pdf, "image.pdf");

        var images = new List<ProcessedImage>();

        images.AddRange(await _importer.Import(f1).ToListAsync());
        images.AddRange(await _importer.Import(f2).ToListAsync());
        images.AddRange(await _importer.Import(f3).ToListAsync());
        images.Add(ScanningContext.CreateProcessedImage(LoadImage(ImageResources.ocr_test)));
        Assert.Equal(5, images.Count);

        SetUpFakeOcr(new()
        {
            { LoadImage(PdfResources.word_p1), "Page one."},
            { LoadImage(PdfResources.word_p2), "Page two."},
            { LoadImage(PdfResources.word_patcht_p1), "Sized for printing unscaled"},
            { LoadImage(ImageResources.dog), ""},
            { LoadImage(ImageResources.ocr_test), "ADVERTISEMENT."},
        });
        await _exporter.Export(_exportPath, images, ocrParams: config.OcrParams);

        PdfAsserts.AssertImages(_exportPath,
            PdfResources.word_p1,
            PdfResources.word_p2,
            PdfResources.word_patcht_p1,
            ImageResources.dog,
            ImageResources.ocr_test);

        PdfAsserts.AssertContainsTextOnce("Page one.", _exportPath);
        PdfAsserts.AssertContainsTextOnce("Page two.", _exportPath);
        if (config.OcrParams != null)
        {
            PdfAsserts.AssertContainsTextOnce("ADVERTISEMENT.", _exportPath);
            PdfAsserts.AssertContainsTextOnce("Sized for printing unscaled", _exportPath);
        }
        else
        {
            PdfAsserts.AssertDoesNotContainText("ADVERTISEMENT.", _exportPath);
            PdfAsserts.AssertDoesNotContainText("Sized for printing unscaled", _exportPath);
        }
    }

    [Fact]
    public async Task ImportJpegExportWithoutEncoding()
    {
        SetUpFileStorage();

        var path = CopyResourceToFile(ImageResources.dog, "image.jpg");
        var images = await new ImageImporter(ScanningContext).Import(path).ToListAsync();
        Assert.Single(images);

        await _exporter.Export(_exportPath, images);

        var renderer = new PdfiumPdfRenderer();
        var pdfImage = renderer.RenderPage(ImageContext, _exportPath, PdfRenderSize.Default);
        ImageAsserts.Similar(ImageResources.dog, pdfImage, 0);
    }

    [Fact]
    public async Task ImportExifJpegExportWithoutEncoding()
    {
        SetUpFileStorage();

        var path = CopyResourceToFile(ImageResources.dog_exif, "image.jpg");
        var images = await new ImageImporter(ScanningContext).Import(path).ToListAsync();
        Assert.Single(images);

        await _exporter.Export(_exportPath, images);

        var renderer = new PdfiumPdfRenderer();
        var pdfImage = renderer.RenderPage(ImageContext, _exportPath, PdfRenderSize.Default);
        ImageAsserts.Similar(ImageResources.dog_exif, pdfImage, 0, ignoreResolution: true);
    }
}