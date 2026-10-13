# Phase 3 — Events + Boss: Design Spec

Date: 2026-10-13
Parent spec: `2026-10-13-vampire-survivor-base-design.md` (module contract, layers, conventions — still binding)
Builds on: Phase 1 (stage core loop) and Phase 2 (ability system, `2026-10-13-phase2-ability-system-design.md`), both on `main`.

## 1. Intent

During a stage, **events** fire at random but announced times (shown on the timeline); each is a short objective
with a time limit that pays gold, stone and **promotion tickets** on success. When the stage time runs out a **boss**
appears; killing it clears the stage. The system stays data-driven (events, rewards, boss and patterns are `.tres` +
scenes), uses GodotGAS where it fits (boss patterns are abilities, all damage goes through effects) and is testable by
driving time through `step(delta)`.

## 2. Owner decisions

- Event reward = currency + **promotion tickets** (option "3").
- Tickets are **banked** in the wallet (`Ticket ×N` in the HUD). A slot click spends a ticket **before** any currency;
  one ticket pays for any shop action: buy (empty slot), promote, reveal (legendary → revealed), reroll.
- Event times are rolled from the run seed at stage start and **shown on the timeline bar** with their own mark style.
- Every event has a **time limit**; only success pays. Statues: destroy all within the limit. Meteors: survive the
  duration taking at most `max_hits` meteor hits (exceeding it fails at once). Elites: kill all within the limit.
- Boss patterns (first boss): **charge, radial burst, summon, ground slam**.
- The boss **chases the player**; normal enemy spawning **continues** during the boss fight; the boss fight has **no
  time limit**.
- First content: **three events** — statues, meteors, elite enemies — and **one boss**.
- **Where GAS abilities belong (agreed rule):** an *ability* is an action an actor with an ASC performs, triggered by
  its controller (input, AI brain, gameplay event) and constrained by tags/cooldowns. An *effect* is a state change
  applied to an actor. Stage progression (waves, events, when the boss appears) is plain stage code. Therefore boss
  patterns are `ActorAbility` scenes driven by a `BossBrain`; events are `StageEvent` controllers that place GAS actors
  and effects and watch them, not abilities.

## 3. Defaults chosen by the designer (all data, adjustable)

| Item | Value |
|---|---|
| Events per stage (`stage_01`) | 3, window 20–155 s, `min_gap` 30 s between events, `wave_clearance` 5 s from wave times |
| Statues | 4 statues, 60 HP each, ≥ 250 px from building and player, limit 25 s; reward 150 gold, 2 stone, 1 ticket |
| Meteors | duration 20 s, one meteor every 1.2 s, warning 0.9 s, radius 60 px, 15 damage, landing ≤ 120 px from the player, `max_hits` 3; reward 100 gold, 3 stone, 1 ticket |
| Elites | 3 elite orcs (300 HP, speed 70, scale 1.8, red tint, target player, contact 15), limit 25 s; reward 200 gold, 2 stone, 1 ticket |
| Boss "Orc Warlord" | 2500 HP, speed 60, scale 3.0, dark tint, contact 20; `pattern_gap` 2.0 s; order RANDOM, weight 1 each |
| Charge | warning line 0.7 s, speed 520 px/s, distance 360 px, 25 damage on contact while charging |
| Radial burst | 12 projectiles, speed 220 px/s, lifetime 2.0 s, 10 damage each |
| Summon | 4 orc swarmers on a 80 px ring around the boss |
| Ground slam | warning circle 1.0 s at the player's position, radius 90 px, 30 damage |

Event and boss numbers live in the controller/pattern scenes' exported values and in `.tres` files — none in code.

## 4. Stage flow and time

- Flow: `PREP → RUNNING → (clock ended) → BOSS → (boss died) → CLEARED`; building or player death → `FAILED` from
  RUNNING or BOSS. `StageFlow` already allows these transitions.
- `StageDef.boss: BossDef` (optional). Null → clock end = CLEARED as today (backward compatible).
- `StageClock` emits `ended` once at `duration` and then **keeps ticking** (`elapsed` grows past `duration`), so the
  trickle `SpawnDirector` continues during the boss fight at its end-of-stage interval. Waves and events are scheduled
  inside `duration` only, so none start during the boss fight.
- When the boss appears, an event still running is **aborted** (no reward, its objects removed). Validation makes this
  rare; abort is the safety net.
- `StageDef.events: EventSchedule` (optional): `count`, `window_start`, `window_end`, `min_gap`, `wave_clearance`,
  `pool: Array[EventDef]`.
  - Times are rolled once by `EventDirector` from `RunRng` on a 1-second grid: a second is *allowed* when it lies in
    the window and is more than `wave_clearance` from every wave time. Events are placed in order; each is drawn
    uniformly from the allowed seconds that are ≥ `min_gap` after the previous event **and** still leave room to place
    the remaining events greedily. This always succeeds when the schedule is feasible.
  - `EventSchedule.validate_against(duration, wave_times)` reports an error when greedy earliest placement fails, when
    the window lies outside `(0, duration)`, when `window_end` plus the longest `time_limit` in the pool exceeds `duration` (every event can finish before the boss), when `count < 0`, or when `min_gap` is shorter than the longest
    `time_limit` in the pool (so at most one event runs at a time). It also validates every `EventDef`.
  - The event picked for each time is drawn from `pool`, never the same def twice in a row when the pool has
    alternatives.
- Event times become timeline marks of their own kind; the HUD timeline shows "BOSS" once the boss fight starts.

## 5. Events

### 5.1 Contract

- `stage/StageEvent` (`Node2D`, abstract base): `begin(context: EventContext)`, `abort()`, `step(delta)`;
  signals `succeeded`, `failed`, `progress_changed(text: String)`; `objectives() -> Array[Node2D]` (things the HUD
  points at when off-screen; empty by default); `remaining_time() -> float`.
  - The base owns the time limit (from `EventDef.time_limit`). Running out calls the overridable `_on_time_up()`,
    which fails by default; meteors override it to succeed.
  - Subclasses implement `_on_begin()`, `_cleanup()` and their success/failure checks; the base guarantees it ends
    exactly once and calls `_cleanup()` on success, failure and abort.
  - Physics processing drives `step` in play; tests call `step` after `set_physics_process(false)`.
- `stage/EventContext` (RefCounted): world layer, `EnemyFactory`, `TargetProvider`, player, building, `RunRng`,
  arena bounds, the `EventDef`. Event code never imports `Stage`.
- `stage/EventDirector` (Node): `bind(stage_def, clock, context_factory, rng, wave_times)`; rolls the times, starts
  each event on the clock tick that crosses its time, keeps at most one running, `abort_running()`; signals
  `event_started(def, event)`, `event_progressed(def, text)`, `event_ended(def, success: bool)`; exposes
  `event_times()` for the timeline.
- Rewards: on success `Stage` credits `RewardDef` to the run wallet. Failure pays nothing.

### 5.2 Data

- `defs/EventDef`: `id`, `display_name`, `icon`, `controller_scene` (root extends `StageEvent`), `time_limit`,
  `reward: RewardDef`, `validate()` (instantiates the scene to check the root, frees it; reports a root `validate()`
  if the controller implements one).
- `defs/RewardDef`: `gold`, `stone`, `tickets` (all ≥ 0, at least one > 0).
- `defs/EventSchedule`: as in §4.

### 5.3 The three events (`src/events/`)

1. **Statues** (`statue_event`): spawns `statue_count` statues at random arena points at least `min_distance` from
   the building and the player. A statue is a new actor `actors/statue` (ASC + `Health` + `Hurtbox` on `ENEMY_HURT`,
   joins `Groups.ENEMIES` so auto-aim targets it; not an `Enemy`, so no kill count, no `Event.Kill`, no drops; drawn
   as a simple shape). Progress "Statues k/N". All destroyed → success; time up → remaining statues removed, failure.
2. **Meteors** (`meteor_event`): every `interval` seconds picks a point within `landing_spread` of the player, shows a
   `Telegraph` circle for `warning`, then an `AreaStrike` hits **only the player's hurtbox** for `damage` through
   `EffectBuilder.damage`, with a small environment ASC owned by the event as the effect source. Each hit on the player
   counts; more than `max_hits` → failure at once. Surviving the duration → success. Progress "Hits k/max".
3. **Elites** (`elite_event`): spawns `count` of `elite_enemy` (a new `EnemyDef`, `data/enemies/elite_orc.tres`)
   through `EnemyFactory` on the spawn ring, so kills, `Event.Kill` (Bounty) and drops work. All dead → success;
   time up → failure, and the surviving elites **stay** in the fight. Progress "Elites k/N".

### 5.4 Shared components (`components/`)

- `Telegraph` (Node2D): draws a warning circle or line for a duration, then emits `elapsed` and frees itself.
- `AreaStrike`: a short-lived circular `Hitbox` + `EffectApplier` + `Lifetime` hit whose target mask and optional
  hurtbox filter are parameters. Phase 2's `MeleeStrike` becomes a configuration of it (or is reimplemented on it)
  so there is one area-hit path.

## 6. Boss

- **Actor** `actors/boss/` (`Boss`, `CharacterBody2D`): ASC, `Health`, `Hurtbox` (`ENEMY_HURT`), `Mover`,
  `ActorVisual`, contact damage, `BossBrain`. Joins `Groups.ENEMIES`. Configured by `BossDef` through
  `setup(def, target, context)`. Chases its target unless it owns `State.Casting`. Not created by `EnemyFactory`:
  its death does not count as a kill and sends no `Event.Kill`; it emits `died` and the stage clears. Death cleanup as
  every actor (`asc.cleanup.call_deferred()`, abilities aborted).
- **Brain** `boss/BossBrain` (component): after `pattern_gap` seconds of chasing, picks the next pattern
  (`SEQUENCE` = list order, wrapping; `RANDOM` = weighted, never the same pattern twice in a row when there are
  alternatives) and activates it through the ASC by its ability tag; waits for that ability to end, then starts the
  next gap. `step(delta)` for tests.
- **Patterns** `boss/patterns/` — each an `ActorAbility` scene with `activation_owned_tags = State.Casting`, numbers
  as exported values, damage through `EffectBuilder.damage` with the boss as instigator (its `attack` applies):
  - `charge`: a `Telegraph` line toward the player for `warning`, then moves `distance` at `speed`; a contact
    `Hitbox` on `ALLY_HURT` (player only) damages once per charge.
  - `radial_burst`: fires `count` projectiles evenly around the boss using `boss_bolt` (`data/projectiles/`).
    `ProjectileDef` gains `hits: Team { ENEMIES, ALLIES }` (default ENEMIES) that sets the projectile's hit mask.
  - `summon`: spawns `count` of `enemy` on a ring of `radius` around the boss through `AbilityContext.summon`
    (a `Callable(def: EnemyDef, position: Vector2)` the stage injects; it calls `EnemyFactory.spawn`).
  - `ground_slam`: a `Telegraph` circle at the player's position, then an `AreaStrike` hitting the player only.
- **Data**: `defs/BossDef` (`id`, `display_name`, `scene`, `visual`, `visual_scale`, `tint`, `stats`, `contact_effect`,
  `contact_damage`, `contact_interval`, `body_radius`, `patterns: Array[BossPatternDef]`, `order`, `pattern_gap`,
  `validate()`); `defs/BossPatternDef` (`ability_scene`, `weight > 0`, `validate()`). `Stage.start` also checks that
  every pattern scene root is an `ActorAbility` (an abilities-layer check like Phase 2's `validate_defs`, because
  `defs/` cannot name `ActorAbility`).
- **Appearance** `stage/BossDirector`: on clock `ended`, if the stage has a boss: `StageFlow` → `BOSS`,
  `EventDirector.abort_running()`, spawns the boss on the spawn ring around the building, emits
  `boss_appeared(boss_health: Health, display_name: String)`; boss `died` → stage finishes CLEARED.
- `AbilityContext` gains `summon: Callable` (empty for the player).

## 7. Tickets in the shop

- `Wallet.Kind` gains `TICKET`; `Wallet.changed` becomes `changed(gold, stone, tickets)`; `Hud` and `AbilityBar`
  listeners are updated.
- `GradeService`: after the applicability check, the price of an applicable action is `Price(TICKET, 1)` whenever the
  wallet holds at least one ticket, otherwise the Phase 2 price. `promote_cost(i)` / `reroll_cost(i)` return the same
  price, so slot labels show the ticket and dimming stays correct. Atomic order unchanged (check → price → roll →
  spend → write); an `EMPTY_POOL` promote spends nothing.
- HUD wallet label: `Gold X   Stone Y   Ticket Z`.

## 8. UI (all bound by `Main`; the stage still knows nothing about `ui/`)

- `TimelineBar` draws wave marks and event marks in different styles; `Hud.bind` receives the event times; shows
  "BOSS" once `boss_appeared` fires.
- `EventBanner`: event name, progress text and remaining time while an event runs; "Success +rewards" / "Failed"
  briefly after it ends.
- `ObjectiveArrows`: edge-of-screen arrows toward the running event's off-screen `objectives()`.
- `BossBar`: name + health bar, shown on `boss_appeared`.
- `Stage` exposes `events: EventDirector` signals and `boss_appeared`; `Main` connects them to the HUD.

## 9. Modules and layers

| Layer | New / changed |
|---|---|
| core | `Wallet` (+TICKET) |
| defs | `EventDef`, `RewardDef`, `EventSchedule`, `BossDef`, `BossPatternDef`; `StageDef` (+`events`, +`boss`); `ProjectileDef` (+`hits`) |
| components | `Telegraph`, `AreaStrike`; `AbilityContext` (+`summon`) |
| actors | `statue/`, `boss/` (actor only); projectile hit mask from def |
| stage | `StageEvent`, `EventContext`, `EventDirector`, `BossDirector`; `StageClock` overtime; `Stage` wiring |
| events (extends stage) | `statue_event`, `meteor_event`, `elite_event` |
| boss (sibling of stage) | `BossBrain`, `patterns/` (charge, radial_burst, summon, ground_slam), pattern validation |
| abilities | `GradeService` (tickets); `MeleeStrike` on `AreaStrike` |
| ui | `TimelineBar`, `EventBanner`, `ObjectiveArrows`, `BossBar`, `Hud`, `AbilityBar` (wallet signal) |
| app | `Main` binds the new signals |

`stage/` instantiates event and boss scenes from defs and never imports `events/` or `boss/` scripts (parent §3.4).
`Stage` (composition root) may call the boss-layer pattern validation, as it does `abilities/`.
New tags: `State.Casting`, `Ability.Boss.Charge`, `Ability.Boss.Burst`, `Ability.Boss.Summon`, `Ability.Boss.Slam`
(single-word segments; see the Phase 2 tag rule).

## 10. Error handling

Bad data fails at `Stage.start` with every problem logged: infeasible event schedule, event controller root not a
`StageEvent`, pattern root not an `ActorAbility`, empty pattern list, non-positive weights/limits, negative or all-zero
rewards, invalid elite/boss stats. Nothing errors mid-run; a running event is aborted cleanly when the boss appears or
the run ends.

## 11. Testing & acceptance

TDD with the headless runner. Pure logic: `EventSchedule` rolling (seeded, gaps, clearance, infeasible rejected),
`Wallet` tickets, `GradeService` ticket-first pricing, `BossBrain` (sequence, weighted random without repeats, gaps).
Events, each driven by `step()`: success, failure, abort cleanup; `EventDirector`: starts at its time, one at a time,
reward on success, `abort_running`. Boss: each pattern's behaviour (charge moves and damages once, burst spawns N
ally-hitting projectiles, summon spawns M enemies through the factory, slam damages the player only after the
warning), boss death cleanup. `Stage` integration: clock end → BOSS → boss killed → CLEARED; no boss → CLEARED at time
up; boss appearance aborts a running event; event success credits the reward. UI: event marks, banner, boss bar,
ticket display. Editor acceptance with the godot-ai bridge, then the owner's manual play.

## 12. Out of scope

Boss phases/enrage, heal sources (so Phase 2's pre-heal follow-ups stay open), sound, gem rewards and meta
(Phase 4), new art (placeholders: statues and telegraphs are drawn shapes; the boss and elites reuse the orc visual).

## 13. Risks

- `drop_multiplier` 10000 in `stage_01` makes event currency rewards irrelevant during prototyping; tickets still
  matter.
- Auto-aim treats statues and the boss as enemies; statues may draw fire away from nearby enemies.
- The clock running past `duration` must not re-trigger waves/events (they only schedule inside `duration`) — pinned
  by tests.

## 14. As built (2026-10-13)

Decisions and deviations from the sections above:
- `Boss.setup(def, target, targets, projectile_layer, summon)` builds the boss's own `AbilityContext` (it needs the boss's `Mover`).
- `BossBrain` is not a node of `boss.tscn`: `actors/` and `stage/BossDirector` may not reference `boss/`. `BossDirector` emits `boss_spawned(boss)` and `Stage` (composition root, allowed to reference `boss/`) calls `BossBrain.attach_to(boss, rng)`, which also stops the brain on the boss's `died`.
- Scene-root checks fail at `Stage.start`: `EventDirector.validate_defs` (controller root is a `StageEvent`; defs cannot name stage classes), `BossDirector.validate_def` (boss scene root is `Boss`), `BossBrain.validate_def` (patterns are `ActorAbility`), and each controller/pattern `validate()` checks the root of the scenes it instantiates (`Statue`, `AreaStrike`, `Projectile`).
- Events get a dedicated `World/Events` layer.
- An event whose time is crossed while another runs waits and starts when the running one ends (deferred). With validated data (`min_gap` >= longest limit) this queue never triggers: it is a safety net. `abort_running()` emits `event_ended(def, false)` and drops waiting events; an abort from an `event_started` listener never begins the event.
- Controller ruling: `EventSchedule.validate_against` rejects `window_end + longest time_limit > duration`; stage_01's window is therefore 20-155 s instead of section 3's 20-160 s (allowed seconds end at 144 because of the 150 s wave clearance; 144 + 25 = 169 < 180).
- Elites are ordinary enemies once spawned and stay in the fight on abort and on time-up (deviation: they are not despawned).
- Meteors count only landed hits: a dash's invulnerability dodges the strike, so `Hits k/3` counts real hits.
- `AreaStrike` filters by actor (`only_actor`, compared to `Hurtbox.actor`), not by hurtbox.
- `Mover.cancel_dash` stops a charge dash when the pattern aborts; `ActorBody` (components) is the helper shared by `Enemy` and `Boss` for shape sizing, contact wiring and the death sequence.
- A ticket price shows as `1T` on the slot label; the first meteor drops one `interval` after the event begins.
- `fx_spawner` was not built (deferred).
- HUD entry points: `Stage.flow_state()` / `current_boss()` for state; `Hud.show_event`, `show_event_progress`, `show_event_result`, `show_boss` for display (wired by `Main`).
- Boundary agreed: an ability is an action an actor performs; an effect is a state change applied to an actor; stage code is progression (waves, events, boss timing).
