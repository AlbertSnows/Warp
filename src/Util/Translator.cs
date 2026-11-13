using RWCustom;

namespace Warp.Util;

/// <summary>
/// Looks text up in the game's translations for the player's language. Text with no translation comes back as it
/// was written.
/// </summary>
internal static class Translator
{
    private const string NoTranslation = "!NO TRANSLATION!";

    /// <summary> Stands in a translated text for a "|" that the translator may move or drop. </summary>
    private const string Separator = "#SEPARATOR#";

    /// <summary> The text in the player's language, or as it was written if there is no translation. </summary>
    public static string Translate(string text)
    {
        string translated = Custom.rainWorld.inGameTranslator.Translate(text);
        return string.IsNullOrEmpty(translated) || translated == NoTranslation ? text : translated;
    }

    /// <summary>
    /// The translation of text with "{0}", "{1}" and so on in it, with the arguments put in afterwards. Translate the
    /// text with its placeholders, so there is one entry for it in the translation files and not one for each value.
    /// </summary>
    public static string Format(string text, params object[] arguments)
    {
        string result = Translate(text);
        for (int i = 0; i < arguments.Length; i++)
        {
            result = result.Replace("{" + i + "}", arguments[i]?.ToString());
        }
        return result;
    }

    /// <summary>
    /// <see cref="Format"/> and <see cref="TranslateWithSeparator"/> in one: the arguments are put in, and the separator
    /// becomes "|".
    /// </summary>
    public static string FormatWithSeparator(string text, params object[] arguments) => Format(text, arguments).Replace(Separator, "|");

    /// <summary>The translation of text that has a "#SEPARATOR#" in it,
    /// which stands for a "|" the translator may move.</summary>
    public static string TranslateWithSeparator(string text) => Translate(text).Replace(Separator, "|");
}
