namespace Warp;

/// <summary>
/// The plugin's identity, in one place. BepInEx reads these from an attribute, which needs constants, so they cannot
/// come from the project file; a test checks that <see cref="Version"/> matches the one in Warp.csproj.
/// A file of its own because the plugin class, its incompatibility attribute and the version test all need these, and
/// they are the only things that change when the plugin is renamed or released.
/// </summary>
internal static class PluginInfo
{
    /// <summary> The id BepInEx knows this plugin by. It is this fork's own, so it can be told apart from the original. </summary>
    public const string Guid = "AJ_Snow.WarpForMeadow";

    public const string Name = "Warp For Meadow";

    public const string Version = "1.9.1";

    /// <summary>The original Warp Menu's id. The two add the same menu, so they cannot both be enabled.</summary>
    public const string UpstreamGuid = "LeeMoriya.Warp";
}
