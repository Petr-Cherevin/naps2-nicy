using System.Drawing;
using System.Windows.Forms;
using NAPS2.EtoForms.Desktop;

namespace NAPS2.EtoForms.Ui;

public class WinFormsPreviewForm : PreviewForm
{
    public WinFormsPreviewForm(Naps2Config config, DesktopCommands desktopCommands, UiImageList imageList,
        IIconProvider iconProvider, ColorScheme colorScheme, EditWithController editWithController,
        NAPS2.Ocr.PageTextProvider pageTextProvider) : base(config, desktopCommands, imageList, iconProvider, colorScheme,
        editWithController, pageTextProvider)
    {
    }

    protected override void OnLoad(EventArgs eventArgs)
    {
        base.OnLoad(eventArgs);
        var toolStrip = (ToolStrip) ToolBar.ControlObject;
        EtoPlatform.Current.AttachDpiDependency(this,
            scale => toolStrip.ImageScalingSize = new Size((int) (16 * scale), (int) (16 * scale)));
    }
}