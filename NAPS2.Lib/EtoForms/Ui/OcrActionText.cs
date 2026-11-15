using System.Globalization;

namespace NAPS2.EtoForms.Ui;

/// <summary>Russian and English captions for forced OCR actions.</summary>
public static class OcrActionText
{
    private static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
    public static string TextStatus => Ru ? "Статус текста страницы" : "Page text status";
    public static string RecognizeSelectedAgain => Ru ? "Распознать заново выделенные" : "Recognize selected again";
    public static string RecognizePageAgain => Ru ? "Распознать страницу заново" : "Recognize page again";
    public static string ConfigureAndRecognize => Ru ? "Настроить и распознать заново..." : "Configure and recognize again...";
    public static string ConfigureOcr => Ru ? "Настройки распознавания..." : "Recognition settings...";
    public static string OriginalText => Ru ? "Исходный текст PDF" : "Original PDF text";
    public static string Recognized => Ru ? "Текст распознан в NAPS2" : "Text recognized in NAPS2";
    public static string RecognizedElsewhere => Ru ? "Распознанный текст из PDF" : "Previously recognized PDF text";
    public static string LostText => Ru ? "Текст больше не соответствует странице" : "Text no longer matches the page";
}
