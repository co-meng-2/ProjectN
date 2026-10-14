# vampire-survivor-ai

Top-down 2D vampire-survivor-like prototype in Godot 4.7 (GDScript) on top of GodotGAS.
Design: `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`. Plans: `docs/superpowers/plans/`.

## Commands
- Run all tests: `tools/run_tests.sh` (`FILTER=<path substring>` for one file; `IMPORT=1` if the editor is closed;
  `TEST_SUITE_TIMEOUT=<seconds>`, default 120). Tests live in `tests/` mirroring `src/`, plus `support/` helpers;
  a test file extends `TestCase` and is picked up by the runner `tests/run_tests.tscn`.
- Rebuild gameplay tags after editing `TAGS` in `tools/sync_tags.gd`:
  `Godot --headless --path . -s res://tools/sync_tags.gd`, then rescan the editor filesystem
  (godot-ai `filesystem_manage` scan) before running tests.

## Architecture rules
- Layers (a file may only use layers below it): `core → defs → gas → components → actors → stage/abilities/boss → ui → app/meta`
  (`stage`, `abilities`, `boss` are siblings; `events/` extends `stage/`).
  Upward communication is signals only. `Stage` is the composition root and wires everything.
  `stage/` never imports `events/` scripts (it instantiates event scenes from defs); only `Stage` may reference
  `abilities/` and `boss/` (`BossBrain.attach_to` / `validate_def`).
  It does not know the HUD: `Main` (`src/app/main.gd`) instantiates Stage and Hud, calls
  `Hud.bind(player_health, building_health, wallet, clock, timeline, event_times)` and connects `Stage.events`
  (`event_started`/`event_progressed`/`event_ended`) and `Stage.boss_appeared` to
  `Hud.show_event`/`show_event_progress`/`show_event_result`/`show_boss`.
- Components never find their neighbours: actors inject dependencies via `bind(...)`.
- All content is `.tres` in `data/`, typed by `src/defs/`; no balance numbers in code.
- Damage always flows through `EffectBuilder.damage` -> GAS `GameplayEffect`. Never edit `addons/` (vendored plugins).
- `addons/GodotGAS` and `addons/godot_ai` are real copies committed to git, not symlinks, so the plugin versions are
  version-controlled with the project. To upgrade one, replace the whole folder and commit it as its own change.
- Every actor has an `AbilitySystemComponent` child named exactly that (instance `src/gas/actor_asc.tscn`);
  apply stats only after the node is in the tree; call `asc.cleanup()` before freeing, deferred (`_asc.cleanup.call_deferred()` as in `enemy.gd`/`player.gd`,
  since death happens inside physics callbacks). The plugin's ASC also cleans itself up on `NOTIFICATION_PREDELETE`.
- Add physics objects from physics callbacks with `call_deferred`.
- Abilities: `src/abilities/` holds the shop (`Loadout`/`AbilitySlot`, `AbilityPool`, `GradeService`) and the
  `AbilityInstaller` that mirrors slots onto the player's ASC (passives = INFINITE effects held by handle,
  attacks = `ActorAbility` scenes granted through `Player.grant_ability`). Grade maps to GAS `ability_level`
  (COMMON 1 ... REVEALED 6); every ability number is a `Curve` sampled at that level. `Stage` exposes `loadout`
  and `grade_service`; `Main` binds `Hud.ability_bar` and routes its `promote_requested`/`reroll_requested`
  signals - the HUD never mutates models. `Stage` sends `Event.Kill` (payload: the enemy) to the player's ASC
  on every enemy death; on-kill abilities use `trigger_event_tag`. `Stage.start` validates the profile and
  `AbilityInstaller.validate_defs` (every `ability_scene` root must be an `ActorAbility`).
- Gameplay tag names must survive the GAS registry, which lowercases segment tails: use `Ability.Sweep`, not
  camel-case segments like `Ability.BladeSweep`.
- Attack cooldowns live in `PeriodicAbility` (a curve + `step(delta)`), not in GAS cooldown effects; a
  promoted/rerolled attack starts with a fresh cooldown.
- Where GAS belongs: an *ability* is an action an actor performs (triggered by its controller: input, AI brain,
  gameplay event; constrained by tags/cooldowns); an *effect* is a state change applied to an actor; *stage code*
  is progression (waves, events, boss timing). So boss patterns are `BossPattern` (`ActorAbility`) scenes owning
  `State.Casting`, activated by tag from a `BossBrain`; events are `StageEvent` controllers that place GAS
  actors/effects (`Statue`, `AreaStrike`) and watch them.
- Events: `EventDirector` rolls `StageDef.events` (`EventSchedule`) once from `RunRng` on a 1-second grid and runs one
  `StageEvent` at a time under `World/Events`; success credits the `RewardDef` (gold, stone, tickets) in `Stage`.
  `StageClock` keeps ticking after `ended` (overtime); with a `StageDef.boss`, `BossDirector` switches to BOSS,
  aborts a running event and spawns the boss, whose death clears the stage. `EventSchedule.validate_against` requires
  `window_end + longest time_limit <= duration` (stage_01: window 20-155 s).
- A held promotion ticket pays any applicable shop action (`GradeService` prices it as `Price(TICKET, 1)` first).
- Bad scene roots fail at `Stage.start`: `EventDirector.validate_defs` (StageEvent), `BossDirector.validate_def`
  (Boss), and each controller/pattern `validate()` checks the root type of scenes it instantiates.
- `ActorBody` (components) holds the shape sizing / contact wiring / death sequence shared by `Enemy` and `Boss`.
- Phase 4a meta flow: autoloads `SceneRouter` (`src/app/`) and `SaveService` (`src/meta/`), neither with a `class_name`.
  Screens (`Main`, `MainMenu`, `SettingsScreen`, `Logo`) take `router`/`save_service` as optional properties that
  default to the autoloads in `_ready`; tests inject `tests/support/scene_router_probe.gd` and a `SaveService` with a
  temp `path`. `Stage` never sees the autoloads. Routes live only in `SceneRouter.ROUTES`; one-shot data goes through
  `go(route, params)` / `take_params()` with keys defined on the router (`PARAM_STAGE_DEF`).
- Save: JSON at `user://save.json` (`SaveData` -> `SaveStore`). To change the schema bump `SaveMigrations.CURRENT_VERSION`
  and append one step to `SaveMigrations.steps()`; add a migration test. Unreadable files are moved to `save.json.bak`.
  `Main` sets `RunResult.gem` from `StageDef.gem_for(outcome)` and credits it once per run; gem is the only persistent currency.
- Phase 4b hub: `Start` goes to `HUB`; `Hub` (`src/meta/hub/`) lists `data/stages/stage_catalog.tres` (an ordered `StageCatalog`;
  order is unlock order) and sends `go(STAGE, {PARAM_STAGE_DEF: stage})`. Unlock is derived (`StageProgress`: first stage
  open, otherwise the previous stage must be in `SaveData.cleared_stages`; a cleared stage stays playable) - never store an
  unlocked list. `Main` calls `SaveService.record_clear(stage_def.id)` for a CLEARED run next to the gem credit and saves
  once; Enter on the result screen returns to the hub. Save schema is v2; ids in `cleared_stages` that are not in the
  catalog are kept, not dropped.

## Adding an enemy (data only)
1. Duplicate `data/enemies/orc.tres`; change `id`, stats, tint/scale, contact values, `target_policy`.
   `visual` and `drops` point at the orc's shared `.tres` files unless you point them at new ones.
2. Add it to `StageDef.trickle_enemies` or a `WaveEntry`; `StageDef.validate()` then validates it (checked by
   `test_stage_01_validates`). `tests/data/test_game_data.gd` uses hard-coded lists, so also add the file to `DATA_FILES`
   and to the list in `test_enemy_defs_use_the_generic_enemy_scene` if it should be covered when no stage references it.

## Adding an event (scene + data)
1. A scene whose root script extends `StageEvent` (`src/events/`): numbers as exported values, `validate()` on the
   root, placed objects as its children, `objectives()` for HUD arrows, `report_progress(text)`, `_succeed()`/`_fail()`;
   override `_on_time_up()` if surviving is the goal.
2. Add an `EventDef` in `data/events/` (id, name, `controller_scene`, `time_limit`, `RewardDef`) and put it in the
   `EventSchedule.pool` (`data/events/stage_01_events.tres`); `min_gap` must cover the longest time limit.
   Add the file to `DATA_FILES` in `tests/data/test_game_data.gd`.

## Adding a boss pattern
1. A scene whose root extends `BossPattern` (`src/boss/patterns/`): a unique `Ability.Boss.<Word>` tag (add it to
   `TAGS` in `tools/sync_tags.gd`), `activation_owned_tags = [State.Casting]`, exported numbers,
   `_begin()`/`_advance()`/`_complete()`/`_abort()`, damage through `AreaStrike`/`Projectile` (boss ASC as source).
2. Add a `BossPatternDef` (scene + weight) to the boss's `patterns` in `data/bosses/`.

## Adding an ability (data only)
1. Duplicate a file in `data/abilities/` (`vitality.tres` for a passive, `volley.tres` for an attack): set `id`,
   names, `icon` (and `revealed_icon` or rely on the shine tint), `min_grade`. A passive is an INFINITE
   `GameplayEffect` whose modifiers use `scaling_curve` (6 points, x = grade level 1..6) with `magnitude = 1.0`
   (GAS multiplies the curve by it - 0 zeroes every grade; `validate()` catches it). An attack points
   `ability_scene` at a scene whose root extends `ActorAbility` (usually `PeriodicAbility`) with its numbers as
   exported curves; the root may implement `validate()`.
2. Add it to `PlayerProfile.abilities` in `data/characters/default_profile.tres`; `Stage.start` validates it.
   Add the file to `DATA_FILES` in `tests/data/test_game_data.gd` and, for a passive, to `PASSIVES` in
   `tests/data/test_ability_data.gd`.
3. Shop prices live in `data/grade_table.tres`; the run's start gold/stone and `drop_multiplier` in the
   `StageDef` (`stage_01.tres` uses 10000 for prototyping - set it to 1 for balanced play).
4. Icons in `assets/art/icons/` are currently generated placeholders (see `assets/SOURCES.md`).

## Adding a stage (data only)
1. Duplicate `data/stages/stage_02.tres` (drop the header `uid` so it does not clash); change `id`, `display_name`, stats,
   `gem_reward`, and point `waves`/`events`/`boss` at new or shared `.tres` files. `StageDef.validate()` must pass.
2. Append it to `data/stages/stage_catalog.tres` (`stages` order = unlock order) and add the file to `DATA_FILES` in
   `tests/data/test_game_data.gd`; update the catalog order assertion in the same file.

## Running the game
- Main scene: `src/app/logo.tscn` (logo -> main menu -> `src/app/main.tscn` as the stage screen). `application/run/main_scene`
  in `project.godot` was hand-edited (the godot-ai tool refuses to set it); re-set it in Project Settings if it goes missing.
  After a run ends Enter returns to the hub; R still restarts (dev shortcut - a restarted clear pays gem again).
