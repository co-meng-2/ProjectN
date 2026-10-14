# C# + Forge Port — Slice 4 (Events + Boss) Implementation Plan

> Executed inline with superpowers:executing-plans (TDD per task, one final whole-branch review). Every ported test names
> its GDScript source; exact numbers in the asserts come from that source.

**Goal:** Port Phase 3: stage events (`StageEvent`, `EventContext`, `EventDirector`; Statue, Meteor and Elite events),
the end-of-stage boss (`Boss` actor, `BossDirector`, `BossBrain`, four patterns), `Telegraph`, `Statue`, the defs
(`RewardDef`, `EventDef`, `EventSchedule`, `BossDef`), the HUD widgets (event banner, objective arrows, boss bar, timeline
event marks and BOSS label), the data (events, elite orc, boss bolt, Orc Warlord) and the `StageRoot`/`DevStage` wiring.

**Spec:** `docs/superpowers/specs/2026-10-14-csharp-forge-port-design.md` (sections 3, 4, 7.3 slice 4, 8.5). GDScript
reference: `src/stage/{stage_event,event_context,event_director,boss_director}.gd`, `src/events/`, `src/boss/`,
`src/actors/{boss,statue}/`, `src/components/telegraph.gd`, `src/defs/{reward_def,event_def,event_schedule,boss_def,
boss_pattern_def}.gd`, `src/ui/hud/{event_banner,objective_arrows,boss_bar,timeline_bar,hud}.gd`, `data/events/`,
`data/bosses/`; tests under `tests/stage`, `tests/events`, `tests/boss`, `tests/defs`, `tests/ui`, `tests/components`,
`tests/actors`.

## Global Constraints

As slices 2c/3: branch `port/slice-4`; explicit `git add`; commit trailer `Co-Authored-By: Claude Opus 5.5
<noreply@anthropic.com>`; `RUNNER=cs`; GDScript stays at 648; no `[GlobalClass]` on colliding names; namespace = top-level
folder; Godot signals for nodes, C# events for plain classes, plain events unsubscribed in `_ExitTree`; damage only
through `DamageEffects.Hit`; tags only via `GameplayTags`. Layer rules: `Stage/` never uses `Events`; only
`Stage/StageRoot.cs` uses `Boss` (`BossBrain.AttachTo`, `BossBrain.ValidateDef`); `Events` may use `Stage`. C# starts at 383.

## Decisions

1. **Pattern-ended notice (spec 8.5):** Forge 0.4.0 has `EntityAbilities.OnAbilityEnded(AbilityEndedData { Ability,
   WasCanceled })`. `ActorEntity` exposes it as `event Action<AbilityHandle, bool> AbilityEnded` (the Forge seam); the
   brain waits for the handle it activated.
2. **Patterns:** `Boss/Patterns/PatternDef : ActiveAbilityDef` (abstract; constructor sets `OwnedTags = [state.casting]`,
   `AutoFire = false`) + `PatternBehavior : IAbilityBehavior` (abstract): `OnStarted` -> `Begin()` (false ends at once),
   `OnUpdate(dt)` -> `Advance(dt)` while running, `Complete()` ends the instance, `OnEnded` calls `Abort()` when the
   instance ends before `Complete()` (boss death, cleanup). Patterns: `ChargeDef`, `GroundSlamDef`, `RadialBurstDef`,
   `SummonDef` with their behaviors; numbers as exports. The brain activates patterns through their `AbilityGrant`
   handles (not by tag); `BossBrain.ValidateDef` still requires registered, unique pattern tags.
3. **Boss def:** `BossDef { Id, DisplayName, Scene, Visual, VisualScale, Tint, Stats, ContactDamage, ContactInterval,
   BodyRadius, Patterns: Array<BossPatternEntry>, Order: BossOrder { Sequence, Random }, PatternGap }`;
   `BossPatternEntry { ActiveAbilityDef Pattern, float Weight }` (spec 4). Defs cannot name `PatternDef` (Boss layer),
   so `BossBrain.ValidateDef` checks each pattern is a `PatternDef` and its tags are registered.
4. **Scene root checks:** `Defs/IValidatable { List<string> Validate(); }`. `EventDef` instantiates its controller scene,
   requires a `Node2D` and calls `IValidatable.Validate()`; `EventDirector.ValidateDefs` requires `StageEvent`;
   `BossDirector.ValidateDef` requires `Boss`; event/pattern defs check the root types of scenes they instantiate
   (`AreaStrike`, `Statue`, `Projectile`) in `Validate`.
5. **Damage results:** `DamageEffects.Hit` returns `bool` (applied or refused); `AreaStrike.Struck` fires only for applied
   hits (slice 3 deferred minor; meteors count `Struck`).
6. **Boss wallet:** `AbilityContext.Wallet` becomes `Wallet?` (the boss has none); Bounty already skips without a context
   and now also without a wallet (spec open decision for slice 4).
7. **Meteor damage source:** the meteor event adds an `actor_entity.tscn` child as the GAS source of its strikes.

## Review Focus

1. An event aborted by the boss or the run end pays nothing, frees what it placed, and a waiting event never starts
   afterwards.
2. A boss that dies mid-pattern removes the pattern's telegraphs/strikes and stops the brain; the boss is not a kill.
3. Meteors that land while the player dashes (invulnerable) do not count as hits.
4. An event that ends inside a physics callback starts the next waiting event deferred, not inside the callback.
5. The boss bar and banner never touch a freed boss/event (restart mid-boss, HUD freed).

## Tasks

1. Seams: `DamageEffects.Hit -> bool`, `AreaStrike.Struck` gated, `AbilityContext.Wallet?`, `ActorEntity.AbilityEnded`.
2. `Components/Telegraph` (6 tests, `test_telegraph.gd`).
3. Defs: `IValidatable`, `RewardDef`, `EventDef`, `EventSchedule`, `BossOrder`, `BossPatternEntry`, `BossDef`;
   `StageDef.Events/Boss` (+ `validate_against` with wave times); tests from `test_event_def.gd` (7),
   `test_event_schedule.gd` (12), `test_boss_def.gd` (7), `test_stage_def.gd` event/boss parts.
4. `Actors/Statue` + scene (4, `test_statue.gd`).
5. `Stage/EventContext`, `Stage/StageEvent` + `tests/Support/ProbeEvent` (6, `test_stage_event.gd`).
6. `Stage/EventDirector` (9, `test_event_director.gd`).
7. `Events/StatueEvent`, `MeteorEvent`, `EliteEvent` + scenes (11 + 8 + 5).
8. `Actors/Boss` + scene, `Boss/Patterns/PatternDef`, `PatternBehavior`, the four patterns (4 + 4 + 6 + 4 + 4 + 4).
9. `Boss/BossBrain` (10) and `Stage/BossDirector` (5).
10. `StageRoot`: events, boss, summon, rewards, `BossAppeared`, `Events`/`CurrentBoss`; tests from `test_stage.gd`.
11. UI: `EventBanner` (8), `ObjectiveArrows` (4), `BossBar` (1), `TimelineBar` event marks + BOSS label (2), `Hud`
    (`Bind` takes event times; `ShowEvent`/`ShowEventProgress`/`ShowEventResult`/`ShowEventAborted`/`ShowBoss`; 4) and
    `DevStage` wiring.
12. Data: `elite_orc`, `boss_bolt`, `events/{statues,meteors,elites,stage_01_events}`, `bosses/orc_warlord`, scenes'
    numbers; stage_01 gets events and boss; `DataFilesTest` checks the spec numbers.
13. Notes (status, spec slice 4 notes, section 9 learning rows), full run, final review, fixes, merge.
