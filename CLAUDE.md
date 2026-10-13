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
- Layers (a file may only use layers below it): `core → defs → gas → components → actors → stage/abilities/boss → ui → app/meta`.
  Upward communication is signals only. `Stage` is the composition root and wires everything.
- Components never find their neighbours: actors inject dependencies via `bind(...)`.
- All content is `.tres` in `data/`, typed by `src/defs/`; no balance numbers in code.
- Damage always flows through `EffectBuilder.damage` -> GAS `GameplayEffect`. Never edit `addons/` (symlinked shared plugins).
- Every actor has an `AbilitySystemComponent` child named exactly that (instance `src/gas/actor_asc.tscn`);
  apply stats only after the node is in the tree; call `asc.cleanup()` before freeing.
- Add physics objects from physics callbacks with `call_deferred`.

## Adding an enemy (data only)
1. Duplicate `data/enemies/orc.tres`; change `id`, stats, tint/scale, contact values, `target_policy`.
2. Add it to `StageDef.trickle_enemies` or a `WaveEntry`. `tests/data/test_game_data.gd` validates it.
