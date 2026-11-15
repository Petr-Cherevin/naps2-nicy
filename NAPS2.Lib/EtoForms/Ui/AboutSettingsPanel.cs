using NAPS2.EtoForms.Layout;
using NAPS2.Scan;
using NAPS2.Update;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Shows the "about" information in the settings window, so it's still available if the About button is hidden from
/// the toolbar.
/// </summary>
internal class AboutSettingsPanel : SettingsPanel
{
    private readonly AboutContent _content;

    public AboutSettingsPanel(IFormBase form, UpdateChecker updateChecker, ScanningContext scanningContext)
        : base(form)
    {
        _content = new AboutContent(Config, updateChecker, scanningContext);
    }

    public override string Title => UiStrings.About;

    public override string IconName => "information";

    public override LayoutElement BuildScrollableLayout() => _content.BuildLayout();

    public override void Save()
    {
    }

    public override void RestoreDefaults()
    {
    }
}
