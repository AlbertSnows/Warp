using Warp.Switching;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for the decisions of a warp inside a region (src/Switching/RoomWarpPolicy.cs).</summary>
internal static class RoomWarpPolicyTests
{
    private const float Timeout = 60f;
    private const float Start = 10f;

    public static void Run()
    {
        Group(nameof(PolicyTests), PolicyTests);
        Group(nameof(StartTests), StartTests);
    }

    private static RoomWarpAction Next(RoomWarpFacts facts) => RoomWarpPolicy.Next(Start, facts, Timeout);

    private static void PolicyTests()
    {
        CheckEqual("a warp that no longer applies is dropped", RoomWarpAction.Drop,
            Next(new RoomWarpFacts(StillApplies: false, RoomReady: true, PlayersCanMove: true, Now: 11f)));
        CheckEqual("being dropped wins over timing out", RoomWarpAction.Drop,
            Next(new RoomWarpFacts(StillApplies: false, Now: 500f)));

        CheckEqual("a room that is not ready is waited for", RoomWarpAction.Wait, Next(new RoomWarpFacts(Now: 11f)));
        CheckEqual("a ready room with a player in a shortcut is waited for", RoomWarpAction.Wait,
            Next(new RoomWarpFacts(RoomReady: true, Now: 11f)));
        CheckEqual("players that are free do not matter until the room is ready", RoomWarpAction.Wait,
            Next(new RoomWarpFacts(PlayersCanMove: true, Now: 11f)));
        CheckEqual("a ready room and free players move", RoomWarpAction.Move,
            Next(new RoomWarpFacts(RoomReady: true, PlayersCanMove: true, Now: 11f)));

        CheckEqual("exactly at the limit it keeps waiting", RoomWarpAction.Wait,
            Next(new RoomWarpFacts(Now: Start + Timeout)));
        CheckEqual("past the limit it times out", RoomWarpAction.TimedOut,
            Next(new RoomWarpFacts(Now: Start + Timeout + 0.01f)));
        CheckEqual("past the limit with a player stuck in a shortcut it times out", RoomWarpAction.TimedOut,
            Next(new RoomWarpFacts(RoomReady: true, Now: Start + Timeout + 0.01f)));
        CheckEqual("a warp that is ready after the limit still moves", RoomWarpAction.Move,
            Next(new RoomWarpFacts(RoomReady: true, PlayersCanMove: true, Now: Start + Timeout + 5f)));
        CheckEqual("a minute is the default limit", 60f, RoomWarpPolicy.DefaultTimeoutSeconds);
    }

    private static WarpStartAction NextStart(WarpStartFacts facts) => WarpStartPolicy.Next(Start, facts, Timeout);

    private static void StartTests()
    {
        CheckEqual("with no player alive the request is dropped", WarpStartAction.Drop,
            NextStart(new WarpStartFacts(StillApplies: false, Now: 11f)));
        CheckEqual("and that wins over timing out", WarpStartAction.Drop,
            NextStart(new WarpStartFacts(StillApplies: false, Now: 500f)));
        CheckEqual("players that cannot move yet are waited for", WarpStartAction.Wait, NextStart(new WarpStartFacts(Now: 11f)));
        CheckEqual("players that can move start the warp", WarpStartAction.Begin,
            NextStart(new WarpStartFacts(PlayersCanMove: true, Now: 11f)));
        CheckEqual("exactly at the limit it keeps waiting", WarpStartAction.Wait, NextStart(new WarpStartFacts(Now: Start + Timeout)));
        CheckEqual("past the limit it times out", WarpStartAction.TimedOut, NextStart(new WarpStartFacts(Now: Start + Timeout + 0.01f)));
        CheckEqual("players that can move after the limit still start the warp", WarpStartAction.Begin,
            NextStart(new WarpStartFacts(PlayersCanMove: true, Now: Start + Timeout + 5f)));
    }
}
