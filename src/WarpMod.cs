using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using Warp.Hooks;
using Warp.Util;

// BepInEx plugins that use the publicized game libraries are built with verification skipped. The runtime no longer
// enforces the permission, but Unity's Mono still reads it, so it stays.
#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Warp;

/// <summary>
/// The mod's entry point. The plugin id is this fork's own, so BepInEx does not take it for Lee Moriya's Warp Menu.
/// The two add the same menu and hook the same game methods, so this one declares the original incompatible and
/// BepInEx will not load it beside the original.
/// </summary>
[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInIncompatibility(PluginInfo.UpstreamGuid)]
public class WarpMod : BaseUnityPlugin
{
    private bool _initialized;

    public void OnEnable()
    {
        WarpLog.Initialize(Logger);
        On.RainWorld.OnModsInit += RainWorldOnModsInit;
    }

    /// <summary>
    /// Sets Warp up once the game has loaded its mods: installs the hooks and loads the sprites. A failure in either is
    /// logged and does not reach the game's own mod start-up, which every mod after this one depends on. It is marked
    /// done first, so a partly installed set of hooks is never installed a second time.
    /// </summary>
    private void RainWorldOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        WarpHooks.Install();
        try
        {
            Futile.atlasManager.LoadAtlas("sprites/warpatlas");
        }
        catch (Exception error)
        {
            WarpLog.Exception("loading the Warp sprites", error);
        }
    }
}
