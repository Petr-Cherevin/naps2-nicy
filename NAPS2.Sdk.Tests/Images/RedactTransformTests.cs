using NAPS2.Images.Bitwise;
using NAPS2.Sdk.Tests.Asserts;
using Xunit;

namespace NAPS2.Sdk.Tests.Images;

public class RedactTransformTests : ContextualTests
{
    [Theory]
    // Coordinates relative to the actual image size
    [InlineData(ImagePixelFormat.RGB24, 2, 3, 4, 5, 10, 10)]
    [InlineData(ImagePixelFormat.ARGB32, 2, 3, 4, 5, 10, 10)]
    [InlineData(ImagePixelFormat.Gray8, 2, 3, 4, 5, 10, 10)]
    [InlineData(ImagePixelFormat.BW1, 2, 3, 4, 5, 10, 10)]
    // Coordinates relative to a larger original (as when applying to a thumbnail)
    [InlineData(ImagePixelFormat.RGB24, 20, 30, 40, 50, 100, 100)]
    [InlineData(ImagePixelFormat.BW1, 20, 30, 40, 50, 100, 100)]
    public void Redact(ImagePixelFormat pixelFormat, int left, int top, int width, int height, int originalWidth,
        int originalHeight)
    {
        var image = ImageContext.Create(10, 10, pixelFormat);
        FillColorImageOp.White.Perform(image);

        image = image.PerformTransform(
            new RedactTransform(left, top, width, height, originalWidth, originalHeight));

        using var rgb = image.CopyWithPixelFormat(ImagePixelFormat.RGB24);
        image.Dispose();
        // The redacted region is x=[2,6), y=[3,8)
        ImageAsserts.PixelColors(rgb, new()
        {
            { (0, 0), 255, 255, 255 },
            { (1, 3), 255, 255, 255 },
            { (2, 2), 255, 255, 255 },
            { (2, 3), 0, 0, 0 },
            { (5, 3), 0, 0, 0 },
            { (2, 7), 0, 0, 0 },
            { (5, 7), 0, 0, 0 },
            { (6, 7), 255, 255, 255 },
            { (5, 8), 255, 255, 255 },
            { (9, 9), 255, 255, 255 }
        });
    }

    [Fact]
    public void RedactWhite()
    {
        var image = ImageContext.Create(10, 10, ImagePixelFormat.RGB24);
        FillColorImageOp.Black.Perform(image);

        image = image.PerformTransform(new RedactTransform(0, 0, 5, 5, 10, 10, true));

        using var result = image;
        ImageAsserts.PixelColors(result, new()
        {
            { (0, 0), 255, 255, 255 },
            { (4, 4), 255, 255, 255 },
            { (5, 4), 0, 0, 0 },
            { (4, 5), 0, 0, 0 }
        });
    }

    [Fact]
    public void RedactOutOfBounds()
    {
        var image = ImageContext.Create(10, 10, ImagePixelFormat.RGB24);
        FillColorImageOp.White.Perform(image);

        // Partially outside the image
        image = image.PerformTransform(new RedactTransform(8, 8, 10, 10, 10, 10));
        // Completely outside the image
        image = image.PerformTransform(new RedactTransform(20, 20, 5, 5, 10, 10));

        using var result = image;
        ImageAsserts.PixelColors(result, new()
        {
            { (7, 7), 255, 255, 255 },
            { (8, 8), 0, 0, 0 },
            { (9, 9), 0, 0, 0 }
        });
    }

    [Fact]
    public void RedactedImageIsNotPassedThrough()
    {
        // A redacted image has to be re-rendered on export, which relies on the transform state being non-empty
        using var image = ScanningContext.CreateProcessedImage(ImageContext.Create(10, 10, ImagePixelFormat.RGB24));
        using var redacted = image.WithTransform(new RedactTransform(2, 3, 4, 5, 10, 10));

        Assert.False(redacted.TransformState.IsEmpty);
        Assert.NotEqual(image, redacted);
    }
}
