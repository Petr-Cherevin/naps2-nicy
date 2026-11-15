using NAPS2.EtoForms.Layout;
using NAPS2.Scan;
using NAPS2.Update;

namespace NAPS2.EtoForms.Ui;

public class AboutForm : EtoDialogBase
{
    private readonly AboutContent _content;

    public AboutForm(Naps2Config config, UpdateChecker updateChecker, ScanningContext scanningContext)
        : base(config)
    {
        Title = UiStrings.AboutFormTitle;
        IconName = "information";

        _content = new AboutContent(config, updateChecker, scanningContext);
    }

    protected override void BuildLayout()
    {
        FormStateController.Resizable = false;
        FormStateController.RestoreFormState = false;

        LayoutController.DefaultSpacing = 2;
        LayoutController.Content = _content.BuildLayout(C.DialogButton(this, UiStrings.OK, true, true));
    }
}
