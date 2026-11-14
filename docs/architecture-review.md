# Warp architecture

Warp adds a menu to the pause screen. Clicking a room moves the players there, loading the room's region first if
needed. This page shows how the code is arranged and why.

In the diagrams, **blue-bordered** boxes are decided in pure code and tested without the game, and **amber-bordered**
boxes touch the game and are checked in the game.

| 7 | 73 | ~6.7k | 400 | 9 |
| --- | --- | --- | --- | --- |
| layers | source files | lines | automatic checks | manual checks in game |

## Layers

Each layer uses only the layers below it. Model and Preview never mention a game type, so the test project compiles them
as they are.

```mermaid
flowchart TD
    H["<b>Hooks</b><br/>hooks and pause-menu glue · 2 files"]:::game
    UI["<b>UI</b><br/>menu objects, largest layer · 15 files"]:::game
    SW["<b>Switching</b><br/>carries out a warp, frame by frame · 15 files"]:::game
    G["<b>Game</b><br/>the game's files and the menu's state · 8 files"]:::game
    P["<b>Preview</b><br/>room map: parse, paint, export · 4 files"]:::pure
    M["<b>Model</b><br/>parse, sort, layout, colours, settings · 17 files"]:::pure
    UG["<b>Util, game side</b><br/>log, translator, Guard"]:::game
    UP["<b>Util, pure side</b><br/>Atom, Once, Lists, Maps, Numbers, EmbeddedText"]:::pure

    H --> UI
    H --> SW
    UI --> G
    SW --> G
    G --> P
    G --> UG
    P --> M
    M --> UP

    classDef pure fill:#3b82f626,stroke:#3b82f6,stroke-width:2px
    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

Every layer also uses Model and Util directly. UI and Switching never use each other. The Switching and Util files that
need no game are compiled into the tests too; the amber ones are not.

## State

Seven atoms in `WarpState` hold everything the menu remembers. Each holds an immutable record, so a reader sees one whole
value or the next one, never half of a change.

```mermaid
flowchart LR
    UI["<b>UI parts</b><br/>buttons, grid, editor,<br/>room preview"]:::game
    A["<b>WarpState atoms</b><br/>preferences<br/>favourites<br/>palette<br/>den position<br/>selected region<br/>pending warp<br/>error text"]:::game
    D["<b>WarpDriver</b><br/>runs once a frame"]:::game
    F["<b>Files on disk</b><br/>Settings.txt<br/>Favourites.txt<br/>Colors.txt"]:::game

    UI -->|swap a new value| A
    A -->|read| UI
    A -->|reads pending warp| D
    A -->|saved| F

    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

Preferences and favourites are saved by their setters. Colours change in memory as the sliders move and reach the disk
on SAVE. `RegionCatalog` is a second atom: the regions read so far.

## One warp, end to end

A click only records a request and closes the pause menu. The warp itself runs inside the game's own update, one frame at
a time.

```mermaid
flowchart TD
    Click["<b>Room button</b><br/>or a click in the preview"]:::game
    Start["<b>StartWarp</b><br/>PauseMenuActions"]:::game
    Pending["<b>Pending warp set</b><br/>CONTINUE closes the menu"]:::game
    Driver["<b>WarpDriver.Update</b><br/>OverWorld.Update hook, every frame"]:::game
    RW["<b>RoomWarp.Step</b><br/>realize room, wait, move players"]:::game
    SS["<b>SwitchStepper</b><br/>load region, prepare room, wait"]:::game
    PT["<b>PlayerTransfer.Run</b><br/>moves players and what they carry"]:::game
    End["<b>WarpDriver.End</b><br/>pending warp cleared"]:::game

    Click --> Start --> Pending
    Pending -->|the game runs again| Driver
    Driver -->|same region| RW
    Driver -->|other region| SS
    SS -->|policy says go| PT
    RW -->|done| End
    PT -->|done| End

    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

Any exception on this path goes to the failure route shown below.

## Region switch

A region loads over many frames, so the game keeps running while it does. That is the Rain Meadow freeze fix. Each
frame, a pure policy decides the next move from what the game looks like.

```mermaid
flowchart TD
    L["<b>LoadingWorld</b><br/>the loader steps once a frame"]:::pure
    P["<b>PreparingRoom</b><br/>room is realized and prepared"]:::pure
    M["<b>MovePlayers</b><br/>the policy's action"]:::pure
    T["<b>PlayerTransfer.Run</b>"]:::game
    X["<b>Leaves the switch early</b><br/>still applies = false: Drop, ends quietly<br/>60 s in one stage: Fail, the error names the stage"]:::pure

    L -->|loader finished| P
    P -->|rooms ready, players free| M
    M --> T
    L -.->|checked every frame| X
    P -.->|checked every frame| X

    classDef pure fill:#3b82f626,stroke:#3b82f6,stroke-width:2px
    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

The loader is left idle if the pause menu cancels the switch part way. The game has no way to stop it.

## Decisions and effects

The stepper only gathers facts and carries out actions. Every choice in between is a pure function, so each timeout and
each drop is unit-tested.

```mermaid
flowchart LR
    O["<b>Observe</b><br/>stepper reads the game:<br/>loader, rooms, players,<br/>the frame's clock"]:::game
    Pol["<b>SwitchPolicy.Next</b><br/>WarpWaitPolicy.Next<br/>no game types,<br/>same input, same answer"]:::pure
    Act["<b>Act</b><br/>stepper does the action:<br/>realize the room, move<br/>players, or throw a timeout"]:::game

    O -->|facts| Pol
    Pol -->|action| Act

    classDef pure fill:#3b82f626,stroke:#3b82f6,stroke-width:2px
    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

The new state is kept, so the next frame starts from it. The warp inside a region uses the same shape with
`WarpWaitPolicy`: wait, go, drop or time out.

## When something fails

A step runs as a named stage. A failure leaves carrying that stage, the innermost one wins, and the player sees the
message for it.

```mermaid
flowchart LR
    S["<b>Staged.Run</b><br/>(stage, step)"]:::game
    E["<b>WarpStageException</b><br/>stage + the original"]:::pure
    F["<b>Driver.Fail</b><br/>one place"]:::game
    Log["<b>Log</b><br/>full stack trace"]:::game
    PM["<b>Pause menu</b><br/>red text for the stage"]:::game
    End["<b>End()</b><br/>pending warp cleared"]:::game

    S -->|throws| E --> F
    F --> Log
    F --> PM
    F --> End

    classDef pure fill:#3b82f626,stroke:#3b82f6,stroke-width:2px
    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

A fault inside Warp's own part of a hook is logged once for each place by `Guard` and never reaches the game.

## Where Warp runs inside the game

Four hooks. In each, the game's method runs untouched, and Warp's part sits before or after it inside its own `try`.

| Hook | Warp, before | The game's method | Warp, after |
| --- | --- | --- | --- |
| `PauseMenu` constructor | `Prepare` | builds the pause menu | `Attach`: add the Warp panel |
| `PauseMenu.Singal` | `Signal`: room and toggle buttons | handles the message | none |
| `PauseMenu.Update` | none | updates (skipped while a preview is open) | `Update`: keys, F5 scan |
| `OverWorld.Update` | none | the world update | `WarpDriver.Update` |

A hook never leaves out the game's method, apart from the paused update while a preview is open.

## Data and packaging

The built-in colours are data, not code. They live in one file that is built into the plugin and copied out for the
player to edit.

```mermaid
flowchart LR
    D["<b>data/colors.txt</b>"]:::pure
    W["<b>Warp.dll</b>"]:::game
    C["<b>ColorPalette.Default</b>"]:::pure
    U["<b>Colors.txt</b><br/>in the player's folder"]:::game

    D -->|built in| W -->|read once| C
    C -->|written for the player if they have no file| U

    classDef pure fill:#3b82f626,stroke:#3b82f6,stroke-width:2px
    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

```mermaid
flowchart LR
    R["<b>text/  package/</b><br/>translations, modinfo, sprites"]:::game
    PM["<b>PackageMod</b><br/>bin/package/warp-for-meadow"]:::game
    WS["<b>Workshop item</b><br/>needs a thumbnail"]:::game

    R --> PM --> WS

    classDef game fill:#e0a33a26,stroke:#e0a33a,stroke-width:2px
```

The game reads `text/` and `package/` from fixed places, so they stay where they are. `data/` holds one file so far. The
room-tag table is the next candidate.

## How it is checked

| Layer | Checked by |
| --- | --- |
| Model, Preview | Compiled into a net8 test project, so a game type there fails the build. 400 automatic checks. |
| Switching, Util (pure files) | The policies, `Staged`, `Atom`, `Once` and the helpers are compiled into the same tests. |
| Game, UI, Hooks | In the game, with the short checklist in [testing.md](testing.md) and the log. |
| SwitchStepper, PlayerTransfer, RoomWarp | In the game only. Their decisions are in the pure policies; the effects are not. |
