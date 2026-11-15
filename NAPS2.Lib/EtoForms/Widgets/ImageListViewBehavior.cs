using Eto.Drawing;
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

    public ImageListViewBehavior(UiThumbnailProvider thumbnailProvider,
        ColorScheme colorScheme, Naps2Config config, PageTextStatusProvider pageTextStatusProvider) : base(colorScheme)
    {
        _thumbnailProvider = thumbnailProvider;
        _config = config;
        _pageTextStatusProvider = pageTextStatusProvider;
        _pageTextStatusProvider.StatusChanged += (_, _) => BadgesChanged?.Invoke(this, EventArgs.Empty);
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
        DrawTextBadge(image, item, listView.ImageSize.Width);
        return image;
    }

    // Draws a small badge in the top-left of the thumbnail to show if the page has text and where it's from
    private void DrawTextBadge(Bitmap image, UiImage item, int thumbnailSize)
    {
        try
        {
            var badge = GetBadge(_pageTextStatusProvider.GetStatus(item));
            if (badge == null)
            {
                return;
            }
            // 18px at the default thumbnail size, scaling up (but not down too far) with the thumbnails
            int size = Math.Max(14, (int) Math.Round(thumbnailSize * 18 / 128.0));
            size = Math.Min(size, Math.Min(image.Width, image.Height));
            int offset = Math.Max(2, size / 6);
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