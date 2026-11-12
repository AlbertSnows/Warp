using System;
using System.Globalization;
using System.Threading;
using Warp.Switching;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>
/// Checks for the decisions a region switch makes (src/Switching/SwitchPolicy.cs). They need no game files.
/// </summary>
internal static class SwitchPolicyTests
{
    private const float Timeout = 60f;

    public static void Run()
    {
        Group(nameof(IdleTests), IdleTests);
        Group(nameof(DropTests), DropTests);
        Group(nameof(LoadingWorldTests), LoadingWorldTests);
        Group(nameof(PreparingRoomTests), PreparingRoomTests);
        Group(nameof(DefaultTimeoutTests), DefaultTimeoutTests);
        Group(nameof(StateTests), StateTests);
        Group(nameof(ElapsedTests), ElapsedTests);
    }

    /// <summary>
    /// A switch that is not running does nothing, whatever the game looks like.
    /// </summary>
    private static void IdleTests()
    {
        var idle = new SwitchStep(SwitchState.Idle, SwitchAction.None);

        var allDone = SwitchPolicy.Next(
            SwitchState.Idle,
            new SwitchFacts(LoaderFinished: true, RoomsPrepared: true, PlayersCanMove: true),
            Timeout);
        CheckEqual("an idle switch does nothing and stays idle", idle, allDone);

        var notApplying = SwitchPolicy.Next(SwitchState.Idle, new SwitchFacts(StillApplies: false, Now: 500f), Timeout);
        CheckEqual("an idle switch has nothing to drop", idle, notApplying);
    }

    /// <summary>
    /// A switch that no longer fits the game (the player died, or the world changed) is dropped, in either stage and
    /// before anything else is considered.
    /// </summary>
    private static void DropTests()
    {
        var loading = SwitchState.Begin(10f, 100);
        var preparing = loading.MoveTo(SwitchStage.PreparingRoom, 12f);
        var dropped = new SwitchStep(SwitchState.Idle, SwitchAction.Drop);

        var whileLoading = SwitchPolicy.Next(loading, new SwitchFacts(StillApplies: false, Now: 11f), Timeout);
        CheckEqual("a switch that no longer applies is dropped while loading", dropped, whileLoading);

        var whilePreparing = SwitchPolicy.Next(
            preparing,
            new SwitchFacts(StillApplies: false, RoomsPrepared: true, PlayersCanMove: true, Now: 13f),
            Timeout);
        CheckEqual(
            "a switch that no longer applies is dropped while preparing, even if ready", dropped, whilePreparing);

        var afterTimeout = SwitchPolicy.Next(loading, new SwitchFacts(StillApplies: false, Now: 500f), Timeout);
        CheckEqual("being dropped wins over timing out", dropped, afterTimeout);

        var loadFinished = SwitchPolicy.Next(
            loading, new SwitchFacts(StillApplies: false, LoaderFinished: true, Now: 11f), Timeout);
        CheckEqual("being dropped wins over a finished load", dropped, loadFinished);
    }

    /// <summary>
    /// The loading stage: wait for the loader, give up after the limit, and move on when it finishes.
    /// </summary>
    private static void LoadingWorldTests()
    {
        var loading = SwitchState.Begin(10f, 100);
        var waiting = new SwitchStep(loading, SwitchAction.Wait);

        var stillLoading = SwitchPolicy.Next(loading, new SwitchFacts(Now: 11f), Timeout);
        CheckEqual("a region that is still loading is waited for, with the state unchanged", waiting, stillLoading);

        var atLimit = SwitchPolicy.Next(loading, new SwitchFacts(Now: 10f + Timeout), Timeout);
        CheckEqual("exactly at the limit it keeps waiting", waiting, atLimit);

        var past = SwitchPolicy.Next(loading, new SwitchFacts(Now: 10f + Timeout + 0.01f), Timeout);
        CheckEqual(
            "past the limit the load times out and the switch goes back to idle",
            new SwitchStep(SwitchState.Idle, SwitchAction.LoadTimedOut), past);

        var finished = SwitchPolicy.Next(loading, new SwitchFacts(LoaderFinished: true, Now: 12.5f), Timeout);
        CheckEqual(
            "a finished load moves on to realizing the room; the stage clock restarts, the switch keeps its start",
            new SwitchStep(new SwitchState(SwitchStage.PreparingRoom, 10f, 100, 12.5f), SwitchAction.RealizeRoom),
            finished);

        const float lateTime = 10f + Timeout + 5f;
        var late = SwitchPolicy.Next(loading, new SwitchFacts(LoaderFinished: true, Now: lateTime), Timeout);
        CheckEqual(
            "a load that finishes after the limit has still finished",
            new SwitchStep(new SwitchState(SwitchStage.PreparingRoom, 10f, 100, lateTime), SwitchAction.RealizeRoom),
            late);
    }

    /// <summary>
    /// The preparing stage: wait for the rooms, then for the players to leave the shortcuts, then move them, with a
    /// limit on each wait that runs from the start of the stage.
    /// </summary>
    private static void PreparingRoomTests()
    {
        var preparing = SwitchState.Begin(10f, 100).MoveTo(SwitchStage.PreparingRoom, 12f);
        var waiting = new SwitchStep(preparing, SwitchAction.Wait);
        var moved = new SwitchStep(SwitchState.Idle, SwitchAction.MovePlayers);

        var notPrepared = SwitchPolicy.Next(preparing, new SwitchFacts(Now: 13f), Timeout);
        CheckEqual("rooms that are still preparing are waited for", waiting, notPrepared);

        var roomTimeout = SwitchPolicy.Next(preparing, new SwitchFacts(Now: 12f + Timeout + 0.01f), Timeout);
        CheckEqual(
            "rooms that never finish time out",
            new SwitchStep(SwitchState.Idle, SwitchAction.RoomTimedOut),
            roomTimeout);

        var roomsAtLimit = SwitchPolicy.Next(preparing, new SwitchFacts(Now: 12f + Timeout), Timeout);
        CheckEqual("exactly at the limit the rooms are still waited for", waiting, roomsAtLimit);

        var fromStageStart = SwitchPolicy.Next(preparing, new SwitchFacts(Now: 10f + Timeout + 0.01f), Timeout);
        CheckEqual("the timeout runs from the start of the stage, not the switch", waiting, fromStageStart);

        var blocked = SwitchPolicy.Next(preparing, new SwitchFacts(RoomsPrepared: true, Now: 13f), Timeout);
        CheckEqual("prepared rooms with a player in a shortcut are waited for", waiting, blocked);

        var moveTimeout = SwitchPolicy.Next(
            preparing, new SwitchFacts(RoomsPrepared: true, Now: 12f + Timeout + 0.01f), Timeout);
        CheckEqual(
            "a player who never leaves the shortcut times out the move",
            new SwitchStep(SwitchState.Idle, SwitchAction.MoveTimedOut), moveTimeout);

        var moveAtLimit = SwitchPolicy.Next(
            preparing, new SwitchFacts(RoomsPrepared: true, Now: 12f + Timeout), Timeout);
        CheckEqual("exactly at the limit a blocked player is still waited for", waiting, moveAtLimit);

        var ready = SwitchPolicy.Next(
            preparing, new SwitchFacts(RoomsPrepared: true, PlayersCanMove: true, Now: 13f), Timeout);
        CheckEqual("everything ready moves the players and the switch is done", moved, ready);

        var movingLate = SwitchPolicy.Next(
            preparing, new SwitchFacts(RoomsPrepared: true, PlayersCanMove: true, Now: 12f + Timeout + 5f), Timeout);
        CheckEqual("ready after the limit still moves the players", moved, movingLate);

        var playersFirst = SwitchPolicy.Next(preparing, new SwitchFacts(PlayersCanMove: true, Now: 13f), Timeout);
        CheckEqual("players being free does not matter until the rooms are prepared", waiting, playersFirst);
    }

    /// <summary>
    /// The limit a switch uses when it is not given one.
    /// </summary>
    private static void DefaultTimeoutTests()
    {
        CheckEqual("a stage may take a minute by default", 60f, SwitchPolicy.DefaultTimeoutSeconds);
    }

    /// <summary>
    /// A state never changes once made, and the same inputs always give the same answer.
    /// </summary>
    private static void StateTests()
    {
        var loading = SwitchState.Begin(10f, 100);
        var preparing = loading.MoveTo(SwitchStage.PreparingRoom, 12f);

        CheckEqual(
            "a new switch is loading, with both clocks at its start",
            new SwitchState(SwitchStage.LoadingWorld, 10f, 100, 10f),
            loading);
        CheckEqual(
            "moving to another stage restarts the stage clock and nothing else",
            new SwitchState(SwitchStage.PreparingRoom, 10f, 100, 12f), preparing);
        CheckEqual(
            "and the old state is as it was",
            new SwitchState(SwitchStage.LoadingWorld, 10f, 100, 10f), loading);

        var facts = new SwitchFacts(LoaderFinished: true, Now: 11f);
        CheckEqual(
            "the same inputs always give the same answer",
            SwitchPolicy.Next(loading, facts, Timeout), SwitchPolicy.Next(loading, facts, Timeout));
    }

    /// <summary>
    /// The time and frame count in the log, including that the decimal point does not follow the player's language.
    /// </summary>
    private static void ElapsedTests()
    {
        var state = SwitchState.Begin(10f, 100);
        CheckEqual("time and frames are reported", "2.4 seconds, 96 frames", SwitchPolicy.Elapsed(state, 12.4f, 196));
        CheckEqual("a switch that has just begun", "0.0 seconds, 0 frames", SwitchPolicy.Elapsed(state, 10f, 100));

        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            CheckEqual(
                "the decimal point does not depend on the player's language",
                "2.4 seconds, 96 frames",
                SwitchPolicy.Elapsed(state, 12.4f, 196));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
