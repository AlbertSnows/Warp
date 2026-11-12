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

    private static readonly Atom<Scan> State = new(new Scan(false, 0));

    /// <summary>Call once a frame while the pause menu is open.</summary>
    public static void Update(RainWorldGame game)
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            Export();
        }
        if (Input.GetKeyDown(KeyCode.F5) && !State.Deref().Running)
        {
            State.Reset(new Scan(true, 0));
        }
        if (State.Deref().Running)
        {
            Step(game.overWorld.regions);
        }
    }

    private static void Step(Region[] regions)
    {
        var scan = State.Deref();
        if (scan.Next < regions.Length)
        {
            RegionCatalog.Reload(regions[scan.Next].name);
            State.Reset(scan with
            {
                Next = scan.Next + 1
            });
        }
        if (State.Deref().Next >= regions.Length)
        {
            State.Reset(new Scan(false, 0));
            Export();
        }
    }

    private static void Export()
    {
        var regions = RegionCatalog.All.Select(pair => new KeyValuePair<string, RoomInfo[]>(pair.Key, pair.Value.Rooms));
        var path = WarpFiles.WriteRoomList(RoomListExport.Format(regions));
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
