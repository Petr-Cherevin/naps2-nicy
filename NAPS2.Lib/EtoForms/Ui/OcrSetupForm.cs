using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Lang;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Explicit, mutually exclusive OCR language modes. This is a selector, not a set of
/// navigation tabs: the highlighted mode is the one used after OK, and all three
/// independent choices persist across sessions.
/// </summary>
public class OcrSetupForm : EtoDialogBase
{
    private readonly TesseractLanguageManager _tesseractLanguageManager;
    private readonly Button[] _modeButtons =
    [
        C.Button(OcrSetupText.SingleTab),
        C.Button(OcrSetupText.FavoriteTab),
        C.Button(OcrSetupText.CustomTab)
    ];
    private readonly Panel _modeContent = new();
    private readonly Label _modeHint = new() { Wrap = WrapMode.Word };
    private Control[] _modePages = [];
    private int _activeMode;

    private readonly DropDown _singleLanguage = C.DropDown();
    private readonly IListView<Language> _customLanguages;
    private readonly IListView<Language> _favoriteLanguages;
    // Two full-width, fixed-height summary rows. The second row is normally empty.
    // Their size never depends on the length of the saved language names.
    private readonly Panel _favoriteSummaryArea = new() { Height = 48 };
    private readonly Label _favoriteSummaryLine1 = new() { Wrap = WrapMode.None, Height = 24 };
    private readonly Label _favoriteSummaryLine2 = new() { Wrap = WrapMode.None, Height = 24 };
    private readonly LinkButton _favoriteSummaryExpand = C.Link(">>");
    private List<string> _favoriteLanguageNames = [];
    private bool _allowInterfaceLanguageDefault;
    private readonly Button _saveFavorite = C.Button(OcrSetupText.SaveAsFavorite);
    private readonly EnumDropDownWidget<LocalizedOcrMode> _ocrMode = new()
        { Items = [LocalizedOcrMode.Fast, LocalizedOcrMode.Best] };
    private readonly CheckBox _ocrPreProcessing = C.CheckBox(UiStrings.FixWhiteBalanceAndRemoveNoise);
    private readonly LinkButton _moreLanguages = C.Link(UiStrings.GetMoreLanguages);

    private List<Language> _installed = [];
    private string _favoriteCode = "";
    private string _customCode = "";
    private readonly string _originalCode;
    private bool _reloadingLanguages;

    public OcrSetupForm(Naps2Config config, TesseractLanguageManager tesseractLanguageManager,
        IIconProvider iconProvider, OcrLanguagesListViewBehavior ocrLanguagesListViewBehavior) : base(config)
    {
        Title = UiStrings.OcrSetupFormTitle;
        IconName = "text";
        _tesseractLanguageManager = tesseractLanguageManager;
        _customLanguages = EtoPlatform.Current.CreateListView(ocrLanguagesListViewBehavior);
        _favoriteLanguages = EtoPlatform.Current.CreateListView(ocrLanguagesListViewBehavior);
        _favoriteSummaryExpand.Visible = false;
        _favoriteSummaryExpand.ToolTip = OcrSetupText.ShowAllSavedLanguages;
        _favoriteSummaryExpand.Click += (_, _) => ShowAllFavoriteLanguages();
        _favoriteSummaryArea.SizeChanged += (_, _) => UpdateFavoriteSummary();
        _favoriteSummaryArea.Content = new TableLayout(
            new TableRow(new TableCell(_favoriteSummaryLine1, true)),
            new TableRow(new TableCell(new TableLayout(
                new TableRow(
                    new TableCell(_favoriteSummaryLine2, true),
                    new TableCell(_favoriteSummaryExpand)))
            {
                Padding = new Padding(0),
                Spacing = new Size(0, 0)
            }, true)))
        {
            Padding = new Padding(0),
            Spacing = new Size(0, 0)
        };

        _originalCode = Config.Get(c => c.OcrLanguageCode) ?? "";
        _favoriteCode = Config.Get(c => c.OcrFavoriteLanguageCode) ??
                        Config.Get(c => c.LastOcrMultiLangCode) ??
                        (_originalCode.Contains('+') ? _originalCode : "");
        _customCode = Config.Get(c => c.OcrCustomLanguageCode) ??
                      (_originalCode.Contains('+') ? _originalCode : _favoriteCode);

        var mode = Config.Get(c => c.OcrMode);
        _ocrMode.SelectedItem = mode == LocalizedOcrMode.Legacy ? LocalizedOcrMode.Fast : mode;
        _ocrPreProcessing.Checked = Config.Get(c => c.OcrPreProcessing);

        _moreLanguages.Click += MoreLanguages_Click;
        _saveFavorite.Click += (_, _) =>
        {
            var chosen = SelectedCustomCode();
            if (string.IsNullOrEmpty(chosen))
            {
                MessageBox.Show(this, OcrSetupText.SelectLanguage, Title,
                    MessageBoxButtons.OK, MessageBoxType.Warning);
                return;
            }
            // Saving a favorite does not silently change the active OCR mode.
            _favoriteCode = chosen;
            SetFavoriteSelection(_favoriteCode);
            UpdateFavoriteSummary();
        };

        // A user's chosen OCR language always wins over the interface-language
        // suggestion. Do not silently select a different installed language.
        _allowInterfaceLanguageDefault =
            !Config.User.Has(c => c.OcrLanguageCode) &&
            !Config.User.Has(c => c.OcrSingleLanguageCode);
        LoadLanguages(_customCode);
        _favoriteLanguages.SelectionChanged += (_, _) =>
        {
            if (_reloadingLanguages) return;
            _favoriteCode = SelectedFavoriteCode();
            UpdateFavoriteSummary();
        };
        var storedSingleCode = Config.Get(c => c.OcrSingleLanguageCode);
        var singleCode = storedSingleCode ?? (_originalCode.Contains('+') ? "" : _originalCode);
        if (!string.IsNullOrEmpty(singleCode) && _singleLanguage.Items.Any(x => x.Key == singleCode))
            _singleLanguage.SelectedKey = singleCode;
        else if (_allowInterfaceLanguageDefault)
            SelectInterfaceLanguageIfInstalled();
        else
            _singleLanguage.SelectedIndex = -1;

        _modePages = [MakeSinglePage(), MakeFavoritePage(), MakeCustomPage()];
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            int index = i;
            _modeButtons[i].Click += (_, _) => SelectMode(index);
        }

        int savedMode = Math.Clamp(Config.Get(c => c.OcrLanguageTab), 0, 2);
        if (savedMode == 0 && _originalCode.Contains('+'))
            savedMode = _favoriteCode == _originalCode ? 1 : 2;
        SelectMode(savedMode);
        UpdateView();
    }

    private void SelectMode(int selected)
    {
        if (selected < 0 || selected >= _modePages.Length) return;
        _activeMode = selected;
        _modeContent.Content = _modePages[selected];

        string[] labels = [OcrSetupText.SingleTab, OcrSetupText.FavoriteTab, OcrSetupText.CustomTab];
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            bool active = selected == i;
            _modeButtons[i].Text = active ? "✓ " + labels[i] : labels[i];
            _modeButtons[i].BackgroundColor = active
                ? EtoPlatform.Current.ColorScheme.HighlightBackgroundColor
                : EtoPlatform.Current.ColorScheme.BackgroundColor;
        }
        _modeHint.Text = OcrSetupText.ModeHint;
    }

    private Control MakeSinglePage()
    {
        var layout = new DynamicLayout { Padding = new Padding(12), Spacing = new Size(8, 8) };
        layout.AddRow(C.Label(OcrSetupText.SingleDescription));
        layout.AddRow(_singleLanguage);
        layout.Add(null);
        return layout;
    }

    private Control MakeFavoritePage()
    {
        var layout = new DynamicLayout { Padding = new Padding(12), Spacing = new Size(8, 8) };
        layout.AddRow(C.Label(OcrSetupText.FavoriteDescription));
        layout.AddRow(_favoriteSummaryArea);
        // Editing the saved set does NOT switch to the Custom mode.
        layout.Add(_favoriteLanguages.Control, yscale: true);
        return layout;
    }

    private Control MakeCustomPage()
    {
        var layout = new DynamicLayout { Padding = new Padding(12), Spacing = new Size(8, 8) };
        layout.AddRow(C.Label(OcrSetupText.CustomDescription));
        layout.Add(_customLanguages.Control, yscale: true);
        layout.AddRow(_saveFavorite);
        return layout;
    }

    protected override void BuildLayout()
    {
        FormStateController.Resizable = false;
        LayoutController.Content = L.Column(
            C.Label(OcrSetupText.ModePrompt),
            L.Row(_modeButtons[0].Scale(), _modeButtons[1].Scale(), _modeButtons[2].Scale()).Spacing(0),
            _modeHint,
            // Fixed height keeps OCR options and OK/Cancel in place when language
            // selections, descriptions or pages change.
            _modeContent.NaturalSize(490, 270).Height(270),
            L.Row(
                C.Label(UiStrings.OcrModeLabel).AlignCenter().Padding(right: 40),
                _ocrMode.AsControl().Scale()
            ).Aligned(),
            _ocrPreProcessing,
            C.Spacer(),
            L.Row(
                _moreLanguages.AlignCenter().Padding(right: 30),
                C.Filler(),
                L.OkCancel(C.OkButton(this, Save), C.CancelButton(this))
            )
        );
    }

    private void LoadLanguages(string? customCode)
    {
        _reloadingLanguages = true;
        try
        {
        var priorSingle = _singleLanguage.SelectedKey;
        _installed = _tesseractLanguageManager.InstalledLanguages.OrderBy(x => x.Name).ToList();
        _singleLanguage.Items.Clear();
        _singleLanguage.Items.AddRange(_installed.Select(language => new ListItem
        {
            Key = language.Code,
            Text = language.Name
        }));
        if (!string.IsNullOrEmpty(priorSingle) && _singleLanguage.Items.Any(item => item.Key == priorSingle))
            _singleLanguage.SelectedKey = priorSingle;
        else
            _singleLanguage.SelectedIndex = -1;

        _customLanguages.SetItems(_installed);
        _favoriteLanguages.SetItems(_installed);
        SetCustomSelection(customCode);
        SetFavoriteSelection(_favoriteCode);
        UpdateFavoriteSummary();
        }
        finally
        {
            _reloadingLanguages = false;
        }
    }

    private void SetCustomSelection(string? code)
    {
        var selected = (code ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _customLanguages.Selection = ListSelection.From(_installed.Where(x => selected.Contains(x.Code)));
    }

    private void SetFavoriteSelection(string? code)
    {
        var selected = (code ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _favoriteLanguages.Selection = ListSelection.From(_installed.Where(x => selected.Contains(x.Code)));
    }

    private static string SelectionCode(IListView<Language> list) =>
        string.Join("+", list.Selection.OrderBy(x => x.Name).Select(x => x.Code));

    private string SelectedCustomCode() => SelectionCode(_customLanguages);
    private string SelectedFavoriteCode() => SelectionCode(_favoriteLanguages);

    private static string FitSummaryLine(string text, float maxWidth, Font font, out string rest)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            rest = text;
            return "";
        }

        if (font.MeasureString(text).Width <= maxWidth)
        {
            rest = "";
            return text;
        }

        // Binary search by measured pixel width, rather than a character count,
        // so two rows work with different fonts and Windows display scaling.
        int lo = 0, hi = text.Length;
        while (lo < hi)
        {
            int middle = (lo + hi + 1) / 2;
            if (font.MeasureString(text[..middle]).Width <= maxWidth)
                lo = middle;
            else
                hi = middle - 1;
        }

        int length = Math.Max(1, lo);
        int lastSpace = text.LastIndexOf(' ', length - 1, length);
        if (lastSpace > 0)
            length = lastSpace;
        rest = text[length..].TrimStart();
        return text[..length].TrimEnd();
    }

    private void UpdateFavoriteSummary()
    {
        _favoriteLanguageNames = _favoriteCode
            .Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => _installed.FirstOrDefault(lang => lang.Code == code)?.Name ?? code)
            .ToList();

        string summary = string.Join(" + ", _favoriteLanguageNames);
        _favoriteSummaryLine1.ToolTip = summary;
        _favoriteSummaryLine2.ToolTip = summary;
        // A newly constructed Panel has no size yet. Its SizeChanged event
        // recalculates the lines once the form's actual width becomes known.
        int width = _favoriteSummaryArea.Width;
        if (width < 40)
        {
            _favoriteSummaryLine1.Text = " ";
            _favoriteSummaryLine2.Text = " ";
            _favoriteSummaryExpand.Visible = false;
            return;
        }

        var font = _favoriteSummaryLine1.Font;
        string line1 = FitSummaryLine(summary, width, font, out string remaining);
        string line2 = FitSummaryLine(remaining, width, font, out string extra);
        bool overflow = extra.Length > 0;
        if (overflow)
        {
            // Reserve room on the SECOND row for the clickable overflow mark.
            float expandWidth = _favoriteSummaryExpand.Font.MeasureString(">>").Width + 16;
            line2 = FitSummaryLine(remaining, Math.Max(12, width - expandWidth), font, out _);
        }
        _favoriteSummaryLine1.Text = line1.Length == 0 ? " " : line1;
        _favoriteSummaryLine2.Text = line2.Length == 0 ? " " : line2;
        _favoriteSummaryExpand.Visible = overflow;
    }

    private void ShowAllFavoriteLanguages()
    {
        if (_favoriteLanguageNames.Count == 0) return;
        // A native context-menu balloon overlays the dialog without resizing it.
        var popup = new ContextMenu(_favoriteLanguageNames
            .Select(name => (MenuItem)new ButtonMenuItem { Text = name })
            .ToArray());
        popup.Show(_favoriteSummaryExpand);
    }

    private void SelectInterfaceLanguageIfInstalled()
    {
        var uiCulture = CultureInfo.CurrentUICulture;
        string code = uiCulture.Name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                      uiCulture.Name.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                      uiCulture.Name.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase)
            ? "chi_tra"
            : uiCulture.TwoLetterISOLanguageName == "zh"
                ? "chi_sim"
                : uiCulture.ThreeLetterISOLanguageName;

        if (_installed.Any(lang => lang.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
            _singleLanguage.SelectedKey = code;
        else
            _singleLanguage.SelectedIndex = -1;
    }

    private void UpdateView()
    {
        bool languageEnabled = !Config.AppLocked.Has(c => c.OcrLanguageCode);
        foreach (var button in _modeButtons) button.Enabled = languageEnabled;
        _singleLanguage.Enabled = languageEnabled;
        _customLanguages.Control.Enabled = languageEnabled;
        _favoriteLanguages.Control.Enabled = languageEnabled;
        _saveFavorite.Enabled = languageEnabled;
        _moreLanguages.Enabled = languageEnabled;
        _ocrMode.Enabled = !Config.AppLocked.Has(c => c.OcrMode);
        _ocrPreProcessing.Enabled = !Config.AppLocked.Has(c => c.OcrPreProcessing);
    }

    private void MoreLanguages_Click(object? sender, EventArgs e)
    {
        _customCode = SelectedCustomCode();
        _favoriteCode = SelectedFavoriteCode();
        FormFactory.Create<OcrDownloadForm>().ShowModal();
        LoadLanguages(_customCode);
        if (_allowInterfaceLanguageDefault && _singleLanguage.SelectedIndex < 0)
            SelectInterfaceLanguageIfInstalled();
    }

    public bool Saved { get; private set; }

    private bool Save()
    {
        var transaction = Config.User.BeginTransaction();
        if (!Config.AppLocked.Has(c => c.OcrLanguageCode))
        {
            string favorite = SelectedFavoriteCode();
            string custom = SelectedCustomCode();
            string selected = _activeMode switch
            {
                1 => favorite,
                2 => custom,
                _ => _singleLanguage.SelectedKey ?? ""
            };
            var installedCodes = _installed.Select(x => x.Code).ToHashSet();
            if (string.IsNullOrEmpty(selected) ||
                selected.Split('+', StringSplitOptions.RemoveEmptyEntries).Any(x => !installedCodes.Contains(x)))
            {
                MessageBox.Show(this, OcrSetupText.SelectLanguage, Title,
                    MessageBoxButtons.OK, MessageBoxType.Warning);
                return false;
            }

            transaction.Set(c => c.OcrLanguageCode, selected);
            transaction.Set(c => c.OcrSingleLanguageCode, _singleLanguage.SelectedKey);
            transaction.Set(c => c.OcrFavoriteLanguageCode, favorite);
            transaction.Set(c => c.OcrCustomLanguageCode, custom);
            transaction.Set(c => c.OcrLanguageTab, _activeMode);
            if (favorite.Contains('+'))
                transaction.Set(c => c.LastOcrMultiLangCode, favorite);
        }
        if (!Config.AppLocked.Has(c => c.OcrMode))
            transaction.Set(c => c.OcrMode, _ocrMode.SelectedItem);
        if (!Config.AppLocked.Has(c => c.OcrPreProcessing))
            transaction.Set(c => c.OcrPreProcessing, _ocrPreProcessing.IsChecked());
        transaction.Commit();
        Saved = true;
        return true;
    }
}
