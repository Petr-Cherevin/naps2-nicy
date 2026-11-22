using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.Config.Model;
using NAPS2.EtoForms.Desktop;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;

namespace NAPS2.EtoForms.Ui;

internal class ToolbarSettingsPanel : SettingsPanel
{
    private readonly DesktopFormProvider _desktopFormProvider;
    private readonly EnumDropDownWidget<DockStyle> _mainPosition = new(scale: false);
    private readonly EnumDropDownWidget<DockStyle> _profilesPosition = new(scale: false);
    private readonly CheckBox _showProfilesToolbar = C.CheckBox(UiStrings.ShowProfilesToolbar);
    private readonly List<CheckBox> _allCheckBoxes;
    // The checkboxes that can be changed, i.e. excluding Settings
    private readonly List<(ToolbarButtons button, CheckBox checkBox)> _buttonCheckBoxes;
    // Updating the preview is relatively slow, so it's done on a timer to let the UI update first, and to batch
    // multiple changes
    private readonly UITimer _previewTimer = new() { Interval = 0.01 };
    // The layout is edited in its own window so its scrollable list never
    // moves the Up/Down controls out of view.
    private readonly Button _customizePreview;
    private List<PreviewToolbarLayout.Item> _previewItems = [];

    private bool _updatingValues;
    private bool _previewing;

    public ToolbarSettingsPanel(IFormBase form, DesktopFormProvider desktopFormProvider) : base(form)
    {
        _desktopFormProvider = desktopFormProvider;
        _customizePreview = C.Button(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru"
                ? "Настроить кнопки и разделители…"
                : "Customize buttons and separators…",
            CustomizePreview);
        var buttons = GetButtons().Select(x => (x.button, checkBox: C.CheckBox(x.label))).ToList();
        _allCheckBoxes = buttons.Select(x => x.checkBox).ToList();
        // Settings can't be removed (except by an admin with HideSettingsButton) so people don't accidentally lock
        // themselves out of toolbar settings
        var settingsCheckBox = buttons.Single(x => x.button == ToolbarButtons.Settings).checkBox;
        settingsCheckBox.Checked = Config.IsToolbarButtonShown(ToolbarButtons.Settings);
        settingsCheckBox.Enabled = false;
        _buttonCheckBoxes = buttons.Where(x => x.button != ToolbarButtons.Settings).ToList();
        UpdateValues(Config);
        _previewTimer.Elapsed += (_, _) => ApplyPreview();
        _mainPosition.SelectedItemChanged += (_, _) => UpdatePreview();
        _profilesPosition.SelectedItemChanged += (_, _) => UpdatePreview();
        _showProfilesToolbar.CheckedChanged += (_, _) =>
        {
            UpdateEnabled();
            UpdatePreview();
        };
        foreach (var (_, checkBox) in _buttonCheckBoxes)
        {
            checkBox.CheckedChanged += (_, _) => UpdatePreview();
        }
    }

    public override string Title => UiStrings.ToolbarSettings;

    public override string IconName => "application";

    // The buttons in the order they appear in the toolbar
    private static IEnumerable<(ToolbarButtons button, string label)> GetButtons()
    {
        yield return (ToolbarButtons.Scan, UiStrings.Scan);
        yield return (ToolbarButtons.Profiles, UiStrings.Profiles);
        if (!EtoPlatform.Current.IsMac)
            yield return (ToolbarButtons.Recognize, UiStrings.Recognize);
        yield return (ToolbarButtons.Import, UiStrings.Import);
        yield return (ToolbarButtons.SavePdf, UiStrings.SavePdf);
        if (!EtoPlatform.Current.IsMac)
            yield return (ToolbarButtons.SaveSeparatePdfs, DesktopCommands.SaveSeparateLabel);
        yield return (ToolbarButtons.SaveImages, UiStrings.SaveImages);
        if (!EtoPlatform.Current.IsMac)
        {
            yield return (ToolbarButtons.Extract, UiStrings.ExtractSelected);
            yield return (ToolbarButtons.ExtractSeparatePdfs, DesktopCommands.ExtractSeparateLabel);
        }
        if (PlatformCompat.System.CanEmail)
            yield return (ToolbarButtons.EmailPdf, UiStrings.EmailPdf);
        if (PlatformCompat.System.CanPrint)
            yield return (ToolbarButtons.Print, UiStrings.Print);
        yield return (ToolbarButtons.Image, UiStrings.Image);
        yield return (ToolbarButtons.Rotate, UiStrings.Rotate);
        if (!EtoPlatform.Current.IsMac)
            yield return (ToolbarButtons.RotateButtons, UiStrings.RotateButtons);
        yield return (ToolbarButtons.Move, UiStrings.MoveUpDown);
        yield return (ToolbarButtons.Reorder, UiStrings.Reorder);
        yield return (ToolbarButtons.Delete, UiStrings.Delete);
        yield return (ToolbarButtons.Clear, UiStrings.Clear);
        yield return (ToolbarButtons.Language, UiStrings.Language);
        yield return (ToolbarButtons.Settings, UiStrings.Settings);
        yield return (ToolbarButtons.About, UiStrings.About);
    }

    public override LayoutElement BuildScrollableLayout()
    {
        int half = (_allCheckBoxes.Count + 1) / 2;
        return L.Column(
            L.GroupBox(
                UiStrings.Toolbar,
                L.Column(
                    L.Row(
                        C.Label(UiStrings.PositionLabel).AlignCenter().Padding(right: 10),
                        _mainPosition.AsControl().MinWidth(100)
                    ).SpacingAfter(6),
                    C.Label(UiStrings.ButtonsLabel).SpacingAfter(6),
                    L.Row(
                        L.Column(_allCheckBoxes.Take(half).Expand()).Scale(),
                        L.Column(_allCheckBoxes.Skip(half).Expand()).Scale()
                    )
                )
            ),
            // Keep the launch button in Settings; the editor itself has a
            // dedicated, non-scrollable window with a scrollable item list.
            EtoPlatform.Current.IsMac
                ? C.None()
                : L.GroupBox(
                    UiStrings.PreviewToolbar,
                    L.Column(_customizePreview)
                ),
            PlatformCompat.System.SupportsProfilesToolbar
                ? L.GroupBox(
                    UiStrings.ProfilesToolbar,
                    L.Column(
                        _showProfilesToolbar,
                        L.Row(
                            C.Label(UiStrings.PositionLabel).AlignCenter().Padding(right: 10),
                            _profilesPosition.AsControl().MinWidth(100)
                        )
                    )
                )
                : C.None()
        );
    }

    private void UpdateValues(Naps2Config config)
    {
        _updatingValues = true;
        _mainPosition.SelectedItem = config.Get(c => c.DesktopToolStripDock);
        _profilesPosition.SelectedItem = config.Get(c => c.ProfilesToolStripDock);
        _showProfilesToolbar.Checked = config.Get(c => c.ShowProfilesToolbar);
        foreach (var (button, checkBox) in _buttonCheckBoxes)
        {
            checkBox.Checked = config.IsToolbarButtonShown(button);
        }
        _previewItems = PreviewToolbarLayout.Parse(config.Get(c => c.PreviewToolbarLayout));
        _customizePreview.Enabled = !Config.AppLocked.Has(c => c.PreviewToolbarLayout);
        UpdateEnabled();
        _updatingValues = false;
    }

    internal static string GetPreviewItemLabel(string id) => id switch
    {
        PreviewToolbarLayout.PageNumber => UiStrings.PageNumber,
        PreviewToolbarLayout.TextStatus => OcrActionText.TextStatus,
        PreviewToolbarLayout.Previous => UiStrings.Previous,
        PreviewToolbarLayout.Next => UiStrings.Next,
        PreviewToolbarLayout.ZoomWindow => UiStrings.ScaleWithWindow,
        PreviewToolbarLayout.ZoomActual => UiStrings.ZoomActual,
        PreviewToolbarLayout.ZoomIn => UiStrings.ZoomIn,
        PreviewToolbarLayout.ZoomOut => UiStrings.ZoomOut,
        PreviewToolbarLayout.ZoomPercent => UiStrings.ZoomPercent,
        PreviewToolbarLayout.Rotate => UiStrings.Rotate,
        PreviewToolbarLayout.Crop => UiStrings.Crop,
        PreviewToolbarLayout.BrightCont => UiStrings.BrightnessContrast,
        PreviewToolbarLayout.HueSat => UiStrings.HueSaturation,
        PreviewToolbarLayout.BlackWhite => UiStrings.BlackAndWhite,
        PreviewToolbarLayout.Sharpen => UiStrings.Sharpen,
        PreviewToolbarLayout.DocumentCorrection => UiStrings.DocumentCorrection,
        PreviewToolbarLayout.Compress => UiStrings.Compress,
        PreviewToolbarLayout.Redact => UiStrings.Redact,
        PreviewToolbarLayout.RedactColor => UiStrings.RedactColor,
        PreviewToolbarLayout.RedactUndo => $"{UiStrings.Redact}: {UiStrings.Undo}",
        PreviewToolbarLayout.RedactApply => $"{UiStrings.Redact}: {UiStrings.Apply}",
        PreviewToolbarLayout.ShowText => UiStrings.ShowText,
        PreviewToolbarLayout.CopyText => UiStrings.CopyText,
        PreviewToolbarLayout.ClearText => UiStrings.ClearText,
        PreviewToolbarLayout.Split => UiStrings.Split,
        PreviewToolbarLayout.Combine => UiStrings.Combine,
        PreviewToolbarLayout.EditWithApp => UiStrings.EditWithLastApp,
        PreviewToolbarLayout.EditWithPick => UiStrings.EditWith,
        PreviewToolbarLayout.SavePdf => UiStrings.SaveSelectedAsPdf,
        PreviewToolbarLayout.Extract => UiStrings.ExtractSelected,
        PreviewToolbarLayout.SaveImages => UiStrings.SaveSelectedAsImages,
        PreviewToolbarLayout.Delete => UiStrings.Delete,
        _ => UiStrings.SeparatorItem
    };

    private static string? GetPreviewItemIconName(string id) => id switch
    {
        PreviewToolbarLayout.Previous => "arrow_left",
        PreviewToolbarLayout.Next => "arrow_right",
        PreviewToolbarLayout.ZoomWindow => "arrow_out",
        PreviewToolbarLayout.ZoomActual => "zoom_actual",
        PreviewToolbarLayout.ZoomIn => "zoom_in",
        PreviewToolbarLayout.ZoomOut => "zoom_out",
        PreviewToolbarLayout.Rotate => "arrow_rotate_anticlockwise",
        PreviewToolbarLayout.Crop => "transform_crop",
        PreviewToolbarLayout.BrightCont => "contrast_with_sun",
        PreviewToolbarLayout.HueSat => "color_management",
        PreviewToolbarLayout.BlackWhite => "contrast_high",
        PreviewToolbarLayout.Sharpen => "sharpen",
        PreviewToolbarLayout.DocumentCorrection => "document",
        PreviewToolbarLayout.Compress => "spring",
        PreviewToolbarLayout.Redact => "redact",
        PreviewToolbarLayout.RedactUndo => "undo",
        PreviewToolbarLayout.RedactApply => "accept",
        PreviewToolbarLayout.ShowText => "text",
        PreviewToolbarLayout.TextStatus => "text",
        PreviewToolbarLayout.CopyText => "copy",
        PreviewToolbarLayout.ClearText => "text_clear",
        PreviewToolbarLayout.Split => "split",
        PreviewToolbarLayout.Combine => "combine",
        PreviewToolbarLayout.EditWithApp => "pencil",
        PreviewToolbarLayout.EditWithPick => "pencil",
        PreviewToolbarLayout.SavePdf => "file_extension_pdf",
        PreviewToolbarLayout.Extract => "arrow_out",
        PreviewToolbarLayout.SaveImages => "picture",
        PreviewToolbarLayout.Delete => "cross",
        _ => null
    };

    // Gets the same icon the item has on the preview toolbar, or a blank image for items that don't have one (so that
    // the labels all line up)
    internal static Image GetPreviewItemIcon(string id, Naps2Config config)
    {
        var iconName = GetPreviewItemIconName(id);
        var icon = iconName == null
            ? null
            : EtoPlatform.Current.IconProvider.GetIcon(iconName, IconScenario.SmallToolbar);
        if (icon != null)
        {
            return icon;
        }
        var blank = new Bitmap(16, 16, PixelFormat.Format32bppRgba);
        if (id == PreviewToolbarLayout.RedactColor)
        {
            using var graphics = new Graphics(blank);
            graphics.FillRectangle(config.Get(c => c.RedactWithBlack) ? Colors.Black : Colors.White, 2, 2, 12, 12);
            graphics.DrawRectangle(Colors.Gray, 2, 2, 11, 11);
        }
        return blank;
    }

    private void CustomizePreview()
    {
        if (Config.AppLocked.Has(c => c.PreviewToolbarLayout)) return;
        using var editor = new PreviewToolbarSettingsForm(Config, _previewItems);
        editor.ShowModal();
        if (editor.SavedItems is { } saved)
        {
            // The editor works on a copy; Cancel never changes these settings.
            _previewItems = saved;
            UpdatePreview();
        }
    }

    private void UpdateEnabled()
    {
        _mainPosition.Enabled = !Config.AppLocked.Has(c => c.DesktopToolStripDock);
        _profilesPosition.Enabled = !Config.AppLocked.Has(c => c.ProfilesToolStripDock) &&
                                    _showProfilesToolbar.IsChecked();
        _showProfilesToolbar.Enabled = !Config.AppLocked.Has(c => c.ShowProfilesToolbar);
        foreach (var (button, checkBox) in _buttonCheckBoxes)
        {
            checkBox.Enabled = button.IsOptional()
                ? !Config.AppLocked.Has(c => c.ExtraButtons)
                : !Config.AppLocked.Has(c => c.HiddenButtons);
        }
    }

    private TransactionConfigScope<CommonConfig> BeginTransaction(out bool changed)
    {
        // Only update the flags for buttons listed here, so other flags (e.g. Sidebar) are preserved
        var hiddenButtons = Config.Get(c => c.HiddenButtons);
        var extraButtons = Config.Get(c => c.ExtraButtons);
        foreach (var (button, checkBox) in _buttonCheckBoxes)
        {
            if (button.IsOptional())
            {
                extraButtons = checkBox.IsChecked() ? extraButtons | button : extraButtons & ~button;
            }
            else
            {
                hiddenButtons = checkBox.IsChecked() ? hiddenButtons & ~button : hiddenButtons | button;
            }
        }

        var transact = Config.User.BeginTransaction();
        changed = false;
        changed |= transact.SetIfChanged(Config, c => c.DesktopToolStripDock, _mainPosition.SelectedItem);
        changed |= transact.SetIfChanged(Config, c => c.ProfilesToolStripDock, _profilesPosition.SelectedItem);
        changed |= transact.SetIfChanged(Config, c => c.ShowProfilesToolbar, _showProfilesToolbar.IsChecked());
        changed |= transact.SetIfChanged(Config, c => c.HiddenButtons, hiddenButtons);
        changed |= transact.SetIfChanged(Config, c => c.ExtraButtons, extraButtons);
        // This only affects preview windows opened later, so it doesn't count as a change for the desktop form
        transact.Set(c => c.PreviewToolbarLayout, PreviewToolbarLayout.Serialize(_previewItems));
        return transact;
    }

    // Show the uncommitted settings on the desktop form so the user can see what they look like
    private void UpdatePreview()
    {
        if (_updatingValues) return;
        _previewTimer.Start();
    }

    private void ApplyPreview()
    {
        _previewTimer.Stop();
        var transact = BeginTransaction(out _);
        _desktopFormProvider.DesktopForm.PreviewToolbarSettings(Config.WithTransaction(transact));
        _previewing = true;
    }

    private void EndPreview()
    {
        _previewTimer.Stop();
        if (_previewing)
        {
            _desktopFormProvider.DesktopForm.PreviewToolbarSettings(null);
            _previewing = false;
        }
    }

    public override void Save()
    {
        _previewTimer.Stop();
        var transact = BeginTransaction(out bool changed);
        transact.Commit();

        if (_previewing)
        {
            EndPreview();
        }
        else if (changed)
        {
            _desktopFormProvider.DesktopForm.ApplyToolbarSettings();
        }
    }

    public override void Cancel()
    {
        EndPreview();
    }

    public override void RestoreDefaults()
    {
        UpdateValues(Config.DefaultsOnly);
        UpdatePreview();
    }
}
