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
    private static readonly Atom<Preferences> PreferencesAtom = new(new Preferences());
    private static readonly Atom<HashSet<string>> FavouritesAtom = new(new HashSet<string>());
    private static readonly Atom<ColorPalette> PaletteAtom = new(ColorPalette.Default);
    private static readonly Atom<WarpRequest> PendingAtom = new(null);
    private static readonly Atom<string> DenAtom = new(null);
    private static readonly Atom<string> RegionAtom = new("");
    private static readonly Atom<string> ErrorAtom = new("");

    public static Preferences Preferences => PreferencesAtom.Deref();

    /// <summary>The regions marked as favourites. The set must not be changed; swap in a new one.</summary>
    public static IReadOnlyCollection<string> Favourites => FavouritesAtom.Deref();

    public static ColorPalette Palette => PaletteAtom.Deref();

    /// <summary>The warp the player asked for that has not finished yet, or null.</summary>
    public static WarpRequest Pending => PendingAtom.Deref();

    /// <summary>The room the next story session starts in, set with shift + click, or null.</summary>
    public static string DenPosition => DenAtom.Deref();

    /// <summary>The region whose rooms the menu is showing, which is where a room button warps to.</summary>
    public static string SelectedRegion => RegionAtom.Deref();

    /// <summary>Text for the menu to show after a failed warp, or an empty string.</summary>
    public static string Error => ErrorAtom.Deref();

    /// <summary>Reads preferences, favourites and colors from disk. Call it when the pause menu opens.</summary>
    public static void Load()
    {
        PreferencesAtom.Reset(WarpFiles.LoadPreferences());
        FavouritesAtom.Reset(WarpFiles.LoadFavourites());
        PaletteAtom.Reset(WarpFiles.LoadPalette());
    }

    public static void UpdatePreferences(Func<Preferences, Preferences> change)
    {
        var swapped = PreferencesAtom.SwapVals(change);
        if (!Equals(swapped.Old, swapped.New))
        {
            WarpFiles.SavePreferences(swapped.New);
        }
    }

    /// <summary>Adds the region to the favourites or removes it. Returns true if it is a favourite afterwards.</summary>
    public static bool ToggleFavourite(string region)
    {
        var favourites = FavouritesAtom.Swap(current => Toggling.Toggle(current, region));
        WarpFiles.SaveFavourites(favourites);
        return favourites.Contains(region);
    }

    public static void UpdatePalette(Func<ColorPalette, ColorPalette> change) => PaletteAtom.Swap(change);

    public static void SavePalette() => WarpFiles.SavePalette(Palette);

    public static void RequestWarp(WarpRequest request) => PendingAtom.Reset(request);

    public static void EndWarp() => PendingAtom.Reset(null);

    public static void SetDenPosition(string room) => DenAtom.Reset(room);

    public static void SelectRegion(string region) => RegionAtom.Reset(region);

    public static void ReportError(string text) => ErrorAtom.Reset(text);

    public static void ClearError() => ErrorAtom.Reset("");
}
