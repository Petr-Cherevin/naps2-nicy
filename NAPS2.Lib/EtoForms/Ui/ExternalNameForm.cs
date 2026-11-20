using System.Globalization;
using Eto.Drawing;
using Eto.Forms;
using NAPS2.EtoForms.Layout;

namespace NAPS2.EtoForms.Ui;

internal sealed class ExternalNameForm : EtoDialogBase
{
    private readonly TextBox _program = new(), _arguments = new();
    private readonly DropDown _input = new();
    internal static string LabelFor(string ru, string en) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? ru : en;

    public ExternalNameForm(Naps2Config config, string? program, string? args, string? input) : base(config)
    {
        Title = LabelFor("Имя файла из внешней программы", "Name from external program");
        _program.Text = program ?? "";
        _arguments.Text = args ?? "";
        _input.Items.Add(LabelFor("Значение QR-кода", "QR code value"));
        _input.Items.Add(LabelFor("Количество сохраняемых страниц", "Number of pages being saved"));
        _input.Items.Add(LabelFor("Сохраняемый документ", "Document being saved"));
        _input.SelectedIndex = input switch { "page-count" => 1, "document" => 2, _ => 0 };
    }

    public bool Accepted { get; private set; }
    public string ProgramPath => _program.Text.Trim();
    public string Arguments => _arguments.Text;
    public string Input => _input.SelectedIndex switch { 1 => "page-count", 2 => "document", _ => "qr" };

    private void Browse(bool script)
    {
        using var picker = new OpenFileDialog();
        if (picker.ShowDialog(this) != DialogResult.Ok) return;
        if (script) _arguments.Text = "\"" + picker.FileName + "\"";
        else _program.Text = picker.FileName;
    }

    protected override void BuildLayout()
    {
        FormStateController.DefaultClientSize = new Size(680, 280);
        Resizable = true;
        LayoutController.Content = L.Column(
            C.Label(LabelFor("Программа", "Program")),
            L.Row(_program.Scale(), C.Button(LabelFor("Обзор…", "Browse…"), () => Browse(false))),
            C.Label(LabelFor("Аргументы", "Arguments")),
            L.Row(_arguments.Scale(), C.Button(LabelFor("Скрипт…", "Script…"), () => Browse(true))),
            C.Label(LabelFor("Передавать в программу", "Send to program")), _input,
            C.Label(LabelFor("Пример результата", "Example result")), new TextBox { Text = "2026-2-291", ReadOnly = true },
            C.Filler(), L.Row(C.Filler(), L.OkCancel(C.OkButton(this, Accept), C.CancelButton(this))));
    }

    private bool Accept()
    {
        if (!Path.IsPathFullyQualified(ProgramPath) || !File.Exists(ProgramPath))
        {
            MessageBox.Show(this, LabelFor("Выберите файл программы.", "Select a program file."));
            return false;
        }
        return Accepted = true;
    }
}
