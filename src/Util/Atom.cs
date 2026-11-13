using System;
using System.Threading;

namespace Warp.Util;

/// <summary>The value an atom held before and after a swap.</summary>
internal readonly struct Swapped<T>(T old, T @new)
{
    /// <summary> The value before the swap. </summary>
    public readonly T old = old;
    /// <summary> The value after the swap. </summary>
    public readonly T @new = @new;
}

/// <summary>
/// A mutable cell that holds one immutable value. The menu's state lives in atoms; everything stored in them is an
/// immutable value, so a reader never sees a half-made change. Reads and swaps need no lock.
/// </summary>
internal sealed class Atom<T>(T initial)
{
    // The value is wrapped so any T, including a value type, can be swapped with one reference compare-and-set.
    private sealed class Cell(T value)
    {
        /// <summary> The value held in the cell. </summary>
        public readonly T value = value;
    }

    private Cell _cell = new(initial);

    /// <summary>The current value.</summary>
    public T Deref() => Volatile.Read(ref _cell).value;

    /// <summary>
    /// Replace the value with update(value) and return the old and new values. If another thread swaps first,
    /// update runs again on the newer value, so it must be a pure function.
    /// </summary>
    public Swapped<T> SwapVals(Func<T, T> update)
    {
        while (true)
        {
            Cell old = Volatile.Read(ref _cell);
            var next = new Cell(update(old.value));
            Cell valueBeforeChange = Interlocked.CompareExchange(ref _cell, next, old);
            if (ReferenceEquals(valueBeforeChange, old))
            {
                return new Swapped<T>(old.value, next.value);
            }
        }
    }

    /// <summary>Replace the value with update(value) and return the new value.</summary>
    public T Swap(Func<T, T> update) => SwapVals(update).@new;

    /// <summary>Replace the value and return it.</summary>
    public T Reset(T value) => SwapVals(_ => value).@new;
}
