# Vampire-Survivor-like Base Architecture — Design Spec

Date: 2026-10-13
Engine: Godot 4.7 (Forward Plus), typed GDScript
Plugins: GodotGAS v1.1.2 (`addons/GodotGAS`, vendored copy of the plugin, **read-only: never edit plugin source**), godot_ai (MCP editor bridge, not a game dependency)

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

1. The shop is the six HUD slots (click = promote, Ctrl+click = reroll), usable at any time while the stage keeps running; only currency gates it (Phase 2).
2. Arrow auto-fires at the nearest enemy in range; the manual-aim seam is deferred (the arrow always targets the nearest enemy).
3. Boss death = stage CLEARED; building death at any time = FAILED.
4. Abilities, gold and stone live only within a stage run; **gem** is the only persistent currency.
5. Assets are **copied** from `D:\CM\Godot\res` into `res://assets/` (no external path dependency).
6. Player death also ends the run as FAILED.

## 2. GodotGAS: where it is used and where it is not

Verified against plugin source (spot-checked 2026-10-13: ASC lookup by child name `"AbilitySystemComponent"` at `ability_system_component.gd:691,731,752,966`; `cleanup()` `:219`; `grant_ability` `:279`; `apply_effect_spec_to_target` `:616`; `apply_gameplay_effect` `:633`; `initialize_attribute_overrides` `:588`; `INSTANCED_PER_ACTOR/EXECUTION` and `ability_level`, `cooldown_effect`, `activation_owned_tags` in `abilities/gameplay_ability.gd`; `GameplayExecutionCalculation.execute(spec, target_asc) -> Dictionary`).

**Use GAS for**
- Stats on player, enemy, building, statue: health, max_health, move_speed, attack, gold multiplier (attributes only change through effects; a `cooldown_reduction` attribute is not implemented yet).
- One damage pipeline: every hit is a `GameplayEffect` spec applied via `apply_effect_spec_to_target`.
- States as tags: `State.Invulnerable`, `State.Stunned`, `State.Slowed`.
- Player active abilities: dash (cooldown effect + `activation_owned_tags = [State.Invulnerable]`), arrow shot (PER_ACTOR ability gated by cooldown). Damage effects carry an `application_query` ignoring `State.Invulnerable`.
- Passive abilities as INFINITE effects held by handle; "on kill / on hit" passives via event tags.
- `ability_level` / scaling curves for numbers-per-grade.

**Do not use GAS for**
- Wave/spawn scheduling, stage events (controllers; the things they place are GAS actors/effects), boss sequencing (`BossBrain` is plain code; its patterns are abilities), enemy movement/AI.
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
├─ assets/                     art/, fonts/, tiles/ (sprites are described by `AnimationStrip` resources in `VisualDef` and built into `SpriteFrames` at runtime)
├─ data/                       .tres instances: stages/ waves/ enemies/ effects/ abilities/ grade_table.tres events/ bosses/ characters/ drops/
├─ src/
│  ├─ core/        pure logic: run_rng, timeline, wallet, grade, price, physics_layers, groups, input_actions, anim_names, spawn_geometry
│  ├─ defs/        Resource classes: enemy_def, wave_schedule, wave_entry, stage_def, drop_table, drop_entry, drop_roll,
│  │               visual_def, animation_strip, stat_block, projectile_def, player_profile, run_result; ability_def, grade_table [P2];
│  │               [P3] event_def, reward_def, event_schedule, boss_def, boss_pattern_def
│  ├─ gas/         attributes/{vitals,combat,economy}_attribute_set, executions/damage_execution, cues/, effect_builder
│  ├─ components/  health, hurtbox, hitbox, effect_applier, mover, contact_damage, loot_dropper,
│  │               lifetime, actor_visual, target_provider, actor_ability, ability_context; [P3] telegraph, area_strike, actor_body
│  ├─ actors/      player/ (arrow_ability, dash_ability), enemy/, building/, projectile/, pickup/ (incl. loot_collector),
│  │               [P3] statue/, boss/
│  ├─ stage/       stage, arena, run_state, stage_clock, stage_flow, spawn_director, wave_director, enemy_factory,
│  │               loot_director; [P3] stage_event, event_context, event_director, boss_director
│  ├─ abilities/   ability_slot, loadout, ability_pool, grade_result, grade_service, ability_installer, periodic_ability, volley/, blade_sweep/, bounty/ [P2]
│  ├─ events/      [P3] statue_event, meteor_event, elite_event (extend stage/stage_event)
│  ├─ boss/        [P3] boss_brain, patterns/ (boss_pattern, charge, radial_burst, summon, ground_slam)
│  ├─ ui/          hud/ (health, building health, wallet, timeline bar, ability_bar, ability_slot_view [P2]; event_banner, objective_arrows, boss_bar [P3]), [P4] menus/, widgets/
│  ├─ app/         main (entry scene: instantiates Stage and Hud, binds Hud to the stage models, restart on R); [P4] scene_router (autoload), settings
│  └─ meta/        [P4] save_service (autoload), save_data, hub/, skill_tree/
└─ tests/          mirrors src/ (core, defs, gas, components, actors, stage, ui, data) plus support/; in-repo runner `run_tests.tscn`
```
Scenes live next to their scripts with the same basename. `[Pn]` = built in phase n.

### 3.2 Module responsibilities (Phase 1 set)

| Module | Single responsibility | Interface | Depends on |
|---|---|---|---|
| `core/RunRng` | Seeded RNG for the run | `pick`, `weighted`, `randf`, `randi_range` | — |
| `core/Timeline` | Schedule math over times | `next_after(t)`, `progress(t)` | — |
| `core/Wallet` | Gold/stone/ticket balances | `changed(gold, stone, tickets)`, `add`, `try_spend`, `Kind.TICKET` | — |
| `core/*` consts | Named physics layers, groups, input actions | consts | — |
| `defs/*` | Typed static tuning data | `@export` fields | GAS Resources |
| `gas/attributes/*` | Attribute sets + clamps | `var x: AttributeData`, `pre_attribute_change` | addons |
| `gas/DamageExecution` | Damage math (set-by-caller × attack × target mods) | `execute(spec, target_asc)` | addons |
| `gas/EffectBuilder` | Build damage spec with context | `damage(instigator, causer, effect, amount, level)` | addons |
| `components/Health` | Observes ASC `health`, emits domain events (UI/loot read this) | `changed(cur,max)`, `died` | ASC |
| `components/Hurtbox` / `Hitbox` | Passive hurt area exposing its ASC and actor / detect hurtboxes (once per target per activation) | `bind(asc, actor)` / `hurtbox_entered(hb)` | ASC |
| `components/EffectApplier` | Apply spec to target through instigator ASC | `bind(source_asc)`, `hit(hurtbox, effect, amount, causer)` | ASC, EffectBuilder |
| `components/Mover` | Move at ASC `move_speed`; dash override | `set_direction`, `dash` | ASC |
| `components/ContactDamage` | Periodic damage to overlapped hurtboxes | `@export effect/interval` | Hitbox, EffectApplier |
| `components/LootDropper` | Roll drops on death | `drop_requested(roll, position)` | Health, defs |
| `actors/pickup/LootCollector` | Magnet + pickup | `collected(kind, amount)` | Pickup |
| `components/ActorAbility` / `AbilityContext` | Base for hand-placed actor abilities / the dependencies injected into them (actor, mover, targets, projectile layer, wallet, summon) | `bind_context(ctx)` | Mover, TargetProvider |
| `components/ActorVisual` | Bind `VisualDef` to AnimatedSprite2D | `apply(def, scale, tint)`, `set_moving`, `face`, `play_once`, `flash_hit` | defs |
| `components/Lifetime` | Free after time | `@export seconds` | — |
| `actors/*` | Thin scenes assembled from components | `died`, `asc`, `health`; `Player.ability_context/grant_ability/revoke_ability` | components, defs, gas |
| `stage/RunState` | Per-run mutable state (rng, wallet, clock, profile, stats) → `RunResult` | `to_result()` | core, defs |
| `stage/StageClock` | Elapsed time, pause, end; keeps ticking after `ended` (overtime) | `ticked(t)`, `ended` | — |
| `stage/StageFlow` | Enum states PREP/RUNNING/BOSS/CLEARED/FAILED, transitions only | `state_changed(s)` | — |
| `components/TargetProvider` | "What should this enemy attack" / nearest enemy for the arrow | `target_for(policy)`, `nearest_enemy(origin, range)` | groups |
| `stage/LootDirector` | Turns `drop_requested` into pickups in the world (deferred add), scaled by `drop_multiplier` | `drop_requested` wiring, `drop_multiplier` | actors, defs |
| `stage/EnemyFactory` | Build enemy from `EnemyDef`, apply attribute overrides; pooling seam | `spawn(def, pos)` | actors, defs |
| `stage/SpawnDirector` | Gentle trickle with caps | `bind(...)`, `current_interval(elapsed)` | EnemyFactory, RunRng |
| `stage/WaveDirector` | Drive `WaveSchedule` against clock; expose timeline | `bind(...)`, `timeline`, `mark_times()`, `wave_started(entry)` | Timeline, EnemyFactory |
| `core/Grade` / `core/Price` | Grade ladder (NONE..REVEALED, `level`, `next`, `color`) / a gold-or-stone cost | `Grade.level(g)`, `Price(kind, amount).is_affordable(wallet)` | - |
| `defs/AbilityDef` / `defs/GradeTable` | One shop ability (passive INFINITE effect and/or `ability_scene`, curves over grade level) / the prices | `icon_for(grade)`, `validate()` / `promote_price(g)`, `reroll_price(g)` | GAS Resources |
| `abilities/Loadout` / `AbilityPool` | Slot array / abilities rollable at a grade | `slot`, `set_slot`, `slot_changed` / `roll(grade, rng, exclude)` | defs |
| `abilities/GradeService` | Atomic promote/reveal/reroll over Wallet + RunRng | `promote`, `reroll`, `promote_cost`, `reroll_cost`, `resolved(GradeResult)` | Loadout, Pool, Wallet |
| `abilities/AbilityInstaller` | Mirror slots onto the player's ASC (effect handles / granted abilities) | `bind(loadout, player)`, `validate_defs(defs)` | Loadout, Player |
| `abilities/PeriodicAbility` / `MeleeStrike` | Cooldown-curve base for attack abilities / radial hit | `step(delta)`, `start_cooldown()` / `strike(...)` | components |
| `ui/hud/AbilityBar` | Six slot views; emits requests only | `bind(loadout, wallet, service)`, `promote_requested`, `reroll_requested` | signals only |
| `defs/EventDef` / `RewardDef` / `EventSchedule` | One event (controller scene, time limit, reward) / gold+stone+tickets / seeded event times and pool | `roll_times`, `roll_defs`, `validate_against` | defs |
| `defs/BossDef` / `BossPatternDef` | The boss actor data, pattern order and gap / one pattern (ability scene + weight) | `validate()` | defs |
| `components/Telegraph` / `AreaStrike` | Visual warning circle/line / short-lived circular hit (`MeleeStrike` is one) | `show_circle/show_line`, `elapsed` / `strike(..., target_mask, only_actor)`, `struck` | components |
| `components/ActorBody` | Shape sizing, contact wiring and death sequence shared by `Enemy` and `Boss` | `size_shapes`, `wire_contact`, `die` | components |
| `actors/statue/Statue`, `actors/boss/Boss` | Destructible event target / the boss | `setup(...)`, `destroyed` / `setup(def, target, targets, projectile_layer, summon)`, `patterns()`, `is_casting()` | components |
| `stage/StageEvent` / `EventContext` / `EventDirector` | Event controller base / its injected dependencies / rolls times, runs one event at a time | `event_started/progressed/ended`, `event_times()`, `abort_running()`, `validate_defs` | defs, actors |
| `stage/BossDirector` | Switches to BOSS at time-up, spawns the boss | `appear()`, `boss_appeared`, `boss_defeated`, `validate_def` | actors |
| `boss/BossBrain` / `boss/patterns/BossPattern` | Picks the next pattern by tag (SEQUENCE/RANDOM, gap) / abstract boss `ActorAbility` | `attach_to`, `validate_def` / `_begin/_advance/_complete/_abort` | components |
| `ui/hud/EventBanner` / `ObjectiveArrows` / `BossBar` | Event name/progress/timer and result / edge arrows to off-screen objectives / boss health | `show_event`, `show_progress`, `show_result` / `track`, `refresh` / `show_boss` | signals only |
| `stage/Stage` | Composition root; exposes `player`, `building`, `run_state`, `clock`, `waves`; enemy and wave spawn rings are centred on the building | `start(StageDef, PlayerProfile)`, `finished(RunResult)`, `loadout`, `grade_service`, `events`, `boss_appeared`, `flow_state()`, `current_boss()`; sends `Event.Kill` to the player's ASC | all below it |
| `ui/Hud` | View only, binds to models (bound by `Main`, not `Stage`) | `bind(...)` | signals only |

### 3.3 Decisions

- **Autoloads:** none of ours in Phase 1 (only the plugin's `GameplayCueManager`). Phase 4 adds exactly `SceneRouter` and `SaveService`. No global game-state singleton; `RunState` dies with the stage.
- **Events:** no global event bus. Direct typed past-tense signals wired in `Stage._wire()`, which is the readable map of the game.
- **Data vs code:** all content as `.tres` in `data/` using typed `defs/` classes; behaviors referenced from data (`EnemyDef.scene`, `AbilityDef.ability_scene`, `EventDef.controller_scene`, `BossPatternDef.ability_scene`).
- **Composition over inheritance:** no actor class hierarchy.
- **UI/model separation:** UI reads models via `bind()` and signals; models never import `ui/`.
- **Save seam:** `Stage.start(StageDef, PlayerProfile)` → `finished(RunResult)`. Phase 1 uses a default profile and logs the result; Phase 4's `SaveData` is the persisted side of `PlayerProfile` + unlocks.
- **Asset seams:** `VisualDef` (`AnimationStrip`s built into `SpriteFrames` via `sprite_frames()`, offsets, anim names), `ProjectileDef`, `StageDef.ground_texture` (a repeated `Sprite2D` in `Arena`; a tileset/map scene is deferred). Swapping art touches only `data/` and `assets/`.

- **Where GAS belongs:** an ability is an action an actor with an ASC performs (triggered by input, an AI brain or a gameplay event; constrained by tags/cooldowns); an effect is a state change applied to an actor; stage progression is plain stage code. Boss patterns are abilities driven by `BossBrain`; events are controllers placing GAS actors/effects.

**Considered and rejected:** global EventBus, generic FSM library (StageFlow is an enum; boss patterns are coroutines), ECS, a `Health` that stores HP (duplicates the GAS attribute), pooling now (seam kept in `EnemyFactory`), service locator, input-manager autoload, resource registry/loader, "GameManager" singleton, localization, networking, audio module (`FxSpawner`, Phase 3, is where audio would attach later).

### 3.4 Dependency rules and conventions

Layers (a module may import only layers below it; upward communication = signals):

```
0 addons/GodotGAS, godot_gas/      5 actors/
1 core/                            6 stage/  abilities/  boss/   (events/ extends stage/stage_event)
2 defs/                            7 ui/
3 gas/                             8 app/  meta/   (only layer allowed to touch autoloads)
4 components/
```
- `stage/Stage` (composition root) may reference `abilities/` and `boss/`; no other `stage/` file may.
- No `get_node("/root/...")`. `get_parent()` casts only inside components to reach their actor root (documented).
- Groups only via `Groups` consts, only for physics/targeting queries.
- `stage/` never imports `events/` or `boss/` scripts; it instantiates `PackedScene`/`Script` fields from defs.
- Typed GDScript everywhere (`var x: T`, `-> T`); `class_name` on any script referenced by another script or def; `snake_case.gd` files ↔ `PascalCase` classes.
- Signals are past-tense events with typed args (`wave_started(entry: WaveEntry)`, `died`).
- Member order: signals, `@export`, vars, `_ready`, public, private (`_prefixed`); constants `UPPER_SNAKE`; `#region` per concern.
- Tags: `State.*`, `Cooldown.*`, `Ability.*`, `Event.*`, `Cue.*`, `Damage.*` (set-by-caller keys); referenced only through `GameplayTags` constants.
- Physics layers (six, no `*Hit` layers): `World, AllyBody, EnemyBody, AllyHurt, EnemyHurt, Pickup`.

## 4. Phase mapping

**Phase 1 — Stage core loop (first plan):** `core/`, Phase-1 `defs/`, `gas/` (three attribute sets, `DamageExecution`, `EffectBuilder`, effects `damage_basic`, `cooldown_dash`, `cooldown_arrow`; invulnerability comes from the dash's `activation_owned_tags`), all `components/`, actors player/enemy/building/projectile/pickup, `stage/` (non-Phase-3), HUD (health bars, wallet, **timeline bar** showing waves). Player abilities (arrow, dash) are placed by hand in `player.tscn`; Phase 2 replaces this via `abilities/` without touching actors. Initial content as built: Soldier + Orc + swarmer content, 5 waves, 180 s, no boss yet (time up = CLEARED); Arrow, House as the building, grass ground texture. Done = a stage is playable from start to FAILED or time-up, with waves visible on the timeline and gold/stone dropping.

**Phase 2 — Ability system (built):** `Grade`, `Price`, `AbilityDef` (passive INFINITE effect and/or ability scene, curves over grade level), `GradeTable`; `Loadout`/`AbilitySlot`, `AbilityPool`, `GradeService` (atomic promote/reveal/reroll over Wallet + RunRng, `GradeResult` outcomes), `AbilityInstaller` (handles per slot; `validate_defs` run by `Stage.start`); `Player.grant_ability/revoke_ability`; `Event.Kill` sent by `Stage`; HUD `AbilityBar` bound by `Main`. Abilities: Prospector, Swift Boots, Vitality, Might (passives), Volley, Blade Sweep (`PeriodicAbility` scenes, tag `Ability.Sweep`), Bounty (on-kill hybrid). Prices 100/150/250/400/600 gold, reveal 10 stone, reroll 2 stone; 200 start gold; `drop_multiplier` 10000 in `stage_01` for prototyping.

**Phase 3 — Events + boss (built):** `EventDef`, `RewardDef`, `EventSchedule` (seeded 1-second-grid times with gap/clearance feasibility), `BossDef`, `BossPatternDef`; `StageEvent` + `EventContext` + `EventDirector` (one event at a time, rewards on success), `BossDirector`; `StageClock` overtime; `Telegraph`, `AreaStrike` (Blade Sweep's `MeleeStrike` is one); events statues, meteors, elites; the `Boss` actor, `BossBrain` and four `BossPattern` abilities (charge, radial burst, summon, ground slam); promotion tickets in the wallet and shop; HUD event marks, banner, objective arrows, boss bar. Content: three events (stage_01: 3 per run, 20–155 s) and the Orc Warlord.

**Phase 4 — Meta:** `SceneRouter`, `SaveService` + versioned `SaveData`, hub, stage select/unlock, gem skill tree (modifies `PlayerProfile` → initial overrides/effects), menus, settings (volume etc.). `Stage` unchanged.

## 5. Error handling and testing

- Defs validate themselves in `_validate_property`/an explicit `validate() -> PackedStringArray`, run by `Stage.start` in debug builds; a bad `.tres` fails loudly at stage start, not mid-wave.
- Pure logic (`core/`, `GradeService`, wave scheduling math) is unit-testable without scenes; tests live in `tests/`. Tests run on an in-repo headless runner (`tests/run_tests.tscn`, `tools/run_tests.sh`); the editor MCP runner cannot call non-`@tool` scripts.
- Each phase ends with a run-through in the editor via the MCP bridge (`project_run`, screenshot, log read) as acceptance evidence.

## 6. Risks

- GodotGAS has no docs; behavior beyond the spot-checked calls must be verified in code before relying on it. The Phase 1 plan starts with a short GAS spike (ASC + attribute set + damage effect + dash cooldown on a throwaway scene).
- ~300 enemies × one ASC `_process` each; mitigated by keeping enemy effect lists empty and leaving a pooling seam in `EnemyFactory`.
- TinyRPG and FarmRPG assets ship without a license file; fine for prototyping, verify before any release.

## 7. Phase 1 notes

**Acceptance (automated editor run):** no script errors; GAME OVER, CLEARED and R-restart verified; 145 FPS with 100 enemies. The manual play-through is still pending for the owner.

**Known follow-ups:**
- Enemies do not collide with each other (they stack).
- (Done in Phase 2) `VitalsAttributeSet` re-clamps health when `max_health` drops.
- `application/run/main_scene` was hand-edited in `project.godot` (the godot-ai tool refuses to set it); re-set it in Project Settings if the editor re-saves settings.

## 8. Phase 2 notes

**Acceptance (in-editor, godot-ai, partial):** Verified with simulated input: the main scene ran with no game-log errors; six `100g` slots and `Gold 200   Stone 0` at start; buying and promoting slots (slot 0 reached an Epic frame, next cost 400g); `drop_multiplier` applied to pickups; GAME OVER ignored slot clicks; R restarted to 200 gold with empty slots. A Ctrl+click was sent but its outcome was not confirmed. **Verified afterwards by the owner in manual play (2026-10-13):** refused-click flash/dimming, the Legendary/Revealed walk and the `10s`/`MAX` labels, reroll outcome, passive effects (Vitality/Swift Boots/Might), Volley arrows, Bounty gold, stacked duplicates, clicks not moving the soldier, frame rate.

**Known follow-ups:**
- `max_health` passives do not heal on raise; health is re-clamped when `max_health` drops. In Phase 2 health never exceeds the new max (there is no heal source), so the re-clamp is unreachable in play.
- Phase 3 follow-up, before the first heal source lands: (a) `Health` should not emit `damaged` for a spec-less decrease (the re-clamp passes no spec); (b) promoting/rerolling a Vitality slot uninstalls the old effect before installing the new one, which would cost healed HP, so it needs install-before-remove or equivalent.
- Attack cooldowns reset when a slot is promoted/rerolled (fresh instance).
- Rerolling a one-ability pool returns the same ability and still charges.
- Icons in `assets/art/icons` are generated placeholders (the 128x128 ones opaque); swap for the real art library.
- Selling/removing abilities is out of scope.

## 9. Phase 3 notes

**Acceptance (in-editor, godot-ai, partial; throwaway data tweaks were reverted and never committed):**
- Verified automatically: the main scene started with the shipped data and no game-log errors; the screenshot showed the timeline with five wave diamonds and three green event circles (none on a diamond) and the wallet `Gold 200   Stone 0   Ticket 0`.
- Verified with a temporary 1-event / 3-10 s window: the event banner appeared top right (`Destroy the Statues`, `Statues 0/4`, countdown) and yellow arrows pointed at off-screen statues; game log clean. The result line (`Failed`/`Success`) was not captured.
- Verified with a temporary 6 s duration and compressed waves: at time-up the timeline showed `BOSS`, the `Orc Warlord` bar appeared and a large dark orc arrived near the soldier; game log clean.
- Unattended runs ended in GAME OVER before 180 s (the soldier does not move), so the real 180 s timing, event success/fail results and rewards, tickets in the shop, the meteor and elite events, the boss patterns, the boss kill (STAGE CLEARED), restart and frame rate were NOT verified automatically. They are left to the owner's manual play (checklist: Phase 3 plan, Task 16 Step 4).

**Known follow-ups:**
- `fx_spawner` (hit/death FX) is still not built.
- Boss bolts also hit the building (ALLY team).
- Elites stay in the fight when their event fails or is aborted.
- With validated data no event can still run at 180 s (`window_end` + longest limit <= duration); the boss's abort and the overlap queue remain safety nets only.
- Statues draw auto-aim away from nearby enemies.
- `drop_multiplier` 10000 makes event gold/stone irrelevant while prototyping; tickets still matter.
- The Phase 2 pre-heal follow-ups stay open (no heal source yet).
