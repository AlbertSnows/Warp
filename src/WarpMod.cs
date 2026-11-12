using System;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using UnityEngine;
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
/// The mod's entry point. The plugin id is the original author's, so other mods that depend on Warp by id keep working.
/// </summary>
[BepInPlugin("LeeMoriya.Warp", "Warp", "1.9.1")]
public class WarpMod : BaseUnityPlugin
{
    private bool _initialized;

    public void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorldOnModsInit;
    }

    private void RainWorldOnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);
        if (_initialized)
        {
            return;
        }
        WarpHooks.Install();
        try
        {
            Futile.atlasManager.LoadAtlas("sprites\\warpatlas");
        }
        catch (Exception error)
        {
            WarpLog.Exception("loading the Warp sprites", error);
        }
        _initialized = true;
    }
}
