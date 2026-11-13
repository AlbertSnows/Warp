#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>
/// Checks that the translation files and the code still agree. The text the code passes to the translator is the key
/// in the files, so a changed spelling in the code (a spelling checker's "fix", say) silently leaves the translation
/// unused.
/// </summary>
internal static partial class TranslationTests
{
    /// <summary>
    /// Keys in the files that the code does not use as one piece of text. The first two are read by the game, not by
    /// the code. The others have been unused since upstream, and are kept for the translators.
    /// </summary>
    private static readonly string[] _notUsedByCode =
        ["warp-name", "warp-description", "ScavengerTrader", "ScavengerOutpost", "SCREENS", "Error"];

    /// <summary>
    /// Keys with two different translations in a file that need a speaker of the language to choose. Korean has both
    /// "박쥐" and "박쥐 방" for SWARM.
    /// </summary>
    private static readonly string[] _conflictsToSettle = ["SWARM"];

    public static void Run()
    {
        Group(nameof(KeyTests), KeyTests);
        Group(nameof(VersionTests), VersionTests);
    }

    internal static string? FindRepositoryRoot()
    {
        string? folder = AppContext.BaseDirectory;
        while (folder != null && !File.Exists(Path.Combine(folder, "Warp.sln")))
        {
            folder = Path.GetDirectoryName(folder);
        }
        return folder;
    }

    private static void KeyTests()
    {
        string? root = FindRepositoryRoot();
        Check("the tests can find the repository", root != null);
        if (root == null)
        {
            return;
        }

        HashSet<string> literals = [.. Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "\"((?:[^\"\\\\]|\\\\.)*)\"").Select(match => match.Groups[1].Value))];

        foreach (string file in Directory.GetFiles(Path.Combine(root, "text"), "strings.txt", SearchOption.AllDirectories))
        {
            string language = Path.GetFileName(Path.GetDirectoryName(file))!;
            List<string> keys = [.. File.ReadAllLines(file)
                .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal) && line.Contains('|'))
                .Select(line => line.Substring(0, line.IndexOf('|')))];

            CheckSequence(language + ": every key is used by the code, so a rename there has not left a translation behind", [],
                keys.Where(key => !literals.Contains(key) && !_notUsedByCode.Contains(key)));
            // The same line twice is harmless; two different translations of one key is a conflict somebody has to settle
            CheckSequence(language + ": no key has two different translations", [],
                File.ReadAllLines(file)
                    .Where(line => line.Contains('|') && !line.StartsWith("//", StringComparison.Ordinal))
                    .GroupBy(line => line.Substring(0, line.IndexOf('|')))
                    .Where(group => group.Distinct().Count() > 1 && !_conflictsToSettle.Contains(group.Key))
                    .Select(group => group.Key));
        }
    }

    /// <summary> The plugin's version is written in two places, because BepInEx needs a constant. They have to agree. </summary>
    private static void VersionTests()
    {
        string? root = FindRepositoryRoot();
        if (root == null)
        {
            return;
        }

        string project = File.ReadAllText(Path.Combine(root, "Warp.csproj"));
        string info = File.ReadAllText(Path.Combine(root, "src", "PluginInfo.cs"));
        string projectVersion = ProjectVersionElement().Match(project).Groups[1].Value;
        string codeVersion = PluginInfoVersionConstant().Match(info).Groups[1].Value;
        CheckEqual("Warp.csproj and PluginInfo.cs give the same version", projectVersion, codeVersion);
        Check("and it is a version", Regex.IsMatch(codeVersion, @"^\d+\.\d+\.\d+$"));
    }

    /// <summary> The version in Warp.csproj: the text inside its Version element. </summary>
    [GeneratedRegex("<Version>([^<]+)</Version>")]
    private static partial Regex ProjectVersionElement();
    /// <summary> The version in PluginInfo.cs: the text in quotes after "Version = ". </summary>
    [GeneratedRegex("Version = \"([^\"]+)\"")]
    private static partial Regex PluginInfoVersionConstant();
}
