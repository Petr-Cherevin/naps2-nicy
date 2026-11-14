// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace NAPS2.Images.Transforms;

/// <summary>
/// Covers a rectangular region of the image with a solid opaque color (black by default), destroying the original
/// pixel data in that region when the image is rendered.
/// </summary>
public record RedactTransform : Transform
{
    public RedactTransform()
    {
    }

    /// <summary>
    /// Creates a redaction for the given rectangle, with coordinates in pixels relative to an image of the given
    /// original size. The rectangle is scaled as needed if the transform is applied to an image of a different size
    /// (e.g. a thumbnail).
    /// </summary>
    public RedactTransform(int left, int top, int width, int height, int originalWidth, int originalHeight,
        bool white = false)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
        White = white;
    }

    public int Left { get; private set; }
    public int Top { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public int OriginalWidth { get; private set; }
    public int OriginalHeight { get; private set; }

    public bool White { get; private set; }

    public override bool IsNull => Width <= 0 || Height <= 0;
}
