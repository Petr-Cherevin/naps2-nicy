namespace NAPS2.Images.Transforms;

/// <summary>
/// Marks that the image shouldn't keep any text it had when it was imported. This doesn't change the image itself, but
/// as with any transform, it means that a page imported from a PDF is exported as an image rather than by copying the
/// original page (which would include the original text layer).
/// </summary>
public record RemoveTextTransform : Transform
{
    public override bool CanSimplify(Transform other) => other is RemoveTextTransform;

    public override Transform Simplify(Transform other) => this;
}
