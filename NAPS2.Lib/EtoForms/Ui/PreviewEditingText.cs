using System.Globalization;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Small set of labels for the new preview editing options. English is the fallback,
/// and Russian is provided for the main custom build language.
/// </summary>
internal static class PreviewEditingText
{
    private static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    public static string SettingsTitle => Ru ? "Редактирование страниц" : "Page editing";
    public static string EditorGroup => Ru ? "Внешний графический редактор" : "External image editor";
    public static string CurrentEditor => Ru ? "Текущий редактор:" : "Current editor:";
    public static string NotSelected => Ru ? "Не выбран" : "Not selected";
    public static string ChooseEditor => Ru ? "Выбрать редактор..." : "Choose editor...";
    public static string BrowseExecutable => Ru ? "Указать файл редактора (.exe)..." : "Browse for an editor (.exe)...";
    public static string ChooseExecutable => Ru ? "Выберите графический редактор" : "Choose an image editor";
    public static string ExecutableFiles => Ru ? "Программы Windows" : "Windows applications";
    public static string OutlineGroup => Ru ? "Рамка при цензурировании" : "Redaction outline";
    public static string OutlineNone => Ru ? "Без рамки" : "No outline";
    public static string OutlineDuringDrag => Ru ? "Только пока рисую прямоугольник" : "Only while drawing a rectangle";
    public static string OutlineUntilApply => Ru ? "До нажатия «Применить», общий внешний контур" :
        "Until Apply, merged outer outline";
}
