namespace NAPS2.EtoForms.Desktop;

public class DesktopImagesController
{
    private readonly UiImageList _imageList;

    public DesktopImagesController(UiImageList imageList)
    {
        _imageList = imageList;
    }

    /// <summary>
    /// Constructs a receiver for scanned images.
    /// This keeps images from the same source together, even if multiple sources are providing images at the same time.
    /// </summary>
    /// <param name="insertAfter">The image to insert the received images after, or null to append them to the end
    /// of the list.</param>
    /// <returns></returns>
    public Action<ProcessedImage> ReceiveScannedImage(UiImage? insertAfter = null)
    {
        var lockObj = new object();
        UiImage? last = insertAfter;
        return scannedImage =>
        {
            lock (lockObj)
            {
                var uiImage = new UiImage(scannedImage);
                var shouldSelect = !_imageList.Selection.Any();
                _imageList.Mutate(new ImageListMutation.InsertAfter(uiImage, last), isPassiveInteraction: true);
                if (shouldSelect)
                {
                    _imageList.UpdateSelection(ListSelection.Of(uiImage));
                }
                last = uiImage;
            }
        };
    }

    public void AppendImageBatch(IEnumerable<ProcessedImage> images)
    {
        _imageList.Mutate(
            new ImageListMutation.Append(images.Select(image => new UiImage(image))),
            isPassiveInteraction: true);
    }
}