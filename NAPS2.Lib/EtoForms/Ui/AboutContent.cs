using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Scan;
using NAPS2.Update;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// The "about" information (version, links, update check, etc.), which is shown both in its own window and as a panel
/// in the settings window.
/// </summary>
internal class AboutContent
{
    private const string NAPS2_HOMEPAGE = "https://www.naps2.com";
    private const string ICONS_HOMEPAGE = "https://web.archive.org/web/20230301031953/https://www.fatcow.com/free-icons";
    private const string ICONS_HOMEPAGE_LABEL = "FatCow";
    private const string DONATE_URL = "https://www.naps2.com/donate?src=about";

    private readonly Naps2Config _config;
    private readonly UpdateChecker _updateChecker;
    private readonly Button _donateButton;
    private readonly CheckBox _enableDebugLogging = C.CheckBox(UiStrings.EnableDebugLogging);

    public AboutContent(Naps2Config config, UpdateChecker updateChecker, ScanningContext scanningContext)
    {
        _config = config;
        _updateChecker = updateChecker;

        _donateButton = C.Button(UiStrings.Donate, () => ProcessHelper.OpenUrl(DONATE_URL));
        _donateButton.BackgroundColor = Color.FromRgb(0xfeda96);
        _donateButton.TextColor = Color.FromRgb(0x1b464e);
        _donateButton.Font = new Font(_donateButton.Font.Family, _donateButton.Font.Size * 11 / 10,
            FontStyle.Italic | FontStyle.Bold);
        EtoPlatform.Current.ConfigureDonateButton(_donateButton);

        _enableDebugLogging.Checked = config.Get(c => c.EnableDebugLogging);
        _enableDebugLogging.CheckedChanged += (_, _) =>
        {
            config.User.Set(c => c.EnableDebugLogging, _enableDebugLogging.IsChecked());
            NLogConfig.EnvDebugLogging = _enableDebugLogging.IsChecked();
            scanningContext.WorkerFactory?.RecreateSpareWorkers();
        };
    }

    /// <summary>
    /// Builds the layout for the content.
    /// </summary>
    /// <param name="closeButton">A button to show in the bottom right, if the content is in its own window.</param>
    public LayoutElement BuildLayout(LayoutElement? closeButton = null)
    {
        return L.Row(
            L.Column(new ImageView { Image = Icons.scanner_128.ToEtoImage() }).Padding(right: 4),
            L.Column(
                new TextBox { Text = $"{BuildVariant.ProductName} {BuildVariant.ReleaseNumber}", ReadOnly = true },
                L.Row(
                    L.Column(
                        C.NoWrap(string.Format(MiscResources.Version, AssemblyHelper.Version)),
                        C.NoWrap(UiStrings.UnofficialBuild),
                        C.UrlLink(NAPS2_HOMEPAGE)
                    ),
                    _config.Get(c => c.HiddenButtons).HasFlag(ToolbarButtons.Donate)
                        ? C.None()
                        : L.Column(
                            C.Filler(),
                            _donateButton
                        ).Padding(left: 10)
                ),
                GetUpdateWidget(),
                C.TextSpace(),
                C.NoWrap(string.Format(UiStrings.CopyrightFormat, AssemblyHelper.COPYRIGHT_YEARS)),
                _config.AppLocked.Has(c => c.EnableDebugLogging)
                    ? C.None()
                    : new[] { C.Spacer(), _enableDebugLogging.Padding(left: 4) }.Expand(),
                C.TextSpace(),
                L.Row(
                    L.Column(
                        C.NoWrap(UiStrings.IconsFrom),
                        C.UrlLink(ICONS_HOMEPAGE, ICONS_HOMEPAGE_LABEL)
                    ).Scale(),
                    closeButton != null
                        ? L.Column(
                            C.Filler(),
                            closeButton
                        ).Padding(left: 20)
                        : C.None()
                )
            )
        );
    }

    private LayoutElement GetUpdateWidget()
    {
#if MSI
        return C.None();
#else
#if NET6_0_OR_GREATER
        if (!OperatingSystem.IsWindows()) return C.None();
#endif
        return new UpdateCheckWidget(_updateChecker, _config);
#endif
    }
}
