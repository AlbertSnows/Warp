namespace Warp.Switching;

/// <summary>
/// The warp that is being carried out, in one value. The driver holds one of these, or null when there is no warp, so
/// ending or dropping a warp is setting it to null and cannot leave part of it behind.
/// A file of its own because <c>WarpDriver</c>, <c>RoomWarp</c> and <c>SwitchStepper</c> all read or build it, so it
/// belongs to none of them.
/// </summary>
/// <param name="Request">What the player asked for.</param>
/// <param name="FirstSeen">The time the driver first saw the request: the one clock for the warp.</param>
/// <param name="Switch">The region switch, once a warp to another region has started loading.</param>
/// <param name="Room">The destination room, once a warp inside the region has looked it up.</param>
internal sealed record ActiveWarp(
    Model.WarpRequest Request, float FirstSeen, SwitchStepper Switch = null, AbstractRoom Room = null);
