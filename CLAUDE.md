# vampire-survivor-ai

Top-down 2D vampire-survivor-like prototype in Godot 4.7 **.NET** (C#) on top of the Forge gameplay framework (`addons/forge`, 0.4.0).
Design: `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md` (the game) and
`docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (the C# + Forge architecture; the GDScript + GodotGAS tree it
replaced is gone since the cutover, see git history before `chore: cut over to the C# tree`). Plans: `docs/superpowers/plans/`.
Status and machine setup: `docs/superpowers/notes/2026-10-14-port-status-and-setup.md`.
This project is the owner's textbook for learning Godot: explain the *why* in doc comments and keep the spec's section 9 table current.

## Commands
- Run all tests: `tools/run_tests.sh` (`FILTER=<substring>` of a label like `Meta/HubTest::TestBack`; `IMPORT=1` if the editor is
  closed or files moved; `NO_BUILD=1` skips `dotnet build`; `TEST_SUITE_TIMEOUT=<seconds>`, default 120). It needs the **.NET**
  Godot build (`GODOT_BIN`, default `/d/CM/Godot/Godot_v4.7.2-stable_mono_win64/...`); open the project with it, not the standard
  build. Tests live in `tests/` mirroring `src/` (`tests/<Layer>/<Type>Test.cs`, plus `Support/`, `Data/`, `Architecture/`); a test
  class extends `TestCase` and has public `Test*` methods returning `void` or `Task`; runner `tests/run_tests.tscn`.
- Regenerate the Forge tags after editing `GameplayTags.All` (`src/Gas/GameplayTags.cs`):
  `Godot --headless --path . res://tools/sync_tags.tscn` (writes `addons/forge/core/forge_data.tres`, the only file under `addons/`
  that tools may write).
- Swarm benchmark: `Godot --path . res://tools/swarm_bench.tscn` (add `--headless` for the physics cost only).
- Commit the generated `.cs.uid` files (Godot creates them on import; run the tests with `IMPORT=1` first).

## Architecture rules
- One assembly (`VampireSurvivor.csproj`), namespace = folder (`VampireSurvivor.Stage`), nullable warnings are errors.
  C# files are PascalCase, scenes/resources snake_case.
- Layers (a file may only use layers below it): `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Events
  -> Ui -> App | Meta` (App and Meta may use each other). `tests/Architecture/LayerRulesTest` reads the `using` lines and enforces
  it; never write project namespaces fully qualified. Only `Stage/StageRoot.cs` may use `Abilities` and `Boss`; `Stage/` never uses
  `Events`. Upward communication is `[Signal]` (Node classes) or C# `event` (plain classes, non-Variant payloads such as `RunResult`).
- A class named like its namespace folder cannot be referenced from other namespaces (CS0118): hence `StageRoot`, `BossActor`,
  `HubScreen`.
- `StageRoot` (`src/Stage/stage.tscn`) is the composition root and wires everything. It does not know the HUD: `App/Main`
  (`src/App/main.tscn`, the stage screen) instantiates the stage and the `Hud`, calls `Hud.Bind(...)`, connects the event director
  and `BossAppeared` to the HUD, binds `Hud.AbilityBar` and routes its promote/reroll requests to `StageRoot.GradeService` while no
  result is shown - the HUD never mutates models.
- Components never find their neighbours: actors inject dependencies via `Bind(...)`/`Setup(...)`; in-scene references are
  `[Export]` node properties (`node_paths` in the `.tscn`), not string `GetNode` paths.
- All content is `.tres` in `data/`, typed by `src/Defs/` (`[GlobalClass]` Resources with `List<string> Validate()`); no balance
  numbers in code. Forge `EffectData`/`AbilityData` are built by code from Defs (`Gas/PassiveEffects`, `CooldownEffects`,
  `AbilityFactory`) and never saved. Hand-written `.tres` files load C#-typed arrays as `Array[ExtResource("<script>")]([...])`.
- `Gas/` is the only Forge adapter besides `Components/ActorEntity` (a `ForgeEntity`): `ApplyStats`, `FillHealth`,
  `ApplyPassive/RemovePassive`, `Grant/Revoke` (`AbilityGrant`), `TryActivate(tag)`, `Raise<T>(tag, payload)`, `Tick`, `Cleanup`,
  `AbilityEnded`. Stats are fixed-point integers inside Forge; `ApplyStats`/`Stats` are the only conversion points (compare
  `DisplayValue` in tests).
- Damage always flows through `Gas/DamageEffects.Hit(source, target, amount)` (returns whether it applied; `state.invulnerable`
  blocks it). `Components/AreaStrike` is the one area-hit path (sweeps, meteors, boss slams).
- Every actor owns an `ActorEntity` child (`[Export] Entity`); death cleans it up deferred (`ActorEntity.CleanupDeferred`, as in
  `ActorBody`), since death happens inside physics callbacks. Add physics objects from physics callbacks with `CallDeferred`.
- Never edit `addons/` (vendored `forge` and `godot_ai`, committed copies). To upgrade one, replace the whole folder and commit it
  as its own change; Forge upgrades touch only `Gas/` and `ActorEntity`.
- Tags: only through `GameplayTags` constants, lowercase dotted (`ability.boss.charge`, `state.casting`, `event.kill`).
  `StageRoot.Start` fails on an unregistered tag.
- Abilities: `Abilities/` holds the shop (`Loadout`/`AbilitySlot`, `AbilityPool`, `GradeService`) and the `AbilityInstaller` that
  mirrors slots onto the player's entity: passives are INFINITE effects held by handle (a new one is applied before the old one is
  removed), attacks are `ActiveAbilityDef` grants (the old one is revoked first, so a promoted/rerolled attack starts with a fresh
  cooldown). Grade maps to the Forge ability level (Common 1 ... Revealed 6); every ability number is a `Curve` sampled at that
  level. Cooldowns are Forge effects (`cooldown.slot.a`..`j` per slot, so `PlayerProfile.MaxSlots = 10` and two slots with the same
  attack stack); `AutoCaster` fires auto attacks and commits the cooldown only when the behaviour actually fired. `StageRoot` raises
  `event.kill` (payload: the enemy) on the player's entity on every death; on-kill abilities set `TriggerEventTag`.
- A held promotion ticket pays any applicable shop action (`GradeService` prices it as `Price(Ticket, 1)` first).
- Where Forge belongs: an *ability* is an action an actor performs (triggered by input, an AI brain or a gameplay event); an
  *effect* is a state change on an actor; *stage code* is progression (waves, events, boss timing). Boss patterns are `PatternDef :
  ActiveAbilityDef` (owning `state.casting`, never auto-fired) + `PatternBehavior` (`Begin`/`Advance`/`Complete`/`Abort`),
  activated by `BossBrain` through their grants; the brain waits on `ActorEntity.AbilityEnded`. Events are `StageEvent`
  controllers that place actors/effects (`Statue`, `AreaStrike`) and watch them.
- Events: `EventDirector` rolls `StageDef.Events` (`EventSchedule`) once from `RunRng` on a 1-second grid and runs one `StageEvent`
  at a time under the event layer; success credits the `RewardDef` (gold, stone, tickets) in `StageRoot`. `StageClock` keeps ticking
  after the end (overtime); with a `StageDef.Boss`, `BossDirector` aborts a running event and spawns the boss, whose death clears the
  stage. `EventSchedule.ValidateAgainst` requires `WindowEnd + longest TimeLimit <= Duration` (stage_01: window 20-155 s).
- Bad scene roots fail at `StageRoot.Start`: `EventDirector.ValidateDefs` (StageEvent), `BossDirector.ValidateDef` (BossActor),
  `BossBrain.ValidateDef` (registered, distinct pattern tags).
- `Components/ActorBody` holds the shape sizing / contact wiring / death sequence shared by `Enemy` and `BossActor`.
- Meta flow: autoloads `SceneRouter` (`src/App/SceneRouter.cs`) and `SaveService` (`src/Meta/SaveService.cs`); `/root/...` lookups
  only in `App/Autoloads`. Screens (`Main`, `Logo`, `MainMenu`, `SettingsScreen`, `HubScreen`, `SkillTreeScreen`) take
  `Router`/`SaveService` as properties that default to the autoloads in `_Ready`; tests inject `tests/Support/SceneRouterProbe` and a
  `SaveService` with a scratch `Path`. `StageRoot` never sees the autoloads. Routes live only in `SceneRouter.Routes`; one-shot data
  goes through `Go(route, params)` / `TakeParams()` with keys on the router (`ParamStageDef`).
- Save: JSON at `user://save.json` (`SaveData` <-> `System.Text.Json` `JsonObject`, `SaveStore` writes `<path>.tmp` then renames).
  To change the schema bump `SaveMigrations.CurrentVersion` and append one step to `SaveMigrations.Steps()`; add a migration test.
  Unreadable files (bad JSON, wrong types, duplicate keys, future versions) are moved to `save.json.bak`. `Main` sets
  `RunResult.Gem` from `StageDef.GemFor(outcome)`, credits it and records a clear once per run, and saves once; gem is the only
  persistent currency.
- Hub: `Start` goes to `Hub`; `HubScreen` lists `data/stages/stage_catalog.tres` (order is unlock order) and sends
  `Go(Stage, {ParamStageDef: stage})`. Unlock is derived (`StageProgress`: first stage open, otherwise the previous stage must be in
  `SaveData.ClearedStages`; a cleared stage stays playable) - never store an unlocked list. Ids not in the catalog are kept.
- Skill tree: `SkillTreeDef` (`data/skill_tree/skill_tree.tres`, ordered `SkillNodeDef`s; a node may only require nodes listed
  before it) is spent from the hub's Skill Tree screen (`SkillTreeProgress.Buy` is all-or-nothing). Only `SaveData.SkillLevels` is
  saved; availability is derived. `ProfileBuilder.Build(base, tree, levels)` (pure) derives the run's `PlayerProfile`: each bought
  passive node becomes a `SkillBonus(PassiveDef, level)` and `SlotBonus` raises `SlotCount` (capped at `MaxSlots`).
  `Player.Setup` applies the bonuses and then refills health to the modified max. `Main` falls back to the base profile if the tree
  fails `Validate()`. Saved levels above `MaxLevel` / unknown ids are kept in the save and clamped/ignored at build.

## Adding an enemy (data only)
1. Duplicate `data/enemies/orc.tres`; change `Id`, stats, tint/scale, contact values, `TargetPolicy`. `Visual` and `Drops` point at
   the orc's shared `.tres` files unless you point them at new ones.
2. Add it to `StageDef.TrickleEnemies` or a `WaveEntry`; `StageDef.Validate()` checks it. `tests/Data/DataFilesTest` loads every
   `.tres` under `data/` automatically; add spec-number assertions there if the enemy matters.

## Adding an event (scene + data)
1. A scene in `src/Events/` whose root script extends `StageEvent`: numbers as `[Export]` properties, `Validate()` on the root,
   placed objects as its children, `Objectives()` for HUD arrows, `ReportProgress(text)`, `Succeed()`/`Fail()`; override
   `OnBegin`/`OnStep`/`Cleanup`, and `OnTimeUp()` if surviving is the goal.
2. Add an `EventDef` in `data/events/` (`Id`, `DisplayName`, `ControllerScene`, `TimeLimit`, `RewardDef`) and put it in the
   `EventSchedule.Pool` (`data/events/stage_01_events.tres`); `MinGap` must cover the longest time limit.

## Adding a boss pattern
1. A `PatternDef` subclass (`src/Boss/Patterns/<Name>Def.cs`, `[GlobalClass]`, exported numbers, `CreateBehavior()`) and a
   `PatternBehavior` subclass (`Begin()` returns false to refuse, `Advance(delta)`, `Complete()` when done, `Abort()` cleans up).
   Give it a unique `ability.boss.<word>` tag in `GameplayTags.All` and run the tag sync. Damage goes through `AreaStrike`/
   `Projectile` with the boss entity as source; do not activate other abilities from `Advance`.
2. Author it as a `.tres` in `data/bosses/patterns/` and add a `BossPatternEntry` (pattern + weight) to the boss's `Patterns` in
   `data/bosses/`.

## Adding an ability (data only, unless it is a new kind of attack)
1. Duplicate a file in `data/abilities/` (`vitality.tres` for a passive, `volley.tres` for an attack): set `Id`, names, `Icon`
   (and `RevealedIcon` or rely on the shine tint), `MinGrade`. A passive is a `PassiveDef { Stat, Operation Flat/Percent, ByLevel }`
   whose curve has six points at x = 1..6 (not all zero; `Validate()` checks). An attack is an `ActiveAbilityDef` subclass
   (`VolleyDef`, `BladeSweepDef`, `BountyDef`, ...) with its numbers as curves over the grade level; a new kind of attack is a new
   `<Name>Def` + `<Name>Behavior` pair in `src/Abilities/Attacks/`.
2. Add it to `PlayerProfile.Abilities` in `data/characters/default_profile.tres`; `StageRoot.Start` validates it.
3. Shop prices live in `data/grade_table.tres`; the run's start gold/stone and `DropMultiplier` in the `StageDef` (`stage_01.tres`
   uses 10000 for prototyping - set it to 1 for balanced play).
4. Icons in `assets/art/icons/` are currently generated placeholders (see `assets/SOURCES.md`).

## Adding a stage (data only)
1. Duplicate `data/stages/stage_02.tres`; change `Id`, `DisplayName`, stats, `GemReward`, and point `Waves`/`Events`/`Boss` at new
   or shared `.tres` files. `StageDef.Validate()` must pass.
2. Append it to `data/stages/stage_catalog.tres` (`Stages` order = unlock order) and update the catalog assertion in
   `tests/Data/DataFilesTest`.

## Adding a skill node (data only)
1. Duplicate `data/skill_tree/vitality.tres` (stat passive) or `extra_slot.tres` (slot bonus). Set `Id`, names, `MaxLevel`, `Costs`
   (one gem cost per level), `Requires` (ids of nodes listed EARLIER in the tree). A passive's curve has one point per purchasable
   level (x = 1..`MaxLevel`).
2. Append it to `data/skill_tree/skill_tree.tres` in dependency order and extend `tests/Data/SkillTreeDataTest` with its expected
   values.

## Running the game
- Main scene: `src/App/logo.tscn` (logo -> main menu -> hub -> `src/App/main.tscn` as the stage screen). After a run ends Enter
  returns to the hub; R restarts (dev shortcut - a restarted clear pays gem again). In a run: click a slot to buy/promote,
  Ctrl+click to reroll.
