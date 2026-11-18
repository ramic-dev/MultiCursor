[Italiano](README.md) | **English**

# MultiCursor

A tiny Windows tool that adds extra mouse cursors: they follow the real cursor at a fixed distance and replicate clicks and wheel scrolling at every point, almost at the same instant.

A single C# source file, no dependencies, no installation. It builds with the compiler that already ships with Windows.

> The program's own interface (help window, messages, comments in the settings file) is in Italian.

## How it works

Windows has only one system cursor. The extra cursors are therefore small always-on-top, click-through windows that draw an arrow. When you press a mouse button, MultiCursor holds back the real event and re-emits it in rapid sequence at every point (yours included), then puts the cursor back where it was.

## Usage

1. Start `MultiCursor.exe`: an icon appears in the notification area.
2. Move the mouse to each point where you want a cursor and press `Ctrl+Shift+F1`.
3. Move the mouse to its starting point and press `Ctrl+Shift+F2`: the cursors turn coloured, follow the mouse and replicate your clicks.
4. Press `Ctrl+Shift+F2` again to pause clicks: the cursors turn grey and keep following the mouse.
5. Press `Ctrl+Shift+F8` to stop the cursors (they turn white) and move only the mouse; press it again to re-attach them at the new distances.

| Default shortcut | Command | Effect |
|---|---|---|
| `Ctrl+Shift+F1` | Add | Adds a cursor at the mouse position (the cursors stop) |
| `Ctrl+Shift+F7` | Remove | Removes the cursor closest to the mouse |
| `Ctrl+Shift+F3` | Clear | Removes all cursors |
| `Ctrl+Shift+F2` | Toggle | Pauses / resumes clicks; the cursors keep following the mouse. If they are stopped, re-attaches them with clicks on |
| `Ctrl+Shift+F8` | Detach | Stops the cursors so you can move only the mouse; again to re-attach them |
| `Ctrl+Shift+F5` | Save | Saves the current layout |
| `Ctrl+Shift+F6` | Load | Recreates the saved layout around the mouse at the same distances; clicks stay on or paused as before |
| `Ctrl+Shift+F4` | Quit | Closes the program |

The same list, with the shortcuts actually configured, opens from the **Aiuto** (Help) entry in the notification-area icon menu, or by double-clicking the icon.

## Settings

On first start, `MultiCursor.ini` is created next to the executable. Restart the program after editing it.

| Key | Meaning |
|---|---|
| `Add`, `Remove`, `Clear`, `Toggle`, `Detach`, `Save`, `Load`, `Quit` | Shortcuts: `Ctrl`, `Alt`, `Shift`, `Win` plus a key (`A`, `F1`, `D1`, `NumPad1`, `Space`, ...) |
| `Replicate` | What to replicate: `Left`, `Right`, `Middle`, `X1`, `X2`, `Wheel` |
| `HoldMs` | How long, in milliseconds, each point is held down (0 = instant) |
| `GapMs` | Pause, in milliseconds, between one point and the next (0 = none) |
| `Color` | Colour of the cursors when active (.NET colour name: `OrangeRed`, `Lime`, `Cyan`, ...) |

The saved layout is stored in `MultiCursor.layout`, also next to the executable. There is a single slot.

## Building

All you need is Windows with .NET Framework 4.x, already included in Windows 10 and 11.

```bat
build.cmd
```

This produces `MultiCursor.exe` in the same folder. The `MultiCursor.ico` icon can be regenerated with `tools\make-icon.ps1`.

## Limitations

- **Dragging:** while active, every press becomes an instant click, so you cannot drag. Pause clicks to do so.
- **Games:** some games do not register zero-length clicks: try `HoldMs=20`. Exclusive-fullscreen games do not show the extra cursors.
- **Wheel:** there is a minimum 1 ms pause between points, otherwise Windows merges the notches into one. If some notches still get merged, increase `GapMs`.
- **Administrator windows:** to act on programs started as administrator, MultiCursor must be started as administrator too.
- **Monitors with different scaling:** the program has only been tested on a single monitor.

## Warnings

- **Antivirus:** the executable is unsigned, hooks the mouse and generates synthetic clicks. Some antivirus products or SmartScreen may flag it. The source is a single file: read it and build it yourself.
- **Anti-cheat and terms of service:** in online games, synthetic input may be blocked or may break the game's rules. Use it at your own risk.

## License

[MIT](LICENSE)
