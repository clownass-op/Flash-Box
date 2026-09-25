# FlashBox: additional bugs found

These turned up while tracing the reported render bugs through the app and
the decompiled game client. Sections 1 and 2 are **fixed** (details in
`PATCH_NOTES.md`, most with a headless test). Section 3 was reviewed and
left as-is on purpose. Section 4 is **still open**.

## 1. Found and fixed

| # | Bug | Where | Test |
|---|---|---|---|
| 1 | Hiding the helm left the character bald (back hair hidden, hair never re-shown) | `MainTimeline.hideHelm` | `helm_hide_shows_hair` |
| 2 | Long hair never showed its back half: `HairBack` looked up in the armor's domain | `AvatarMC.onHairLoadComplete` | `hair_backhair_from_hair_file` |
| 3 | Hair finishing after the helm drew over the helm and replaced the helm's back locks | `AvatarMC.onHairLoadComplete` | `helm_backhair_survives_late_hair` |
| 4 | Misc-tab Hide helm/cape/pet/weapon sent `"true"`/`"false"`; unticking never un-hid | `ui/index.html` `miscFlashCall` | `ui_hide_checkboxes_toggle` |
| 5 | Item files flipped the character's gender (female + `hair/M/...` turned male, armor failed) | `LinkageParser.ParseHair/ParseArmor` | `linkage_parser_prefers_gender`, `ui_vars_apply_colors_gender` |
| 6 | Cache keyed by bare file name: male/female hair and class armor overwrote each other | `Downloads.BaseOf`, `DownloadAllCore` | `downloads_cache_keeps_gender_paths` |
| 7 | Background color picker had no effect; the previous scene stayed | `FlashCore.LoadBackground` | `ui_background_color_clears_scene` |
| 8 | `closeUii` threw unless a ground-rune name had been set ("Show item names" broken) | `MainTimeline.closeUii` | `closeuii_before_misc_name` |
| 9 | Unescaped XML in ExternalInterface calls: names with `&`, `<`, `"` never arrived | `FlashCore.Call` | `flash_call_escapes_names` |
| 10 | `loadResize` reset a turned avatar to face right | `AvatarMC.loadResize` | `resize_keeps_facing` |
| 11 | Combat and spell emotes (Attack1-4, Cast*, Psychic1/2, ranged) froze on game-only `castSpellFX` hooks | `mcSkel` frame scripts | `emote_labels_all_play` |
| 12 | Capes without `Idle`/`Move` labels threw inside skeleton frame scripts | `mcSkel` frame scripts | covered by the emote tests |
| 13 | "Hide cape"/"Hide pet" undone when the load finished; un-hiding showed the template cape; `hidePet` threw with no pet | `AvatarMC`, `MainTimeline` | `ui_hide_checkboxes_toggle` |
| 14 | Overlapping loads (quick Cosmetics toggle) attached pieces from the wrong file | `AvatarMC` loaders | covered by the armor tests |
| 15 | `Inflate` did a single `Read`; the parser could see a truncated SWF | `LinkageParser.Inflate` | covered by fixture loads |
| 16 | `FlashCore.Ready` fired before `AppForm` subscribed: no `hostReady`, and the startup name-typing line never appeared | `FlashCore` / `AppForm.OnLoad` | observed in `fb-debug.log` |
| 17 | Interrupted downloads could leave a truncated SWF that the cache then reused forever | `Downloads.DownloadAsync` | none (manual) |
| 18 | CharPage linkages (`strWeaponLink` etc.) were ignored in favor of guessing from the SWF | `Downloads`, `FlashCore` | `live_charpage_roundtrip` |

## 2. Fixed in the second pass

| # | Bug | Fix | Test |
|---|---|---|---|
| 19 | A turned pet snapped back to facing right on `loadResizePet` | keeps the sign of `scaleX` | `pet_resize_keeps_facing` |
| 20 | Older skeleton: no Whip/Gun/Rifle attacks, `RangedAttack3`, `UnarmedAttack3`, horse/throne walks, `Card` | `build_char6.py` transplants the game's skeleton timeline | `emote_labels_all_play` (67 labels) |
| 21 | Back hand drawn in full color while the rest of the back arm is a black silhouette | game parity: back-hand armor piece blacked out | `armor_back_hand_silhouette` |
| 22 | Dead game-client code that threw if reached (`blurr`, `pushMove`, `onWalkClick`, `queueAnim`, `fClose`, `scale`, pet-walk and PvP pad code) | removed or pointed at FlashBox objects | build + full suite |
| 23 | Window froze while fetching a character (network ran on the UI thread) | background jobs polled by the page | `live_load_keeps_ui_responsive` |
| 24 | Failing player calls slept 150 ms on the UI thread and ran twice | retry only while the player isn't answering yet | full suite |
| 25 | WebView2 profile next to the exe: control panel failed from a read-only folder (Program Files) | profile in `%LOCALAPPDATA%\FlashBox\WebView2` | manual (startup) |
| 26 | Unused web-message IPC path duplicated the host-object path | removed | full suite |
| 27 | Emote list omitted `Use`, `Danceweapon`, `Useweapon` (and had no combat animations) | grouped Emotes / Combat list, 67 labels | `emote_labels_all_play` |
| 28 | Gear-list icons and the Misc checkboxes kept separate hide state | icons now flip the checkboxes (one state); Hide armor / Hide ground rune boxes added | `ui_gear_icons_sync_checkboxes` |
| 29 | Background / Emote chips squashed, text cut in half (screenshots 1-2) | grid rows no longer collapse | `ui_layout_no_clipping` |
| 30 | Background color was an empty-looking full-width bar (screenshot 1) | round swatch + hex + Reset | `ui_background_color_clears_scene` |
| 31 | Output folder row overflowed: Browse cut off, horizontal scrollbar (screenshot 3) | field and button stacked; drawer never scrolls sideways | `ui_layout_no_clipping` |
| 32 | Dye swatches rendered as flat rectangles (screenshot 4) | CSS specificity fix: round swatches | `ui_layout_no_clipping` |
| 33 | Name typing line placed over the whole window, covering the gear list (screenshot 4) | centered over the preview near its top, clear of the gear list | `loader_line_avoids_gear_list` |
| 34 | Typing line went behind the main window on any click; its placeholder vanished on focus | owned by the main window; native placeholder | manual (window capture) |

## 3. Reviewed, left as-is

- **Clicking in the preview walks the avatar.** Intended.
- **`Unsheath` force-shows the weapon.** Matches the game.
- **`hideArmor` hides the whole body.** Intended.
- **The cache never refreshes.** Delete the output folder to force a
  refetch.

## 4. Still open (low impact, not changed)

- **LZMA-compressed (`ZWS`) SWFs aren't parsed** for linkage. AQW item
  files are zlib (`CWS`), so this only affects hand-made drag-and-drop files.
- **Split weapons are detected by searching dropped SWFs for the text
  `weaponOff`.** A dropped weapon whose code merely mentions it would be
  split across both hands. CharPage loads use `strWeaponType` and are
  unaffected.
- **Old AS2 weapons with no SymbolClass** (e.g. `items/axes/axe05.swf`)
  attach as a raw movie in the main hand and can't be dyed.
- **`ParseFlashVars` doesn't turn `+` into a space.** CharPage values use
  raw spaces, so this hasn't been seen in practice.
