# Phase 4a — Scene flow, save foundation, main menu, settings

Date: 2026-10-14. Parent design: `2026-10-13-vampire-survivor-base-design.md` (Phase 4 — Meta).
Phase 4 is split into 4a (this spec) → 4b (hub + stage select/unlock) → 4c (gem skill tree). Each gets its own spec, plan and implementation.

## Intent

The deliverable is a clean base: a scene-routing and persistence structure that survives more stages and more meta features.
UI stays minimal. 4a is done when a player can start from the main menu, clear `stage_01`, earn gem that is saved,
relaunch the game and still have it, and when the save format is versioned so later schema changes migrate cleanly.

Constraints carried from the base design: the only new autoloads are `SceneRouter` and `SaveService`; `app/` and `meta/`
are the only layers that may touch autoloads; `Stage` and everything below it stay unaware of them; gem is the only
persistent currency; no balance numbers in code.

## Out of scope (4a)

Hub and stage select (4b), skill tree (4c), multiple save slots, cloud sync, transition effects, key rebinding,
resolution/graphics settings. Settings start with a single master-volume value; new fields are added later through a migration.

## Modules

| Module | Layer | Responsibility | Interface | Depends on |
|---|---|---|---|---|
| `app/SceneRouter` (autoload) | app | Route id → scene path table; scene change; one-shot parameter handoff | `go(route, params := {})`, `take_params() -> Dictionary` (clears on read) | — |
| `meta/SaveData` | meta | Model of everything persisted: `version`, `gem`, `settings` (`master_volume`) | `to_dict()`, `static from_dict(d)` | — |
| `meta/SaveMigrations` | meta | Applies version N → N+1 steps in order | `static migrate(d) -> Dictionary` | — |
| `meta/SaveStore` | meta | File IO as a plain class: atomic write (`.tmp` then rename), corrupt-file recovery | `load_or_default(path) -> SaveData`, `write(path, data) -> bool` | `SaveData`, `SaveMigrations` |
| `meta/SaveService` (autoload) | meta | Holds the live `SaveData` for the session | `data`, `add_gem(n)`, `save()`, `changed` signal | `SaveStore` |
| `app/AudioSettings` | app | Applies settings to the audio bus | `apply(settings)` | — |
| `meta/menu/MainMenu`, `SettingsScreen` | meta | Start / Settings / Quit; volume slider. They request changes from `SaveService`/`SceneRouter`; they never own state | — | autoloads |

`SaveStore` is separate from the autoload so file handling is unit-testable with temp paths; the autoload is wiring only.

### Routes (4a)
`LOGO`, `MAIN_MENU`, `SETTINGS`, `STAGE`. 4b adds `HUB` and `STAGE_SELECT`. The route table is the only place that knows scene paths.

### Changes to existing code
- `app/main.gd` becomes the stage screen. It takes the `StageDef` from `SceneRouter.take_params()` (default `stage_01` when empty, so the scene runs standalone with F6).
- `project.godot` `application/run/main_scene` points to the logo scene (hand-edited, as before; see CLAUDE.md).
- `RunResult` gains `gem: int`. `StageDef` gains `gem_reward: int` (data; `stage_01.tres` sets it). `StageDef.validate()` requires `gem_reward >= 0`.
- `Stage` is unchanged. R-restart stays as a dev shortcut.

## Data flow

1. **Boot.** `SaveService._ready()` loads `user://save.json` via `SaveStore`, then `AudioSettings.apply`. The logo scene shows briefly and calls `go(MAIN_MENU)`.
2. **Main menu.** Start → `go(STAGE, {stage_def})` (`stage_01` fixed in 4a; 4b replaces this with stage select). Settings → `go(SETTINGS)`. Quit → `get_tree().quit()`.
3. **Settings.** Moving the slider updates `SaveService.data.settings` and calls `AudioSettings.apply` immediately. Leaving the screen calls `save()` once. Volume is clamped to 0..1.
4. **Stage.** `Main` starts the stage from the handed-off `StageDef`. On `finished(result)`, `Main` sets `result.gem` (`StageDef.gem_reward` on CLEARED, 0 on FAILED), calls `SaveService.add_gem(result.gem)` then `save()` — exactly once per run. The result panel's Continue returns to `MAIN_MENU`.

## Save format

JSON at `user://save.json`, `{"version": 1, "gem": 0, "settings": {"master_volume": 1.0}}`.
JSON rather than `.tres` because loading a user-writable `.tres` can execute embedded scripts, and dict-based migrations are simple.
`from_dict` fills missing fields with defaults and rejects wrong types (treated as corruption).
Each schema change adds one migration step and bumps the current version; 4b's first field (stage clears) will be v2.

## Error handling

- No save file: start from defaults, no error.
- Unparseable JSON, wrong field types, or a version newer than the build: move the original to `save.json.bak`, start from defaults, `push_warning`. The player's file is never overwritten silently.
- Write failure: `push_error`, the session continues. Writes go to `save.json.tmp` and are renamed over the target, so a crash mid-write cannot leave a half-written save.
- Unknown route or failed scene load: `push_error`, stay on the current screen.
- Empty `take_params()`: the stage screen falls back to `stage_01`.

## Testing

Tests mirror `src/` under `tests/`, extending `TestCase`.
- `SaveMigrations`: old → latest, latest unchanged, future version rejected.
- `SaveData`: `to_dict`/`from_dict` round trip, defaults for missing fields, wrong types rejected, volume clamped.
- `SaveStore` (temp dir): missing, valid, corrupt (creates `.bak`), atomic write leaves no `.tmp`.
- `SceneRouter`: every route's scene path loads; `take_params()` returns a value once.
- Data: `stage_01.gem_reward > 0` and the stage validates; gem rule (CLEARED pays, FAILED pays 0).
- Smoke: logo → main menu → stage loads; a finished run credits gem exactly once. Follows the existing shipped-stage smoke test pattern.
