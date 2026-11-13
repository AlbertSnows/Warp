namespace Warp.Switching;

/// <summary>
/// The time and the frame number of one frame. The driver reads the game's clock once a frame and passes this down, so
/// everything that frame sees the same time, and what runs on it can be tested with a time that is made up.
/// </summary>
/// <param name="Now">Seconds, from a clock that keeps running while the game is paused.</param>
/// <param name="Frame">The frame number, for the log.</param>
internal readonly record struct FrameClock(float Now, int Frame);
