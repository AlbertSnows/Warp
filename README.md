# Rain World Warp Menu ![GitHub all releases](https://img.shields.io/github/downloads/LeeMoriya/Warp/total?style=for-the-badge)

Warp For Meadow is a fork of [Warp Menu](https://github.com/LeeMoriya/Warp) by LeeMoriya. It keeps the menu and reworks the code underneath, and it loads regions without freezing the game so it can be used with Rain Meadow.

Warp is a utility mod for Rain World that allows you to teleport the player to any room in the game, most common uses are to help with the creation of Custom Regions or testing in-development mods.

The Warp menu is accessed via the Pause screen. Rooms in your current region are displayed on the right while a list of vanilla and modded regions are displayed on the left. Clicking on a room button will warp the player to that room. Clicking on a region button will change the list of room buttons to that of the selected region so that you can warp to rooms in different regions without the use of gates.

![Warp](https://imgur.com/Z4ye1e4.png)

## Requirements:
Warp versions v1.71 and above are for Rain World v1.9
Warp versions v.86.0 and above are for Rain World v1.10

## Download:

Get the latest release [Here](https://github.com/LeeMoriya/Warp/releases/tag/v1.85.0)

## Building:
The code uses C# 12, so it needs the .NET 8 SDK. It also needs Rain World with BepInEx 
installed (run the game once with BepInEx so it creates the `HOOKS-Assembly-CSharp` and 
`PUBLIC-Assembly-CSharp` libraries the build uses). It builds on Windows, Linux and macOS. 
The first build downloads a small package from NuGet, so it needs an internet connection that once:

    dotnet build -c Release

Use `-c Release` unless you want the Debug behaviour described below.

The project looks for the game in the usual Steam folders. If yours is somewhere else, pass its folder or set the `RAINWORLD_DIR` environment variable:

    dotnet build -c Release -p:RainWorldDir="/path/to/Rain World"

The result is `bin/Release/Warp.dll`.

A Debug build, which is what `dotnet build` does when you leave out `-c Release`, is written straight into the game's `RainWorld_Data/StreamingAssets/mods/warp/newest/plugins` folder if that folder exists, so a change can be tried in the game at once. **That overwrites the Warp in that folder.** To build a Debug version without that, add `-p:DeployToGame=false`, and it goes to `bin/Debug` instead. Without a `mods/warp` folder, Debug builds always go to `bin/Debug`.

## Newer C# on .NET Framework 4.8:
Warp has to target `net48`, because Rain World's Unity 2020.3 runs mods on that family of libraries, and no newer .NET can be loaded. The compiler can still be newer than the target (`LangVersion` is 12 in `Warp.csproj`). The syntax then works, as long as the library types it relies on exist. For records and `init` properties one type is missing, `IsExternalInit`, and `src/Util/IsExternalInit.cs` supplies an empty copy. The file explains how that works and lists its sources.

## Tests:
The tests cover everything that needs no game files: reading the region files, sorting and laying out the room buttons, the settings, favourites and color files, the room preview drawing, and the region switch decisions. From the repository root:

    dotnet run --project tests/Warp.Tests.csproj

## Code layout:
`src/Model` and `src/Preview` hold the logic and know nothing about the game, which is why the tests can run without it. `src/Game` reads the game's files and keeps the menu's state. `src/Switching` carries out warps. `src/UI` is the menu itself and `src/Hooks` connects it to the game. [docs/architecture.md](docs/architecture.md) explains how the parts fit together, and [docs/testing.md](docs/testing.md) lists what to try in the game after a change, since the menu cannot be tested outside it.
