# FlashBox

Windows preview app for AdventureQuest Worlds characters. Type a character name and it loads the avatar with the real Flash renderer, plus backgrounds, emotes, dyes and outfits to play with.

## Requirements

- Windows 10/11
- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework) (to build)
- Flash Player ActiveX registered (for the avatar preview)
- WebView2 Runtime (for the control panel)

## Build

```
msbuild FlashBox.csproj /p:Configuration=Release /p:Platform=x86
```

The exe lands in `bin\FlashBox.exe`.

## Run

1. Launch `bin\FlashBox.exe`.
2. Type a character name and hit Load.
3. Use the side panel for backgrounds, emotes, names, colors and visibility. Drag any `.swf` onto the preview to try it on.

Item art belongs to Artix Entertainment and is fetched from their servers at runtime. Personal/educational use.
