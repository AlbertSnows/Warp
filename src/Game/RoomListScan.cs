using System.Collections.Generic;
using System.Diagnostics;
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

    /// <summary>
    /// Writes the regions read so far to the room list file and opens it. Failing to open the file is logged, not
    /// thrown.
    /// </summary>
    private static void Export()
    {
        IEnumerable<KeyValuePair<string, RoomInfo[]>> regions = RegionCatalog.All.Select(pair => new KeyValuePair<string, RoomInfo[]>(pair.Key, pair.Value.Rooms));
        string path = WarpFiles.WriteRoomList(RoomListExport.Format(regions));
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (System.Exception error)
        {
            WarpLog.Error("Failed to open " + path + ": " + error.Message);
        }
    }
}
