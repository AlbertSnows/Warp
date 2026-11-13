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
internal static class TranslationTests
{
    /// <summary> Keys in the files that the game reads, not the code. </summary>
    private static readonly string[] _notUsedByCode = ["warp-name", "warp-description"];

    /// <summary>
    /// Keys with two different translations in a file that need a speaker of the language to choose. Korean has both
    /// "박쥐" and "박쥐 방" for SWARM.
    /// </summary>
    private static readonly string[] _conflictsToSettle = ["SWARM"];

    public static void Run()
    {
        Group(nameof(KeyTests), KeyTests);
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
}
