# Vampire-Survivor-like Base Architecture — Design Spec

Date: 2026-10-13
Engine: Godot 4.7 (Forward Plus), typed GDScript
Plugins: GodotGAS v1.1.2 (`addons/GodotGAS`, symlink to `D:\CM\Godot\plugins\GodotGAS`, **read-only: never edit plugin source**), godot_ai (MCP editor bridge, not a game dependency)

## 1. Intent

A top-down 2D vampire-survivor-like **prototype** whose real deliverable is a **clean, modular project base**. Prototyping speed matters, but so does a codebase the owner finds beautiful: every separable concern is its own module with a single responsibility. Story/plot, sound, and networking are out of scope for now.

### 1.1 Game summary (from owner)

- Player: short-range arrow shot + dash (cooldown, invulnerable while dashing).
- Stage: a building at map center must be defended; destroyed => game over.
- Enemies trickle in, then arrive in periodic **waves**; wave times are visible on a **timeline bar**.
- Random-time **events** (special enemies; gimmicks such as "destroy 4 random statues", "meteors fall") with event-specific rewards.
- At stage end a **boss** with distinct patterns appears.
- Kills drop **gold** and **stone**.
- **Abilities** have grades common < rare < epic < unique < legendary < revealed. Promotion costs gold (rising per grade); common→…→legendary re-rolls randomly within the target grade's pool; legendary→revealed costs stone and is a deterministic upgraded form of the same ability. Stone also rerolls an ability. Ability kinds: gold gain, ranged, melee, move speed, hybrids…
- Meta (later): logo → main menu (Start / Settings / Quit) → hub (stage select with sequential unlock, character skill tree paid with **gem** earned on stage clear) → stage.

### 1.2 Build order (agreed)

1. Stage core loop → 2. Ability system → 3. Events + boss → 4. Meta layer.
Each phase gets its own plan and implementation cycle. **This spec defines the whole architecture; the first implementation plan covers Phase 1 only** while leaving the seams for 2–4.

### 1.3 Defaults assumed (owner may override at spec review)

1. Shop (ability promote/reroll) is a pause overlay available at any time during a stage; only currency gates it.
2. Arrow auto-fires at the nearest enemy in range; a manual-aim seam is kept.
3. Boss death = stage CLEARED; building death at any time = FAILED.
4. Abilities, gold and stone live only within a stage run; **gem** is the only persistent currency.
5. Assets are **copied** from `D:\CM\Godot\res` into `res://assets/` (no external path dependency).

## 2. GodotGAS: where it is used and where it is not

Verified against plugin source (spot-checked 2026-10-13: ASC lookup by child name `"AbilitySystemComponent"` at `ability_system_component.gd:691,731,752,966`; `cleanup()` `:219`; `grant_ability` `:279`; `apply_effect_spec_to_target` `:616`; `apply_gameplay_effect` `:633`; `initialize_attribute_overrides` `:588`; `INSTANCED_PER_ACTOR/EXECUTION` and `ability_level`, `cooldown_effect`, `activation_owned_tags` in `abilities/gameplay_ability.gd`; `GameplayExecutionCalculation.execute(spec, target_asc) -> Dictionary`).

**Use GAS for**
- Stats on player, enemy, building, statue: health, max_health, move_speed, attack, gold multiplier, cooldown reduction (attributes only change through effects).
- One damage pipeline: every hit is a `GameplayEffect` spec applied via `apply_effect_spec_to_target`.
- States as tags: `State.Invulnerable`, `State.Stunned`, `State.Slowed`.
- Player active abilities: dash (cooldown effect + `activation_owned_tags = [State.Invulnerable]`), arrow shot (PER_ACTOR ability gated by cooldown). Damage effects carry an `application_query` ignoring `State.Invulnerable`.
- Passive abilities as INFINITE effects held by handle; "on kill / on hit" passives via event tags.
- `ability_level` / scaling curves for numbers-per-grade.

**Do not use GAS for**
- Wave/spawn scheduling, stage events, boss sequencing, enemy movement/AI.
- Projectiles (no ASC; a projectile carries a spec + instigator).
- Wallet/loot, ability grade/promotion/reroll identity switching (Loadout concern), UI, save data.
- GAS cues for high-churn enemy hit/death FX (cues are reparented under the target and die with it); use the stage `FxSpawner`. GAS cues only for persistent player-attached effects.
- `INSTANCED_PER_EXECUTION`; the dashboard attribute-set generator (sets are hand-written, ~20 lines each); `grant_ability_from_script` (double `ability_granted` emission — use `grant_ability(node)`).

**Plugin conventions that constrain us**
- The ASC must be a **direct child named exactly `AbilitySystemComponent`** of the actor root.
- Call `asc.cleanup()` before freeing an actor.
- Contact damage is INSTANT (never stored on enemies); keep enemy active-effect lists near-empty because each ASC runs `_process`.
- Remove the generated `Example.*` tags from `godot_gas/` when adding real ones.

## 3. Architecture

**Principle:** `Stage` is the composition root. Directors, actors and UI never look each other up; `Stage` instantiates them, injects dependencies, and wires typed signals. Data (Resources) selects *which* code runs; code decides *how*. No balance numbers in code.

### 3.1 Folder layout

```
res://
├─ addons/GodotGAS/            plugin (untouched)
├─ godot_gas/                  plugin-generated only (gameplay_tags.gd, registries)
├─ assets/                     art/, fonts/, tiles/, SpriteFrames .tres
├─ data/                       .tres instances: stages/ waves/ enemies/ effects/ abilities/ events/ bosses/ characters/ drops/
├─ src/
│  ├─ core/        pure logic: run_rng, timeline, wallet, physics_layers, groups, input_actions
│  ├─ defs/        Resource classes: enemy_def, wave_schedule(+entry), stage_def, drop_table, visual_def,
│  │               projectile_def, player_profile, run_result; [P2] ability_def, ability_pool, grade_table;
│  │               [P3] event_def, reward_def, boss_def, boss_pattern_def
│  ├─ gas/         attributes/{vitals,combat,economy}_attribute_set, executions/damage_execution, cues/, effect_builder
│  ├─ components/  health, hurtbox, hitbox, effect_applier, mover, contact_damage, loot_dropper,
│  │               loot_collector, lifetime, actor_visual
│  ├─ actors/      player/, enemy/, building/, projectile/, pickup/, [P3] statue/, boss/
│  ├─ stage/       stage, run_state, stage_clock, stage_flow, spawn_director, wave_director, target_provider,
│  │               fx_spawner, enemy_factory; [P3] event_director, stage_event, boss_director
│  ├─ abilities/   [P2] loadout, ability_slot, grade_service, ability_installer
│  ├─ events/      [P3] statue_event, meteor_event (extend stage/stage_event)
│  ├─ boss/        [P3] boss_pattern, pattern_runner, patterns/
│  ├─ ui/          hud/ (health, building health, wallet, timeline bar), [P2] shop/, [P4] menus/, widgets/
│  ├─ app/         main.tscn; [P4] scene_router (autoload), settings
│  └─ meta/        [P4] save_service (autoload), save_data, hub/, skill_tree/
└─ tests/          logic tests for core/ and abilities/ (framework chosen in the plan)
```
Scenes live next to their scripts with the same basename. `[Pn]` = built in phase n.

### 3.2 Module responsibilities (Phase 1 set)

| Module | Single responsibility | Interface | Depends on |
|---|---|---|---|
| `core/RunRng` | Seeded RNG for the run | `pick`, `weighted`, `randf`, `randi_range` | — |
| `core/Timeline` | Schedule math over times | `next_after(t)`, `progress(t)` | — |
| `core/Wallet` | Gold/stone balances | `changed(gold, stone)`, `add`, `try_spend` | — |
| `core/*` consts | Named physics layers, groups, input actions | consts | — |
| `defs/*` | Typed static tuning data | `@export` fields | GAS Resources |
| `gas/attributes/*` | Attribute sets + clamps | `var x: AttributeData`, `pre_attribute_change` | addons |
| `gas/DamageExecution` | Damage math (set-by-caller × attack × target mods) | `execute(spec, target_asc)` | addons |
| `gas/EffectBuilder` | Build damage spec with context | `damage(instigator, causer, effect, amount, level)` | addons |
| `components/Health` | Observes ASC `health`, emits domain events (UI/loot read this) | `changed(cur,max)`, `died` | ASC |
| `components/Hurtbox` / `Hitbox` | Receive / detect hits (once per target per activation) | `hit_received(spec)` / `hurtbox_entered(hb)` | ASC |
| `components/EffectApplier` | Apply spec to target through instigator ASC | `apply(spec, target_asc)` | ASC, EffectBuilder |
| `components/Mover` | Move at ASC `move_speed`; dash override | `set_direction`, `dash` | ASC |
| `components/ContactDamage` | Periodic damage to overlapped hurtboxes | `@export effect/interval` | Hitbox, EffectApplier |
| `components/LootDropper` / `LootCollector` | Roll drops on death / magnet + pickup | `drop_requested(...)` / `collected(kind, amount)` | Health, defs |
| `components/ActorVisual` | Bind `VisualDef` to AnimatedSprite2D | `play(state)`, `face(dir)` | defs |
| `components/Lifetime` | Free after time | `@export seconds` | — |
| `actors/*` | Thin scenes assembled from components | `died`, `asc`, `health` | components, defs, gas |
| `stage/RunState` | Per-run mutable state (rng, wallet, clock, profile, stats) → `RunResult` | `to_result()` | core, defs |
| `stage/StageClock` | Elapsed time, pause, end | `ticked(t)`, `ended` | — |
| `stage/StageFlow` | Enum states PREP/RUNNING/BOSS/CLEARED/FAILED, transitions only | `state_changed(s)` | — |
| `stage/TargetProvider` | "What should this enemy attack" | `get_target(policy)` | — |
| `stage/EnemyFactory` | Build enemy from `EnemyDef`, apply attribute overrides; pooling seam | `spawn(def, pos)` | actors, defs |
| `stage/SpawnDirector` | Gentle trickle with caps | `start/stop`, `spawned` | EnemyFactory, RunRng |
| `stage/WaveDirector` | Drive `WaveSchedule` against clock; expose timeline | `wave_started(entry)`, `schedule` | Timeline, EnemyFactory |
| `stage/FxSpawner` | One-shot world FX | `spawn(fx_id, pos)` | — |
| `stage/Stage` | Composition root | `start(StageDef, PlayerProfile)`, `finished(RunResult)` | all below it |
| `ui/Hud` | View only, binds to models | `bind(...)` | signals only |

### 3.3 Decisions

- **Autoloads:** none of ours in Phase 1 (only the plugin's `GameplayCueManager`). Phase 4 adds exactly `SceneRouter` and `SaveService`. No global game-state singleton; `RunState` dies with the stage.
- **Events:** no global event bus. Direct typed past-tense signals wired in `Stage._wire()`, which is the readable map of the game.
- **Data vs code:** all content as `.tres` in `data/` using typed `defs/` classes; behaviors referenced from data (`EnemyDef.scene`, `AbilityDef.ability_script`, `EventDef.controller_scene`, `BossPatternDef.script`).
- **Composition over inheritance:** no actor class hierarchy.
- **UI/model separation:** UI reads models via `bind()` and signals; models never import `ui/`.
- **Save seam:** `Stage.start(StageDef, PlayerProfile)` → `finished(RunResult)`. Phase 1 uses a default profile and logs the result; Phase 4's `SaveData` is the persisted side of `PlayerProfile` + unlocks.
- **Asset seams:** `VisualDef` (SpriteFrames, offsets, anim names), `ProjectileDef`, `StageDef.tileset/map_scene`. Swapping art touches only `data/` and `assets/`.

**Considered and rejected:** global EventBus, generic FSM library (StageFlow is an enum; boss patterns are coroutines), ECS, a `Health` that stores HP (duplicates the GAS attribute), pooling now (seam kept in `EnemyFactory`), service locator, input-manager autoload, resource registry/loader, "GameManager" singleton, localization, networking, audio module (`FxSpawner` is where audio would attach later).

### 3.4 Dependency rules and conventions

Layers (a module may import only layers below it; upward communication = signals):

```
0 addons/GodotGAS, godot_gas/      5 actors/
1 core/                            6 stage/  abilities/  boss/   (events/ extends stage/stage_event)
2 defs/                            7 ui/
3 gas/                             8 app/  meta/   (only layer allowed to touch autoloads)
4 components/
```
- No `get_node("/root/...")`. `get_parent()` casts only inside components to reach their actor root (documented).
- Groups only via `Groups` consts, only for physics/targeting queries.
- `stage/` never imports `events/` or `boss/` scripts; it instantiates `PackedScene`/`Script` fields from defs.
- Typed GDScript everywhere (`var x: T`, `-> T`); `class_name` on any script referenced by another script or def; `snake_case.gd` files ↔ `PascalCase` classes.
- Signals are past-tense events with typed args (`wave_started(entry: WaveEntry)`, `died`).
- Member order: signals, `@export`, vars, `_ready`, public, private (`_prefixed`); constants `UPPER_SNAKE`; `#region` per concern.
- Tags: `State.*`, `Cooldown.*`, `Ability.*`, `Event.*`, `Cue.*`, `Damage.*` (set-by-caller keys); referenced only through `GameplayTags` constants.
- Physics layers: `World, PlayerBody, EnemyBody, PlayerHurt, EnemyHurt, PlayerHit, EnemyHit, Pickup`.

## 4. Phase mapping

**Phase 1 — Stage core loop (first plan):** `core/`, Phase-1 `defs/`, `gas/` (three attribute sets, `DamageExecution`, `EffectBuilder`, effects `damage_basic`, `cooldown_dash`, `invulnerable`), all `components/`, actors player/enemy/building/projectile/pickup, `stage/` (non-Phase-3), HUD (health bars, wallet, **timeline bar** showing waves). Player abilities (arrow, dash) are placed by hand in `player.tscn`; Phase 2 replaces this via `abilities/` without touching actors. Initial content: Soldier player, Orc enemy (+ one or two others), Arrow, House as the building, grass tileset. Done = a stage is playable from start to FAILED or time-up, with waves visible on the timeline and gold/stone dropping.

**Phase 2 — Ability system:** `AbilityDef` (id, grade, kind flags, passive effects, ability script, `revealed_form`), `AbilityPool` per grade, `GradeTable` (costs); `Loadout`/`AbilitySlot` (holds grant handles), `GradeService` (pure promote/reroll/reveal rules over Wallet + RunRng), `AbilityInstaller` (def → ASC grant/remove); shop UI. Grade maps to `ability_level`.

**Phase 3 — Events + boss:** `EventDef`, `RewardDef`, `BossDef`, `BossPatternDef`; `EventDirector`, `StageEvent` base, `BossDirector`; statue and meteor events; boss actor and `PatternRunner`. `StageFlow` gains BOSS handling; `WaveDirector` unchanged.

**Phase 4 — Meta:** `SceneRouter`, `SaveService` + versioned `SaveData`, hub, stage select/unlock, gem skill tree (modifies `PlayerProfile` → initial overrides/effects), menus, settings (volume etc.). `Stage` unchanged.

## 5. Error handling and testing

- Defs validate themselves in `_validate_property`/an explicit `validate() -> PackedStringArray`, run by `Stage.start` in debug builds; a bad `.tres` fails loudly at stage start, not mid-wave.
- Pure logic (`core/`, `GradeService`, wave scheduling math) is unit-testable without scenes; tests live in `tests/`. The test framework (GUT vs the godot_ai test runner) is chosen in the Phase 1 plan after checking which is available.
- Each phase ends with a run-through in the editor via the MCP bridge (`project_run`, screenshot, log read) as acceptance evidence.

## 6. Risks

- GodotGAS has no docs; behavior beyond the spot-checked calls must be verified in code before relying on it. The Phase 1 plan starts with a short GAS spike (ASC + attribute set + damage effect + dash cooldown on a throwaway scene).
- ~300 enemies × one ASC `_process` each; mitigated by keeping enemy effect lists empty and leaving a pooling seam in `EnemyFactory`.
- TinyRPG and FarmRPG assets ship without a license file; fine for prototyping, verify before any release.
- The project is **not a git repository**; the spec cannot be committed. Recommend `git init` before implementation begins.
