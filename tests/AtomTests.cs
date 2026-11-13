using System.Linq;
using System.Threading;
using Warp.Util;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for the cell that holds the menu's state (src/Util/Atom.cs).</summary>
internal static class AtomTests
{
    public static void Run()
    {
        Group(nameof(BasicTests), BasicTests);
        Group(nameof(RetryTests), RetryTests);
        Group(nameof(ThreadTests), ThreadTests);
    }

    private static void BasicTests()
    {
        var atom = new Atom<int>(5);
        CheckEqual("an atom holds what it starts with", 5, atom.Deref());
        CheckEqual("Swap returns the new value", 6, atom.Swap(value => value + 1));
        CheckEqual("and keeps it", 6, atom.Deref());
        CheckEqual("Reset replaces the value and returns it", 40, atom.Reset(40));
        CheckEqual("and keeps it", 40, atom.Deref());

        Swapped<int> swapped = atom.SwapVals(value => value * 2);
        CheckEqual("SwapVals returns the value before", 40, swapped.Old);
        CheckEqual("and the value after", 80, swapped.New);

        var text = new Atom<string>(null);
        CheckEqual("an atom may hold null", null, text.Deref());
        CheckEqual("and a reference type", "abc", text.Reset("abc"));

        var list = new Atom<int[]>([1, 2]);
        int[] before = list.Deref();
        list.Swap(items => [.. items, 3]);
        CheckSequence("a new value is a new object, and the old one is left as it was", new[] { 1, 2 }, before);
        CheckSequence("the atom holds the new one", new[] { 1, 2, 3 }, list.Deref());
    }

    /// <summary>
    /// If something else changes the atom while the update function runs, the swap does not go through and the
    /// function runs again on the newer value.
    /// </summary>
    private static void RetryTests()
    {
        var atom = new Atom<int>(0);
        int calls = 0;
        int result = atom.Swap(value =>
        {
            calls++;
            if (calls == 1)
            {
                atom.Reset(100);
            }
            return value + 1;
        });
        CheckEqual("the update runs again after another change got in first", 2, calls);
        CheckEqual("and builds on the newer value, not the one it first saw", 101, result);
        CheckEqual("which is what the atom holds", 101, atom.Deref());
    }

    private static void ThreadTests()
    {
        const int threadCount = 8;
        const int perThread = 2000;
        var atom = new Atom<int>(0);
        Thread[] threads = Enumerable.Range(0, threadCount)
            .Select(_ => new Thread(() =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    atom.Swap(value => value + 1);
                }
            }))
            .ToArray();
        foreach (Thread thread in threads)
        {
            thread.Start();
        }
        foreach (Thread thread in threads)
        {
            thread.Join();
        }
        CheckEqual("no update is lost when many threads change one atom", threadCount * perThread, atom.Deref());
    }
}
