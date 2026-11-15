using System.Diagnostics;
using System.Globalization;
using System.Threading;
using NAPS2.EtoForms;
using NAPS2.ImportExport;
using NAPS2.ImportExport.Email;
using NAPS2.Ocr;

namespace NAPS2.Pdf;

internal class SavePdfOperation : OperationBase
{
    private readonly PdfExporter _pdfExporter;
    private readonly IOverwritePrompt _overwritePrompt;
    private readonly NameManager _nameManager;
    private readonly IEmailProviderFactory? _emailProviderFactory;

    public SavePdfOperation(PdfExporter pdfExporter, IOverwritePrompt overwritePrompt, NameManager nameManager,
        IEmailProviderFactory? emailProviderFactory = null)
    {
        _pdfExporter = pdfExporter;
        _overwritePrompt = overwritePrompt;
        _nameManager = nameManager;
        _emailProviderFactory = emailProviderFactory;

        AllowCancel = true;
        AllowBackground = true;
    }

    public string? FirstFileSaved { get; private set; }

    /// <summary>
    /// Saves the provided images to a PDF file with the given name, or one PDF per image if specified by the settings.
    /// </summary>
    /// <param name="fileName">The file name to save to, which may contain placeholders.</param>
    /// <param name="images">The images to save.</param>
    /// <param name="pdfSettings">The PDF settings.</param>
    /// <param name="ocrParams">The OCR parameters.</param>
    /// <param name="emailMessage">If specified, the saved file(s) are attached to the message and it is sent.</param>
    /// <param name="saveParams">How files are named and existing files are handled.</param>
    public bool Start(string fileName, ICollection<ProcessedImage> images, PdfSettings pdfSettings,
        OcrParams ocrParams, EmailMessage? emailMessage = null, SaveParams? saveParams = null,
        PdfOcrScope ocrScope = PdfOcrScope.PagesWithoutText)
    {
        ProgressTitle = emailMessage != null ? MiscResources.EmailPdfProgress : MiscResources.SavePdfProgress;
        saveParams ??= new SaveParams();

        var subFileName = _nameManager.Get(fileName, NameContext.FromSaveParams(saveParams));
        if (Directory.Exists(subFileName))
        {
            // Not supposed to be a directory, but ok...
            fileName = Path.Combine(subFileName, "$(n).pdf");
        }
        var imagesByFile = pdfSettings.SinglePagePdfs
            ? images.Select(x => new[] { x }).ToArray()
            : new[] { images.ToArray() };
        saveParams = saveParams with { BatchCount = Math.Max(saveParams.BatchCount ?? 1, imagesByFile.Length) };
        var context = NameContext.FromSaveParams(saveParams);

        var reservations = Enumerable.Range(0, imagesByFile.Length)
            .Select(i => _nameManager.Reserve(fileName, context with { BatchIndex = (context.BatchIndex ?? 0) + i }))
            .ToDisposableList();
        var fileNames = reservations.Select(x => x.Name).ToArray();

        Status = new OperationStatus
        {
            StatusText = string.Format(MiscResources.SavingFormat, Path.GetFileName(fileNames[0])),
            MaxProgress = images.Count
        };
        var saveTimer = Stopwatch.StartNew();
        DiagnosticLog.Write("SavePdf",
            $"begin pages={images.Count} outputFiles={imagesByFile.Length} scope={ocrScope}");

        var singleFile = imagesByFile.Length == 1;
        if (singleFile && saveParams.OverwriteMode.ShouldPromptForPath(fileNames[0]) &&
            _overwritePrompt.ConfirmOverwrite(fileNames[0]) != OverwriteResponse.Yes)
        {
            reservations.Dispose();
            FailedToStart();
            return false;
        }

        RunAsync(async () =>
        {
            bool result = false;
            long lastLoggedAt = -1000;
            try
            {
                int i = 0;
                foreach (var imagesForFile in imagesByFile)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        result = false;
                        break;
                    }
                    var currentFileName = fileNames[i];
                    Status.StatusText = string.Format(MiscResources.SavingFormat, Path.GetFileName(currentFileName));
                    InvokeStatusChanged();

                    int prepared = 0, prepareTotal = 0, completed = 0;
                    bool hadPreparation = false;
                    void ReportPdfProgress()
                    {
                        if (!singleFile) return;
                        int max = imagesForFile.Length + Volatile.Read(ref prepareTotal);
                        int current = Volatile.Read(ref prepared) + Volatile.Read(ref completed);
                        OnProgress(Math.Min(current, max), max);
                    }

                    var progress = new ProgressHandler(singleFile ? (NAPS2.Util.ProgressCallback)((current, max) =>
                    {
                        Volatile.Write(ref completed, current);
                        ReportPdfProgress();
                        if (hadPreparation && current > 0)
                        {
                            Status.StatusText = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                                ? $"Сборка PDF: обработано страниц {current} из {max}"
                                : $"Assembling PDF: {current} of {max} pages";
                            InvokeStatusChanged();
                        }
                        long at = saveTimer.ElapsedMilliseconds;
                        if (current == max || current == 0 || at - lastLoggedAt >= 1000)
                        {
                            lastLoggedAt = at;
                            DiagnosticLog.Write("SavePdf",
                                $"pagesComplete={current}/{max} prepared={prepared}/{prepareTotal} elapsedMs={at}");
                        }
                    }) : null, CancelToken);
                    result = await RunWithRetry(async () =>
                            await WaitForFileNotInUse(currentFileName) &&
                            await _pdfExporter.Export(currentFileName, imagesForFile,
                                new PdfExportParams(pdfSettings.Metadata.Substitute(_nameManager, context.DateTime),
                                    pdfSettings.Encryption, pdfSettings.Compat, pdfSettings.Compression)
                                {
                                    OcrScope = ocrScope
                                },
                                ocrParams, progress, message =>
                                {
                                    DiagnosticLog.Write("PdfPipeline", message);
                                    if (message.StartsWith("stage=finalizePdfSharp") ||
                                        message.StartsWith("stage=passthroughMerge") ||
                                        message.StartsWith("stage=writeMergedPdf"))
                                        DiagnosticLog.SampleProcess();
                                },
                                (done, total) =>
                                {
                                    hadPreparation = true;
                                    Volatile.Write(ref prepareTotal, total);
                                    Volatile.Write(ref prepared, done);
                                    if (singleFile)
                                    {
                                        Status.ProgressType = OperationProgressType.BarOnly;
                                        Status.StatusText =
                                            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                                                ? $"Проверка страниц PDF: {done} из {total}"
                                                : $"Checking PDF pages: {done} of {total}";
                                        ReportPdfProgress();
                                    }
                                    if (done == 1 || done == total || done % 16 == 0)
                                        DiagnosticLog.Write("PdfPrepare",
                                            $"checked={done}/{total} elapsedMs={saveTimer.ElapsedMilliseconds}");
                                }),
                        GetSaveErrorMessage);
                    DiagnosticLog.Write("SavePdf",
                        $"fileDone index={i + 1}/{imagesByFile.Length} pages={imagesForFile.Length} " +
                        $"success={result} elapsedMs={saveTimer.ElapsedMilliseconds}");
                    if (!result)
                    {
                        break;
                    }
                    emailMessage?.Attachments.Add(new EmailAttachment
                    {
                        FilePath = currentFileName,
                        AttachmentName = Path.GetFileName(currentFileName)
                    });
                    if (i == 0)
                    {
                        FirstFileSaved = currentFileName;
                    }
                    i++;
                    if (!singleFile)
                    {
                        OnProgress(i, imagesByFile.Length);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                InvokeError(MiscResources.DontHavePermission, ex);
            }
            catch (Exception ex)
            {
                Log.ErrorException(MiscResources.ErrorSaving, ex);
                InvokeError(MiscResources.ErrorSaving, ex);
            }
            finally
            {
                reservations.Dispose();
                DiagnosticLog.Write("SavePdf",
                    $"exportEnded success={result} elapsedMs={saveTimer.ElapsedMilliseconds} " +
                    $"cancelled={CancelToken.IsCancellationRequested}");
                GC.Collect();
            }

            if (result && emailMessage != null && _emailProviderFactory != null)
            {
                if (CancelToken.IsCancellationRequested)
                {
                    return false;
                }
                Status.StatusText = MiscResources.UploadingEmail;
                Status.CurrentProgress = 0;
                Status.MaxProgress = 1;
                Status.ProgressType = OperationProgressType.MB;
                InvokeStatusChanged();

                result = await RunWithRetry(
                    () => _emailProviderFactory.Default.SendEmail(emailMessage, ProgressHandler),
                    _ => MiscResources.ErrorEmailing);
            }

            return result;
        });
        Success.ContinueWith(task =>
        {
            if (task.Result)
            {
                if (emailMessage != null)
                {
                    Log.Event(EventType.Email, new EventParams
                    {
                        Name = MiscResources.EmailPdf,
                        Pages = images.Count,
                        FileFormat = ".pdf"
                    });
                }
                else
                {
                    Log.Event(EventType.SavePdf, new EventParams
                    {
                        Name = MiscResources.SavePdf,
                        Pages = images.Count,
                        FileFormat = ".pdf"
                    });
                }
            }
        }, TaskContinuationOptions.OnlyOnRanToCompletion);

        return true;
    }
}
