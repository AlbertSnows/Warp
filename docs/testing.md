# Testing

## Automatic

    dotnet run --project tests/Warp.Tests.csproj

It prints the number of checks that passed and failed and exits with 1 if any failed. CI runs it, after checking the
formatting with `dotnet format whitespace --folder --verify-no-changes` and building the test project with warnings as
errors. The tests cover `src/Model`, `src/Preview`, the
`Atom` and the region switch decisions in `src/Switching`. They need no game files.

## In the game

The menu itself is Rain World's `MenuObject`s and cannot be run outside the game. After a change to `src/UI`,
`src/Hooks`, `src/Game` or `src/Switching`, build a Debug version into the game's mods folder and go through this in a
story session. Look at `BepInEx/LogOutput.log` afterwards: Warp's lines are tagged "Warp For Meadow", and any
error from it is there with its stack trace.

1. Pause. The Warp menu is on the left and the room buttons on the right, in the current region. HIDE hides the menu and
   brings back the controls picture; WARP shows it again. The choice is kept when you pause again.
2. Click a room button. You are moved to the room. Do it with a creature in your hands.
3. Choose another region from the dropdown and click one of its rooms. The game keeps running while the region loads,
   then you arrive. Do this with a spear on your back and an item in your stomach and check both come along.
4. The list button switches between the dropdown and the grid of region buttons. With shift held while going to the
   buttons, the order changes between alphabetical and the game's.
5. The favourite button favourites the region; favourites come first. Check the dropdown and the grid.
6. SORT and VIEW: each of the three, and sorting by subregion with viewing by type (a second list of subregion names
   appears). Hover a control and read the help line at the bottom.
7. STATS shows the counts. Check it under the key, and beside the region buttons.
8. COLORS: pick a color, move the sliders, see the buttons change. DONE returns to the room buttons with the change.
   SAVE and reopen the pause menu to see it kept.
9. A region with more than 21 rooms in a group has LEFT and RIGHT page buttons. Check the headings move with the
   buttons.
10. Right click a room button. The map opens. Left click in it to warp to that spot, in the same region and in another.
    Right click closes it. Shift + E shows the export bar: try each button, and EXPORT REGION on a small region.
11. Shift + click a room sets the den position; C clears it. Start a new session and check it starts there.
12. F5 reads every region and opens the room list; F6 writes the regions read so far.
13. Make a warp fail (for example, a region whose files are broken) and check the pause menu shows a red message.
14. Right click a room, open the export bar and press SCALE more than once. The label reads "SCALE: X2", "X5", "X10"
    and then "X1" again, and EXPORT saves a picture of that size. Try a large room at X10: it should say the picture
    is too big.
15. With both Warp Menu and this mod installed, only one of them loads (look for the BepInEx incompatibility message).
16. Warp to a room in your own region with a second player in a shortcut. The warp waits until they are out.
17. Open and close the room preview a few times, then pause and unpause. Nothing should be left on screen and the pause
    menu should not stay frozen.
18. Messages Warp handles do not leave Warp. With another mod enabled that has buttons called DONE, SAVE, STATS or FAV
    (or by watching the log of a mod that hooks `PauseMenu.Singal`), open the colour editor, press SAVE and DONE, press
    the region toggle, STATS and the favourite button, and use the sort and view buttons and the LEFT and RIGHT page
    buttons. Nothing outside Warp should react. Room buttons still warp, and HIDE / WARP still hides the menu.
19. With the export bar open, turn BORDERS on and off and check the pixel size in the label changes by 6 pixels a side
    (before scaling) for a room whose type gets a border under that mode. The saved picture has that size.
20. The log tags Warp's lines with the plugin's name in `BepInEx/LogOutput.log` (not in `consoleLog.txt`), and a warp
    writes the "Loading room ..." and "Region switch finished ..." lines there.
21. On a native Linux or macOS build, the buttons have their symbols (the atlas path uses forward slashes).
22. Open a room preview and close it with the right button. The texts at the bottom fade in and out by the same amount,
    and do not jump when it starts to close.
23. Make a warp fail (for example, in a test region, rename one room's file so it cannot load) and check the pause menu
    shows the message for the stage that failed (loading the region, or realising the room), not a general one.
24. Warp to another region, then open the pause menu while it is still loading. The warp is dropped and a new one can be
    started.
