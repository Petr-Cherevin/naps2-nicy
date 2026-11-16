using System.Globalization;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// The redaction rectangles are not part of the saved scan until Apply is used.
/// The warning is intentionally explicit about the risk of exporting original data.
/// </summary>
internal static class RedactionWarningText
{
    private static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    public static string Title => Ru ? "Цензурирование не сохранено" : "Redaction not applied";

    public static string Message => Ru
        ? "На странице есть неприменённое цензурирование. Пока вы не нажали «Применить», закрытые участки останутся видимыми в сохранённом файле. Применить цензурирование перед продолжением?"
        : "This page contains unapplied redaction. Until you click Apply, the covered areas remain visible in exported files. Apply the redaction before continuing?";

    public static string Apply => Ru ? "Применить цензурирование" : "Apply redaction";

    public static string Discard => Ru ? "Продолжить без применения" : "Continue without applying";
}
