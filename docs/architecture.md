# Architecture

Warp adds a menu to Rain World's pause screen. The menu lists the rooms of any installed region, and clicking one
moves the players there, loading the room's region first if needed. This page describes how the code is arranged and
why. For the steps to try in the game after a change, see [testing.md](testing.md).

## Layers

```
src/
  Model/       Pure logic: no game types. Compiled into the test project.
  Preview/     Pure logic for the room map: reading a room file, drawing it, export choices.
  Util/        Atom, the log, the translator.
  Game/        What touches the game's files and holds the menu's state.
  Switching/   Carrying out a warp, one frame at a time.
  UI/          The menu: Rain World MenuObjects.
  Hooks/       The connection to the game: hooks, and what is added to the pause menu.
tests/         Runs without the game: dotnet run --project tests/Warp.Tests.csproj
```

The rule that keeps this useful: a file in `Model/` or `Preview/` never mentions a game or Unity type. That is what lets
the tests run anywhere, and it forces the decisions (where a button goes, how rooms are sorted, what the settings file
says) out of the menu code, where they could only be checked by playing.

### Model

| File | Decides |
| --- | --- |
| `RegionFiles` | What a world, map, properties, room or spoilers file says. Takes lines, returns values. |
| `RoomInfo`, `RegionData` | One room, and one region's rooms and subregion names. Immutable records. |
| `RoomSorting` | The three orders (type, size, subregion), each ending in the room name. |
| `RoomGridLayout` | Where each room button and heading goes, and which page it is on. |
| `LeftPanelLayout` | The height of every control in the left panel, from the picker's height down. |
| `RegionList` | The order of regions in the dropdown and in the button grid. |
| `RegionStats` | The counts and the text of the stats panel. |
| `Preferences`, `Favourites` | What is saved between sessions, and the text of the files. |
| `ColorPalette`, `ColorFile` | Button colors, and the text of Colors.txt. |
| `RoomListExport` | The text of the F5 / F6 export. |
| `WarpRequest` | A warp the player asked for: region, room, and optionally a tile. |

### State

The menu's state is in `Game/WarpState`: one atom for each of preferences, favourites, colors, the pending warp, the
den position, the selected region and the error text. Every value in an atom is immutable, so the parts of the menu read
it at any time and change it only by swapping in a new value. Preferences, favourites and colors are saved to disk by
their setters. There are no other mutable statics in the menu apart from the loaded regions in `RegionCatalog`, which
is also an atom.

### The menu

`UI/WarpContainer` is the root. It owns the parts, decides what a click changes, and calls `LeftPanelLayout` after
anything that changes a height. The parts draw themselves and report what the player did:

- `RegionPicker`: the dropdown or the grid of region buttons. Raises `RegionChosen`.
- `RoomGrid`: the room buttons and headings, and the two pages.
- `Legend`: the sort and view buttons, the color key and the subregion list. Raises `Changed`.
- `StatsPanel`: the counts.
- `ColorEditor`: the color customiser. Changes the palette as sliders move; SAVE writes it.
- `RoomPreviewDialog`: the room map, opened by right click. Left click on it warps to that tile.

Each part is a `MenuObject` with its own children, so removing a part removes everything it showed.

### Warping

`WarpDriver.Update` runs once a frame after the game's own overworld update. While a `WarpRequest` is pending it does
one of two things:

- The region is the one the players are in: `RoomWarp` realizes the room, moves the players when it is ready, and moves
  the camera.
- It is another region: a `SwitchStepper` loads the region with the game's `WorldLoader` one step per frame, realizes
  the room, waits for the players to be out of shortcuts, and then `PlayerTransfer` moves them, following the game's
  `OverWorld.WorldLoaded`. `SwitchPolicy` makes the decisions (wait, move on, give up) from facts the stepper gathers, and
  it is pure, so its tests cover every timeout.

Both paths wait for every player to be out of a shortcut, and both give up after `WarpTimeout.Seconds` (60), as
`SwitchPolicy` and `WarpWaitPolicy` decide; both are pure and tested. The driver reads the game's clock once a frame and
passes it down as a `FrameClock`.

The running warp is one `ActiveWarp` held by the driver (the request, when it was first seen, and the region switch or the
destination room once there is one), or null when there is none, so ending or dropping a warp is setting it to null. The
region switch holds its own state in one value too, with the loader only in the stage that has it.

A step of a warp is run as a stage with `Staged.Run`. If it fails, the exception leaves as a `WarpStageException` that
names the stage, and the innermost stage wins. The driver chooses the error text for the player from it, so there is no
"current stage" to keep up to date. A failure is logged, shown in a new pause menu, and ends the warp.

Every hook that calls into Warp catches what Warp throws and logs it once for each place (`Guard`), so a fault in Warp
cannot stop the pause menu opening or repeat every frame. The game's own original method is never inside the catch.

### Region switch: what to expect

- A switch to another region takes about as long as a gate transition. The loader is stepped once a frame, and the game
  stays playable while it loads.
- `OverWorld.LoadWorld` is no longer called, so another mod that hooks it will not see Warp's switches. Mods that hook
  `WorldLoader` still do.
- Opening the pause menu while a switch is in progress cancels it. A `WorldLoader` that is abandoned part way leaves its
  worker thread idle for the rest of the session; the game has no way to stop it.
- The log, `BepInEx/LogOutput.log`, records each switch and how long it took, in lines tagged with the plugin's name.
- If moving the players fails part way, the new world is already the active one and some players may have moved. That is
  not undone, because the players are partly in each world; only the HUD is rebuilt, so the camera does not fail on every
  frame. The log says the session may be unstable.

### Known limitations with Rain Meadow

This mod fixes the freeze, and nothing else about Meadow, by decision: anything more would be a change to Rain Meadow
itself or a separate mod that works beside this one. These come from the notes made while the non-blocking switch was
developed, and have not all been retested on this build.

- Warp does not call the game's `OverWorld.WorldLoaded`, so Rain Meadow's own region change handling (releasing the old
  region, avatar fix-ups) is skipped. The non-blocking switch removes the freeze, not that gap. A Meadow hook for "a mod
  changed region" is the clean way to close it.
- Meadow logged, for each warp, an error level stack trace from its own `RoomOnAddObject` guard (a weapon being realized
  into a room whose update list already had it), and once an `entity with repeated ID` message. Both look harmless alone
  and are the thing to watch with a second player.
- Tested with Meadow as the host with nobody else in the lobby. Warping with a second player, shelters and sleeping, and
  dying or walking into a gate during a load were not tested.
- MeadowWarpFix works around the blocking loop of the original Warp, which this build no longer has, so it should not be
  needed beside this one. That has not been tested.
- The preview's atlas is unloaded when the preview closes. That has not been tried in the game yet.
- Both Warp Menu and this mod add the same menu, so this one declares the original incompatible, and BepInEx will not
  load both. That has not been tried with both installed.

## Differences from the original Warp

Intended changes, in case one is noticed in the game:

- Unsaved color edits stay for the rest of the session. They used to be undone when the room buttons were rebuilt, because
  the colors file was read again each time.
- Subregion colors past the built-in ones are generated from the golden ratio and not at random, so they are the same
  every time.
- The second page of room headings moves with its buttons. A comment in the old code said it did not work.
- F5 and F6 are edge triggered. Holding F6 used to write and open the file every frame,
  and F5 only worked once per session.
- Clicking in the room preview warps to the tile under the pointer. The old maths rounded to the nearest tile edge, which could pick the next tile along in rooms with an even width or height.
- A warp to a tile in the preview puts every player there. Only the first used to go.
- Duplicate objects are removed from a room's update list without deleting the objects after them, which the old loop did.
- The camera count of a room without a room file is 1 when the room is read, and not when its button is made.
- A fault during a warp inside the region ends the warp with an error. It used to repeat every frame.
- A room that cannot be previewed is logged instead of crashing, and a room file that is too short is read as one camera.
- A favourite region with no full name shows its id, as the others do.
- The room list export writes the subregion, not the camera count twice.
- Exporting a preview scales the picture itself, so the render textures it used to allocate for each export are gone.
- Room names and region ids are ordered by character code and not by the player's language, so the order is the same on
  every machine. Names the game translates (the dropdown) still follow the player's language.
- Favourites come first in the dropdown whether or not it is alphabetical, as the tooltip says. They used to sort first
  only because "> " collates before letters.
- A room line in a world file has to start with its region's prefix once its conditions are taken off, and conditions
  can be stacked, so a room of region LSU is not read as one of SU.
- Colors that are not numbers from 0 to 1 are rejected as a bad line and the default is kept.
- Warp's own signals carry a prefix and are only read from Warp's buttons, so another mod's message that ends in "warp"
  no longer starts a warp.
- A warp inside a region waits for every player to be out of a shortcut and gives up after 60 seconds, as a warp to
  another region does. The spear on a player's back is moved once the room is ready, not on every frame before.
- If the game's controls picture is hidden, it is only hidden when the Warp menu was added.
- The room list and the preview exports: an I/O error is logged and not thrown, nothing is opened if the file was not
  written, regions are written in order of name, and a picture too big for a texture is refused with a message.
- Preferences, favourites and colors are read from disk once per run of the game; the values in memory are the ones that
  count from then on, so a second story session in the same run keeps them. Preferences and favourites are written as
  they change. Colors are changed in memory as the sliders move and are only written to disk by SAVE.
- The version is in `Warp.csproj` and in `PluginInfo`, because BepInEx needs a constant. The build stops if they differ.
- The mod's start-up cannot fail the game's own mod initialisation: the hooks are installed one at a time from a table, a
  failure is logged by name, and the plugin marks itself started first so a partial install is never repeated.
- Warp's messages are written through BepInEx's log, so they are in `BepInEx/LogOutput.log` with the plugin's name, where
  they used to go to Unity's `consoleLog.txt` with a "WARP:" prefix.
- The sprites are loaded by a path with a forward slash, which works on every platform.
- The preview's texts fade in and out by the same amount; the closing fade used to start them from 1.
