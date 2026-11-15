using Eto.Drawing;
using System.Collections.Concurrent;
using System.Threading;
using System.Runtime.CompilerServices;
using Eto.Forms;
using Google.Protobuf;
using NAPS2.ImportExport.Images;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Widgets;

public class ImageListViewBehavior : ListViewBehavior<UiImage>
{
    private readonly UiThumbnailProvider _thumbnailProvider;
    private readonly Naps2Config _config;
    private readonly PageTextStatusProvider _pageTextStatusProvider;
    private readonly ImageTransfer _imageTransfer = new();
    private readonly Dictionary<PageTextStatus, Bitmap> _badges = new();
    // Status detection may read a PDF text layer. Never do that on the UI/thumbnail rendering thread.
    // A single sequential worker lets thumbnails and import progress stay responsive on slower PCs.
    private ConditionalWeakTable<UiImage, BadgeCacheState> _statusCache = new();
    private readonly ConcurrentQueue<(UiImage Image, BadgeCacheState Entry, object State, string? Language)> _pending = new();
    private int _workerRunning;
    private int _importActive;

    public void SetImportActive(bool active)
    {
        Interlocked.Exchange(ref _importActive, active ? 1 : 0);
        if (!active)
        {
            BadgesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class BadgeCacheState
    {
        public object? State;
        public string? Language;
        public PageTextStatus? Status;
        public bool Pending;
    }

    // Badge size and distance from the top-left corner, as fractions of the thumbnail size
    private const float BadgeSizeFraction = 0.13f;
    private const float BadgeOffsetFraction = 0.015f;
    // Limits on the badge size, in pixels at 100% display scaling
    private const float BadgeMinSize = 11;
    private const float BadgeMaxSize = 26;

    public ImageListViewBehavior(UiThumbnailProvider thumbnailProvider,
        ColorScheme colorScheme, Naps2Config config, PageTextStatusProvider pageTextStatusProvider) : base(colorScheme)
    {
        _thumbnailProvider = thumbnailProvider;
        _config = config;
        _pageTextStatusProvider = pageTextStatusProvider;
        _pageTextStatusProvider.StatusChanged += (_, _) =>
        {
            System.Threading.Interlocked.Exchange(ref _statusCache,
                new ConditionalWeakTable<UiImage, BadgeCacheState>());
            BadgesChanged?.Invoke(this, EventArgs.Empty);
        };
        MultiSelect = true;
        ShowLabels = false;
        ScrollOnDrag = true;
        UseHandCursor = true;
    }

    public override bool ShowPageNumbers => _config.Get(c => c.ShowPageNumbers);

    /// <summary>
    /// Occurs when the badges drawn on the images may be out of date. This can be raised on any thread.
    /// </summary>
    public event EventHandler? BadgesChanged;

    public override Image GetImage(IListView<UiImage> listView, UiImage item)
    {
        using var thumbnail = _thumbnailProvider.GetThumbnail(item, listView.ImageSize.Width);
        var image = thumbnail.ToEtoImage();
        // WinForms draws all badges on the screen, after scaling the thumbnail.
        // Other platforms bake the cached badge in, and never synchronously parse a PDF.
        if (!EtoPlatform.Current.IsWinForms)
        {
            DrawTextBadge(image, item, listView.ImageSize.Width, listView.Control);
        }
        return image;
    }

    // Draws a small badge in the top-left of the thumbnail to show if the page has text and where it's from
    private void DrawTextBadge(Bitmap image, UiImage item, int thumbnailSize, Control listViewControl)
    {
        try
        {
            var badge = GetBadgeFor(item);
            if (badge == null)
            {
                return;
            }
            // The badge is sized relative to the thumbnail so that it's noticeable without covering much of the page,
            // whatever the thumbnail size and display scaling. The limits keep it legible on the smallest thumbnails
            // and unobtrusive on the largest.
            float scale = listViewControl.ParentWindow is { } window ? EtoPlatform.Current.GetScaleFactor(window) : 1f;
            float minSize = BadgeMinSize * scale;
            float maxSize = Math.Max(BadgeMaxSize * scale, minSize);
            int size = (int) Math.Round(Math.Max(minSize, Math.Min(thumbnailSize * BadgeSizeFraction, maxSize)));
            size = Math.Min(size, Math.Min(image.Width, image.Height));
            int offset = (int) Math.Round(Math.Max(scale, thumbnailSize * BadgeOffsetFraction));
            using var graphics = new Graphics(image);
            graphics.ImageInterpolation = ImageInterpolation.High;
            graphics.DrawImage(badge, offset, offset, size, size);
        }
        catch (Exception ex)
        {
            // The badge is only informational so it shouldn't stop the thumbnail from being displayed
            Log.ErrorException("Error drawing page text badge", ex);
        }
    }

    /// <summary>Returns a badge if the status is known; otherwise schedules background detection.</summary>
    public Bitmap? GetBadgeFor(UiImage item)
    {
        if (item.IsDisposed) return null;
        var state = item.TransformState;
        var language = _config.Get(c => c.OcrLanguageCode);
        var entry = _statusCache.GetValue(item, _ => new BadgeCacheState());
        lock (entry)
        {
            if (!Equals(entry.State, state) || entry.Language != language)
            {
                entry.State = state;
                entry.Language = language;
                entry.Status = null;
                entry.Pending = false;
            }
            if (entry.Status is { } status)
            {
                return GetBadge(status);
            }
            if (Volatile.Read(ref _importActive) > 0)
            {
                return null;
            }
            if (!entry.Pending)
            {
                entry.Pending = true;
                _pending.Enqueue((item, entry, state, language));
                StartWorker();
            }
        }
        return null;
    }

    private void StartWorker()
    {
        if (Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
        {
            _ = Task.Run(ProcessPendingStatuses);
        }
    }

    private void ProcessPendingStatuses()
    {
        while (true)
        {
            while (_pending.TryDequeue(out var request))
            {
                var (item, entry, state, language) = request;
                try
                {
                    if (item.IsDisposed) continue;
                    // The page might have been modified while waiting; stale results are ignored below.
                    var result = _pageTextStatusProvider.GetStatus(item);
                    lock (entry)
                    {
                        if (Equals(entry.State, state) && entry.Language == language)
                        {
                            entry.Status = result;
                            entry.Pending = false;
                        }
                    }
                    BadgesChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Log.ErrorException("Error checking OCR badge in background", ex);
                    lock (entry) entry.Pending = false;
                }
            }
            Interlocked.Exchange(ref _workerRunning, 0);
            if (_pending.IsEmpty || Interlocked.CompareExchange(ref _workerRunning, 1, 0) != 0)
            {
                return;
            }
        }
    }

    private Bitmap? GetBadge(PageTextStatus status)
    {
        if (status == PageTextStatus.None)
        {
            return null;
        }
        lock (_badges)
        {
            if (!_badges.TryGetValue(status, out var badge))
            {
                badge = (status switch
                {
                    PageTextStatus.Recognized => Icons.textbadge_ours,
                    PageTextStatus.RecognizedElsewhere => Icons.textbadge_other,
                    PageTextStatus.OriginalText => Icons.textbadge_real,
                    _ => Icons.textbadge_lost
                }).ToEtoImage();
                _badges[status] = badge;
            }
            return badge;
        }
    }

    public override bool AllowDragDrop => true;

    public override bool AllowFileDrop => true;

    public override string CustomDragDataType => _imageTransfer.TypeName;

    public override byte[] SerializeCustomDragData(UiImage[] items)
    {
        using var processedImages = items.Select(x => x.GetClonedImage()).ToDisposableList();
        return _imageTransfer.ToBinaryData(processedImages);
    }

    public override DragEffects GetCustomDragEffect(byte[] data)
    {
        var dataObj = _imageTransfer.FromBinaryData(data);
        return dataObj.ProcessId == Process.GetCurrentProcess().Id
            ? DragEffects.Move
            : DragEffects.Copy;
    }

    public override byte[] MergeCustomDragData(byte[][] dataItems)
    {
        // TODO: Move to ImageTransfer?
        var mergedObj = new ImageTransferData();
        foreach (var data in dataItems)
        {
            var dataObj = _imageTransfer.FromBinaryData(data);
            if (mergedObj.ProcessId != 0 && mergedObj.ProcessId != dataObj.ProcessId)
            {
                throw new ArgumentException("Inconsistent process IDs in drag data");
            }
            mergedObj.ProcessId = dataObj.ProcessId;
            mergedObj.SerializedImages.AddRange(dataObj.SerializedImages);
        }
        return mergedObj.ToByteArray();
    }
}