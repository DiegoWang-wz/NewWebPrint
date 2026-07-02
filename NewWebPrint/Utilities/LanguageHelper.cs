using System.Globalization;

namespace NewWebPrint.Utilities;

public static class LanguageHelper
{
    public static CultureInfo UiCulture => CultureInfo.CurrentUICulture;

    public static string LangCode =>
        UiCulture.TwoLetterISOLanguageName.ToLowerInvariant();

    public static bool IsZh =>
        LangCode.StartsWith("zh");

    public static bool IsEn =>
        LangCode == "en";

    public static string L(string zh, string en) =>
        IsZh ? zh : en;

    public static string SapLangParam =>
        IsZh ? "1" : "E";

    public static string Bilingual(string zh, string en, string separator = " / ") =>
        $"{zh}{separator}{en}";
}
