# FlashBox

Windows preview app for AdventureQuest Worlds characters. Type a character
name, and the app fetches their CharPage, downloads the equipped item SWFs,
and renders the avatar with the real Flash renderer (`char6.swf`) next to a
WebView2 control panel.

## Requirements

- Windows 10/11 (x86 or x64)
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework)
  (Developer Pack, to build)
- Flash Player ActiveX (`ShockwaveFlash.ShockwaveFlash`) registered on the
  machine — the avatar will not render without it
- WebView2 Runtime (for the control panel)

## Build

Open `FlashBox.csproj` in Visual Studio (Release, x86) and build, or from a
developer prompt:

```bat
msbuild FlashBox.csproj /p:Configuration=Release
```

The exe lands in `bin\FlashBox.exe`. Dependencies resolve from `flash\`
(interop + JSON/zip libs) and `packages\` (WebView2, committed because this
old-style project references it directly with no `packages.config`).

## Run

1. Launch `bin\FlashBox.exe`.
2. Type a character name and hit Load.
3. Toggle slots, Cosmetics, backgrounds, emotes, names and colors from the
   side panel. Click the gear-list icons floating over the preview to
   hide/show individual pieces.

Tip: you can also drag any `.swf` file onto the Flash preview — Hair /
Helm / Armor / Cape / Weapon / Pet slot boxes appear over it, and the file
loads straight into that slot (never saved to disk). Box look and slots
live in `ui\dropboxes.json`, no rebuild needed. Split weapon sets are
auto-detected and split across both hands like CharPage. (Dragging onto
the side panel works too, via `ui\drop.js`.)

Item SWFs download on demand next to the exe; backgrounds ship in `flash\`
(drop another `.swf` in there and it appears in the BG tab).

## How it works

- `Program.cs` / `AppForm.cs` — WinForms shell: WebView2 UI (`ui/`),
  Flash host, gear HUD overlay, background/emote/name dispatch.
- `FlashCore.cs` — Flash ActiveX hosting, `load*` item calls, SWF linkage
  parsing, background library.
- `Downloads.cs` — CharPage fetch, FlashVars parsing, item downloads.
- `ui/index.html` — control panel (slots, cosmetics, BG grid, emotes,
  names, colors, log/monitor).
- `flash/char6.swf` — the avatar player (native ground-rune `loadMisc`,
  cosmetic/name support, Dagger-type weapons attached CharPage-style to
  `weapon`/`weaponOff`, game-parity armor/hair/helm/dye logic). It is built
  from `flash/char6-orig.swf` (the untouched backup) plus the ActionScript
  in `player-src/`; see "Rebuilding the player" below. `PATCH_NOTES.md`
  lists what changed; `BUGS.md` lists known open issues.
- `Headless.cs` — `--headless` render regression suite (below).

## Rebuilding the player

Edit `player-src/*.as` (`AvatarMC`, `mcSkel`, `character5_fla/MainTimeline`),
then:

```bat
python tools\build_char6.py
```

This needs Python 3 and the JPEXS FFDec CLI (`ffdec-cli.exe`, found in
Program Files or via `FFDEC_CLI`). Starting from a copy of `char6-orig.swf`,
the script:

1. Replaces the avatar skeleton's timeline with the live game's. It reads
   that timeline from `..\references\...\assets.swf` when present, else
   from the cached `player-src\game-skeleton.bin`. `--old-skeleton` skips
   this step.
2. Strips stray `head` placements (only the old skeleton has them).
3. Compiles the three classes.
4. Writes `flash\char6.swf`.

`player-src\mcSkel.as` is written for the game skeleton's frame numbers.
Rebuild the app afterwards so `bin\flash\` picks up the new player.

## Headless tests

```bat
bin\FlashBox.exe --headless
```

This runs the real app (WebView2 page + Flash ActiveX + `char6.swf`) in an
offscreen window with no overlays or dialogs. It loads real item SWFs, then
checks the rig through the player's `getAvatarState` callback: armor pieces
per body slot, the walking front foot, dye transforms against the game's
formula, every panel emote, hair/helm/back-hair rules, and the page's
toggles, colors and gender handling. Results go to `bin\test-results\`
(`report.md`, `report.json`, `snapshots\*.png`). The exit code is 0 when
everything passes, 1 on failures, and 2 when the run could not start.

Options: `--out <dir>`, `--fixtures <dir>` (item SWF cache, default
`bin\test-fixtures`, downloaded from the game CDN on first use), `--player
<swf>`, `--only <name,...>`, `--offline` (skips the live CharPage test;
fixtures must already be cached) and `--timeout <sec>`, and `--char <name>` (also renders that live character
to `snapshots\char_<name>.png`).

## Notes

- All game art (character/item/background SWFs) belongs to Artix
  Entertainment and is fetched from their servers at runtime (backgrounds
  are bundled for convenience). This project is for personal/educational
  use.
- `bin/`, `obj/` and debug logs are build/runtime artifacts and are not
  tracked (see `.gitignore`).
