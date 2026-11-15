using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Lang;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Selects the active OCR languages using a single language, a saved favorite set,
/// or an arbitrary installed-language selection. Each tab remembers its own choice.
/// </summary>
public class OcrSetupForm : EtoDialogBase
{
    private readonly TesseractLanguageManager _tesseractLanguageManager;
    private readonly TabControl _languageTabs = new();
    private readonly DropDown _singleLanguage = C.DropDown();
    private readonly IListView<Language> _customLanguages;
    private readonly Label _favoriteSummary = new() { Wrap = WrapMode.Word };
    private readonly Button _editFavorite = C.Button(OcrSetupText.EditFavorite);
    private readonly Button _saveFavorite = C.Button(OcrSetupText.SaveAsFavorite);
    private readonly EnumDropDownWidget<LocalizedOcrMode> _ocrMode = new()
        { Items = [LocalizedOcrMode.Fast, LocalizedOcrMode.Best] };
    private readonly CheckBox _ocrPreProcessing = C.CheckBox(UiStrings.FixWhiteBalanceAndRemoveNoise);
    private readonly LinkButton _moreLanguages = C.Link(UiStrings.GetMoreLanguages);

    private List<Language> _installed = [];
    private string _favoriteCode;
    private string _customCode;
    private readonly string _originalCode;
    private bool _loading;

    public OcrSetupForm(Naps2Config config, TesseractLanguageManager tesseractLanguageManager,
        IIconProvider iconProvider, OcrLanguagesListViewBehavior ocrLanguagesListViewBehavior) : base(config)
    {
        Title = UiStrings.OcrSetupFormTitle;
        IconName = "text";
        _tesseractLanguageManager = tesseractLanguageManager;
        _customLanguages = EtoPlatform.Current.CreateListView(ocrLanguagesListViewBehavior);

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
        _editFavorite.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_favoriteCode))
            {
                SetCustomSelection(_favoriteCode);
            }
            _languageTabs.SelectedIndex = 2;
        };
        _saveFavorite.Click += (_, _) =>
        {
            var chosen = SelectedCustomCode();
            if (string.IsNullOrEmpty(chosen))
            {
                MessageBox.Show(this, OcrSetupText.SelectLanguage, Title,
                    MessageBoxButtons.OK, MessageBoxType.Warning);
                return;
            }
            _favoriteCode = chosen;
            UpdateFavoriteSummary();
            _languageTabs.SelectedIndex = 1;
        };

        LoadLanguages(_customCode);

        var storedSingleCode = Config.Get(c => c.OcrSingleLanguageCode);
        var singleCode = storedSingleCode ?? (_originalCode.Contains('+') ? "" : _originalCode);
        if (_singleLanguage.Items.Any(x => x.Key == singleCode))
        {
            _singleLanguage.SelectedKey = singleCode;
        }
        else if (_singleLanguage.Items.Count > 0)
        {
            _singleLanguage.SelectedIndex = 0;
        }

        int tab = Math.Clamp(Config.Get(c => c.OcrLanguageTab), 0, 2);
        // Existing users did not have tabs; migrate an active multi-language setting sensibly.
        if (tab == 0 && _originalCode.Contains('+'))
        {
            tab = _favoriteCode == _originalCode ? 1 : 2;
        }

        _languageTabs.Pages.Add(new TabPage
        {
            Text = OcrSetupText.SingleTab,
            Content = MakeSinglePage()
        });
        _languageTabs.Pages.Add(new TabPage
        {
            Text = OcrSetupText.FavoriteTab,
            Content = MakeFavoritePage()
        });
        _languageTabs.Pages.Add(new TabPage
        {
            Text = OcrSetupText.CustomTab,
            Content = MakeCustomPage()
        });
        _languageTabs.SelectedIndex = tab;
        UpdateView();
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
        layout.AddRow(_favoriteSummary);
        layout.AddRow(_editFavorite);
        layout.Add(null);
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
            _languageTabs.NaturalSize(490, 270).Scale(),
            L.Row(
                C.Label(UiStrings.OcrModeLabel).AlignCenter().Padding(right: 40),
                _ocrMode.AsControl().Scale()
            ).Aligned(),
            _ocrPreProcessing,
            C.Spacer(),
            L.Row(
                _moreLanguages.AlignCenter().Padding(right: 30),
                C.Filler(),
                L.OkCancel(
                    C.OkButton(this, Save),
                    C.CancelButton(this))
            )
        );
    }

    private void LoadLanguages(string? customCode)
    {
        _loading = true;
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
            _singleLanguage.SelectedKey = priorSingle;
            if (_singleLanguage.SelectedIndex < 0 && _singleLanguage.Items.Count > 0)
            {
                _singleLanguage.SelectedIndex = 0;
            }
            _customLanguages.SetItems(_installed);
            SetCustomSelection(customCode);
            UpdateFavoriteSummary();
        }
        finally
        {
            _loading = false;
        }
    }

    private void SetCustomSelection(string? code)
    {
        var selected = (code ?? "").Split('+', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _customLanguages.Selection = ListSelection.From(_installed.Where(x => selected.Contains(x.Code)));
    }

    private string SelectedCustomCode() =>
        string.Join("+", _customLanguages.Selection.OrderBy(x => x.Name).Select(x => x.Code));

    private void UpdateFavoriteSummary()
    {
        var codes = _favoriteCode.Split('+', StringSplitOptions.RemoveEmptyEntries);
        _favoriteSummary.Text = codes.Length == 0
            ? OcrSetupText.NoFavorite
            : string.Join(" + ", codes.Select(code =>
                _installed.FirstOrDefault(lang => lang.Code == code)?.Name ?? code));
    }

    private void UpdateView()
    {
        bool languageEnabled = !Config.AppLocked.Has(c => c.OcrLanguageCode);
        _languageTabs.Enabled = languageEnabled;
        _moreLanguages.Enabled = languageEnabled;
        _ocrMode.Enabled = !Config.AppLocked.Has(c => c.OcrMode);
        _ocrPreProcessing.Enabled = !Config.AppLocked.Has(c => c.OcrPreProcessing);
    }

    private void MoreLanguages_Click(object? sender, EventArgs e)
    {
        _customCode = SelectedCustomCode();
        FormFactory.Create<OcrDownloadForm>().ShowModal();
        LoadLanguages(_customCode);
    }

    public bool Saved { get; private set; }

    private bool Save()
    {
        var transaction = Config.User.BeginTransaction();
        if (!Config.AppLocked.Has(c => c.OcrLanguageCode))
        {
            string selected = _languageTabs.SelectedIndex switch
            {
                1 => _favoriteCode,
                2 => SelectedCustomCode(),
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
            transaction.Set(c => c.OcrFavoriteLanguageCode, _favoriteCode);
            transaction.Set(c => c.OcrCustomLanguageCode, SelectedCustomCode());
            transaction.Set(c => c.OcrLanguageTab, _languageTabs.SelectedIndex);
            if (_favoriteCode.Contains('+'))
            {
                transaction.Set(c => c.LastOcrMultiLangCode, _favoriteCode);
            }
        }
        if (!Config.AppLocked.Has(c => c.OcrMode))
        {
            transaction.Set(c => c.OcrMode, _ocrMode.SelectedItem);
        }
        if (!Config.AppLocked.Has(c => c.OcrPreProcessing))
        {
            transaction.Set(c => c.OcrPreProcessing, _ocrPreProcessing.IsChecked());
        }
        transaction.Commit();
        Saved = true;
        return true;
    }
}
