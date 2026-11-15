namespace NAPS2.Config;

/// <summary>
/// Visual outline shown around preview redaction rectangles. The outline is never part of the exported page.
/// </summary>
public enum RedactionOutlineMode
{
    DuringDrag = 0,
    None = 1,
    UntilApply = 2
}
