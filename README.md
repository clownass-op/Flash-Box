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
- `flash/char6.swf` — the avatar player, patched with RABCDAsm (native
  ground-rune `loadMisc`, cosmetic/name support, Dagger-type weapons
  attached CharPage-style directly to `weapon`/`weaponOff`).
  `flash/char6-orig.swf` is the untouched backup.

## Notes

- All game art (character/item/background SWFs) belongs to Artix
  Entertainment and is fetched from their servers at runtime (backgrounds
  are bundled for convenience). This project is for personal/educational
  use.
- `bin/`, `obj/` and debug logs are build/runtime artifacts and are not
  tracked (see `.gitignore`).
