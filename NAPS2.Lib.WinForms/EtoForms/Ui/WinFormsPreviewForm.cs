using System.Drawing;
using System.Windows.Forms;
using Eto.WinForms;
using NAPS2.EtoForms.Desktop;

namespace NAPS2.EtoForms.Ui;

public class WinFormsPreviewForm : PreviewForm
{
    private ToolStripSplitButton? _editorButton;
    private ToolStripSplitButton? _textButton;

    public WinFormsPreviewForm(Naps2Config config, DesktopCommands desktopCommands, UiImageList imageList,
        IIconProvider iconProvider, ColorScheme colorScheme, EditWithController editWithController,
        NAPS2.Ocr.PageTextProvider pageTextProvider, NAPS2.Ocr.PageTextStatusProvider pageTextStatusProvider)
        : base(config, desktopCommands, imageList, iconProvider, colorScheme,
        editWithController, pageTextProvider, pageTextStatusProvider)
    {
        editWithController.AppChanged += EditorAppChanged;
    }

    protected override void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);
        var toolStrip = (ToolStrip) ToolBar.ControlObject;
        EtoPlatform.Current.AttachDpiDependency(this,
            scale => toolStrip.ImageScalingSize = new Size((int) (16 * scale), (int) (16 * scale)));

        // Give the existing Show Text button an independent drop-down without changing its primary action.
        if (ShowTextToolItem.ControlObject is ToolStripItem textOriginal)
        {
            int textIndex = toolStrip.Items.IndexOf(textOriginal);
            if (textIndex >= 0)
            {
                _textButton = new ToolStripSplitButton
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    Image = textOriginal.Image,
                    ToolTipText = UiStrings.ShowText,
                    AutoToolTip = true
                };
                _textButton.ButtonClick += (_, _) => ShowTextCommand.Execute();
                _textButton.DropDownOpening += (_, _) => PopulateTextMenu();
                toolStrip.Items.RemoveAt(textIndex);
                toolStrip.Items.Insert(textIndex, _textButton);
            }
        }

        // Eto's ButtonToolItem has only a primary Click action. Replace the native WinForms
        // control with a real split button: the large part edits, the arrow chooses an editor.
        if (EditWithAppToolItem.ControlObject is ToolStripItem original)
        {
            int index = toolStrip.Items.IndexOf(original);
            if (index >= 0)
            {
                _editorButton = new ToolStripSplitButton
                {
                    DisplayStyle = ToolStripItemDisplayStyle.Image,
                    Image = original.Image,
                    ToolTipText = original.ToolTipText,
                    AutoToolTip = true
                };
                _editorButton.ButtonClick += (_, _) => EditWithController.EditWithApp([CurrentImage]);
                _editorButton.DropDownOpening += (_, _) => PopulateEditors();
                toolStrip.Items.RemoveAt(index);
                toolStrip.Items.Insert(index, _editorButton);
                UpdateEditorButton();
            }
        }
    }

    private void PopulateTextMenu()
    {
        if (_textButton == null) return;
        _textButton.DropDownItems.Clear();

        var show = new ToolStripMenuItem(UiStrings.ShowText);
        show.Click += (_, _) => ShowTextCommand.Execute();
        _textButton.DropDownItems.Add(show);
        _textButton.DropDownItems.Add(new ToolStripSeparator());

        var rerun = new ToolStripMenuItem(OcrActionText.RecognizePageAgain);
        rerun.Click += async (_, _) => await RecognizeCurrentAgain(false);
        _textButton.DropDownItems.Add(rerun);

        var configureAndRerun = new ToolStripMenuItem(OcrActionText.ConfigureAndRecognize);
        configureAndRerun.Click += async (_, _) => await RecognizeCurrentAgain(true);
        _textButton.DropDownItems.Add(configureAndRerun);

        _textButton.DropDownItems.Add(new ToolStripSeparator());
        var configure = new ToolStripMenuItem(OcrActionText.ConfigureOcr);
        configure.Click += (_, _) => ShowRecognitionSettings();
        _textButton.DropDownItems.Add(configure);
    }

    private void EditorAppChanged(object? sender, EventArgs e) =>
        Invoker.Current.InvokeDispatch(UpdateEditorButton);

    private void UpdateEditorButton()
    {
        if (_editorButton == null) return;
        _editorButton.ToolTipText = EditWithController.HasApp
            ? string.Format(UiStrings.EditWithAppName, EditWithController.AppName)
            : UiStrings.EditWith;
        if (EditWithController.AppIcon is { } icon)
        {
            _editorButton.Image = icon.ToSD();
        }
    }

    private void PopulateEditors()
    {
        if (_editorButton == null) return;
        _editorButton.DropDownItems.Clear();

        foreach (var editor in EditWithController.GetAvailableEditors())
        {
            var item = new ToolStripMenuItem(editor.Name)
            {
                Checked = editor.Id == EditWithController.AppId
            };
            item.Click += (_, _) => EditWithController.EditWithEntry(editor, [CurrentImage]);
            _editorButton.DropDownItems.Add(item);
        }

        if (_editorButton.DropDownItems.Count > 0)
        {
            _editorButton.DropDownItems.Add(new ToolStripSeparator());
        }

        var choose = new ToolStripMenuItem(PreviewEditingText.ChooseEditor);
        choose.Click += (_, _) => EditWithController.EditWithPick([CurrentImage]);
        _editorButton.DropDownItems.Add(choose);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            EditWithController.AppChanged -= EditorAppChanged;
        }
        base.Dispose(disposing);
    }
}
