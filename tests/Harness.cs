using System;
using System.Collections.Generic;

namespace Warp.Tests;

/// <summary>
/// A small test runner: checks are counted, a failure prints its name, and a group that throws counts as one failure
/// without stopping the rest.
/// </summary>
internal static class Harness
{
    private static int _passed, _failed;

    public static bool Failed => _failed > 0;

    public static void Check(string name, bool ok)
    {
        if (ok)
        {
            _passed++;
        }
        else
        {
            _failed++;
            Console.WriteLine("FAIL: " + name);
        }
    }

    /// <summary>Like Check, but compares two values and, on a failure, shows both.</summary>
    public static void CheckEqual<T>(string name, T expected, T actual)
    {
        bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
        Check(name, ok);
        if (ok)
        {
            return;
        }
        Console.WriteLine("      expected: " + Show(expected));
        Console.WriteLine("      actual:   " + Show(actual));
    }

    /// <summary>Compares two sequences element by element.</summary>
    public static void CheckSequence<T>(string name, IEnumerable<T> expected, IEnumerable<T> actual)
    {
        var expectedList = new List<T>(expected);
        var actualList = new List<T>(actual);
        bool ok = expectedList.Count == actualList.Count;
        for (int i = 0; ok && i < expectedList.Count; i++)
        {
            ok = EqualityComparer<T>.Default.Equals(expectedList[i], actualList[i]);
        }
        Check(name, ok);
        if (ok)
        {
            return;
        }
        Console.WriteLine("      expected: " + Show(expectedList));
        Console.WriteLine("      actual:   " + Show(actualList));
    }

    public static void Group(string name, Action tests)
    {
        try
        {
            tests();
        }
        catch (Exception error)
        {
            _failed++;
            Console.WriteLine("FAIL: " + name + " threw " + error.GetType().Name + ": " + error.Message);
        }
    }

    public static int Report()
    {
        Console.WriteLine(_passed + " passed, " + _failed + " failed");
        return _failed == 0 ? 0 : 1;
    }

    private static string Show<T>(T value)
    {
        return value is System.Collections.IEnumerable items and not string
            ? "[" + string.Join(", ", ToStrings(items)) + "]"
            : value?.ToString() ?? "null";
    }

    private static IEnumerable<string> ToStrings(System.Collections.IEnumerable items)
    {
        foreach (object item in items)
        {
            yield return item?.ToString() ?? "null";
        }
    }
}
