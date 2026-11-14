# Testing

## Automatic

    dotnet run --project tests/Warp.Tests.csproj

Needs no game, and exits 1 if a check fails. CI also checks formatting and builds the tests with warnings as errors.
The test project compiles the pure files from `src/` (`Model/`, `Preview/` and the game-free files in `Switching/` and
`Util/`), so a game type in them fails the build.

## In the game

Close the game, run `dotnet build Warp.csproj -c Release -t:PackageMod`, copy `bin/package/warp-for-meadow` into
`RainWorld_Data/StreamingAssets/mods/`, enable it in Remix and start a story session. Warp's lines in
`BepInEx/LogOutput.log` are tagged "Warp For Meadow".

### Watch the log

Open a terminal, before or after starting the game. `-F` keeps following when the game restarts and rewrites the file.
This is the path for Steam as a Flatpak; elsewhere it is `BepInEx/LogOutput.log` in the game folder.

Nushell:

    tail -F ($nu.home-path | path join ".var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Rain World/BepInEx/LogOutput.log") | grep -i warp

bash and zsh:

    tail -F ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Rain\ World/BepInEx/LogOutput.log | grep -i warp

Change `grep -i warp` to `grep -iE "warp|error"` to see other mods' errors too, or drop it for the whole log.

### Checks

- The Warp menu shows on pause. HIDE and WARP toggle it.
- Click a room in the same region: you arrive.
- Warp to another region: the game keeps running while it loads, and a spear on your back and an item in your
  stomach come along. Opening the pause menu while it loads drops the warp.
- Right click a room for the map. Left click a spot to warp there. Right click closes it, and the pause menu is not
  frozen afterwards.
- Shift + E in the map: SCALE, BORDERS and EXPORT save a picture.
- COLORS: change a colour, press SAVE, reopen the pause menu, and it is kept.
- Sort, view, stats, favourites, the list button and the page buttons all work.
- A warp to a room whose file you renamed shows a red message naming the stage. Rename it back.
- The log shows "Region switch finished" and no errors.
