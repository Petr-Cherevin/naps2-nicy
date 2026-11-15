using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// A simple prompt with a message and a custom set of buttons (plus Cancel), for when the standard message box
/// buttons don't fit.
/// </summary>
public class ChoiceForm : EtoDialogBase
{
    public ChoiceForm(Naps2Config config) : base(config)
    {
    }

    public string Message { get; set; } = "";

    public List<string> Choices { get; set; } = [];

    /// <summary>
    /// The index of the choice that was clicked, or null if the dialog was cancelled.
    /// </summary>
    public int? SelectedChoice { get; private set; }

    protected override void BuildLayout()
    {
        FormStateController.RestoreFormState = false;
        FormStateController.FixedHeightLayout = true;

        var buttons = new List<LayoutElement> { C.Filler() };
        for (int i = 0; i < Choices.Count; i++)
        {
            int choice = i;
            buttons.Add(C.Button(Choices[i], () =>
            {
                SelectedChoice = choice;
                Close();
            }));
        }
        buttons.Add(C.CancelButton(this));

        LayoutController.Content = L.Column(
            C.Label(Message).DynamicWrap(400),
            C.Filler(),
            L.Row(buttons.ToArray())
        );
    }
}
