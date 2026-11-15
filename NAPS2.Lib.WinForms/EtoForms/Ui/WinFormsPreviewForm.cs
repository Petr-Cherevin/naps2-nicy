using System.Drawing;
using System.Windows.Forms;
using Eto.WinForms;
using NAPS2.EtoForms.Desktop;

namespace NAPS2.EtoForms.Ui;

public class WinFormsPreviewForm : PreviewForm
{
    private ToolStripSplitButton? _editorButton;

    public WinFormsPreviewForm(Naps2Config config, DesktopCommands desktopCommands, UiImageList imageList,
        IIconProvider iconProvider, ColorScheme colorScheme, EditWithController editWithController,
        NAPS2.Ocr.PageTextProvider pageTextProvider) : base(config, desktopCommands, imageList, iconProvider, colorScheme,
        editWithController, pageTextProvider)
    {
        editWithController.AppChanged += EditorAppChanged;
    }

    protected override void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);
        var toolStrip = (ToolStrip) ToolBar.ControlObject;
        EtoPlatform.Current.AttachDpiDependency(this,
            scale => toolStrip.ImageScalingSize = new Size((int) (16 * scale), (int) (16 * scale)));

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
