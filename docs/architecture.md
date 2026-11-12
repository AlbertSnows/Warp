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

A failure is logged, the stage it happened in is named in the error text, and the pause menu is made again to show it.

## Differences from the original Warp

Intended changes, in case one is noticed in the game:

- Unsaved color edits stay for the rest of the session. They used to be undone when the room buttons were rebuilt, because
  the colors file was read again each time.
- Subregion colors past the built-in ones are generated from the golden ratio and not at random, so they are the same
  every time.
- The second page of room headings moves with its buttons. A comment in the old code said it did not work.
- F5 and F6 are edge triggered. Holding F6 used to write and open the file every frame,
  and F5 only worked once per session.
- A warp to a tile in the preview puts every player there. Only the first used to go.
- Duplicate objects are removed from a room's update list without deleting the objects after them, which the old loop did.
- The camera count of a room without a room file is 1 when the room is read, and not when its button is made.
- A fault during a warp inside the region ends the warp with an error. It used to repeat every frame.
- A room that cannot be previewed is logged instead of crashing, and a room file that is too short is read as one camera.
- A favourite region with no full name shows its id, as the others do.
- The room list export writes the subregion, not the camera count twice.
- Exporting a preview scales the picture itself, so the render textures it used to allocate for each export are gone.
