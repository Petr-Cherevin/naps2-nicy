using System.Threading;
using NAPS2.Scan;

namespace NAPS2.Ocr;

/// <summary>
/// Runs OCR for a set of images without exporting them. The results aren't accessed directly; as with OCR after
/// scanning, they populate the OcrRequestQueue cache for future Save PDF operations.
///
/// Images can either be provided all at once, or added incrementally (e.g. as they're imported) with Add followed by
/// Seal once there are no more images.
/// </summary>
public class RecognizeOperation : OperationBase
{
    private readonly ScanningContext _scanningContext;
    private readonly List<Task> _tasks = [];
    private readonly TaskCompletionSource<bool> _sealed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // Rendering images to disk for OCR can use a lot of memory, so we limit how many are rendered at once
    private readonly SemaphoreSlim _renderLimiter = new(Math.Max(Environment.ProcessorCount / 2, 1));
    private IOcrEngine? _ocrEngine;
    private OcrParams _ocrParams = OcrParams.Empty;
    private OcrPriority _ocrPriority;

    public RecognizeOperation(ScanningContext scanningContext)
    {
        _scanningContext = scanningContext;

        ProgressTitle = MiscResources.OcrProgress;
        AllowCancel = true;
        AllowBackground = true;
        SkipExitPrompt = true;
    }

    /// <summary>
    /// Starts the operation with a fixed set of images.
    /// </summary>
    public bool Start(IEnumerable<UiImage> images, OcrParams ocrParams, OcrPriority ocrPriority)
    {
        if (!Start(ocrParams, ocrPriority))
        {
            return false;
        }
        foreach (var image in images)
        {
            Add(image.GetClonedImage());
        }
        Seal();
        return true;
    }

    /// <summary>
    /// Starts the operation with no images. Use Add to provide images, then Seal once all images have been added.
    /// </summary>
    public bool Start(OcrParams ocrParams, OcrPriority ocrPriority)
    {
        _ocrEngine = _scanningContext.OcrEngine;
        if (_ocrEngine == null || string.IsNullOrEmpty(ocrParams.LanguageCode))
        {
            return false;
        }
        _ocrParams = ocrParams;
        _ocrPriority = ocrPriority;
        Status = new OperationStatus
        {
            StatusText = MiscResources.RunningOcr
        };
        CancelToken.Register(Seal);

        RunAsync(async () =>
        {
            await _sealed.Task;
            Task[] tasks;
            lock (this)
            {
                tasks = _tasks.ToArray();
            }
            await Task.WhenAll(tasks);
            return !CancelToken.IsCancellationRequested;
        });

        return true;
    }

    /// <summary>
    /// Queues an image for OCR. This takes ownership of the image and disposes it when done.
    /// </summary>
    public void Add(ProcessedImage image)
    {
        lock (this)
        {
            Status.MaxProgress += 1;
            _tasks.Add(Task.Run(() => Process(image)));
        }
        InvokeStatusChanged();
    }

    /// <summary>
    /// Indicates that no more images will be added, so the operation can finish once all queued images are done.
    /// </summary>
    public void Seal() => _sealed.TrySetResult(true);

    private async Task Process(ProcessedImage image)
    {
        try
        {
            using var imageToDispose = image;
            var queue = _scanningContext.OcrRequestQueue;
            if (!CancelToken.IsCancellationRequested && !queue.HasCachedResult(_ocrEngine!, image, _ocrParams))
            {
                // The request queue takes ownership of the temp file and guarantees its eventual deletion
                string tempImageFilePath;
                await _renderLimiter.WaitAsync(CancelToken);
                try
                {
                    tempImageFilePath = _scanningContext.SaveToTempFile(image);
                }
                finally
                {
                    _renderLimiter.Release();
                }
                await queue.Enqueue(_scanningContext, _ocrEngine!, image, tempImageFilePath, _ocrParams, _ocrPriority,
                    CancelToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error running OCR", ex);
        }
        lock (this)
        {
            Status.CurrentProgress += 1;
        }
        InvokeStatusChanged();
    }
}
