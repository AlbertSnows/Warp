using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Warp.Model;
using Warp.Util;

namespace Warp.Game;

/// <summary>
/// The room list export. F5 reads every installed region, a region each frame, and then writes the list of all rooms to
/// a text file and opens it. F6 writes the regions read so far.
/// </summary>
internal static class RoomListScan
{
    private readonly record struct Scan(bool Running, int Next);

    private static readonly Atom<Scan> _state = new(new Scan(false, 0));

    /// <summary>Call once a frame while the pause menu is open.</summary>
    public static void Update(RainWorldGame game)
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            Export();
        }
        if (Input.GetKeyDown(KeyCode.F5) && !_state.Deref().Running)
        {
            _state.Reset(new Scan(true, 0));
        }
        if (_state.Deref().Running)
        {
            Step(game.overWorld.regions);
        }
    }

    /// <summary>
    /// Reads the next region of the scan, and when the last has been read, ends the scan and writes the list.
    /// </summary>
    private static void Step(Region[] regions)
    {
        Scan scan = _state.Deref();
        if (scan.Next < regions.Length)
        {
            RegionCatalog.Reload(regions[scan.Next].name);
            _state.Reset(scan with
            {
                Next = scan.Next + 1
            });
        }

        if (_state.Deref().Next < regions.Length)
        {
            return;
        }

        _state.Reset(new Scan(false, 0));
        Export();
    }

    /// <summary> Abandons a scan that is part way through. A new pause menu starts fresh, so a half-done scan is not carried over. </summary>
    public static void Reset() => _state.Reset(new Scan(false, 0));

    /// <summary>
    /// Writes the regions read so far, in order of their names, to the room list file and opens it. Nothing is opened
    /// if the file could not be written.
    /// </summary>
    private static void Export()
    {
        IEnumerable<KeyValuePair<string, RoomInfo[]>> regions = RegionCatalog.All
            .OrderBy(pair => pair.Key, System.StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, RoomInfo[]>(pair.Key, pair.Value.Rooms));
        string path = WarpFiles.WriteRoomList(RoomListExport.Format(regions));
        if (path != null)
        {
            Shell.OpenFile(path);
        }
    }
}
