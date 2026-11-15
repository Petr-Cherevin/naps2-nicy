using Eto.Forms;
using NAPS2.EtoForms.Desktop;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Lets the user pick the interface language from the settings window, so it's still available if the Language button
/// is hidden from the toolbar.
/// </summary>
internal class LanguageSettingsPanel : SettingsPanel
{
    private readonly DesktopFormProvider _desktopFormProvider;
    private readonly ListBox _languages = new();
    private readonly string? _initialLanguage;

    public LanguageSettingsPanel(IFormBase form, DesktopFormProvider desktopFormProvider, CultureHelper cultureHelper)
        : base(form)
    {
        _desktopFormProvider = desktopFormProvider;
        var currentCulture = Config.Get(c => c.Culture);
        foreach (var (langCode, langName) in cultureHelper.GetAvailableCultures())
        {
            _languages.Items.Add(new ListItem { Key = langCode, Text = langName });
            if (string.Equals(langCode, currentCulture, StringComparison.OrdinalIgnoreCase))
            {
                _languages.SelectedIndex = _languages.Items.Count - 1;
            }
        }
        _initialLanguage = _languages.SelectedKey;
        _languages.Enabled = !Config.AppLocked.Has(c => c.Culture);
    }

    public override string Title => UiStrings.Language;

    public override string IconName => "world";

    public override LayoutElement BuildFixedLayout() => L.Column(_languages.NaturalSize(200, 200).Scale()).Scale();

    public override void Save()
    {
        var selected = _languages.SelectedKey;
        if (string.IsNullOrEmpty(selected) || selected == _initialLanguage || Config.AppLocked.Has(c => c.Culture))
        {
            return;
        }
        // Changing the language recreates the desktop window, so it has to wait until the settings window (which is
        // modal to it) has finished closing
        Invoker.Current.InvokeDispatch(() => _desktopFormProvider.DesktopForm.ChangeLanguage(selected));
    }

    public override void RestoreDefaults()
    {
        // Resetting the language isn't something the user would expect from this, so it's intentionally not affected
    }
}
