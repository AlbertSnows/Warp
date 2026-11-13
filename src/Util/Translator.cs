using RWCustom;

namespace Warp.Util;

/// <summary>
/// Looks text up in the game's translations for the player's language. Text with no translation comes back as it
/// was written.
/// </summary>
internal static class Translator
{
    private const string NoTranslation = "!NO TRANSLATION!";

    /// <summary> The text in the player's language, or as it was written if there is no translation. </summary>
    public static string Translate(string text)
    {
        string translated = Custom.rainWorld.inGameTranslator.Translate(text);
        return string.IsNullOrEmpty(translated) || translated == NoTranslation ? text : translated;
    }

    /// <summary>The translation of text that has a "#SEPARATOR#" in it,
    /// which stands for a "|" the translator may move.</summary>
    public static string TranslateWithSeparator(string text) => Translate(text).Replace("#SEPARATOR#", "|");
}
