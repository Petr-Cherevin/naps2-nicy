using System.ComponentModel;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Desktop;
using NAPS2.EtoForms.Layout;
using NAPS2.EtoForms.Widgets;
using NAPS2.Ocr;

namespace NAPS2.EtoForms.Ui;

public class PreviewForm : EtoDialogBase
{
    private readonly DesktopCommands _desktopCommands;
    private readonly IIconProvider _iconProvider;
    private readonly KeyboardShortcutManager _previewKsm;
    private readonly EditWithController _editWithController;

    private readonly ButtonToolItem _pageNumberButton = new();
    private readonly ButtonToolItem _ocrStatusButton = new() { Enabled = false, Visible = false };
    private readonly ButtonToolItem _showTextToolItem = new();
    protected ButtonToolItem ShowTextToolItem => _showTextToolItem;
    private readonly PageTextStatusProvider _pageTextStatusProvider;
    private int _previewStatusRefreshQueued;
    private bool _previewClosed;
    private UiImage? _lastStatusImage;
    private PageTextStatus? _lastStatus;
    private readonly ButtonToolItem _zoomPercentButton = new();
    private readonly ButtonToolItem _editWithAppButton = new();
    protected ButtonToolItem EditWithAppToolItem => _editWithAppButton;
    protected EditWithController EditWithController => _editWithController;
    private readonly PageTextProvider _pageTextProvider;
    private readonly PageTextView _textView = new();
    private readonly LayoutVisibility _textVisibility = new(false);
    private readonly CheckToolItem _redactButton = new() { ToolTip = UiStrings.Redact };
    private readonly ButtonToolItem _redactColorButton = new() { ToolTip = UiStrings.RedactColor };
    private bool _redactMode;
    // While the newly redacted bitmap is rendering, retain the covering
    // rectangles so the old unredacted bitmap is never exposed.
    private bool _redactionAppliedPendingRefresh;
    private int _imageRefreshRevision;
    private UiImage? _currentImage;

    public PreviewForm(Naps2Config config, DesktopCommands desktopCommands, UiImageList imageList,
        IIconProvider iconProvider, ColorScheme colorScheme, EditWithController editWithController,
        PageTextProvider pageTextProvider, PageTextStatusProvider pageTextStatusProvider) : base(config)
    {
        _pageTextProvider = pageTextProvider;
        _pageTextStatusProvider = pageTextStatusProvider;
        _pageTextStatusProvider.StatusChanged += PageTextStatusChanged;
        Title = UiStrings.PreviewFormTitle;
        Icon = EtoPlatform.Current.IsGtk ? new Icon(1f, Icons.scanner_128.ToEtoImage()) : Icons.favicon.ToEtoIcon();
        Maximizable = true;

        _desktopCommands = desktopCommands;
        ImageList = imageList;
        _iconProvider = iconProvider;
        _editWithController = editWithController;

        ImageViewer.ColorScheme = colorScheme;
        ImageViewer.OutlineMode = config.Get(c => c.RedactionOutlineMode);
        ImageViewer.ZoomChanged += ImageViewerZoomChanged;
        ImageViewer.SelectionRectsChanged += (_, _) => UpdateRedactCommands();
        _redactButton.Click += (_, _) => SetRedactMode(!_redactMode);
        _redactColorButton.Click += (_, _) => ToggleRedactColor();
        ImageList.ImagesUpdated += ImageList_ImagesUpdated;
        _editWithController.AppChanged += EditWithController_AppChanged;

        GoToPrevCommand = new ActionCommand(() => GoTo(ImageIndex - 1))
        {
            Text = UiStrings.Previous,
            IconName = "arrow_left"
        };
        GoToNextCommand = new ActionCommand(() => GoTo(ImageIndex + 1))
        {
            Text = UiStrings.Next,
            IconName = "arrow_right"
        };
        ZoomInCommand = new ActionCommand(() => ImageViewer.ChangeZoom(1))
        {
            Text = UiStrings.ZoomIn,
            IconName = "zoom_in"
        };
        ZoomOutCommand = new ActionCommand(() => ImageViewer.ChangeZoom(-1))
        {
            Text = UiStrings.ZoomOut,
            IconName = "zoom_out"
        };
        ZoomWindowCommand = new ActionCommand(ImageViewer.ZoomToContainer)
        {
            // TODO: Update this string as it's now a button and not a toggle
            Text = UiStrings.ScaleWithWindow,
            IconName = "arrow_out"
        };
        ZoomActualCommand = new ActionCommand(ImageViewer.ZoomToActual)
        {
            Text = UiStrings.ZoomActual,
            IconName = "zoom_actual"
        };
        DeleteCurrentImageCommand = new ActionCommand(DeleteCurrentImage)
        {
            Text = UiStrings.Delete,
            IconName = "cross"
        };
        // Redaction is a two-step process: rectangles are drawn as a preview (and can be undone one at a time), then
        // they only affect the actual image once applied. Unapplied rectangles are discarded.
        RedactUndoCommand = new ActionCommand(ImageViewer.RemoveLastSelectionRect)
        {
            Text = UiStrings.Undo,
            IconName = "undo",
            Enabled = false
        };
        RedactApplyCommand = new ActionCommand(ApplyRedaction)
        {
            Text = UiStrings.Apply,
            IconName = "accept",
            Enabled = false
        };
        ShowTextCommand = new ActionCommand(ToggleText)
        {
            Text = UiStrings.ShowText,
            IconName = "text"
        };
        CopyTextCommand = new ActionCommand(CopyText)
        {
            Text = UiStrings.CopyText,
            IconName = "copy",
            Enabled = false
        };
        ClearTextCommand = new ActionCommand(ClearText)
        {
            Text = UiStrings.ClearText,
            IconName = "text_clear"
        };

        _previewKsm = new KeyboardShortcutManager();
        EtoPlatform.Current.HandleKeyDown(this, _previewKsm.Perform);
    }

    private void ImageList_ImagesUpdated(object? sender, ImageListEventArgs e)
    {
        Invoker.Current.InvokeDispatch(async () =>
        {
            bool shouldClose = false;
            lock (ImageList)
            {
                if (ImageList.Images.Contains(CurrentImage))
                {
                    UpdateImageIndex();
                    UpdatePage();
                    return;
                }
                if (ImageList.Images.Any())
                {
                    // Update the GUI for the newly displayed image
                    var nextIndex = ImageIndex >= ImageList.Images.Count ? ImageList.Images.Count - 1 : ImageIndex;
                    CurrentImage = ImageList.Images[nextIndex];
                    ImageList.UpdateSelection(ListSelection.Of(CurrentImage));
                }
                else
                {
                    shouldClose = true;
                    ImageList.UpdateSelection(ListSelection.Empty<UiImage>());
                }
            }
            if (shouldClose)
            {
                // No images left to display, so no point keeping the form open
                Close();
            }
            else
            {
                UpdatePage();
                await UpdateImage();
            }
        });
    }

    private void UpdateImageIndex()
    {
        var index = ImageList.Images.IndexOf(CurrentImage);
        if (index == -1)
        {
            index = 0;
        }
        ImageIndex = index;
    }

    protected ScrollZoomImageViewer ImageViewer { get; } = new();

    private void ImageViewerZoomChanged(object? sender, ZoomChangedEventArgs e)
    {
        _zoomPercentButton.Text = e.Zoom.ToString("P0");
    }

    protected override void BuildLayout()
    {
        FormStateController.AutoLayoutSize = false;
        FormStateController.DefaultClientSize = new Size(800, 600);

        LayoutController.RootPadding = 0;
        // The text view is only shown (to the right of the image) when the user asks for it
        LayoutController.Content = L.Row(
            ImageViewer.Scale(),
            _textView.Scale().Visible(_textVisibility)
        );
    }

    protected DesktopCommands Commands { get; set; } = null!;
    protected ActionCommand DeleteCurrentImageCommand { get; }
    protected ActionCommand GoToPrevCommand { get; }
    protected ActionCommand GoToNextCommand { get; }
    protected ActionCommand ZoomInCommand { get; }
    protected ActionCommand ZoomOutCommand { get; }
    protected ActionCommand ZoomWindowCommand { get; }
    protected ActionCommand ZoomActualCommand { get; }
    protected ActionCommand RedactUndoCommand { get; }
    protected ActionCommand RedactApplyCommand { get; }
    protected ActionCommand ShowTextCommand { get; }
    protected ActionCommand CopyTextCommand { get; }
    protected ActionCommand ClearTextCommand { get; }

    protected UiImageList ImageList { get; }

    public UiImage CurrentImage
    {
        get => _currentImage ?? throw new InvalidOperationException();
        set
        {
            if (_currentImage != null)
            {
                _currentImage.ThumbnailInvalidated -= ImageThumbnailInvalidated;
            }
            if (_currentImage != value)
            {
                // Cancel any in-flight render of a different page.
                ++_imageRefreshRevision;
                // If redaction was already committed, keep its visual covering
                // until the replacement page has rendered; otherwise switching
                // away could briefly expose the old unredacted bitmap.
                if (!_redactionAppliedPendingRefresh)
                {
                    _redactButton.Enabled = true;
                    ImageViewer.ClearSelectionRects();
                }
            }
            _currentImage = value;
            UpdateImageIndex();
            Commands = _desktopCommands.WithSelection(() => ListSelection.Of(_currentImage));
            _currentImage.ThumbnailInvalidated += ImageThumbnailInvalidated;
        }
    }

    private void ImageThumbnailInvalidated(object? sender, EventArgs e)
    {
        // Keep unfinished redaction rectangles in place even when a background
        // thumbnail refresh invalidates the current image.
        if (HasPendingRedaction) return;
        Invoker.Current.InvokeDispatch(() => UpdateImage(preserveViewport: true).AssertNoAwait());
    }

    protected int ImageIndex { get; private set; }

    protected override async void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);

        AssignKeyboardShortcuts();

        // TODO: We should definitely start with separate image forms, but it might be fairly trivial to, when opened
        // from the preview form, have the temporary rendering be propagated back to the viewer form and have the
        // dialog only show the editing controls. So it feels more like an image editor. Clicking e.g. "Crop" in the
        // desktop form should still open the full image editing form (for now at least).
        CreateToolbar();
        EtoPlatform.Current.ConfigureSmallToolBar(ToolBar);

        await UpdateImage();
        UpdatePage();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ImageViewer.ZoomToContainer();
    }

    protected virtual void CreateToolbar()
    {
        EtoPlatform.Current.AttachDpiDependency(this, UpdateEditWithAppButton);
        EtoPlatform.Current.AttachDpiDependency(this, UpdateRedactColor);
        _showTextToolItem.Command = ShowTextCommand;
        _showTextToolItem.ToolTip = ShowTextCommand.Text;
        var items = new Dictionary<string, ToolItem>
        {
            // TODO: Either embed an editable textbox, or make this button pop up a window or widget to enter a page number
            [PreviewToolbarLayout.PageNumber] = _pageNumberButton,
            [PreviewToolbarLayout.TextStatus] = _ocrStatusButton,
            [PreviewToolbarLayout.Previous] = MakeToolButton(GoToPrevCommand),
            [PreviewToolbarLayout.Next] = MakeToolButton(GoToNextCommand),
            [PreviewToolbarLayout.ZoomWindow] = MakeToolButton(ZoomWindowCommand),
            [PreviewToolbarLayout.ZoomActual] = MakeToolButton(ZoomActualCommand),
            [PreviewToolbarLayout.ZoomIn] = MakeToolButton(ZoomInCommand),
            [PreviewToolbarLayout.ZoomOut] = MakeToolButton(ZoomOutCommand),
            [PreviewToolbarLayout.ZoomPercent] = _zoomPercentButton,
            [PreviewToolbarLayout.Rotate] = WithToolItemIcon(new DropDownToolItem
            {
                ToolTip = UiStrings.Rotate,
                Items =
                {
                    GuardRedaction(Commands.RotateLeft),
                    GuardRedaction(Commands.RotateRight),
                    GuardRedaction(Commands.Flip),
                    GuardRedaction(Commands.Deskew),
                    GuardRedaction(Commands.CustomRotate)
                }
            }, "arrow_rotate_anticlockwise"),
            [PreviewToolbarLayout.Crop] = MakeToolButton(GuardRedaction(Commands.Crop)),
            [PreviewToolbarLayout.BrightCont] = MakeToolButton(GuardRedaction(Commands.BrightCont)),
            [PreviewToolbarLayout.HueSat] = MakeToolButton(GuardRedaction(Commands.HueSat)),
            [PreviewToolbarLayout.BlackWhite] = MakeToolButton(GuardRedaction(Commands.BlackWhite)),
            [PreviewToolbarLayout.Sharpen] = MakeToolButton(GuardRedaction(Commands.Sharpen)),
            [PreviewToolbarLayout.DocumentCorrection] = MakeToolButton(GuardRedaction(Commands.DocumentCorrection)),
            [PreviewToolbarLayout.Compress] = MakeToolButton(GuardRedaction(Commands.Compress)),
            [PreviewToolbarLayout.Redact] = WithToolItemIcon(_redactButton, "redact"),
            [PreviewToolbarLayout.RedactColor] = _redactColorButton,
            [PreviewToolbarLayout.RedactUndo] = MakeToolButton(RedactUndoCommand),
            [PreviewToolbarLayout.RedactApply] = MakeToolButton(RedactApplyCommand),
            [PreviewToolbarLayout.ShowText] = WithToolItemIcon(_showTextToolItem, "text"),
            [PreviewToolbarLayout.CopyText] = MakeToolButton(CopyTextCommand),
            [PreviewToolbarLayout.ClearText] = MakeToolButton(ClearTextCommand),
            [PreviewToolbarLayout.Split] = MakeToolButton(GuardRedaction(Commands.Split)),
            [PreviewToolbarLayout.Combine] = MakeToolButton(GuardRedaction(Commands.Combine)),
            [PreviewToolbarLayout.EditWithApp] = _editWithAppButton,
            [PreviewToolbarLayout.Delete] = MakeToolButton(DeleteCurrentImageCommand)
        };
        if (!Config.Get(c => c.HiddenButtons).HasFlag(ToolbarButtons.SavePdf))
        {
            items[PreviewToolbarLayout.SavePdf] = MakeToolButton(GuardRedaction(Commands.SaveSelectedPdf), "file_extension_pdf");
        }
        // Unlike the main window, Commands was scoped to CurrentImage in
        // CurrentImage's setter. Extract must save/remove ONLY that page.
        items[PreviewToolbarLayout.Extract] =
            MakeToolButton(GuardRedaction(Commands.ExtractSelected), "arrow_out");
        if (!Config.Get(c => c.HiddenButtons).HasFlag(ToolbarButtons.SaveImages))
        {
            items[PreviewToolbarLayout.SaveImages] = MakeToolButton(GuardRedaction(Commands.SaveSelectedImages), "picture");
        }

        // The order and visibility of the buttons can be customized by the user
        ToolBar = new ToolBar();
        var layout = PreviewToolbarLayout.Parse(Config.Get(c => c.PreviewToolbarLayout));
        foreach (var id in PreviewToolbarLayout.GetDisplayedIds(layout, items.ContainsKey))
        {
            ToolBar.Items.Add(PreviewToolbarLayout.IsSeparator(id) ? new SeparatorToolItem() : items[id]);
        }
    }

    private void UpdateEditWithAppButton(float scale)
    {
        var command = Commands.CreateEditWithApp();
        _editWithAppButton.Command = GuardRedaction(command);
        _editWithAppButton.ToolTip = command.Text;
        // Always show the button. On first use it will prompt for an editor; the portable edition
        // must not hide the only way to configure its external editor.
        _editWithAppButton.Visible = true;
        EtoPlatform.Current.SetImageSize(_editWithAppButton, (int) (16 * scale));
        _editWithAppButton.Image = command.GetIconImage(IconScenario.SmallToolbar, scale);
    }

    private void EditWithController_AppChanged(object? sender, EventArgs e) =>
        UpdateEditWithAppButton(EtoPlatform.Current.GetScaleFactor(this));

    private ToolItem MakeToolButton(ActionCommand command, string? iconName = null) =>
        WithToolItemIcon(new ButtonToolItem
        {
            Command = command,
            Text = null,
            ToolTip = command.Text
        }, iconName ?? command.IconName!);

    private ToolItem WithToolItemIcon(ToolItem toolItem, string iconName)
    {
        EtoPlatform.Current.AttachDpiDependency(this,
            scale =>
            {
                EtoPlatform.Current.SetImageSize(toolItem, (int) (16 * scale));
                toolItem.Image = EtoPlatform.Current.IconProvider.GetIcon(iconName, IconScenario.SmallToolbar, scale);
            });
        return toolItem;
    }

    private void UpdateRedactCommands()
    {
        RedactUndoCommand.Enabled = RedactApplyCommand.Enabled = HasPendingRedaction;
    }

    // Once AddTransforms succeeds the rectangles are already committed; they
    // only remain visible until the updated bitmap is ready for display.
    private bool HasPendingRedaction =>
        !_redactionAppliedPendingRefresh && ImageViewer.SelectionRects.Count > 0;

    /// <summary>
    /// Unapplied rectangles are only a preview and will not appear in exported
    /// scans. An explicit decision is required before changing pages, exporting,
    /// performing other image edits or closing the preview.
    /// </summary>
    private bool ConfirmPendingRedaction()
    {
        if (!HasPendingRedaction) return true;
        var prompt = new ChoiceForm(Config)
        {
            Title = RedactionWarningText.Title,
            Message = RedactionWarningText.Message,
            Choices = [RedactionWarningText.Apply, RedactionWarningText.Discard]
        };
        prompt.ShowModal();
        switch (prompt.SelectedChoice)
        {
            case 0:
                ApplyRedaction();
                return !HasPendingRedaction;
            case 1:
                EndRedaction();
                return true;
            default:
                return false;
        }
    }

    private ActionCommand GuardRedaction(ActionCommand command) => new(() =>
    {
        if (ConfirmPendingRedaction())
            command.Execute();
    })
    {
        Text = command.Text,
        IconName = command.IconName
    };

    private void ApplyRedaction()
    {
        var image = ImageViewer.Image;
        if (image == null || ImageViewer.SelectionRects.Count == 0)
        {
            return;
        }
        // The rectangles are relative to the rendered image (i.e. after all current transforms), which is also what
        // the new transforms will be applied to. We round outwards so nothing that was covered is left visible.
        var transforms = ImageViewer.SelectionRects.Select(rect =>
        {
            int left = (int) Math.Floor(rect.Bounds.Left);
            int top = (int) Math.Floor(rect.Bounds.Top);
            int right = (int) Math.Ceiling(rect.Bounds.Right);
            int bottom = (int) Math.Ceiling(rect.Bounds.Bottom);
            return (Transform) new RedactTransform(left, top, right - left, bottom - top, image.Width, image.Height,
                rect.Color == Colors.White);
        }).ToList();
        // As the redaction is a transform, the affected pixels are overwritten whenever the image is rendered (for
        // display, saving, OCR, etc). An imported PDF page with a transform is exported as a rendered image rather
        // than by copying the original PDF page, and cached OCR results are keyed on the transform state, so the
        // redacted content doesn't end up in exported files.
        // Only clear the preview once the mutation succeeds. If applying fails,
        // the user must still be able to retry or explicitly discard the rectangles.
        ImageList.Mutate(new ImageListMutation.AddTransforms(transforms), ListSelection.Of(CurrentImage));
        // Invalidate earlier renders that may contain the original pixels.
        ++_imageRefreshRevision;
        _redactionAppliedPendingRefresh = true;
        SetRedactMode(false);
        // Do not allow another redaction to be drawn into an out-of-date bitmap
        // while the already committed redaction is being rendered.
        _redactButton.Enabled = false;
        UpdateRedactCommands();
        // Render the committed scan before removing the visual covering.
        // A background thumbnail event may also trigger a render; the latest
        // successful render is the one that completes this transition.
        UpdateImage(preserveViewport: true).AssertNoAwait();
    }

    private void EndRedaction()
    {
        ImageViewer.ClearSelectionRects();
        SetRedactMode(false);
    }

    private void SetRedactMode(bool enabled)
    {
        _redactMode = enabled;
        ImageViewer.RectSelectionEnabled = enabled;
        // The toolbar item may toggle its own checked state when clicked (before or after this runs depending on the
        // platform), so we also sync it after the click has been fully handled
        SyncRedactButton();
        Invoker.Current.InvokeDispatch(SyncRedactButton);
    }

    private void SyncRedactButton()
    {
        if (_redactButton.Checked != _redactMode)
        {
            _redactButton.Checked = _redactMode;
        }
    }

    private void ToggleRedactColor()
    {
        Config.User.Set(c => c.RedactWithBlack, !Config.Get(c => c.RedactWithBlack));
        UpdateRedactColor(EtoPlatform.Current.GetScaleFactor(this));
    }

    // Updates the color used for new redaction rectangles, and shows it as a swatch on the color button
    private void UpdateRedactColor(float scale)
    {
        var color = Config.Get(c => c.RedactWithBlack) ? Colors.Black : Colors.White;
        ImageViewer.SelectionRectColor = color;

        int size = (int) (16 * scale);
        var swatch = new Bitmap(size, size, PixelFormat.Format32bppRgba);
        using (var graphics = new Graphics(swatch))
        {
            graphics.FillRectangle(color, 2, 2, size - 4, size - 4);
            graphics.DrawRectangle(Colors.Gray, 2, 2, size - 5, size - 5);
        }
        EtoPlatform.Current.SetImageSize(_redactColorButton, size);
        _redactColorButton.Image = swatch;
    }

    private async Task GoTo(int index)
    {
        if (index == ImageIndex || index < 0 || index >= ImageList.Images.Count)
            return;
        if (!ConfirmPendingRedaction())
            return;
        lock (ImageList)
        {
            if (index < 0 || index >= ImageList.Images.Count)
                return;
            CurrentImage = ImageList.Images[index];
            ImageList.UpdateSelection(ListSelection.Of(CurrentImage));
        }
        await UpdateImage();
        UpdatePage();
    }

    protected virtual void UpdatePage()
    {
        _pageNumberButton.Text = string.Format(UiStrings.XOfY, ImageIndex + 1, ImageList.Images.Count);
        RefreshPreviewTextStatus();
    }

    private void PageTextStatusChanged(object? sender, EventArgs e)
    {
        if (System.Threading.Interlocked.Exchange(ref _previewStatusRefreshQueued, 1) != 0) return;
        _ = Task.Delay(220).ContinueWith(_ => Invoker.Current.InvokeDispatch(() =>
        {
            System.Threading.Interlocked.Exchange(ref _previewStatusRefreshQueued, 0);
            if (!_previewClosed) RefreshPreviewTextStatus();
        }));
    }

    private async void RefreshPreviewTextStatus()
    {
        var page = _currentImage;
        if (page == null) return;
        PageTextStatus status;
        try
        {
            status = await Task.Run(() => _pageTextStatusProvider.GetStatus(page));
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error updating preview OCR badge", ex);
            return;
        }
        if (page != _currentImage || _previewClosed) return;
        if (page == _lastStatusImage && status == _lastStatus) return;
        _lastStatusImage = page;
        _lastStatus = status;
        _ocrStatusButton.Visible = status != PageTextStatus.None;
        if (status == PageTextStatus.None) return;
        _ocrStatusButton.ToolTip = status switch
        {
            PageTextStatus.Recognized => OcrActionText.Recognized,
            PageTextStatus.RecognizedElsewhere => OcrActionText.RecognizedElsewhere,
            PageTextStatus.OriginalText => OcrActionText.OriginalText,
            _ => OcrActionText.LostText
        };
        _ocrStatusButton.Image = (status switch
        {
            PageTextStatus.Recognized => Icons.textbadge_ours,
            PageTextStatus.RecognizedElsewhere => Icons.textbadge_other,
            PageTextStatus.OriginalText => Icons.textbadge_real,
            _ => Icons.textbadge_lost
        }).ToEtoImage();
    }

    private async Task UpdateImage(bool preserveViewport = false)
    {
        if (HasPendingRedaction) return;
        var page = CurrentImage;
        int revision = ++_imageRefreshRevision;
        try
        {
            using var imageToRender = page.GetClonedImage();
            using var rendered = await Task.Run(() => imageToRender.Render());
            // Ignore renders of obsolete pages or the image state before a new
            // redaction. Never discard rectangles added during an async render.
            if (_previewClosed || CurrentImage != page ||
                revision != _imageRefreshRevision || HasPendingRedaction)
                return;

            // Change the bitmap underneath the covering rectangles first.
            // Only after that can we safely clear the old preview overlays.
            ImageViewer.ReplaceImage(rendered.ToEtoImage(),
                preserveViewport || _redactionAppliedPendingRefresh);
            ImageViewer.OutlineMode = Config.Get(c => c.RedactionOutlineMode);
            if (_redactionAppliedPendingRefresh)
            {
                _redactionAppliedPendingRefresh = false;
                EndRedaction();
                _redactButton.Enabled = true;
            }
            if (_textVisibility.IsVisible)
            {
                await RefreshText();
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException("Could not refresh the preview image", ex);
            if (_redactionAppliedPendingRefresh && !_previewClosed && CurrentImage == page)
            {
                // The underlying redaction has been committed. Keep the covering
                // rectangles until the preview can be rendered again.
                MessageBox.Show(this, RedactionWarningText.PreviewRefreshFailed,
                    RedactionWarningText.Title, MessageBoxType.Warning);
            }
        }
    }

    private async Task ToggleText()
    {
        if (_textVisibility.IsVisible)
        {
            _textVisibility.IsVisible = false;
            SetText(null);
            return;
        }
        var text = await GetTextWithPrompts();
        if (text != null)
        {
            SetText(text);
            _textVisibility.IsVisible = true;
        }
        else
        {
            // The text view may have been shown with a progress message
            _textVisibility.IsVisible = false;
            SetText(null);
        }
    }

    private async Task ClearText()
    {
        Commands.ClearTextSelected.Execute();
        if (_textVisibility.IsVisible)
        {
            await RefreshText();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmPendingRedaction())
            e.Cancel = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _previewClosed = true;
        base.OnClosed(e);
        // Make sure the desktop reflects any changes to the text of pages made from this window
        _pageTextProvider.NotifyStatusChanged();
        _pageTextStatusProvider.StatusChanged -= PageTextStatusChanged;
    }

    private void SetText(PageText? text)
    {
        _textView.Text = text;
        CopyTextCommand.Enabled = text is { IsEmpty: false };
    }

    private void CopyText()
    {
        if (_textView.Text is { IsEmpty: false } text)
        {
            Clipboard.Instance.Text = text.PlainText;
        }
    }

    // Updates the displayed text after the image changes, but only from sources that don't need to ask the user
    private async Task RefreshText()
    {
        var uiImage = CurrentImage;
        using var image = uiImage.GetClonedImage();
        var text = await Task.Run(() => _pageTextProvider.GetSource(image) switch
        {
            PageTextSource.Ocr => _pageTextProvider.GetCachedOcrText(image),
            PageTextSource.Pdf => _pageTextProvider.GetPdfText(image),
            _ => null
        });
        if (uiImage == CurrentImage)
        {
            SetText(text);
        }
    }

    private async Task<PageText?> GetTextWithPrompts()
    {
        using var image = CurrentImage.GetClonedImage();
        var source = await Task.Run(() => _pageTextProvider.GetSource(image));
        switch (source)
        {
            case PageTextSource.Ocr:
                return _pageTextProvider.GetCachedOcrText(image);
            case PageTextSource.Pdf:
                return await Task.Run(() => _pageTextProvider.GetPdfText(image));
            case PageTextSource.PdfChanged:
                var prompt = new ChoiceForm(Config)
                {
                    Title = UiStrings.ShowText,
                    Message = UiStrings.PageChangedTextPrompt,
                    Choices = [UiStrings.Show, UiStrings.RecognizeAgain]
                };
                prompt.ShowModal();
                return prompt.SelectedChoice switch
                {
                    0 => await Task.Run(() => _pageTextProvider.GetPdfText(image)),
                    1 => await RecognizeText(image),
                    _ => null
                };
            case PageTextSource.PdfRedacted:
                // The original text layer would reveal what was redacted, so recognizing again is the only option
                return Confirm(UiStrings.PageRedactedTextPrompt) ? await RecognizeText(image) : null;
            default:
                return Confirm(UiStrings.PageNotRecognizedPrompt) ? await RecognizeText(image) : null;
        }
    }

    private bool Confirm(string message) =>
        MessageBox.Show(this, message, UiStrings.ShowText, MessageBoxButtons.YesNo, MessageBoxType.Question) ==
        DialogResult.Yes;

    public void ShowRecognitionSettings()
    {
        var dialog = FormFactory.Create<OcrSetupForm>();
        dialog.ShowModal();
        if (dialog.Saved)
        {
            _pageTextProvider.NotifyStatusChanged();
            RefreshPreviewTextStatus();
        }
    }

    public async Task RecognizeCurrentAgain(bool configure)
    {
        if (configure)
        {
            var dialog = FormFactory.Create<OcrSetupForm>();
            dialog.ShowModal();
            if (!dialog.Saved) return;
        }
        var page = CurrentImage;
        using var image = page.GetClonedImage();
        var previous = _textView.Text;
        var text = await RecognizeText(image, force: true);
        if (CurrentImage != page) return;
        if (text != null && !text.IsEmpty)
        {
            SetText(text);
            _textVisibility.IsVisible = true;
        }
        else
        {
            // Failed OCR should not erase the previously displayed text.
            SetText(previous);
            _textVisibility.IsVisible = previous != null;
        }
        RefreshPreviewTextStatus();
    }

    private async Task<PageText?> RecognizeText(ProcessedImage image, bool force = false)
    {
        if (!_pageTextProvider.CanRecognize)
        {
            // OCR needs to be set up before we can run it
            Commands.OcrSettings.Execute();
        }
        if (!_pageTextProvider.CanRecognize)
        {
            return null;
        }
        ShowTextCommand.Enabled = false;
        // OCR can take a while, so show that something is happening where the text will appear
        _textView.Message = UiStrings.RecognizingText;
        _textVisibility.IsVisible = true;
        try
        {
            var text = await _pageTextProvider.Recognize(image, force: force);
            if (text == null || text.IsEmpty)
            {
                MessageBox.Show(this, UiStrings.NoTextFound, UiStrings.ShowText, MessageBoxType.Information);
                return null;
            }
            _pageTextProvider.NotifyStatusChanged();
            return text;
        }
        catch (Exception ex)
        {
            Log.ErrorException("Error recognizing page text", ex);
            return null;
        }
        finally
        {
            ShowTextCommand.Enabled = true;
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (EtoPlatform.Current.IsGtk)
        {
            // Gtk delays adjusting the imageview size after the container size changes, which messes this up unless
            // we queue it after the current UI thread options.
            // We don't want to do this on Windows/Mac as it results in the render size lagging the window size.
            Invoker.Current.InvokeDispatch(ImageViewer.ZoomToContainer);
        }
        else
        {
            ImageViewer.ZoomToContainer();
        }
    }

    private void DeleteCurrentImage()
    {
        if (MessageBox.Show(this,
                string.Format(MiscResources.ConfirmDeleteItems, 1),
                MiscResources.Delete, MessageBoxButtons.OKCancel,
                MessageBoxType.Question, MessageBoxDefaultButton.OK) != DialogResult.Ok)
            return;
        if (!ConfirmPendingRedaction())
            return;
        // We don't want to run Commands.Delete as that runs on DesktopController and uses that selection.
        Commands.ImageListActions.DeleteSelected();
    }

    private void AssignKeyboardShortcuts()
    {
        // Unconfigurable defaults

        // Escape must never silently close the preview or discard redaction.
        _previewKsm.Assign("Esc", () => { });
        _previewKsm.Assign("Del", DeleteCurrentImageCommand);
        _previewKsm.Assign("Mod+0", ZoomActualCommand);
        _previewKsm.Assign("Mod+Z", GuardRedaction(Commands.Undo));
        _previewKsm.Assign(EtoPlatform.Current.IsWindows ? "Mod+Y" : "Mod+Shift+Z", GuardRedaction(Commands.Redo));

        _previewKsm.Assign("PageDown", () => _ = GoTo(ImageIndex + 1));
        _previewKsm.Assign("Right", () => _ = GoTo(ImageIndex + 1));
        _previewKsm.Assign("Down", () => _ = GoTo(ImageIndex + 1));

        _previewKsm.Assign("PageUp", () => _ = GoTo(ImageIndex - 1));
        _previewKsm.Assign("Left", () => _ = GoTo(ImageIndex - 1));
        _previewKsm.Assign("Up", () => _ = GoTo(ImageIndex - 1));

        // Configured defaults

        var ks = Config.Get(c => c.KeyboardShortcuts);

        _previewKsm.Assign(ks.Delete, DeleteCurrentImageCommand);
        _previewKsm.Assign(ks.ImageBlackWhite, GuardRedaction(Commands.BlackWhite));
        _previewKsm.Assign(ks.ImageBrightness, GuardRedaction(Commands.BrightCont));
        _previewKsm.Assign(ks.ImageContrast, GuardRedaction(Commands.BrightCont));
        _previewKsm.Assign(ks.ImageCrop, GuardRedaction(Commands.Crop));
        _previewKsm.Assign(ks.ImageHue, GuardRedaction(Commands.HueSat));
        _previewKsm.Assign(ks.ImageSaturation, GuardRedaction(Commands.HueSat));
        _previewKsm.Assign(ks.ImageSharpen, GuardRedaction(Commands.Sharpen));
        _previewKsm.Assign(ks.ImageDocumentCorrection, GuardRedaction(Commands.DocumentCorrection));
        _previewKsm.Assign(ks.ImageCompress, GuardRedaction(Commands.Compress));
        _previewKsm.Assign(ks.ImageSplit, GuardRedaction(Commands.Split));
        _previewKsm.Assign(ks.ImageCombine, GuardRedaction(Commands.Combine));
        _previewKsm.Assign(ks.ImageEditWith, GuardRedaction(Commands.CreateEditWithApp()));

        _previewKsm.Assign(ks.RotateCustom, GuardRedaction(Commands.CustomRotate));
        _previewKsm.Assign(ks.RotateDeskew, GuardRedaction(Commands.Deskew));
        _previewKsm.Assign(ks.RotateFlip, GuardRedaction(Commands.Flip));
        _previewKsm.Assign(ks.RotateLeft, GuardRedaction(Commands.RotateLeft));
        _previewKsm.Assign(ks.RotateRight, GuardRedaction(Commands.RotateRight));

        if (PlatformCompat.System.CombinedPdfAndImageSaving)
        {
            _previewKsm.Assign(ks.SavePDFAll, GuardRedaction(Commands.SaveSelected));
        }
        else
        {
            _previewKsm.Assign(ks.SavePDF, GuardRedaction(Commands.SaveSelectedPdf));
            _previewKsm.Assign(ks.SaveImages, GuardRedaction(Commands.SaveSelectedImages));
            _previewKsm.Assign(ks.SavePDFAll, GuardRedaction(Commands.SaveSelectedPdf));
            _previewKsm.Assign(ks.SaveImagesAll, GuardRedaction(Commands.SaveSelectedImages));
        }

        _previewKsm.Assign(ks.ZoomIn, ZoomInCommand);
        _previewKsm.Assign(ks.ZoomOut, ZoomOutCommand);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ImageViewer.Image?.Dispose();
            ImageViewer.Image = null;
            ImageList.ImagesUpdated -= ImageList_ImagesUpdated;
            _editWithController.AppChanged -= EditWithController_AppChanged;
        }
        base.Dispose(disposing);
    }
}