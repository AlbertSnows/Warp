using System;
using System.Collections.Generic;
using Warp.Model;
using Toggling = Warp.Model.Favourites;
using Warp.Util;

namespace Warp.Game;

/// <summary>
/// Everything the Warp menu remembers while the game runs. Each piece is an immutable value in an atom, so the parts
/// of the menu can read it at any time and change it only by swapping in a new value. Preferences, favourites and
/// colors are saved to disk by their setters, so a change cannot be forgotten.
/// </summary>
internal static class WarpState
{
    private static readonly Atom<Preferences> _preferencesAtom = new(new Preferences());
    private static readonly Atom<HashSet<string>> _favouritesAtom = new([]);
    private static readonly Atom<ColorPalette> _paletteAtom = new(ColorPalette.Default);
    private static readonly Atom<WarpRequest> _pendingAtom = new(null);
    private static readonly Atom<string> _denAtom = new(null);
    private static readonly Atom<string> _regionAtom = new("");
    private static readonly Atom<string> _errorAtom = new("");

    /// <summary> The player's saved choices. Change them with <see cref="UpdatePreferences"/>. </summary>
    public static Preferences Preferences => _preferencesAtom.Deref();

    /// <summary>The regions marked as favourites. The set must not be changed; swap in a new one.</summary>
    public static IReadOnlyCollection<string> Favourites => _favouritesAtom.Deref();

    /// <summary> The button colors. Change them with <see cref="UpdatePalette"/>. </summary>
    public static ColorPalette Palette => _paletteAtom.Deref();

    /// <summary>The warp the player asked for that has not finished yet, or null.</summary>
    public static WarpRequest Pending => _pendingAtom.Deref();

    /// <summary>The room the next story session starts in, set with shift + click, or null.</summary>
    public static string DenPosition => _denAtom.Deref();

    /// <summary>The region whose rooms the menu is showing, which is where a room button warps to.</summary>
    public static string SelectedRegion => _regionAtom.Deref();

    /// <summary>Text for the menu to show after a failed warp, or an empty string.</summary>
    public static string Error => _errorAtom.Deref();

    /// <summary>Reads preferences, favourites and colors from disk. Call it when the pause menu opens.</summary>
    public static void Load()
    {
        _preferencesAtom.Reset(WarpFiles.LoadPreferences());
        _favouritesAtom.Reset(WarpFiles.LoadFavourites());
        _paletteAtom.Reset(WarpFiles.LoadPalette());
    }

    /// <summary>
    /// Changes the preferences with <paramref name="change"/> and saves them if they differ. The function must be
    /// pure.
    /// </summary>
    public static void UpdatePreferences(Func<Preferences, Preferences> change)
    {
        Swapped<Preferences> swapped = _preferencesAtom.SwapVals(change);
        if (!Equals(swapped.Old, swapped.New))
        {
            WarpFiles.SavePreferences(swapped.New);
        }
    }

    /// <summary>Adds the region to the favourites or removes it. Returns true if it is a favourite afterwards.</summary>
    public static bool ToggleFavourite(string region)
    {
        HashSet<string> favourites = _favouritesAtom.Swap(current => Toggling.Toggle(current, region));
        WarpFiles.SaveFavourites(favourites);
        return favourites.Contains(region);
    }

    /// <summary>
    /// Changes the palette with <paramref name="change"/>. The change is kept in memory; <see cref="SavePalette"/>
    /// writes it to disk.
    /// </summary>
    public static void UpdatePalette(Func<ColorPalette, ColorPalette> change) => _paletteAtom.Swap(change);

    /// <summary> Writes the current palette to disk. </summary>
    public static void SavePalette() => WarpFiles.SavePalette(Palette);

    /// <summary> Asks for a warp. The warp driver carries it out over the next frames. </summary>
    public static void RequestWarp(WarpRequest request) => _pendingAtom.Reset(request);

    /// <summary> Marks the pending warp as finished or abandoned. </summary>
    public static void EndWarp() => _pendingAtom.Reset(null);

    /// <summary> Sets the room the next story session starts in. Null clears it. </summary>
    public static void SetDenPosition(string room) => _denAtom.Reset(room);

    /// <summary> Sets the region whose rooms the menu shows, which is where a room button warps to. </summary>
    public static void SelectRegion(string region) => _regionAtom.Reset(region);

    /// <summary> Sets the text the pause menu shows after a failed warp. </summary>
    public static void ReportError(string text) => _errorAtom.Reset(text);

    /// <summary> Forgets the error text once it has been shown. </summary>
    public static void ClearError() => _errorAtom.Reset("");
}
