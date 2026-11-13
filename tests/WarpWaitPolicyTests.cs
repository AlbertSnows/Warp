using Warp.Switching;
using static Warp.Tests.Harness;

namespace Warp.Tests;

/// <summary>Checks for the decisions of a waiting warp (src/Switching/WarpWaitPolicy.cs).</summary>
internal static class WarpWaitPolicyTests
{
    private const float Timeout = 60f;
    private const float Start = 10f;

    public static void Run()
    {
        Group(nameof(PolicyTests), PolicyTests);
        Group(nameof(BeforeLoadingTests), BeforeLoadingTests);
    }

    private static WarpWaitAction Next(WarpWaitFacts facts) => WarpWaitPolicy.Next(Start, facts, Timeout);

    private static void PolicyTests()
    {
        CheckEqual("a warp that no longer applies is dropped", WarpWaitAction.Drop,
            Next(new WarpWaitFacts(StillApplies: false, Ready: true, PlayersCanMove: true, Now: 11f)));
        CheckEqual("being dropped wins over timing out", WarpWaitAction.Drop,
            Next(new WarpWaitFacts(StillApplies: false, Now: 500f)));

        CheckEqual("a room that is not ready is waited for", WarpWaitAction.Wait, Next(new WarpWaitFacts(Now: 11f)));
        CheckEqual("a ready room with a player in a shortcut is waited for", WarpWaitAction.Wait,
            Next(new WarpWaitFacts(Ready: true, Now: 11f)));
        CheckEqual("players that are free do not matter until the room is ready", WarpWaitAction.Wait,
            Next(new WarpWaitFacts(PlayersCanMove: true, Now: 11f)));
        CheckEqual("a ready room and free players move", WarpWaitAction.Go,
            Next(new WarpWaitFacts(Ready: true, PlayersCanMove: true, Now: 11f)));

        CheckEqual("exactly at the limit it keeps waiting", WarpWaitAction.Wait,
            Next(new WarpWaitFacts(Now: Start + Timeout)));
        CheckEqual("past the limit it times out", WarpWaitAction.TimedOut,
            Next(new WarpWaitFacts(Now: Start + Timeout + 0.01f)));
        CheckEqual("past the limit with a player stuck in a shortcut it times out", WarpWaitAction.TimedOut,
            Next(new WarpWaitFacts(Ready: true, Now: Start + Timeout + 0.01f)));
        CheckEqual("a warp that is ready after the limit still moves", WarpWaitAction.Go,
            Next(new WarpWaitFacts(Ready: true, PlayersCanMove: true, Now: Start + Timeout + 5f)));
    }

    /// <summary>
    /// A warp to another region that has not started has no room to wait for, so the room is always ready and only the
    /// players decide.
    /// </summary>
    private static void BeforeLoadingTests()
    {
        CheckEqual("with no player alive the request is dropped", WarpWaitAction.Drop, NextStart(false, false, 11f));
        CheckEqual("and that wins over timing out", WarpWaitAction.Drop, NextStart(false, false, 500f));
        CheckEqual("players that cannot move yet are waited for", WarpWaitAction.Wait, NextStart(true, false, 11f));
        CheckEqual("players that can move start the warp", WarpWaitAction.Go, NextStart(true, true, 11f));
        CheckEqual("exactly at the limit it keeps waiting", WarpWaitAction.Wait, NextStart(true, false, Start + Timeout));
        CheckEqual("past the limit it times out", WarpWaitAction.TimedOut, NextStart(true, false, Start + Timeout + 0.01f));
        CheckEqual("players that can move after the limit still start the warp", WarpWaitAction.Go,
            NextStart(true, true, Start + Timeout + 5f));
        return;

        WarpWaitAction NextStart(bool stillApplies, bool playersCanMove, float now) =>
            WarpWaitPolicy.Next(Start, new WarpWaitFacts(stillApplies, Ready: true, playersCanMove, now), Timeout);
    }
}
