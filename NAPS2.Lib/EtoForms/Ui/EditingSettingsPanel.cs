using Eto.Forms;
using NAPS2.EtoForms.Desktop;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Settings for the image editor and the purely visual redaction outline.
/// </summary>
internal class EditingSettingsPanel : SettingsPanel
{
    private readonly EditWithController _editor;
    private readonly Label _editorName = new();
    private readonly Button _chooseEditor = new() { Text = PreviewEditingText.ChooseEditor };
    private readonly DropDown _outlineMode = new();
    private OpenWithEntry? _pendingEditor;

    public EditingSettingsPanel(IFormBase form, EditWithController editor) : base(form)
    {
        _editor = editor;
        _outlineMode.Items.Add(new ListItem { Text = PreviewEditingText.OutlineDuringDrag });
        _outlineMode.Items.Add(new ListItem { Text = PreviewEditingText.OutlineNone });
        _outlineMode.Items.Add(new ListItem { Text = PreviewEditingText.OutlineUntilApply });
        _chooseEditor.Click += (_, _) =>
        {
            var chosen = _editor.PickApp();
            if (chosen != null)
            {
                _pendingEditor = chosen;
                _editorName.Text = chosen.Name;
            }
        };
        UpdateValues(Config);
    }

    public override string Title => PreviewEditingText.SettingsTitle;

    public override string IconName => "pencil";

    public override LayoutElement BuildScrollableLayout() =>
        L.Column(
            L.GroupBox(PreviewEditingText.EditorGroup,
                L.Column(
                    L.Row(C.Label(PreviewEditingText.CurrentEditor), _editorName),
                    _chooseEditor
                )
            ),
            L.GroupBox(PreviewEditingText.OutlineGroup, _outlineMode)
        );

    private void UpdateValues(Naps2Config config)
    {
        _pendingEditor = null;
        _editorName.Text = config.Get(c => c.EditWithAppName) ?? PreviewEditingText.NotSelected;
        _outlineMode.SelectedIndex = (int) config.Get(c => c.RedactionOutlineMode);
        _outlineMode.Enabled = !config.AppLocked.Has(c => c.RedactionOutlineMode);
        _chooseEditor.Enabled = !config.AppLocked.Has(c => c.EditWithAppPath);
    }

    public override void Save()
    {
        var transact = Config.User.BeginTransaction();
        transact.SetIfChanged(Config, c => c.RedactionOutlineMode,
            (RedactionOutlineMode) Math.Max(0, _outlineMode.SelectedIndex));
        transact.Commit();

        if (_pendingEditor != null)
        {
            _editor.SelectApp(_pendingEditor);
            _pendingEditor = null;
        }
    }

    public override void RestoreDefaults() => UpdateValues(Config.DefaultsOnly);
}
