# FlatscreenATTMod

Open-source MelonLoader/TavernLib flatscreen mod for A Township Tale.

It adds desktop camera, keyboard movement, mouse look, hand emulation, menu interaction, quick server joining, and hand pose controls for self-hosted/TavernLib-based play.

## Controls

- `Caps Lock`: toggle control lock on/off
- `F7`: show/hide the control menu
- `F10`: show/hide debug overlay
- `WASD`: move
- `Shift`: faster movement
- `Mouse`: look while control lock is on
- `Space` / `Ctrl`: fly up/down in the main menu only
- `Q`: toggle left hand active
- `E`: toggle right hand active
- `Mouse wheel`: move toggled hand(s) forward/back
- Arrow keys: move toggled hand(s) left/right/up/down
- Left mouse: left hand grab
- Right mouse: right hand grab
- `6`: hold left hand near face
- `7`: hold right hand near face
- `T`: teleport
- `R`: raise view height
- `F`: lower view height
- `1`: lock/unlock left hand in place
- `2`: lock/unlock right hand in place
- `3`: lock/unlock both hands
- `4`: unlock both hands
- `Home`: reset hands

## Build

The project expects TavernLib dependency DLLs under `TavernLib-main\Dependencies`.

```powershell
.\build.ps1
```

The built mod is:

```text
FlatscreenATTMod\bin\Release\FlatscreenATTMod.dll
```

Place that DLL into the game's `Mods` folder.
