using System.Globalization;

namespace NAPS2.EtoForms.Ui;

/// <summary>
/// Russian and English captions for the three OCR-language tabs.
/// </summary>
internal static class OcrSetupText
{
    private static bool Ru => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";

    public static string SingleTab => Ru ? "Один язык" : "One language";
    public static string FavoriteTab => Ru ? "Мой набор" : "My set";
    public static string CustomTab => Ru ? "Свой выбор" : "Custom choice";
    public static string SingleDescription => Ru ? "Выберите один установленный язык:" : "Choose one installed language:";
    public static string FavoriteDescription => Ru ? "Ваш сохранённый набор языков:" : "Your saved language set:";
    public static string CustomDescription => Ru ? "Отметьте нужные установленные языки:" : "Check the installed languages to use:";
    public static string EditFavorite => Ru ? "Изменить набор..." : "Edit set...";
    public static string SaveAsFavorite => Ru ? "Сохранить как мой набор" : "Save as my set";
    public static string NoFavorite => Ru ? "Набор пока не сохранён" : "No saved set yet";
    public static string SelectLanguage => Ru ? "Выберите хотя бы один установленный язык." :
        "Choose at least one installed language.";
}
