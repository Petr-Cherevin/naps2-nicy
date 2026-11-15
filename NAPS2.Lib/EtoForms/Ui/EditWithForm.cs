using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

internal class EditWithForm : EtoDialogBase
{
    private readonly IOpenWith _openWith;

    public EditWithForm(Naps2Config config, IOpenWith openWith) : base(config)
    {
        Title = UiStrings.EditWithFormTitle;
        IconName = "pencil";
        _openWith = openWith;
    }

    protected override void BuildLayout()
    {
        FormStateController.FixedHeightLayout = true;
        LayoutController.DefaultSpacing = 0;

        var entries = new List<OpenWithEntry>();
        try
        {
            entries = _openWith.GetEntries(".jpg")
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name) &&
                                !entry.Name.StartsWith("NAPS2", StringComparison.OrdinalIgnoreCase))
                .GroupBy(entry => entry.Id)
                .Select(group => group.First())
                .ToList();
        }
        catch (Exception ex)
        {
            Log.ErrorException("Could not list image editors", ex);
        }

        var editorButtons = entries.Select(entry =>
            C.Button(new ActionCommand(() =>
            {
                Result = entry;
                Close();
            })
            {
                Text = entry.Name,
                Image = _openWith.LoadIcon(entry)?.ToEtoImage(),
                IconName = "pencil"
            }, ButtonImagePosition.Left, ButtonFlags.LargeText | ButtonFlags.LargeIcon)
                .NaturalWidth(500).Height(50)).ToList();

        LayoutController.Content = L.Column(
            L.Scrollable(L.Column(editorButtons.ToArray())).Scale(),
            EtoPlatform.Current.IsWindows
                ? C.Button(PreviewEditingText.BrowseExecutable, BrowseForExecutable).NaturalWidth(500)
                : C.None()
        );
    }

    private void BrowseForExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = PreviewEditingText.ChooseExecutable,
            CheckFileExists = true,
            MultiSelect = false,
            Filters = { new FileFilter(PreviewEditingText.ExecutableFiles, ".exe") }
        };
        if (dialog.ShowDialog(this) != DialogResult.Ok || string.IsNullOrEmpty(dialog.FileName))
        {
            return;
        }
        // Keeping the explicit executable path lets the portable edition work even when Windows has no
        // recommended Open With association for .jpg. The executable is launched without a shell association.
        Result = new OpenWithEntry("exe:" + dialog.FileName,
            Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName, 0);
        Close();
    }

    public OpenWithEntry? Result { get; private set; }
}
