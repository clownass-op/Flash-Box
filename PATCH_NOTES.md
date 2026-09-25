# FlashBox patch notes: render fixes

Each fix below was reproduced first with the new headless test suite, then
checked against the decompiled game client (`references/AQW decompiled
game/Game3098r27`: `scripts/AvatarMC.as`, `scripts/mcSkel.as`,
`scripts/Game.as`, and the `mcSkel` sprite inside `scripts/_assets/assets.swf`).

Headless suite results (`FlashBox.exe --headless`). "Old player" is the
original player with only the diagnostics callbacks added, so the tests can
inspect it.

| App (C# + page) | Player | Result |
|---|---|---|
| old | old | 3 passed / 21 failed (first run; two tests reworked since) |
| new | old | 7 passed / 17 failed (every remaining failure is a player bug) |
| **new** | **new** | **24 passed / 0 failed** |

## Reported bugs

### Wrong armor layering
- **The front foot never received the armor.** The game loads the `Foot`
  symbol into both `frontfoot` and `backfoot`. char6 only loaded
  `backfoot`, so the template foot showed through.
- **Robe pieces from the previous armor stayed on screen.** When an armor
  had no `Robe`/`RobeBack`, the old piece stayed visible. Switching
  ccsephA → BeleenSXY kept ccsephA's back robe, and PalidanRevamp →
  Warrior2a kept both robe pieces. Pieces the new armor lacks are now
  hidden, as in the game.
- **One missing symbol aborted the rest of the armor.** Any missing piece
  threw and left old and new armor pieces mixed together. Each piece now
  loads on its own, following the game's `loadArmorPiecesFromDomain`.
- **Overlapping loads attached the wrong pieces.** Quickly toggling
  Cosmetics could finish an older load after a newer one was issued; the old
  load then read the newer load's domain and linkage. Each slot now keeps
  only its latest loader and ignores stale completions (armor, hair, helm,
  cape, weapon, pet, ground rune).
- **Armor files that only ship the other gender's symbols now render**
  instead of leaving the template body.

### Right foot walk not loading
- The front (right) foot during `Walk` is the `frontfoot` clip, which never
  got the armor. See above; it now shows `<armor><gender>Foot`.
- **With the armor hidden, walking showed the foot again.**
  `showFrontFoot()` ignored `hideFeet`. The foot helpers now respect it, and
  un-hiding restores whichever foot the current animation wants.

### Color channels wrong
- **CharPage dye colors were never applied.** `intColorHair/Skin/Eye/Trim/
  Base/Accessory` from the character page were ignored, so every character
  used the panel's defaults. Loading a character now applies all six
  channels and updates the color wheel.
- **Dark shades had the wrong red channel.** The game subtracts 50 red for
  `Dark` on every location except Skin (25). char6 always used 25, so dark
  hair, trim, base and accessory shades came out too red (Hair/Dark on
  `#336699`: red 26 instead of 1).

### Emotes not properly animated
- **Rest and Unsheath broke the head.** The old skeleton places a second,
  blank instance named `head` for one frame in these emotes. That rebinds
  `mcChar.head` to a throwaway template head, and the real head (face, hair,
  helm) was orphaned. Every later helm/hair/hide call then went to a detached
  clip. The game's skeleton has no such placement, so `tools/build_char6.py`
  strips it from the timeline.
- **Attack1-4, Cast1-3, Psychic1/2, ranged and other combat emotes froze.**
  Their frame scripts call the game's `castSpellFX` and projectile code on
  `parent.parent`, which is the Stage in FlashBox. The call threw and the
  animation stopped. These game-only hooks are now guarded.
- **Stern played a different number of loops depending on the previous
  emote.** SternLoop counts `animLoop`, but nothing reset it, so after Laugh
  it played once instead of three times. The loop counter now resets on
  Stern and on every `loadEmote`.
- **Capes without `Idle`/`Move` labels threw inside frame scripts.** That
  skipped the rest of the script (for example Idle's `stop()`). Cape
  animation calls are now safe.
- **`loadEmote` ignores unknown labels** instead of throwing, and stops a
  click-walk in progress so the walk tween can't yank the rig back to
  Walk/Idle mid-emote.

## Other fixes found along the way
- **Hiding the helm left a bald head.** `hideHelm` also hid the back hair
  and never showed the hair. Helm, hair and back hair are now decided in one
  place, following the game's `setHelmVisibility`: a shown helm displays its
  own `_backhair` or none; otherwise the hair and its HairBack show. The
  C#-side "reload the helm to unhide" workaround is gone.
- **Long hair never showed its back half.** `HairBack` was looked up in the
  armor's domain instead of the hair's.
- **A hair that finished loading after the helm drew over the helm.**
  Download order no longer matters.
- **The Hide helm/cape/pet/weapon toggles could never un-hide.** They sent
  `"true"`/`"false"`, but the player compares against `"True"`. The page now
  sends the capitalized form, and the player accepts any case.
- **A pending "Hide cape" or "Hide pet" was undone by the load
  finishing.** Hide state now survives loads, and un-hiding before any cape
  loaded no longer reveals the template cape. `hidePet` also no longer
  throws when there is no pet.
- **Gender came from item files instead of the character.** The parser
  called `setGender` from whatever a hair or armor file exported first. A
  female character wearing a hair from `hair/M/` (Warlic) turned male and
  lost her armor. Gender now comes from `strGender`, and parsing only
  prefers that gender's symbols. When a hair ships only for the other
  gender, it now renders rather than leaving a bald head. This deliberately
  differs from the live game, which shows no hair in that case.
- **Male and female files overwrote each other in the cache.** Downloads
  were cached by bare file name, so `hair/M/Bob.swf` and `hair/F/Bob.swf`
  (or `classes/M|F/x.swf`) shared one file and the next character reused
  the wrong gender's art. The cache now mirrors the game's folder layout,
  path segments are sanitized, and downloads are written to a `.part` file
  and moved into place so a broken download can't poison the cache.
- **CharPage linkages (`strWeaponLink`, `strHelmLink`, …) are now used**
  when the SWF really exports them. Guessing from the SWF is the fallback.
- **The background color picker did nothing.** It only tinted an HTML box
  hidden behind the Flash window, and the previous scene stayed up. Picking
  a color now clears the scene and fills that color. Background swaps also
  unload the previous scene.
- **Resizing flipped a turned avatar back to facing right.** `loadResize`
  now keeps the facing direction. New **Flip facing** button in the Size
  panel.
- **`closeUii` threw until a ground-rune name had been set.** The Names
  panel's "Show item names" toggle failed on most characters.
- **Names containing `&`, `<` or quotes never reached the player.**
  ExternalInterface arguments weren't XML-escaped, so a name like "Shield &
  Blade" made the whole call malformed.
- **Linkage parsing could read a truncated SWF.** A single
  `InflaterInputStream.Read` may return fewer bytes than requested, and the
  zeroed tail usually held the SymbolClass tag. It now loops until done.
- **`OnFlashReady` never ran.** FlashCore raised `Ready` inside its
  constructor, before AppForm subscribed. As a result, `hostReady` was never
  sent and the centered name-typing line never appeared on startup. It now
  does.

## Second pass

Headless suite: **32 passed / 0 failed** (8 new tests).

### Player
- **Game skeleton.** `char6.swf` now runs the live game's avatar
  skeleton timeline (1888 frames), transplanted by `tools/build_char6.py`.
  New animations: WhipAttack, GunAttack/GunAttack2, RifleAttack/RifleAttack2,
  RangedAttack3, UnarmedAttack3, horseWalk, throneWalk and Card. The
  body-part containers are char6's own, so the art-loading code is
  unchanged; snapshots before and after are pixel-identical for existing
  animations.
- **Back hand matches the rest of the back arm.** The skeleton draws the
  far shoulder, thigh, shin and foot as black silhouettes; the back hand
  was the one piece left in full color. The game blacks it out in code, and
  FlashBox now does too.
- **A turned pet keeps facing its way when resized.**
- **Dead game-client code removed or fixed** (it threw whenever reached):
  `blurr` now blurs the background, and `pushMove` uses the avatar.
  `onWalkClick`, `queueAnim`, `fClose`, `scale`, the pet-walk copy and the
  map-event/PvP pad code are gone.

### App
- **The window no longer freezes while a character loads.** The CharPage
  fetch and item downloads run as background jobs that the page polls; the
  UI thread's longest stall during a load is now about 50 ms.
- **A player callback that throws is no longer retried.** The retry is
  kept for its real purpose (player not up yet), which avoids a 150 ms
  UI-thread sleep and running the call twice.
- **The WebView2 profile moved to `%LOCALAPPDATA%\FlashBox\WebView2`**, so
  the control panel works from a read-only install folder.
- **Removed the unused web-message IPC path** (a second copy of character
  load/download that the page never called).

### UI
- **Background and emote chips no longer have their text cut in half.**
- **Background color is a round swatch with its hex value and Reset**, and
  it actually changes the preview.
- **Misc > Output folder fits the drawer.** The Browse button is no longer
  cut off, and the drawer never scrolls sideways.
- **Dye color swatches are round again.** A generic rule was squashing
  them into bars.
- **The title bar is dark like the rest of the UI.** Misc > Window > **Gray
  title bar** brings back the old look and is remembered.
- **Log tab: new Copy button** puts the whole debug log on the clipboard.
- **Emotes panel lists all 67 animations**, grouped as Emotes and Combat
  (adds Use, Danceweapon, Useweapon, Card and the attack/cast set).
- **The gear-list icons and the Misc toggles are one state:** clicking an
  icon ticks the matching box. New **Hide armor** and **Hide ground rune**
  toggles.
- **The name typing line sits at the top of the preview**, clear of the
  gear list (it used to be centered on the whole window and covered it). It
  stays above the window when you click the app, and its "character
  name..." placeholder stays visible until you type.

## Third pass: window and UI

Headless suite: **36 passed / 0 failed** (4 new tests). Window behavior was
also checked in the real app with a scripted mouse, since the offscreen
headless window can't be resized or maximized meaningfully.

- **The window can be resized from every edge and corner again.** The
  page covers the whole window, so Windows never saw the mouse at the edges.
  Invisible edge handles in the page now start the native resize, and the
  preview leaves a 5 px strip at the right and bottom edges while the window
  isn't maximized (the Flash window would otherwise sit on top of the
  handles). Verified: each edge and a corner resize by exactly the dragged
  amount.
- **Maximizing no longer cuts off the UI.** A frameless maximized window is
  placed overhanging the screen by the (invisible) frame width, and the
  old maximize override used the wrong coordinates on secondary monitors.
  The client area is now clamped to the monitor's work area. Verified:
  1920×1040 client on a 1920×1040 work area.
- **The dye color picker no longer closes after about 2 seconds.** The
  watchdog re-stacked the floating buttons with `BringToFront`, which
  activates them and closes any open page popup. It now uses
  `SWP_NOACTIVATE`. Verified: with the old code the picker is gone by 6 s,
  with the fix it stays open.
- **Dye swatches are smooth.** They're now CSS circles with the real color
  input laid invisibly on top (the native swatch drew jagged edges).
- **No white square in the Monitor/Log scrollbars.** The scrollbar corner
  is styled, and the Monitor no longer scrolls sideways; it lists
  downloaded files by their short path instead of full paths.
- **"Empty" background:** the first chip in the BG tab removes the scene
  and leaves the plain background color. The chosen background color is
  remembered.
- **Names tab:**
  - It shows and edits the names of the active outfit, including
    cosmetics. A new **Cosmetic outfit** switch (the same as the shirt
    button) flips between the two sets.
  - Edits update the gear list live.
  - **Show item names** now shows or hides the names in the gear list
    instead of the player's old built-in labels, which duplicated and
    overlapped it.
  - **Display over avatar** shows the character name as a tag above the
    avatar's head (it used to sit in the corner on top of the gear list).
- **Hide interface** hides the gear list, the magnifier and shirt
  buttons, and the name tag, for clean screenshots.
- **Title bar option:** "Title bar uses background color" (replaces "Gray
  title bar") gives the title bar the chosen background color, switching to
  dark buttons on pale colors.

## Tooling
- **`FlashBox.exe --headless`** runs the real app (WebView2 page, Flash
  ActiveX and `char6.swf`) in an offscreen window with no overlays or
  dialogs. It runs 24 render regression tests and writes
  `test-results\report.md`, `report.json` and PNG snapshots. The exit code
  is 0 when all pass, 1 on failures, and 2 when the run could not start. See
  the README for options.
- **`tools/build_char6.py`** rebuilds `flash/char6.swf` reproducibly from
  `flash/char6-orig.swf` plus `player-src/*.as`. It applies the skeleton
  timeline fix, then compiles the scripts with JPEXS FFDec.
- **New player callbacks:** `getAvatarState` (JSON rig snapshot used by the
  tests), `isReady` (the page already polled for it), `getBootError`,
  `setFacing`, `setBackgroundColor` and `clearBackground`.
- The recompiled `AvatarMC` constructor now sets its fields after `super()`.
  FFDec's compiler otherwise emits them before the base constructor, which
  throws and leaves the rig unbound.
