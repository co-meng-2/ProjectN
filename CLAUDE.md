# vampire-survivor-ai

Top-down 2D vampire-survivor-like prototype in Godot 4.7 (GDScript) on top of GodotGAS.
Design: `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`. Plans: `docs/superpowers/plans/`.

## Commands
- Run all tests: `tools/run_tests.sh` (`FILTER=<path substring>` for one file; `IMPORT=1` if the editor is closed;
  `TEST_SUITE_TIMEOUT=<seconds>`, default 120). Tests live in `tests/` mirroring `src/`, plus `support/` helpers;
  a test file extends `TestCase` and is picked up by the runner `tests/run_tests.tscn`.
- Rebuild gameplay tags after editing `TAGS` in `tools/sync_tags.gd`:
  `Godot --headless --path . -s res://tools/sync_tags.gd`.

## Architecture rules
- Layers (a file may only use layers below it): `core → defs → gas → components → actors → stage/abilities/boss → ui → app/meta`
  (`stage`, `abilities`, `boss` are siblings; `events/` extends `stage/`).
  Upward communication is signals only. `Stage` is the composition root and wires everything.
  It does not know the HUD: `Main` (`src/app/main.gd`) instantiates Stage and Hud and calls
  `Hud.bind(player_health, building_health, wallet, clock, timeline)`.
- Components never find their neighbours: actors inject dependencies via `bind(...)`.
- All content is `.tres` in `data/`, typed by `src/defs/`; no balance numbers in code.
- Damage always flows through `EffectBuilder.damage` -> GAS `GameplayEffect`. Never edit `addons/` (vendored plugins).
- `addons/GodotGAS` and `addons/godot_ai` are real copies committed to git, not symlinks, so the plugin versions are
  version-controlled with the project. To upgrade one, replace the whole folder and commit it as its own change.
- Every actor has an `AbilitySystemComponent` child named exactly that (instance `src/gas/actor_asc.tscn`);
  apply stats only after the node is in the tree; call `asc.cleanup()` before freeing, deferred (`_asc.cleanup.call_deferred()` as in `enemy.gd`/`player.gd`,
  since death happens inside physics callbacks). The plugin's ASC also cleans itself up on `NOTIFICATION_PREDELETE`.
- Add physics objects from physics callbacks with `call_deferred`.

## Adding an enemy (data only)
1. Duplicate `data/enemies/orc.tres`; change `id`, stats, tint/scale, contact values, `target_policy`.
   `visual` and `drops` point at the orc's shared `.tres` files unless you point them at new ones.
2. Add it to `StageDef.trickle_enemies` or a `WaveEntry`; `StageDef.validate()` then validates it (checked by
   `test_stage_01_validates`). `tests/data/test_game_data.gd` uses hard-coded lists, so also add the file to `DATA_FILES`
   and to the list in `test_enemy_defs_use_the_generic_enemy_scene` if it should be covered when no stage references it.

## Running the game
- Main scene: `src/app/main.tscn`. `application/run/main_scene` in `project.godot` was hand-edited (the godot-ai tool
  refuses to set it); re-set it in Project Settings if it goes missing. Press R to restart after a run ends.
