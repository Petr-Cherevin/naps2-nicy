using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Prompts for a page number and moves the selected images (as a single contiguous block, in document order) so that
/// the first of them ends up at that position.
/// </summary>
public class MoveToPositionForm : EtoDialogBase
{
    private readonly UiImageList _imageList;
    private readonly UndoableNumericMaskedTextBox<int> _position = new();

    public MoveToPositionForm(Naps2Config config, UiImageList imageList) : base(config)
    {
        _imageList = imageList;

        Title = UiStrings.MoveToPositionFormTitle;
        IconName = "arrow_refresh";
    }

    public List<UiImage> SelectedImages { get; set; } = [];

    // The block of moved images has to fit before the end of the list
    private int MaxPosition => Math.Max(_imageList.Images.Count - SelectedImages.Count + 1, 1);

    protected override void BuildLayout()
    {
        FormStateController.RestoreFormState = false;
        FormStateController.FixedHeightLayout = true;

        var selected = SelectedImages.ToHashSet();
        var currentPosition = _imageList.Images.FindIndex(selected.Contains) + 1;
        _position.Text = currentPosition.Clamp(1, MaxPosition).ToString();

        LayoutController.Content = L.Column(
            C.Label(string.Format(UiStrings.NewPositionLabel, MaxPosition)),
            _position.NaturalWidth(150),
            C.Filler(),
            L.Row(
                C.Filler(),
                L.OkCancel(
                    C.OkButton(this, MoveSelected),
                    C.CancelButton(this))
            )
        );
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _position.Focus();
        _position.SelectAll();
    }

    private bool MoveSelected()
    {
        if (!int.TryParse(_position.Text, out int position) || position < 1 || position > MaxPosition)
        {
            _position.Focus();
            _position.SelectAll();
            return false;
        }
        _imageList.Mutate(new ListMutation<UiImage>.MoveToPosition(position - 1), ListSelection.From(SelectedImages));
        return true;
    }
}
