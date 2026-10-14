# C# + Forge Port — Design Spec

Date: 2026-10-14
Engine: Godot 4.7.2 **.NET** (Forward Plus), C# (net8.0), `Godot.NET.Sdk/4.7.2` (Godot's import migrated the csproj from 4.7.0); the vendored addon is the `forge-godot` tag `v0.4.0`
Plugins: **Forge for Godot 0.4.0** (`addons/forge`, NuGet `Gamesmiths.Forge` 0.4.0) replaces GodotGAS; godot_ai stays (editor bridge only).

## 1. Intent

Port the whole game from GDScript to C#, replace GodotGAS with Forge, and **re-shape the code for C#** instead of translating file by file.
The project is also the owner's **textbook for learning Godot**: prefer a few well-named concepts over clever abstraction, and keep a short "what this teaches" note per module (section 9).

Success criteria:
- Gameplay is equivalent to the GDScript version at the end of each slice (same stats, grades, cooldown scaling, events, boss, meta flow).
- All 109 GDScript tests are ported, mirroring the module layout, and run headless through `tools/run_tests.sh`.
- Layer rules are enforced by a test, not only by convention.
- At cutover the GDScript tree and GodotGAS are deleted and the C# tree lives at `src/`, `data/`, `tests/`.

### 1.1 Decided by the owner

1. Keep Godot (.NET build); port everything to C#.
2. Forge replaces GodotGAS; abilities/effects may be **redesigned** to Forge's model (same behaviour, not the same class structure).
3. Tests: port the existing `TestCase` + headless runner pattern to C#.
4. Structure is rethought for C#; the design below was produced with a delegated architecture pass (Fable model) and is open to owner review.
5. Transition: build the C# tree side by side under `port/` and, at the end, **delete the legacy GDScript tree and promote `port/*` over `src/`, `data/`, `tests/`** (section 7.2).

## 2. Spike findings (Forge fit, 2026-10-14, throwaway code)

| Question | Result |
|---|---|
| Integer attributes vs the game's float stats | Works with a fixed scale. `Attack` stored x1000 (`decimalPlaces: 3`): Might grades 1-6 gave exactly 1.1, 1.2, 1.35, 1.5, 1.7, 2.0 (`PercentBonus` + `ScalableFloat` curve at effect level 1..6). |
| Fractional damage | Results **truncate toward zero** (8 x 1.35 = 10.8 -> 10). Health/damage need a scale (x100) and damage is computed in raw integers, truncating once. |
| `Event.Kill` with a payload | Works. `EventData<Node>` raised on the player's `ForgeEntity.Events`; an ability with `TriggerSource.Event` and a behavior implementing `IAbilityBehavior<Node>` receives the enemy and the ability level. |
| Cooldown scaled by grade | Works. A duration effect with a `ModifierTags` component and a `ScalableFloat` curve scales with the ability level (level 1: 5 fires/10 s, level 6: 10 fires/10 s). |
| `ForgeEffectData` as `.tres` | Round-trips, but each modifier carries ~10 empty sub-resources. Do not hand-author or commit Forge resources (section 5). |

Gotchas that shape the design:
- The released 0.4.0 package uses `Activate` / `CommitCooldown`; docs on Forge `main` use `TryActivate` / `TryCommitCooldown`. **Pin to the 0.4.0 tag and read that tag's source, not `main`.**
- Tags live in one file, `addons/forge/core/forge_data.tres` (const path in `ForgeData`).
- `ForgeEntity` ticks effects/abilities in `_Process`; the manager methods can be called manually (needed for physics-based timing and deterministic tests).
- Forge is WIP / "not production-ready" and logged one editor-side error on headless import (harmless for the game).
- `RollForward=Major` lets the net8.0 project run on a machine with only the .NET 10 runtime.

Verified in slice 1 (all passed; see `docs/superpowers/notes/2026-10-14-slice1-forge-gate.md`, decision **GO**):
1. An attribute-change hook so that lowering `MaxHealth` pulls `Health` down (raising never heals) - `AttributeOnValueChanged` is enough.
2. A `CustomExecution` that reads the **source's** `Attack` - works, in raw integer arithmetic.
3. Removing and re-applying INFINITE passive effects, including two slots with the same passive - independent effects, they stack and unstack separately.
4. Cleanup order when freeing an actor with active effects/abilities - `CancelAbilities(null, null)` then force-remove every effect.
5. New finding: 0.4.0 cannot revoke a permanent grant (`RemoveGrantedAbility` is internal). Abilities are granted through an INFINITE `GrantAbilityEffectComponent` effect and revoked by removing it.

## 3. Assembly, layout, rules

**One assembly** (`VampireSurvivor.csproj` at the project root, `project/assembly_name="VampireSurvivor"`, `<WarningsAsErrors>nullable</WarningsAsErrors>`). Splitting `Core` into a class library would buy one compile-time boundary at the cost of editor build/hot-reload/test-runner complexity. Namespace = folder, root `VampireSurvivor`. C# files are PascalCase (`Wallet.cs`); scenes/resources stay snake_case (`player.tscn`, `orc.tres`). `.cs.uid` files are committed.

```
src/ (root namespace VampireSurvivor)
  Core/        RunRng, Timeline, Wallet, Grade, Price, PhysicsLayers, Groups, InputActions, AnimNames, SpawnGeometry
  Defs/        [GlobalClass] Resource classes + Validate(); PassiveDef, ActiveAbilityDef (abstract) are new
  Gas/         Forge seam: Attributes/{Vitals,Combat,Economy}Attributes, Executions/DamageExecution, Stats, GameplayTags,
               DamageEffects, PassiveEffects, CooldownEffects, AbilityFactory
  Components/  ActorEntity (+ actor_entity.tscn, extends ForgeEntity), Health, Hurtbox, Hitbox, Mover, ContactDamage, LootDropper,
               Lifetime, ActorVisual, TargetProvider, Telegraph, AreaStrike, ActorBody, AbilityContext, AutoCaster
  Actors/      Player, Enemy, Building, Projectile, Pickup, Statue, Boss
  Stage/       Stage, Arena, RunState, StageClock, StageFlow, SpawnDirector, WaveDirector, EnemyFactory, LootDirector,
               StageEvent, EventContext, EventDirector, BossDirector
  Abilities/   Loadout, AbilitySlot, AbilityPool, GradeResult, GradeService, AbilityInstaller, Attacks/{Volley,BladeSweep,Bounty,Dash}{Def,Behavior}
  Boss/        BossBrain, Patterns/{PatternBehavior, Charge, GroundSlam, RadialBurst, Summon}{Def,Behavior}
  Events/      StatueEvent, MeteorEvent, EliteEvent (+ scenes)
  Ui/          Hud, Widgets
  App/         Main, Logo, SceneRouter (autoload), AudioSettings, Autoloads
  Meta/        SaveService (autoload), SaveData, SaveStore, SaveMigrations, SettingsData, StageProgress, Hub, Menu, SkillTree
tests/         Support, Architecture, Data, and one folder per src layer; TestRunner.cs, run_tests.tscn
tools/         run_tests.sh, sync_tags.tscn + SyncTags.cs
```

Layer order is unchanged: `Core -> Defs -> Gas -> Components -> Actors -> Stage | Abilities | Boss -> Ui -> App | Meta` (`Events` is a sibling above `Stage`).

| Rule | Decision |
|---|---|
| Downward dependencies only; upward = signals/events | **Keep.** `Node` classes use `[Signal]`; plain classes (`Wallet`, `Loadout`, `StageClock`) use C# `event`. Payloads that are not Variant-compatible (e.g. `RunResult`) use C# `event`. |
| Enforcement | **Architecture test** `tests/Architecture/LayerRulesTest.cs` reads `using VampireSurvivor.X;` lines in `res://src/**/*.cs` and checks them against the layer table (exceptions: only `Stage/Stage.cs` may use `Abilities`/`Boss`; `Stage/` never uses `Events`). Project namespaces are never written fully qualified. Replaceable by `NetArchTest.Rules` if violations slip through. |
| `Stage` is the composition root | Keep. |
| Content is `.tres`, no balance numbers in code | Keep, except Forge resources are never saved (section 5). |
| Components never find neighbours; `Bind(...)` injection | Keep. In-scene references use `[Export] private Player _player;`, not string `GetNode` paths. |
| ASC child named `AbilitySystemComponent` | **Drop.** Actors hold `[Export] ActorEntity Entity`; Forge does no name lookup. |
| Deferred cleanup before free | Keep (`ActorEntity.Cleanup` via `CallDeferred`); verified in slice 1. |
| Cooldowns in `PeriodicAbility` | **Change** to Forge cooldown effects (curve over ability level) + `AutoCaster`. `ActorEntity.Revoke` removes the ability's cooldown effect so promote/reroll starts a fresh cooldown. |
| Runtime check that `ability_scene` roots are `ActorAbility` | **Drop**; `ActiveAbilityDef` is typed at compile time. |
| Hand-placed Arrow/Dash in `player.tscn` | **Change** to `PlayerProfile.InnateAbilities` (data) granted by `Player.Setup`; one path for shop and innate abilities. |
| `EffectBuilder.damage` single damage path | Keep as `DamageEffects.Hit`. |
| Tag names only via `GameplayTags` constants | Keep; the constants file becomes the **source** that generates `forge_data.tres`. |
| Validation returns `PackedStringArray` | **Change** to `List<string> Validate()` (empty = OK). |

## 4. Old -> new mapping

| Old (`src/`) | New | Notes |
|---|---|---|
| `core/*` | `Core/*` (plain `sealed class`, `record struct Price`, `enum Grade`) | 1:1; `Wallet.Changed` is a C# event |
| `defs/*` | `Defs/*` (`[GlobalClass] partial class X : Resource`) | 1:1 plus `PassiveDef`, `ActiveAbilityDef`. `AbilityDef { Passive, Active }`; `SkillNodeDef.Effect -> Passive`; `BossPatternDef -> BossPatternEntry(ActiveAbilityDef Pattern, float Weight)`; `EnemyDef.contact_effect` removed; `StatBlock` stays float |
| `defs/passive_effect_check.gd` | `PassiveDef.Validate()` | 6-point curve, x = 1..6, magnitude != 0 |
| `gas/attributes/*` | `Gas/Attributes/*Attributes.cs : AttributeSet` | integer storage: Health/MaxHealth/MoveSpeed 2 decimal places, Attack and Gold/Stone gain 3 |
| `gas/actor_asc.tscn` | `Components/ActorEntity` + `actor_entity.tscn` | `ForgeEntity` subclass = the only game-facing Forge seam: `Context`, `ApplyStats`, `FillHealth`, `ApplyPassive/RemovePassive`, `Grant -> AbilityGrant` / `Revoke(AbilityGrant)`, `TryActivate(tag)`, `Raise<T>(tag, payload)`, `Tick(delta)`, `Cleanup()`. Ticks in `_PhysicsProcess`; tests disable it and call `Tick` |
| `gas/effect_builder.gd` | `Gas/DamageEffects.Hit(source, target, amount)` | `causer` argument dropped (unused); `state.invulnerable` target tag blocks the hit |
| `gas/executions/damage_execution.gd` | `Gas/Executions/DamageExecution : CustomExecution` | captures source `Attack`; fallback if unverified item 2 fails: `Hit` multiplies in C# and passes set-by-caller (spike method). Callers are unaffected either way |
| (none) | `Gas/PassiveEffects`, `CooldownEffects`, `AbilityFactory` | build Forge `EffectData`/`AbilityData` in memory from Defs, cached per Def |
| `tools/sync_tags.gd`, `godot_gas/gameplay_tags.gd` | `Gas/GameplayTags.cs` + `tools/SyncTags.cs` | lowercase dotted tags: `ability.boss.charge`, `state.casting`, `cooldown.volley`, `event.kill`, `damage.amount` |
| `components/actor_ability.gd` | deleted | `AbilityContext` (plain class) stays on `ActorEntity.Context` |
| `abilities/periodic_ability.gd` | `Defs/ActiveAbilityDef` (abstract `ForgeAbilityBehavior`; exports `Tag`, `Cooldown` curve, `AutoFire`, `TriggerEventTag`, `OwnedTags`) + `Components/AutoCaster` | cooldown is committed after the behavior actually fires ("a miss costs nothing" is kept) |
| `abilities/{volley,blade_sweep,bounty}`, `arrow_ability`, `dash_ability` | `Abilities/Attacks/*{Def,Behavior}` | Arrow becomes a `VolleyDef` instance; Bounty is `event.kill` + `IAbilityBehavior<Node>`; Dash owns `state.invulnerable` |
| `abilities/ability_installer.gd` | `Abilities/AbilityInstaller` (plain class) | keeps slot -> handle maps; resolves "install before remove" |
| `boss/patterns/boss_pattern.gd` | `Boss/Patterns/PatternBehavior` (`OnStarted -> Begin`, `OnUpdate -> Advance`, `OnEnded -> Abort` if cancelled) + `PatternDef : ActiveAbilityDef` (`OwnedTags=[state.casting]`, `AutoFire=false`) | |
| `boss/boss_brain.gd` | `Boss/BossBrain` | pattern-ended notice via `ActorEntity.AbilityEnded` event unless 0.4.0 `AbilityHandle` has one |
| `actors/*`, `components/*` | `Actors/*`, `Components/*` | `Health` subscribes to the `Vitals.Health` attribute change and emits `[Signal] Changed/Damaged/Died` (instigator argument dropped, unused) |
| `components/actor_body.gd` | `Components/ActorBody` | death -> deferred `Entity.Cleanup` (clear abilities, force-remove effects) |
| `stage/*`, `events/*`, `ui/*` | same names | `Stage` sends `event.kill` with the enemy as payload to the player's entity |
| `app/*`, `meta/*` | `App/*`, `Meta/*` | two autoloads kept; `/root/...` lookup lives in `App/Autoloads` only; `SaveData` uses `System.Text.Json`, migration pattern unchanged |
| `meta/skill_tree/profile_builder.gd` | `ProfileBuilder.Build(base, tree, levels)` | still pure; builds `SkillBonus(PassiveDef, level)` |

Naming as built: `Wallet.Kind` became the top-level `CurrencyKind`; `Grade` helpers are extension methods (`Tint()` instead of `Color()`); `Stats.TryGet` wraps the public `AttributeCaptureDefinition.TryGetAttribute` because `ContainsAttribute` is internal in 0.4.0.

Slice 2a notes: there is no `EffectApplier` in C# (`ContactDamage` calls `DamageEffects.Hit` and checks that the target entity is still alive); `EnemyDef.contact_effect` and `ProjectileDef.hit_effect` were dropped for the same reason; `ProjectileDef` uses `ProjectileTeam` and `EnemyDef` uses `TargetPolicy` as top-level enums; none of the Defs or Components is `[GlobalClass]` while the GDScript tree exists (they all share names with GDScript `class_name`s). `StageDef`, `PlayerProfile`, `GradeTable`, `EventSchedule` and `BossDef` come with slices 2c-4 because they reference types that do not exist yet.

Slice 2b notes: actors are Godot nodes that own an `ActorEntity` child and are wired in `Setup(...)`; node-typed exports use `node_paths` in the hand-written `.tscn`. `AbilityContext` (plain class) lives on `ActorEntity.Context`, which is how ability behaviors reach the mover, the targets and the projectile layer; animations are requested through its `AnimationRequested` event. Attacks are added to the owner's `AutoCaster` by the owner (`Player.Grant` does it for `ActiveAbilityDef.AutoFire` abilities); on-demand abilities (dash) are activated by tag (`ActorEntity.TryActivate`). The innate Arrow is a `VolleyDef` with an arrow count of 1 at every level. `DamageEffects.Hit` refuses a target carrying `state.invulnerable` (checked on the target's tags before the effect is built). `PlayerProfile` currently holds stats, visual, slot count and innate abilities; the shop catalog (slice 3) and skill bonuses (slice 5) are added later. Signal payloads that are not Variant-compatible (`DropRoll`, `CurrencyKind`) are C# events.

Slice 2c notes: the composition root class is `StageRoot` (scene `stage.tscn`): a class `Stage` inside namespace `VampireSurvivor.Stage` cannot be named from the other `VampireSurvivor.*` namespaces (the name resolves to the namespace, CS0118), and the same applies to any class named like its top-level folder. `StageState` and `RunOutcome` are top-level enums; `RunResult` and `StageRoot.Finished` are plain C# (not Variant). `ActorEntity.Raise<T>(tag, payload)` wraps Forge `EventData<T>` and `EventManager.Raise(in ...)`; `StageRoot` raises `event.kill` with the enemy on every death. `StageDef` gains the grade table (slice 3), events and boss (slice 4) and gem reward (slice 5) later; the HUD gains the ability bar, event banner, arrows and boss bar with those slices. `App/DevStage` (`dev_stage.tscn`) stands in for `Main` until slice 5. Hand-written `.tres` files load C#-typed arrays written as `Array[ExtResource("<script>")]([...])`. Swarm benchmark (`port/tools/swarm_bench.tscn`): 100 enemies cost ~4.5 ms of physics per step, 300 enemies ~18 ms, which exceeds a 60 Hz step; mitigation is deferred.

Slice 3 notes: the shop catalog entry is `Defs/AbilityDef { Passive, Active }` and `PlayerProfile.Abilities` lists it; `StageDef.GradeTable` is required. Two slots holding the same attack stack (GDScript parity): Forge blocks by cooldown tag, so the installer grants each slot's attack with the slot's own tag from `GameplayTags.SlotCooldowns` (`cooldown.slot.a`..`j`, so `PlayerProfile.MaxSlots = 10`); `AbilityGrant.CooldownTag` keys `ActorEntity`'s grant books. The installer applies a new passive before removing the old one (no stat dip) but revokes an old attack before granting the new one (a promoted/rerolled attack starts with a fresh cooldown). Event-triggered abilities use `ActiveAbilityDef.TriggerEventTag` -> `AbilityTriggerData.ForEvent<Node>`; Bounty is the first. `Components/AreaStrike` is the single area-hit path (Blade Sweep now, meteors and boss patterns in slice 4); the GDScript `MeleeStrike` subclass is gone. `AutoCaster` churn when nothing is in range was left as is (few player grants; a miss commits nothing). `App/DevStage` routes the HUD bar's requests to `StageRoot.GradeService` only while no result is shown.

Cues are not used (FX stays deferred); `ForgeCueHandler` is where they would go.

## 5. Data and content pipeline

Hand-written content is compact `.tres` Defs only; **Forge `EffectData`/`AbilityData` are assembled by code at load time and never saved**. This keeps diffs readable and confines a plugin upgrade to `Gas/` + `ActorEntity`.

| Content | Authoring | Form |
|---|---|---|
| Stage, Enemy, Wave, Event, Boss, Profile, Catalog, SkillTree, GradeTable | by hand | compact `.tres`, as today |
| Passives (4 abilities + 3 skill nodes) | by hand | `PassiveDef { StatId Attribute; Operation Flat/Percent; Curve ByLevel }`; `StatId` lives in `Defs/`, the enum -> Forge key/decimal-places map in `Gas/Stats.cs` |
| Attacks, dash, boss patterns | by hand | `VolleyDef` etc. (`: ActiveAbilityDef`) with curve/number exports; cooldown tag derived as `cooldown.<tag tail>` |
| Damage effects | code | `DamageEffects`; amounts come from the caller (`EnemyDef.ContactDamage`, pattern Defs) |
| Tags | `Gas/GameplayTags.cs` -> `tools/sync_tags.tscn` -> overwrites `addons/forge/core/forge_data.tres` | Rule change: Forge plugin **source** stays read-only; `forge_data.tres` is the plugin's config file and is written only by the tool. `tests/Gas/GameplayTagsTest` checks constants against `ForgeManagers.Instance.TagsManager` |
| `DATA_FILES` lists | **removed** | `tests/Data/DataFilesTest` scans `res://data/**/*.tres` with `DirAccess`, loads everything and validates every catalog stage |

`StatBlock` stays float; `ActorEntity.ApplyStats` converts with `attribute.ToRawValue` (the single fixed-point conversion point). Expected-value tests compare with `DisplayValue`.

## 6. Tests

| Item | Design |
|---|---|
| `tests/Support/TestCase.cs` | abstract class: `Host`, `Adopt<T>(node)`, `NextFrame/NextPhysicsFrame/Settle/WaitSeconds` (`await Host.ToSignal(...)`), virtual `BeforeEach/AfterEach`. Static `Assert.Eq/True/AlmostEq/NoErrors` throw on the first failure (C# convention) |
| Discovery | `tests/TestRunner.cs` (root of `run_tests.tscn`): reflection over non-abstract `TestCase` types, public `Test*` methods returning `void` or `Task`; labels like `Core/WalletTest::TestAddsGold`; 10 s timeout for `Task` tests |
| CLI | `--filter=` via `OS.GetCmdlineUserArgs()`; exit code 1 on failures |
| Deterministic Forge | `ActorFixture.BareActor(test, stats)` = `Node2D` + `actor_entity.tscn` with physics processing off; tests call `entity.Tick(dt)`, `autoCaster.Step`, `brain.Step`, `event.Step` directly (the existing pattern). The Forge Bootstrap autoload is present in test runs |
| Layout | `tests/<Layer>/<Type>Test.cs` mirrors `src/<Layer>/<Type>.cs`; plus `tests/Data/`, `tests/Architecture/`; `Support/` has `ActorFixture`, `DefFactory`, `ProbeEvent`, `ProbePattern` (now plain `ActiveAbilityDef` subclasses, no scenes needed) |
| `tools/run_tests.sh` | runs `dotnet build` first (`NO_BUILD=1` skips); `FILTER`, `IMPORT`, `TEST_SUITE_TIMEOUT` unchanged; during the port `RUNNER=gd|cs|both` selects the runner(s) |

## 7. Transition and slices

### 7.1 Strategy
Branch `port/csharp`. The C# tree is built under `port/{src,data,tests}/` while the GDScript `src/ data/ tests/` stay untouched as the regression reference (GDScript and C# coexist in one Godot project but cannot inherit from each other, and Windows is case-insensitive so `src/core` and `src/Core` cannot overlap). GDScript tests keep running until cutover.

**One global class namespace.** Godot registers every GDScript `class_name` and every C# `[GlobalClass]` in a single cache, so a port class that is `[GlobalClass]` and shares a name with a GDScript `class_name` replaces it and breaks the GDScript suite (seen with `StatBlock`). While both trees exist, a port `Resource`/`Node` must not be `[GlobalClass]` if a GDScript class of that name exists; `tests/Architecture/GlobalClassCollisionTest` enforces it. Port Defs referenced from `.tres` load by script path and do not need the attribute. Cutover step 7.2.4 re-adds `[GlobalClass]` where the editor needs it.

### 7.2 Cutover (promote over legacy)
1. All 109 tests ported and green in `RUNNER=cs`; manual play-through of one cleared and one failed run.
2. Delete legacy: GDScript `src/ tests/ data/`, `godot_gas/`, `addons/GodotGAS`, the `GameplayCueManager` autoload.
3. Promote: `git mv port/src src`, `port/data data`, `port/tests tests` (folder names that differ only by case, e.g. `core` -> `Core`, are renamed in two steps through a temporary name because of Windows), then replace `res://port/` with `res://` in every `.tscn`/`.tres`/`project.godot` with one scripted pass; re-import and re-run the suite.
4. Re-add `[GlobalClass]` to port Defs that were left off for name collisions (`StatBlock` and any later ones) and delete `GlobalClassCollisionTest`; point `application/run/main_scene` at the C# `logo.tscn`; update `CLAUDE.md` (layers, rules, how-to sections) and `docs`.
Namespaces are unaffected by the move.

### 7.3 Slices
| # | Slice | Contents | Done when |
|---|---|---|---|
| 0 | Scaffold | csproj, `addons/forge` 0.4.0 (own commit), C# runner + `TestCase` + `run_tests.sh` build step, `LayerRulesTest`, `Core/` port (6 tests) | `RUNNER=both` green |
| 1 | **GAS verification** | all of `Gas/`, `ActorEntity`, `Health`, `ActorFixture.BareActor`, `GameplayTags` + sync tool, two passives (Vitality, Might) and one damage kind | tests prove the four unverified items: (a) lowering `MaxHealth` pulls `Health`, raising never heals; (b) `DamageExecution` reads source `Attack` exactly in raw units; (c) passive apply -> remove -> re-apply at another level, two slots with the same passive stack; (d) an actor with active effects/abilities is cleaned up and freed without harming other entities. **Go/no-go gate for Forge** |
| 2 | Core loop | `Defs`, remaining `Components`, `Actors` (Player with innate Volley + Dash, Enemy, Building, Projectile, Pickup), non-Phase-3 `Stage`, basic `Ui/Hud`, dev stage scene | Phase 1 acceptance reproduced; 100-enemy FPS measured |
| 3 | Shop | `Abilities/`, `AutoCaster`, `AbilityInstaller`, `AbilityBar` | Phase 2 tests ported; promote/reroll resets cooldown |
| 4 | Events + boss | `StageEvent`, `Events/`, `Boss/`, `Statue`, boss actor, banners/arrows/boss bar | Phase 3 tests ported; pattern-ended notice API settled |
| 5 | Meta + cutover | `App/`, `Meta/`, autoloads, then section 7.2 | 109 tests ported; legacy removed |

Every slice ports its layer's tests with it.

## 8. Defaults assumed (owner may override at spec review)

1. NuGet and addon pinned to Forge 0.4.0; upgrades are standalone commits touching `Gas/` and `ActorEntity` only.
2. Scales: Health/MaxHealth/MoveSpeed 2 decimals, Attack and gain 3 decimals; data stays float; conversion in `ApplyStats` and `Stats` only.
3. `forge_data.tres` may be written by `tools/` only (exception to "never edit addons/").
4. Cooldowns are Forge effects, not a custom timer.
5. Pattern-ended notice: `PatternBehavior.OnEnded` -> `ActorEntity.AbilityEnded` unless 0.4.0 offers a handle event (checked in slice 4).
6. Tests throw on the first failed assertion; add `Assert.Soft` only if multi-failure collection is needed.
7. Unused arguments dropped: `Health.Damaged` instigator, `EffectBuilder` causer (restorable via effect application context).
8. All tags lowercase dotted.
9. Arrow is absorbed into `VolleyDef`.
10. Architecture test scans `using` lines; swap to `NetArchTest` if violations slip through.
11. Code is written as files; the godot-ai editor bridge is used for run/screenshot (C# needs a build before the editor reloads scenes). To be confirmed in slice 0.
12. 300 enemies x `ForgeEntity` tick cost is unmeasured; slice 2 measures it, mitigation is the existing `EnemyFactory` pooling seam.

## 9. Learning notes (per module)

| Module | What it teaches |
|---|---|
| `Core/` | plain C# (`sealed class`, `record struct`, C# `event`) - "it does not have to be a Node" |
| `Defs/` | `[GlobalClass] partial class : Resource` + `[Export]`; data that selects code (`ActiveAbilityDef` polymorphism); `Validate()` catches bad data at start |
| `Gas/` | a third-party adapter layer: fixed-point stats, `CustomExecution`, building data objects with builders, a constants file that generates a config file |
| `Components/` | node composition; `Bind` injection; a seam that subclasses a plugin node; `_Process` vs `_PhysicsProcess` and test-driven `Tick`; `AreaStrike` as the one area-hit path (`resource_local_to_scene` shapes, a `Lifetime` that frees its owner) |
| `Actors/` | scenes as component assemblies; `[Export] Node` references; `CallDeferred` inside physics callbacks |
| `Stage/` | composition root and explicit wiring; `[Signal]` delegates and `+=`; why the root is `StageRoot` (a class named like its namespace cannot be referenced from other namespaces, CS0118); `RemoveChild` before `QueueFree` so a discarded run leaves its groups at once; `Callable.From(...).CallDeferred()` for work that must not run inside a physics callback |
| `Abilities/` | model (`Loadout`), rules (`GradeService`), mirror (`AbilityInstaller`), behaviour (`IAbilityBehavior`); lifetime by handles; Forge blocks by tag, so per-slot cooldown tags make duplicate slots stack; order matters when swapping (passive: add new then remove old, attack: revoke old then grant new); event-triggered abilities (`IAbilityBehavior<Node>`, `AbilityTriggerData.ForEvent`) |
| `Boss/`, `Events/` | control logic is plain code, behaviour is an ability, placed things are actors |
| `Ui/` | views read models through `Bind` and only emit request signals; Godot signals disconnect themselves when the receiver is freed, plain C# events (`Wallet.Changed`) do not, so views unsubscribe in `_ExitTree` (tests prove it by freeing the view and touching the model) |
| `App/`, `Meta/` | sparing autoloads, `System.Text.Json`, `user://`, migrations; the app (not the HUD) decides what a request does (`DevStage` routes the ability bar to the shop only while no result is shown) |
| `tests/` | reflection runner, `async Task` tests with `ToSignal`, architecture tests |

Suggested reading order: `Core/Wallet` -> `Defs/StatBlock`, `AbilityDef` -> `Gas/Attributes/VitalsAttributes`, `DamageEffects` -> `Components/ActorEntity`, `Health` -> `Actors/Enemy` -> `Stage/StageRoot.Start` and `Wire` -> `Abilities/GradeService`, `AbilityInstaller`, `Attacks/VolleyBehavior`, `BountyBehavior` -> `Boss/BossBrain` -> `Ui/Hud`, `AbilityBar` -> `App/DevStage` (later `App/Main`) -> `tests/TestRunner`.

## 10. Risks

1. Forge is WIP and API names drift between `main` and 0.4.0 (mitigated by pinning and the `Gas/` seam; slice 1 is the exit gate).
2. Integer scaling choices affect balance rounding; mitigated by scale defaults and a single conversion point.
3. Per-entity Forge tick cost at swarm size is unmeasured.
4. Editor tooling for C# (build before scene reload, godot-ai `script_*` tools on `.cs`) is unconfirmed.
5. Two trees coexist for the length of the port; mitigated by `RUNNER=both` runs and a bounded slice plan.
