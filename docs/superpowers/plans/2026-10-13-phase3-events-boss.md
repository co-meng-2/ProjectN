# Phase 3 — Events + Boss Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** During a stage, three announced events (statues, meteors, elites) fire at seeded times shown on the timeline and pay gold, stone and promotion tickets on success; when the stage time runs out the Orc Warlord appears with four patterns (charge, radial burst, summon, ground slam), and killing it clears the stage.

**Architecture:** Stage progression stays plain stage code: `EventDirector` rolls the event times from `RunRng` (an `EventSchedule` on a 1-second grid) and runs one `StageEvent` controller scene at a time; `BossDirector` turns the clock's `ended` into the BOSS state and spawns a `Boss` actor. Things that act or get hit are GAS actors and effects: statues are actors with an ASC, meteors and slams are `AreaStrike`s applying `EffectBuilder.damage`, boss patterns are `BossPattern` (`ActorAbility`) scenes owning `State.Casting`, activated by tag from a `BossBrain` the Stage attaches. Every number is an exported value in a controller/pattern scene or a `.tres`; the HUD gets the new signals through `Main` only.

**Tech Stack:** Godot 4.7.2 (typed GDScript), GodotGAS v1.1.2 (vendored in `addons/`, read-only), the in-repo headless test runner (`tests/run_tests.tscn`, `tools/run_tests.sh`), godot-ai MCP bridge for the open editor.

**Spec:** `docs/superpowers/specs/2026-10-13-phase3-events-boss-design.md` (binding: §2 owner decisions, §3 numbers, §4–§10 rules) and the parent spec `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md` (§3.4 dependency rules, §2 GAS usage). Read both first; `CLAUDE.md` holds the day-to-day conventions.

## Global Constraints

- Godot **4.7.2**; typed GDScript everywhere (`var x: T`, `-> T`); **tabs** for indentation; `class_name` on every script another script or def references.
- **Never edit `addons/`** (vendored GodotGAS and godot_ai). Files in `godot_gas/` change only through `tools/sync_tags.gd`.
- Layers (a file may use only layers below it): `core → defs → gas → components → actors → stage / abilities / boss → ui → app`; `events/` extends `stage/`. Upward communication = signals. `stage/` never imports `events/` or `boss/` scripts — it instantiates `EventDef.controller_scene` / `BossDef.scene` / `BossPatternDef.ability_scene`. Only `Stage` (the composition root) may reference `abilities/` and `boss/` (it attaches the `BossBrain` and runs `BossBrain.validate_def`, as it runs `AbilityInstaller.validate_defs`).
- **Where GAS belongs (spec §2, agreed rule):** an *ability* is an action an actor with an ASC performs, triggered by its controller (input, AI brain, gameplay event) and constrained by tags/cooldowns; an *effect* is a state change applied to an actor; stage progression (waves, events, when the boss appears) is plain stage code. Boss patterns are `ActorAbility` scenes driven by `BossBrain`; events are `StageEvent` controllers that place GAS actors/effects and watch them.
- Damage always flows through `EffectBuilder.damage` → GAS `GameplayEffect` (`AreaStrike`/`Projectile` + `EffectApplier`). Boss patterns use the boss's ASC as source (its `attack` applies); meteors use a small environment ASC owned by the event.
- **No balance numbers in code.** Spec §3 numbers live in controller/pattern scene exports and `.tres` files: events per stage 3, window 20–155 s (controller ruling: the spec's 160 would let an event outlive the clock), `min_gap` 30 s, `wave_clearance` 5 s; statues 4 × 60 HP, ≥ 250 px from building and player, limit 25 s, reward 150 gold / 2 stone / 1 ticket; meteors duration 20 s, one every 1.2 s, warning 0.9 s, radius 60 px, 15 damage, landing ≤ 120 px from the player, `max_hits` 3, reward 100 / 3 / 1; elites 3 × elite orc (300 HP, speed 70, scale 1.8, red tint, target player, contact 15), limit 25 s, reward 200 / 2 / 1; boss "Orc Warlord" 2500 HP, speed 60, scale 3.0, dark tint, contact 20, `pattern_gap` 2.0 s, order RANDOM, weight 1 each; charge warning 0.7 s, 520 px/s, 360 px, 25 damage; radial burst 12 projectiles, 220 px/s, lifetime 2.0 s, 10 damage; summon 4 orc swarmers on an 80 px ring; ground slam warning 1.0 s at the player's position, radius 90 px, 30 damage.
- New gameplay tags (single-word segments; the GodotGAS registry lowercases segment tails): `State.Casting`, `Ability.Boss.Charge`, `Ability.Boss.Burst`, `Ability.Boss.Summon`, `Ability.Boss.Slam`; referenced only as `GameplayTags.X`. Each is added to `TAGS` in `tools/sync_tags.gd` in the task that first needs it.
- Every def has `validate() -> PackedStringArray` that never crashes on bad data; `Stage.start` aggregates every error, logs them and refuses to start (spec §10). Nothing errors mid-run.
- Every actor has an `AbilitySystemComponent` child named exactly that (instance `src/gas/actor_asc.tscn`); stats are applied after the node is in the tree; `asc.cleanup.call_deferred()` before freeing.
- Physics objects are added with `call_deferred` from physics *callbacks* (signals such as `area_entered`, health `died` fired by a hit); adding them from `_physics_process` / a `step()` driven by it is fine.
- Time-driven code exposes `step(delta)`; tests call it after `set_physics_process(false)`, which must come **after** the node is in the tree (Godot re-enables physics processing at READY when `_physics_process` is overridden).
- Tests must be able to fail: non-default values, positions away from (0, 0), ordering-sensitive cases, state not already true before the action. GDScript lambdas capture ints by value — count with an `Array`/`Dictionary`. Shared rigs go in `tests/support/def_factory.gd` / `actor_fixture.gd`, not copied between files.
- No new art: statues, telegraphs and arrows are drawn shapes; the boss and elites reuse the orc visual (tint + scale); the boss bolt reuses `assets/art/tinyrpg/arrow.png`. No icons are copied.
- `.tres`/`.tscn` files that the editor already resaved carry `uid=` in their headers and no `load_steps`: edits insert lines and keep the uids. New files are written without uids (the editor adds them on its next save).
- Do not shadow `Object`/`Node`/`Control` members (no `show`, `update`, `size`, `remove_child`-like names on Nodes).
- Commit after every task, including the generated `*.gd.uid` files. Commit messages end with: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## How tests run

```bash
export GODOT_BIN="/c/Users/wo248/OneDrive/바탕 화면/Godot_v4.7.2-stable_win64.exe/Godot_v4.7.2-stable_win64.exe"
TEST_SUITE_TIMEOUT=240 tools/run_tests.sh                                  # whole suite (281 passed before this phase)
FILTER=tests/stage/test_event_director TEST_SUITE_TIMEOUT=240 tools/run_tests.sh   # one file (substring of its path)
"$GODOT_BIN" --headless --path . -s res://tools/sync_tags.gd               # after editing TAGS
```
The Godot editor is **open** during this work (MCP bridge `godot-ai`). After creating a new `class_name` script, scene or asset, or after a tag sync, call godot-ai `filesystem_manage` with `{"op": "scan"}` and wait for it to finish **before** running the headless suite; otherwise the runner fails with `Could not find type ...` / `Parse Error`. Never run `IMPORT=1` / `--import` while the editor is open. Output lines are `PASS  file::test` / `FAIL  file::test` + reasons, then `---- N passed, M failed ----`; any `SCRIPT ERROR` / `Parse Error` fails the run (`!! script errors detected`). The "Run to verify failure" steps expect exactly that parse failure when a test references a class that does not exist yet.

## Review Focus

Failure modes the spec implies that a happy-path test would miss; each has a pinning test in the owning task:

1. **Two events close together, or the boss arriving mid-event** — at most one event runs; a later one waits for the running one (never overlaps, never silently vanishes); the boss's arrival aborts the running event with no reward, removes its objects and drops anything waiting (Task 6 `test_events_run_one_at_a_time`, `test_abort_running_ends_without_reward_and_drops_waiting_events`; Task 14 `test_boss_appearance_aborts_a_running_event_without_reward`).
2. **An event ends exactly once** — a last statue destroyed, a meteor landing or an elite dying after the event already succeeded, failed or was aborted changes nothing and emits nothing (Task 6 `test_success_ends_exactly_once`, `test_abort_cleans_up_without_a_result`; Task 8 `test_a_hit_after_the_event_ended_changes_nothing`; Task 9 `test_time_up_fails_and_the_survivors_stay`).
3. **Tickets come first, and only for applicable actions** — with a ticket in the wallet every applicable action (buy, promote, reveal, reroll) costs the ticket and no gold/stone; refused actions (empty pool, revealed slot, empty-slot reroll) never spend it; labels and dimming follow (Task 1 `test_a_ticket_pays_for_a_purchase_before_any_gold`, `test_one_ticket_pays_any_shop_action_even_without_currency`, `test_tickets_are_not_spent_on_refused_actions`, `test_a_held_ticket_prices_every_applicable_slot_at_one_ticket`).
4. **Overtime** — after `ended` the clock keeps ticking: the trickle continues at its end interval, waves and events never fire again, a stage without a boss still clears at time up, and the boss fight has no time limit (Task 2 `test_ended_fires_once_and_the_clock_keeps_ticking_into_overtime`, `test_waves_never_fire_again_in_overtime`, `test_trickle_continues_in_overtime_at_the_end_interval`; Task 6 `test_no_event_starts_in_overtime`; Task 14 `test_time_up_brings_the_boss_and_its_death_clears_the_stage`, `test_without_a_boss_time_up_clears_at_once`).
5. **The boss dying mid-pattern** — telegraphs and strikes are removed, `State.Casting` comes off, the brain stops, nothing keeps firing (Task 10 `test_death_aborts_patterns_leaves_the_group_and_frees_itself`; Task 12 `test_abort_during_the_warning_removes_the_telegraph`; Task 11 `test_stop_halts_the_brain`).

---

## File Structure

```
src/core/wallet.gd                 (modify)  + Kind.TICKET, tickets, changed(gold, stone, tickets)
src/defs/reward_def.gd                       RewardDef: gold, stone, tickets, validate()
src/defs/event_def.gd                        EventDef: id, display_name, icon, controller_scene, time_limit, reward, validate()
src/defs/event_schedule.gd                   EventSchedule: count, window, min_gap, wave_clearance, pool; allowed_seconds(),
                                             roll_times(), earliest_times(), roll_defs(), validate_against()
src/defs/boss_pattern_def.gd                 BossPatternDef: ability_scene, weight, validate()
src/defs/boss_def.gd                         BossDef: Order {SEQUENCE, RANDOM}, actor fields, patterns, pattern_gap, validate()
src/defs/stage_def.gd              (modify)  + events: EventSchedule, boss: BossDef (validated)
src/defs/projectile_def.gd         (modify)  + Team {ENEMIES, ALLIES}, hits, hit_mask()
src/components/telegraph.gd                  Telegraph: warning circle/line for N seconds, step(), elapsed, frees itself
src/components/area_strike.gd/.tscn          AreaStrike: short-lived circular hit, target mask + only_actor filter, struck(hurtbox)
src/components/ability_context.gd  (modify)  + summon: Callable
src/actors/projectile/projectile.gd (modify) hitbox mask from ProjectileDef.hit_mask()
src/actors/statue/statue.gd/.tscn            Statue: ASC + Health + Hurtbox(ENEMY_HURT), enemies group, destroyed, remove()
src/actors/boss/boss.gd/.tscn                Boss: chases, contact damage, grants its pattern abilities, stands while casting, died
src/stage/stage_clock.gd           (modify)  ended once, then overtime ticks; has_ended()
src/stage/stage_event.gd                     StageEvent (abstract): begin/abort/step, time limit, succeeded/failed/progress_changed
src/stage/event_context.gd                   EventContext: layer, factory, targets, player, building, rng, bounds, spawn_radius, def
src/stage/event_director.gd                  EventDirector: rolls times, one event at a time, relays signals, abort_running(), validate_defs()
src/stage/boss_director.gd                   BossDirector: appear() on time up, boss_spawned/boss_appeared/boss_defeated
src/stage/stage.gd/.tscn           (modify)  World/Events layer, EventDirector, BossDirector, rewards, brain attach, summon, flow_state()
src/events/statue_event.gd/.tscn             StatueEvent
src/events/meteor_event.gd/.tscn             MeteorEvent
src/events/elite_event.gd/.tscn              EliteEvent
src/boss/boss_brain.gd                       BossBrain: gap, SEQUENCE/RANDOM picks, activation by tag, attach_to(), validate_def()
src/boss/patterns/boss_pattern.gd            BossPattern (abstract ActorAbility): _begin/_advance/_complete/_abort phases, step()
src/boss/patterns/charge_pattern.gd/.tscn    ChargePattern
src/boss/patterns/radial_burst_pattern.gd/.tscn  RadialBurstPattern
src/boss/patterns/summon_pattern.gd/.tscn    SummonPattern
src/boss/patterns/ground_slam_pattern.gd/.tscn   GroundSlamPattern
src/abilities/grade_service.gd     (modify)  ticket-first pricing
src/abilities/blade_sweep/melee_strike.gd (modify)  MeleeStrike extends AreaStrike
src/ui/hud/ability_slot_view.gd    (modify)  ticket cost text "1T"
src/ui/hud/ability_bar.gd          (modify)  3-argument wallet signal
src/ui/hud/timeline_bar.gd         (modify)  event marks (circles) + "BOSS" label
src/ui/hud/event_banner.gd                   EventBanner: name, progress, remaining time; result line
src/ui/hud/objective_arrows.gd               ObjectiveArrows: edge arrows toward off-screen objectives
src/ui/hud/boss_bar.gd                       BossBar: boss name + health
src/ui/hud/hud.gd/.tscn            (modify)  wallet "Ticket Z", event times, show_event*/show_boss
src/app/main.gd                    (modify)  binds event + boss signals to the HUD
tools/sync_tags.gd                 (modify)  + the five tags
data/enemies/elite_orc.tres                  the elite enemy
data/projectiles/boss_bolt.tres              ally-hitting bolt (arrow texture)
data/events/{statues,meteors,elites}.tres    EventDefs with rewards
data/events/stage_01_events.tres             EventSchedule of stage_01
data/bosses/orc_warlord.tres                 BossDef with the four patterns
data/stages/stage_01.tres          (modify)  events + boss
tests/support/def_factory.gd       (modify)  packed(), reward(), event(), schedule(), boss()
tests/support/actor_fixture.gd     (modify)  kill(), event_context(), boss()
tests/support/probe_event.gd/.tscn           ProbeEvent test double (StageEvent)
tests/support/probe_pattern.gd/.tscn         ProbePattern test double (BossPattern)
tests/...                                    one test file per new unit (listed per task)
```

Decisions this plan makes where the spec leaves room (Task 16 records them in the spec's "As built" section):
- `Boss.setup(def, target, targets, projectile_layer, summon)` builds the boss's own `AbilityContext` (it needs the boss's `Mover`), instead of receiving a ready context.
- The `BossBrain` is not a node of `boss.tscn`: `actors/` may not reference `boss/`, and `stage/BossDirector` may not either. `BossDirector` emits `boss_spawned(boss)` and `Stage` (composition root) calls `BossBrain.attach_to(boss, rng)`.
- `EventDef.validate()` checks the controller root is a `Node2D` and runs its `validate()`; the "root is a `StageEvent`" check is `EventDirector.validate_defs(schedule)`, run by `Stage.start` (defs cannot name stage classes — the same split the spec prescribes for boss patterns).
- An event whose time is crossed while another runs **waits** and starts when the running one ends (deferred, since events often end inside physics callbacks). This is only a safety net: with validated data (`min_gap` >= the longest time limit) it never triggers. `abort_running()` emits `event_ended(def, false)` and drops waiting events.
- (Controller ruling) `EventSchedule.validate_against` also rejects `window_end + longest time_limit > duration`, so every event can finish before the clock ends; stage_01's window is therefore 20–155 s (allowed seconds end at 144 because of the 150 s wave clearance; 144 + 25 = 169 < 180) instead of the spec's 20–160 s.
- Abort removes event-owned objects (statues, meteor warnings); elites are ordinary enemies once spawned and stay in the fight on abort, as on time up.
- `StageEvent` helpers: `report_progress(text)`, `_succeed()`, `_fail()`, `_on_step(delta)`; `Telegraph` is visual only (owners keep their own timers so `step()` stays deterministic).
- `AreaStrike` filters by actor (`only_actor`, compared to `Hurtbox.actor`) rather than by a hurtbox reference (the player's hurtbox is private).
- A ticket price shows as `1T` on the slot label; the HUD wallet line reads `Gold X   Stone Y   Ticket Z`.
- The first meteor drops one `interval` after the event begins.

---

### Task 1: Promotion tickets — Wallet, ticket-first GradeService pricing, HUD wallet line

**Files:**
- Modify: `src/core/wallet.gd`, `src/abilities/grade_service.gd`, `src/ui/hud/ability_slot_view.gd`, `src/ui/hud/ability_bar.gd`, `src/ui/hud/hud.gd`
- Test: `tests/core/test_wallet.gd` (modify one test, append), `tests/abilities/test_grade_service.gd` (append), `tests/ui/test_ability_bar.gd` (append), `tests/ui/test_hud.gd` (modify one test)

**Interfaces:**
- Consumes: `Price(kind, amount)` (`src/core/price.gd`), `GradeTable.promote_price/reroll_price`, `Loadout.slot/set_slot`, the existing `GradeService` atomic order (check → price → roll → spend → write).
- Produces:
  - `Wallet.Kind { GOLD, STONE, TICKET }` (appended, existing values unchanged), `Wallet.tickets: int`, `signal changed(gold: int, stone: int, tickets: int)`; `balance/add/try_spend` accept `TICKET`.
  - `GradeService.promote_cost(i)` / `reroll_cost(i)`: `null` when not applicable (unchanged), otherwise `Price(TICKET, 1)` while `wallet.tickets >= 1`, else the table price. `promote()`/`reroll()` spend whatever those return.
  - `AbilitySlotView.cost_text(price)`: `"%d" + {"g", "s", "T"}[kind]`, `"MAX"` for null.
  - HUD wallet label: `Gold X   Stone Y   Ticket Z`.

- [ ] **Step 1: Write the failing tests**

In `tests/core/test_wallet.gd` replace `test_add_increases_balance_and_emits_changed` with:
```gdscript
func test_add_increases_balance_and_emits_changed() -> void:
	var wallet := Wallet.new()
	var emitted: Array = []
	wallet.changed.connect(func(gold: int, stone: int, tickets: int) -> void: emitted.append([gold, stone, tickets]))
	wallet.add(Wallet.Kind.GOLD, 5)
	wallet.add(Wallet.Kind.STONE, 2)
	assert_eq(wallet.gold, 5)
	assert_eq(wallet.stone, 2)
	assert_eq(emitted, [[5, 0, 0], [5, 2, 0]])
```
and append:
```gdscript
func test_tickets_are_a_third_balance() -> void:
	var wallet := Wallet.new()
	var emitted: Array = []
	wallet.changed.connect(func(gold: int, stone: int, tickets: int) -> void: emitted.append([gold, stone, tickets]))
	wallet.add(Wallet.Kind.TICKET, 2)
	assert_eq(wallet.tickets, 2)
	assert_eq(wallet.balance(Wallet.Kind.TICKET), 2)
	assert_eq(wallet.gold, 0, "tickets are not gold")
	assert_true(wallet.try_spend(Wallet.Kind.TICKET, 1))
	assert_false(wallet.try_spend(Wallet.Kind.TICKET, 2), "only one left")
	assert_eq(wallet.tickets, 1)
	assert_eq(emitted, [[0, 0, 2], [0, 0, 1]])
```

Append to `tests/abilities/test_grade_service.gd`:
```gdscript
func test_a_ticket_pays_for_a_purchase_before_any_gold() -> void:
	var service := _build(_single(), 500, 0)
	_wallet.add(Wallet.Kind.TICKET, 1)
	var cost := service.promote_cost(0)
	assert_eq(cost.kind, Wallet.Kind.TICKET)
	assert_eq(cost.amount, 1)
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(_loadout.slot(0).grade, Grade.Value.COMMON)
	assert_eq(_wallet.tickets, 0)
	assert_eq(_wallet.gold, 500, "gold untouched while a ticket was held")
	assert_eq(service.promote_cost(0).kind, Wallet.Kind.GOLD, "back to the table price")
	assert_eq(service.promote_cost(0).amount, 150)


func test_one_ticket_pays_any_shop_action_even_without_currency() -> void:
	var defs := _pair()
	var service := _build(defs, 0, 0)
	_wallet.add(Wallet.Kind.TICKET, 3)
	_loadout.set_slot(0, defs[0], Grade.Value.LEGENDARY)
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK, "reveal for a ticket")
	assert_eq(_loadout.slot(0).grade, Grade.Value.REVEALED)
	assert_eq(_wallet.tickets, 2)
	_loadout.set_slot(1, defs[0], Grade.Value.EPIC)
	assert_eq(service.reroll(1).outcome, GradeResult.Outcome.OK, "reroll for a ticket")
	assert_eq(_loadout.slot(1).def, defs[1])
	assert_eq(_wallet.tickets, 1)
	assert_eq(service.promote(1).outcome, GradeResult.Outcome.OK, "gold promotion for a ticket")
	assert_eq(_loadout.slot(1).grade, Grade.Value.UNIQUE)
	assert_eq(_wallet.tickets, 0)
	assert_eq(service.promote(1).outcome, GradeResult.Outcome.NOT_ENOUGH, "no ticket and no gold left")


func test_tickets_are_not_spent_on_refused_actions() -> void:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"rare_only", null, null, Grade.Value.RARE)]
	var service := _build(defs, 0, 0)
	_wallet.add(Wallet.Kind.TICKET, 1)
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.EMPTY_POOL, "nothing can be COMMON")
	assert_null(service.reroll_cost(0), "an empty slot cannot be rerolled, ticket or not")
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	_loadout.set_slot(1, defs[0], Grade.Value.REVEALED)
	assert_null(service.promote_cost(1))
	assert_eq(service.promote(1).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_eq(_wallet.tickets, 1, "never spent on a refused action")
```

Append to `tests/ui/test_ability_bar.gd`:
```gdscript
func test_a_held_ticket_prices_every_applicable_slot_at_one_ticket() -> void:
	var rig := _rig(0, 0)
	var bar: AbilityBar = rig["bar"]
	var wallet: Wallet = rig["wallet"]
	var loadout: Loadout = rig["loadout"]
	loadout.set_slot(1, rig["def"], Grade.Value.REVEALED)
	assert_eq(_cost_text(bar.slot_view(0)), "100g")
	assert_true(bar.slot_view(0).is_dimmed(), "no gold")
	wallet.add(Wallet.Kind.TICKET, 1)
	assert_eq(_cost_text(bar.slot_view(0)), "1T")
	assert_eq(_cost_text(bar.slot_view(2)), "1T")
	assert_false(bar.slot_view(0).is_dimmed(), "the ticket covers it")
	assert_eq(_cost_text(bar.slot_view(1)), "MAX", "nothing to buy on a revealed slot")
	assert_true(bar.slot_view(0).tooltip_text.contains("1T"))
	wallet.try_spend(Wallet.Kind.TICKET, 1)
	assert_eq(_cost_text(bar.slot_view(0)), "100g", "back to gold once the ticket is gone")
```

In `tests/ui/test_hud.gd` replace `test_wallet_label_follows_the_wallet` with:
```gdscript
func test_wallet_label_follows_the_wallet() -> void:
	var rig := _rig()
	var hud: Hud = rig["hud"]
	var wallet: Wallet = rig["wallet"]
	var label := hud.get_node("%WalletLabel") as Label
	assert_eq(label.text, "Gold 0   Stone 0   Ticket 0")
	wallet.add(Wallet.Kind.GOLD, 7)
	wallet.add(Wallet.Kind.STONE, 2)
	wallet.add(Wallet.Kind.TICKET, 1)
	assert_eq(label.text, "Gold 7   Stone 2   Ticket 1")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/core/test_wallet TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `Wallet.Kind.TICKET` / `tickets` do not exist (`!! script errors detected`).

- [ ] **Step 3: Implement**

`src/core/wallet.gd` (whole file):
```gdscript
class_name Wallet
extends RefCounted
## Per-run balances: gold, stone and promotion tickets (paid by stage events; one ticket pays any shop action).
## Gem is persistent meta currency and lives elsewhere (Phase 4).

signal changed(gold: int, stone: int, tickets: int)

enum Kind { GOLD, STONE, TICKET }

var gold: int = 0
var stone: int = 0
var tickets: int = 0


func balance(kind: Kind) -> int:
	match kind:
		Kind.GOLD:
			return gold
		Kind.STONE:
			return stone
	return tickets


func add(kind: Kind, amount: int) -> void:
	if amount <= 0:
		return
	_apply(kind, balance(kind) + amount)


func try_spend(kind: Kind, amount: int) -> bool:
	if amount < 0 or balance(kind) < amount:
		return false
	_apply(kind, balance(kind) - amount)
	return true


func _apply(kind: Kind, value: int) -> void:
	match kind:
		Kind.GOLD:
			gold = value
		Kind.STONE:
			stone = value
		Kind.TICKET:
			tickets = value
	changed.emit(gold, stone, tickets)
```

`src/abilities/grade_service.gd` — replace the class comment's last sentence and the two cost functions, and add `_ticket_first` (the rest of the file is unchanged):
```gdscript
## The shop rules: promote (NONE->COMMON buys; COMMON..UNIQUE re-roll from the next grade's pool;
## LEGENDARY->REVEALED keeps the ability) and reroll (same grade, a different ability preferred).
## Pure logic over Wallet, RunRng, Loadout, AbilityPool and GradeTable. Every action is atomic:
## check applicability, check the price, roll, spend, then write the slot. A refused action changes nothing.
## While the wallet holds a promotion ticket, every applicable action costs exactly one ticket.
```
```gdscript
## Price of the next promotion of slot `index`; null when none is possible.
func promote_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	if slot == null:
		return null
	return _ticket_first(_table.promote_price(slot.grade))


## Price of rerolling slot `index`; null when the slot is empty or revealed.
func reroll_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	if slot == null or slot.is_empty():
		return null
	return _ticket_first(_table.reroll_price(slot.grade))
```
and add at the end of the file:
```gdscript
## Applicability comes first (a null price stays null); then a held ticket replaces the table price.
func _ticket_first(price: Price) -> Price:
	if price == null or _wallet.tickets < 1:
		return price
	return Price.new(Wallet.Kind.TICKET, 1)
```

`src/ui/hud/ability_slot_view.gd` — add the constant next to the other constants and replace `cost_text`:
```gdscript
## Label suffix per Wallet.Kind (GOLD, STONE, TICKET).
const KIND_SUFFIXES: PackedStringArray = ["g", "s", "T"]
```
```gdscript
static func cost_text(price: Price) -> String:
	if price == null:
		return "MAX"
	return "%d%s" % [price.amount, KIND_SUFFIXES[price.kind]]
```

`src/ui/hud/ability_bar.gd` — replace the wallet handler:
```gdscript
func _on_wallet_changed(_gold: int, _stone: int, _tickets: int) -> void:
	refresh_all()
```

`src/ui/hud/hud.gd` — replace the wallet lines in `bind` and the handler:
```gdscript
	wallet.changed.connect(_on_wallet_changed)
	_on_wallet_changed(wallet.gold, wallet.stone, wallet.tickets)
```
```gdscript
func _on_wallet_changed(gold: int, stone: int, tickets: int) -> void:
	_wallet_label.text = "Gold %d   Stone %d   Ticket %d" % [gold, stone, tickets]
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/core/test_wallet TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`, then `FILTER=tests/abilities/test_grade_service ...`, `FILTER=tests/ui ...`
Expected: 5 + 16 + all ui tests PASS. Likely snag: a remaining 2-argument listener on `Wallet.changed` fails at emit time with "Method expected 2 arguments" — `grep -rn "changed.connect" src tests` and fix every wallet listener (only `ability_bar.gd`, `hud.gd`, `test_wallet.gd` exist today).

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS (281 + 5).
```bash
git add src/core src/abilities src/ui tests/core tests/abilities tests/ui
git commit -q -m "feat(shop): add promotion tickets to the wallet; one ticket pays any applicable shop action first

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: StageClock overtime

**Files:**
- Modify: `src/stage/stage_clock.gd`
- Test: `tests/stage/test_stage_clock.gd` (replace one test, append), `tests/stage/test_wave_director.gd` (append), `tests/stage/test_spawn_director.gd` (append)

**Interfaces:**
- Consumes: `WaveDirector` (crossed marks only, marks < duration), `SpawnDirector.current_interval` (progress already clamped to 1).
- Produces: `StageClock.ended` fires once when `elapsed >= duration`; afterwards `step()` keeps adding to `elapsed` and emitting `ticked` until `stop()`; `has_ended() -> bool`; `start()` clears it. `Stage` still finishes CLEARED on `ended` (Task 14 adds the boss branch), and `_finish` stops the clock.

- [ ] **Step 1: Write the failing tests**

In `tests/stage/test_stage_clock.gd` replace `test_ended_fires_once_and_elapsed_is_clamped` with:
```gdscript
func test_ended_fires_once_and_the_clock_keeps_ticking_into_overtime() -> void:
	var clock := _clock()
	clock.set_physics_process(false)
	var ended: Array = []
	var seen: Array = []
	clock.ended.connect(func() -> void: ended.append(true))
	clock.ticked.connect(func(elapsed: float) -> void: seen.append(elapsed))
	clock.start(2.0)
	clock.step(1.0)
	assert_false(clock.has_ended())
	clock.step(4.0)
	assert_eq(ended.size(), 1)
	assert_true(clock.has_ended())
	assert_true(clock.is_running(), "overtime: the boss fight runs on the same clock")
	clock.step(1.5)
	assert_eq(ended.size(), 1, "ended fires once")
	assert_almost_eq(clock.elapsed, 6.5)
	assert_eq(seen, [1.0, 5.0, 6.5])
```
and append:
```gdscript
func test_start_clears_the_ended_state() -> void:
	var clock := _clock()
	clock.set_physics_process(false)
	var ended: Array = []
	clock.ended.connect(func() -> void: ended.append(true))
	clock.start(1.0)
	clock.step(2.0)
	clock.start(1.0)
	assert_false(clock.has_ended())
	clock.step(1.0)
	assert_eq(ended.size(), 2, "a restarted clock ends again")
```

Append to `tests/stage/test_wave_director.gd`:
```gdscript
func test_waves_never_fire_again_in_overtime() -> void:
	var entries: Array[WaveEntry] = [_entry(1.0, 2, "a"), _entry(9.5, 1, "b")]
	var rig := _rig(entries)
	var clock: StageClock = rig[0]
	clock.set_physics_process(false)
	var labels: Array = []
	(rig[2] as WaveDirector).wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	clock.step(10.5)
	assert_eq(labels, ["a", "b"])
	for i in 5:
		clock.step(3.0)
	assert_almost_eq(clock.elapsed, 25.5, 0.001, "the clock ran on past the 10 s stage")
	assert_eq(labels, ["a", "b"], "no wave re-fires in overtime")
	assert_eq((rig[1] as EnemyFactory).alive_count(), 3)
```

Append to `tests/stage/test_spawn_director.gd`:
```gdscript
func test_trickle_continues_in_overtime_at_the_end_interval() -> void:
	var def := _stage(10)
	def.duration = 2.0
	def.trickle_interval_start = 3.0
	def.trickle_interval_end = 1.0
	var rig := _rig(def)
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	var director: SpawnDirector = rig[2]
	clock.set_physics_process(false)
	assert_almost_eq(director.current_interval(25.0), 1.0, 0.001, "clamped to the end interval")
	clock.step(2.5)
	assert_eq(factory.alive_count(), 1)
	clock.step(1.0)
	assert_eq(factory.alive_count(), 2, "spawns keep coming after the stage time")
	clock.step(1.0)
	assert_eq(factory.alive_count(), 3)
	assert_true(clock.elapsed > def.duration)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/stage/test_stage_clock TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `has_ended` not found.

- [ ] **Step 3: Implement**

`src/stage/stage_clock.gd` (whole file):
```gdscript
class_name StageClock
extends Node
## Elapsed stage time. Everything time-driven (spawning, waves, events, HUD) subscribes to `ticked`.
## `ended` fires once when `elapsed` reaches `duration`; the clock then keeps ticking (overtime: the boss
## fight) until stopped, so the trickle continues while waves and events, scheduled inside `duration`,
## never fire again.

signal ticked(elapsed: float)
signal ended

var elapsed: float = 0.0
var duration: float = 0.0
var _running: bool = false
var _ended: bool = false


func start(p_duration: float) -> void:
	duration = p_duration
	elapsed = 0.0
	_running = true
	_ended = false


func stop() -> void:
	_running = false


func is_running() -> bool:
	return _running


func has_ended() -> bool:
	return _ended


func step(delta: float) -> void:
	if not _running:
		return
	elapsed += delta
	ticked.emit(elapsed)
	if not _ended and elapsed >= duration:
		_ended = true
		ended.emit()


func _physics_process(delta: float) -> void:
	step(delta)
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/stage TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: every stage test PASSES, including the Phase 1 `test_run_reaches_cleared_when_time_is_up` (Stage still finishes on `ended` and `_finish` stops the clock; `elapsed` is the first tick past 1.5 s, inside its 0.15 tolerance).

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/stage tests/stage
git commit -q -m "feat(stage): let the clock run into overtime after ended so the boss fight shares it

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: RewardDef, EventDef, EventSchedule (seeded 1-second-grid rolling + feasibility), StageDef.events

**Files:**
- Create: `src/defs/reward_def.gd`, `src/defs/event_def.gd`, `src/defs/event_schedule.gd`
- Modify: `src/defs/stage_def.gd`, `tests/support/def_factory.gd` (add `packed`, `reward`, `event`, `schedule`)
- Test: `tests/defs/test_event_def.gd` (new), `tests/defs/test_event_schedule.gd` (new), `tests/defs/test_stage_def.gd` (append)

**Interfaces:**
- Consumes: `RunRng.randi_range(from, to)` (inclusive), `RunRng.pick(items)`, `WaveSchedule.times()`.
- Produces:
  - `RewardDef` (Resource): `gold: int`, `stone: int`, `tickets: int` (all default 0); `validate()` — negatives and an all-zero reward are errors.
  - `EventDef` (Resource): `id: StringName`, `display_name: String`, `icon: Texture2D` (optional), `controller_scene: PackedScene`, `time_limit: float`, `reward: RewardDef`; `validate()` instantiates the controller (not in the tree), requires a `Node2D` root, appends the root's own `validate()` when it has one, frees it.
  - `EventSchedule` (Resource): `count: int`, `window_start: float`, `window_end: float`, `min_gap: float`, `wave_clearance: float`, `pool: Array[EventDef]`;
    `allowed_seconds(duration: float, wave_times: PackedFloat32Array) -> PackedInt32Array`;
    `roll_times(duration, wave_times, rng: RunRng) -> PackedFloat32Array` (ascending whole seconds, `count` of them, or empty when infeasible);
    `earliest_times(duration, wave_times) -> PackedFloat32Array` (greedy earliest placement; shorter than `count` when infeasible);
    `roll_defs(amount: int, rng: RunRng) -> Array[EventDef]` (never the same def twice in a row when the pool has alternatives);
    `longest_time_limit() -> float`; `validate_against(duration, wave_times) -> PackedStringArray` (also an error when `window_end + longest_time_limit() > duration`: every event must be able to finish before the clock ends).
  - `StageDef.events: EventSchedule` (optional), validated against `duration` and `waves.times()`.
  - `DefFactory.packed(node: Node) -> PackedScene` (packs then frees the node), `DefFactory.reward(gold = 150, stone = 2, tickets = 1) -> RewardDef`, `DefFactory.event(scene: PackedScene, time_limit: float = 20.0, id: StringName = &"probe") -> EventDef` (display name = `String(id).capitalize()`, reward = `reward()`), `DefFactory.schedule(pool: Array[EventDef], count: int = 1, window_start: float = 10.0, window_end: float = 50.0, min_gap: float = 20.0, wave_clearance: float = 0.0) -> EventSchedule`.

- [ ] **Step 1: Add the factory helpers**

Append to `tests/support/def_factory.gd`:
```gdscript
## Packs `node` into a scene and frees it (the scene keeps its own copy).
static func packed(node: Node) -> PackedScene:
	var scene := PackedScene.new()
	scene.pack(node)
	node.free()
	return scene


static func reward(gold: int = 150, stone: int = 2, tickets: int = 1) -> RewardDef:
	var result := RewardDef.new()
	result.gold = gold
	result.stone = stone
	result.tickets = tickets
	return result


static func event(scene: PackedScene, time_limit: float = 20.0, id: StringName = &"probe") -> EventDef:
	var def := EventDef.new()
	def.id = id
	def.display_name = String(id).capitalize()
	def.controller_scene = scene
	def.time_limit = time_limit
	def.reward = reward()
	return def


static func schedule(pool: Array[EventDef], count: int = 1, window_start: float = 10.0, window_end: float = 50.0, min_gap: float = 20.0, wave_clearance: float = 0.0) -> EventSchedule:
	var result := EventSchedule.new()
	result.pool = pool
	result.count = count
	result.window_start = window_start
	result.window_end = window_end
	result.min_gap = min_gap
	result.wave_clearance = wave_clearance
	return result
```

- [ ] **Step 2: Write the failing tests**

`tests/defs/test_event_def.gd`:
```gdscript
extends TestCase


func _scene() -> PackedScene:
	return DefFactory.packed(Node2D.new())


func test_reward_must_pay_something_and_nothing_negative() -> void:
	assert_eq(RewardDef.new().validate().size(), 1, "all zero pays nothing")
	assert_no_errors(DefFactory.reward(0, 0, 1).validate(), "a single ticket is a reward")
	assert_eq(DefFactory.reward(150, -1, 1).validate().size(), 1, "negative stone")


func test_valid_event_def_reports_no_errors() -> void:
	assert_no_errors(DefFactory.event(_scene(), 25.0).validate(), "event")


func test_event_def_needs_id_name_scene_limit_and_reward() -> void:
	var errors := EventDef.new().validate()
	assert_eq(errors.size(), 5, ", ".join(errors))


func test_reward_errors_are_reported_with_the_event() -> void:
	var def := DefFactory.event(_scene())
	def.reward = DefFactory.reward(-5, 0, 0)
	var errors := def.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe"), "names the event")


func test_controller_root_must_be_a_node2d() -> void:
	var def := DefFactory.event(DefFactory.packed(Node.new()))
	assert_eq(def.validate().size(), 1, "a plain Node cannot be placed in the world")
```

`tests/defs/test_event_schedule.gd`:
```gdscript
extends TestCase

const STAGE_01_WAVES: PackedFloat32Array = [30.0, 60.0, 90.0, 120.0, 150.0]


func _pool(time_limit: float = 20.0) -> Array[EventDef]:
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), time_limit)]
	return pool


func _stage_01_like() -> EventSchedule:
	return DefFactory.schedule(_pool(25.0), 3, 20.0, 155.0, 30.0, 5.0)


func test_allowed_seconds_skip_the_wave_clearance_and_stay_in_the_window() -> void:
	var schedule := DefFactory.schedule(_pool(), 1, 20.0, 40.0, 20.0, 5.0)
	var allowed := schedule.allowed_seconds(100.0, PackedFloat32Array([30.0]))
	assert_eq(allowed, PackedInt32Array([20, 21, 22, 23, 24, 36, 37, 38, 39, 40]))


func test_rolled_times_respect_window_gap_and_clearance_for_many_seeds() -> void:
	var schedule := _stage_01_like()
	var allowed := schedule.allowed_seconds(180.0, STAGE_01_WAVES)
	for run_seed in range(1, 41):
		var times := schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(run_seed))
		assert_eq(times.size(), 3, "seed %d" % run_seed)
		for i in times.size():
			assert_true(allowed.has(int(times[i])), "seed %d: %s is allowed" % [run_seed, times[i]])
			assert_almost_eq(times[i], roundf(times[i]), 0.0001, "whole seconds")
			if i > 0:
				assert_true(times[i] - times[i - 1] >= 30.0, "seed %d: gap" % run_seed)


func test_rolling_is_seeded_and_not_always_the_earliest() -> void:
	var schedule := _stage_01_like()
	assert_eq(schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(8)), schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(8)))
	var firsts: Dictionary[float, bool] = {}
	for run_seed in range(1, 21):
		firsts[schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(run_seed))[0]] = true
	assert_true(firsts.size() > 3, "first times vary with the seed (got %d distinct)" % firsts.size())


func test_a_tight_schedule_has_exactly_one_placement() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 3, 10.0, 30.0, 10.0)
	for run_seed in range(1, 6):
		assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(run_seed)), PackedFloat32Array([10.0, 20.0, 30.0]))


func test_each_pick_leaves_room_for_the_rest() -> void:
	var schedule := DefFactory.schedule(_pool(20.0), 2, 10.0, 40.0, 25.0)
	for run_seed in range(1, 31):
		var times := schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(run_seed))
		assert_eq(times.size(), 2, "seed %d" % run_seed)
		assert_true(times[0] <= 15.0, "seed %d: the first event must leave room for the second (got %s)" % [run_seed, times[0]])
		assert_true(times[1] >= times[0] + 25.0)


func test_infeasible_schedule_rolls_nothing_and_fails_validation() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 4, 10.0, 30.0, 10.0)
	assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(3)).size(), 0)
	assert_eq(schedule.earliest_times(100.0, PackedFloat32Array()).size(), 3)
	var errors := schedule.validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("cannot place"))


func test_wave_clearance_can_make_a_schedule_infeasible() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 1, 25.0, 35.0, 10.0, 6.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array([30.0])).size(), 1, "every second is within 6 s of the wave")
	schedule.wave_clearance = 4.0
	assert_no_errors(schedule.validate_against(100.0, PackedFloat32Array([30.0])), "25 and 35 are clear")


func test_validation_rejects_bad_windows_counts_and_short_gaps() -> void:
	var schedule := DefFactory.schedule(_pool(20.0), 1, 0.0, 50.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 1, "window starts at 0")
	schedule = DefFactory.schedule(_pool(20.0), 1, 10.0, 100.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 2, "window reaches the stage end, so no event there could finish either")
	schedule = DefFactory.schedule(_pool(20.0), -1, 10.0, 50.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 1, "negative count")
	schedule = DefFactory.schedule(_pool(25.0), 1, 10.0, 50.0, 20.0)
	var errors := schedule.validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 1, "min_gap shorter than the longest time limit")
	assert_true(errors[0].contains("min_gap"))


func test_the_window_must_leave_time_for_the_longest_event_to_finish() -> void:
	var schedule := DefFactory.schedule(_pool(25.0), 1, 20.0, 155.0, 30.0, 5.0)
	assert_no_errors(schedule.validate_against(180.0, STAGE_01_WAVES), "155 + 25 = 180 still finishes in time")
	schedule.window_end = 160.0
	var errors := schedule.validate_against(180.0, STAGE_01_WAVES)
	assert_eq(errors.size(), 1, "fails by this rule only: %s" % ", ".join(errors))
	assert_true(errors[0].contains("finish"))


func test_validation_reports_null_and_invalid_event_defs_and_an_empty_pool() -> void:
	var pool := _pool()
	pool.append(null)
	pool.append(EventDef.new())
	var errors := DefFactory.schedule(pool).validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 6, "1 null + 5 from the empty def: %s" % ", ".join(errors))
	var empty: Array[EventDef] = []
	assert_eq(DefFactory.schedule(empty).validate_against(100.0, PackedFloat32Array()).size(), 1, "count 1 with nothing to pick")


func test_zero_count_rolls_nothing_and_validates() -> void:
	var schedule := DefFactory.schedule(_pool(), 0)
	assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(1)).size(), 0)
	assert_no_errors(schedule.validate_against(100.0, PackedFloat32Array()), "no events")


func test_roll_defs_never_repeats_when_there_are_alternatives() -> void:
	var a := DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"a")
	var b := DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"b")
	var pool: Array[EventDef] = [a, b, null]
	var picks := DefFactory.schedule(pool).roll_defs(20, RunRng.new(4))
	assert_eq(picks.size(), 20)
	for i in range(1, picks.size()):
		assert_false(picks[i] == picks[i - 1], "pick %d repeats" % i)
	assert_true(picks.has(a) and picks.has(b))
	var single: Array[EventDef] = [a]
	var repeats := DefFactory.schedule(single).roll_defs(3, RunRng.new(4))
	var expected: Array[EventDef] = [a, a, a]
	assert_eq(repeats, expected, "no alternative: the same def repeats")
```

Append to `tests/defs/test_stage_def.gd`:
```gdscript
func test_stage_validates_its_event_schedule_against_duration_and_waves() -> void:
	var def := DefFactory.stage(60.0)
	var entry := WaveEntry.new()
	entry.time = 30.0
	entry.count = 3
	entry.enemy = DefFactory.enemy()
	def.waves.entries.append(entry)
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 10.0)]
	def.events = DefFactory.schedule(pool, 1, 25.0, 35.0, 10.0, 6.0)
	assert_eq(def.validate().size(), 1, "every allowed second collides with the wave at 30 s")
	def.events.wave_clearance = 4.0
	assert_no_errors(def.validate(), "clearance 4 leaves 25 and 35")
	def.events = null
	assert_no_errors(def.validate(), "events are optional")
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/defs/test_event TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `RewardDef`, `EventDef`, `EventSchedule` not found.

- [ ] **Step 4: Implement**

`src/defs/reward_def.gd`:
```gdscript
class_name RewardDef
extends Resource
## What a successful stage event pays into the run's wallet. Failure pays nothing.

@export var gold: int = 0
@export var stone: int = 0
@export var tickets: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if gold < 0 or stone < 0 or tickets < 0:
		errors.append("RewardDef: gold, stone and tickets must be >= 0")
	elif gold == 0 and stone == 0 and tickets == 0:
		errors.append("RewardDef: pays nothing (gold, stone and tickets are all 0)")
	return errors
```

`src/defs/event_def.gd`:
```gdscript
class_name EventDef
extends Resource
## One kind of stage event: its name, the controller scene that runs it, its time limit and its reward.
## The controller root must extend StageEvent; defs/ cannot name stage classes, so that check is
## EventDirector.validate_defs (run by Stage.start). Here: the root is a Node2D and passes its own validate().

@export var id: StringName
@export var display_name: String = ""
## Optional; the HUD shows the name when there is no icon.
@export var icon: Texture2D
@export var controller_scene: PackedScene
@export var time_limit: float = 0.0
@export var reward: RewardDef


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("EventDef: id is empty")
	if display_name.is_empty():
		errors.append("EventDef '%s': display_name is empty" % id)
	if time_limit <= 0.0:
		errors.append("EventDef '%s': time_limit must be > 0" % id)
	if reward == null:
		errors.append("EventDef '%s': reward is null" % id)
	else:
		for error in reward.validate():
			errors.append("EventDef '%s': %s" % [id, error])
	if controller_scene == null:
		errors.append("EventDef '%s': controller_scene is null" % id)
	else:
		errors.append_array(_validate_scene())
	return errors


func _validate_scene() -> PackedStringArray:
	var errors := PackedStringArray()
	var root := controller_scene.instantiate()
	if root == null:
		errors.append("EventDef '%s': controller_scene cannot be instantiated" % id)
		return errors
	if not (root is Node2D):
		errors.append("EventDef '%s': controller_scene root must be a Node2D (a StageEvent)" % id)
	elif root.has_method("validate"):
		errors.append_array(root.call("validate") as PackedStringArray)
	root.free()
	return errors
```

`src/defs/event_schedule.gd`:
```gdscript
class_name EventSchedule
extends Resource
## When a stage's events happen. Times are whole seconds rolled once per run: a second is allowed when it
## lies inside [window_start, window_end] (and inside the stage) and is more than `wave_clearance` from every
## wave time. Events are placed in order; each is drawn uniformly from the allowed seconds at least
## `min_gap` after the previous event that still leave room to place the remaining events greedily, so a
## schedule whose greedy earliest placement fits always rolls `count` times.

@export var count: int = 0
@export var window_start: float = 0.0
@export var window_end: float = 0.0
## Between two event starts; must cover the longest time limit so at most one event runs at a time.
@export var min_gap: float = 0.0
@export var wave_clearance: float = 0.0
@export var pool: Array[EventDef] = []


func allowed_seconds(duration: float, wave_times: PackedFloat32Array) -> PackedInt32Array:
	var allowed := PackedInt32Array()
	for second in range(ceili(window_start), floori(window_end) + 1):
		if second <= 0 or float(second) >= duration:
			continue
		if _near_a_wave(float(second), wave_times):
			continue
		allowed.append(second)
	return allowed


## `count` ascending times, or an empty array when the schedule cannot be placed.
func roll_times(duration: float, wave_times: PackedFloat32Array, rng: RunRng) -> PackedFloat32Array:
	var times := PackedFloat32Array()
	if count <= 0:
		return times
	var allowed := allowed_seconds(duration, wave_times)
	var previous := -INF
	for placed in count:
		var remaining := count - placed - 1
		var candidates := PackedInt32Array()
		for second in allowed:
			if float(second) >= previous + min_gap and _fits_after(allowed, second, remaining):
				candidates.append(second)
		if candidates.is_empty():
			return PackedFloat32Array()
		previous = float(candidates[rng.randi_range(0, candidates.size() - 1)])
		times.append(previous)
	return times


## The greedy earliest placement; shorter than `count` when the schedule is infeasible.
func earliest_times(duration: float, wave_times: PackedFloat32Array) -> PackedFloat32Array:
	var times := PackedFloat32Array()
	var allowed := allowed_seconds(duration, wave_times)
	var cursor := -INF
	for i in maxi(count, 0):
		var next := _first_at_or_after(allowed, cursor + min_gap)
		if next < 0:
			break
		times.append(float(next))
		cursor = float(next)
	return times


## Defs for `amount` events drawn from the pool, never the same def twice in a row when the pool has
## alternatives. Null entries are skipped (validation reports them).
func roll_defs(amount: int, rng: RunRng) -> Array[EventDef]:
	var picks: Array[EventDef] = []
	var candidates: Array[EventDef] = []
	for def in pool:
		if def != null:
			candidates.append(def)
	if candidates.is_empty():
		return picks
	var previous: EventDef = null
	for i in amount:
		var options: Array[EventDef] = []
		for def in candidates:
			if def != previous:
				options.append(def)
		if options.is_empty():
			options = candidates
		previous = rng.pick(options) as EventDef
		picks.append(previous)
	return picks


func longest_time_limit() -> float:
	var longest := 0.0
	for def in pool:
		if def != null:
			longest = maxf(longest, def.time_limit)
	return longest


func validate_against(duration: float, wave_times: PackedFloat32Array) -> PackedStringArray:
	var errors := PackedStringArray()
	if count < 0:
		errors.append("EventSchedule: count must be >= 0")
	if window_start <= 0.0 or window_end >= duration or window_start > window_end:
		errors.append("EventSchedule: window [%s, %s] must lie inside (0, stage duration %s)" % [window_start, window_end, duration])
	if wave_clearance < 0.0:
		errors.append("EventSchedule: wave_clearance must be >= 0")
	if window_end + longest_time_limit() > duration:
		errors.append("EventSchedule: window_end %s + longest time_limit %s exceeds the stage duration %s (an event must be able to finish before time is up)" % [window_end, longest_time_limit(), duration])
	var usable := 0
	for def in pool:
		if def == null:
			errors.append("EventSchedule: null event in the pool")
			continue
		usable += 1
		errors.append_array(def.validate())
	if count > 0 and usable == 0:
		errors.append("EventSchedule: count is %d but the pool is empty" % count)
	if min_gap < longest_time_limit():
		errors.append("EventSchedule: min_gap %s is shorter than the longest time_limit %s (two events could overlap)" % [min_gap, longest_time_limit()])
	elif count > 0 and earliest_times(duration, wave_times).size() < count:
		errors.append("EventSchedule: cannot place %d events in the window with min_gap %s and wave_clearance %s" % [count, min_gap, wave_clearance])
	return errors


func _near_a_wave(second: float, wave_times: PackedFloat32Array) -> bool:
	for wave_time in wave_times:
		if absf(second - wave_time) <= wave_clearance:
			return true
	return false


## The first allowed second at or after `from`, or -1.
func _first_at_or_after(allowed: PackedInt32Array, from: float) -> int:
	for second in allowed:
		if float(second) >= from:
			return second
	return -1


## Whether `remaining` more events fit greedily after one placed at `start`.
func _fits_after(allowed: PackedInt32Array, start: int, remaining: int) -> bool:
	var cursor := float(start)
	for i in remaining:
		var next := _first_at_or_after(allowed, cursor + min_gap)
		if next < 0:
			return false
		cursor = float(next)
	return true
```
(The feasibility error is reported only when the gap rule holds, so one root cause yields one message; `test_infeasible_schedule_rolls_nothing_and_fails_validation` relies on that.)

`src/defs/stage_def.gd` — add after `@export var grade_table: GradeTable`:
```gdscript
## Optional announced events (Phase 3); null = none.
@export var events: EventSchedule
```
and append inside `validate()` before `return errors`:
```gdscript
	if events != null:
		var wave_times := waves.times() if waves != null else PackedFloat32Array()
		errors.append_array(events.validate_against(duration, wave_times))
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/defs TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: `test_event_def` 5, `test_event_schedule` 12, `test_stage_def` 7 PASS. Likely snags:
- `range(ceili(...), floori(...) + 1)` with a window that rounds empty simply yields nothing — correct.
- `Dictionary[float, bool]` keys: if typed float keys are refused, use an untyped `{}`.

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/defs tests/defs tests/support
git commit -q -m "feat(defs): add RewardDef, EventDef and EventSchedule with seeded feasible event times

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: BossDef, BossPatternDef, StageDef.boss

**Files:**
- Create: `src/defs/boss_pattern_def.gd`, `src/defs/boss_def.gd`
- Modify: `src/defs/stage_def.gd`, `tests/support/def_factory.gd` (add `boss`)
- Test: `tests/defs/test_boss_def.gd` (new), `tests/defs/test_stage_def.gd` (append)

**Interfaces:**
- Consumes: `VisualDef.validate()`, `StatBlock.validate()`, GAS `GameplayAbility`, `ProbeAbility` (`tests/support/probe_ability.tscn`, root `ActorAbility`, `reject_reason` → `validate()`), `BareGameplayAbility`, `DefFactory.packed` (Task 3).
- Produces:
  - `BossPatternDef` (Resource): `ability_scene: PackedScene`, `weight: float = 1.0`; `validate()` — scene required, root must extend `GameplayAbility` (the stricter `ActorAbility` check is `BossBrain.validate_def`, Task 11), root `validate()` appended, `weight > 0`.
  - `BossDef` (Resource): `enum Order { SEQUENCE, RANDOM }`; `id`, `display_name`, `scene: PackedScene`, `visual: VisualDef`, `visual_scale: float = 1.0`, `tint: Color`, `stats: StatBlock`, `contact_effect: GameplayEffect`, `contact_damage: float = 0.0`, `contact_interval: float = 1.0`, `body_radius: float = 10.0`, `patterns: Array[BossPatternDef]`, `order: Order = RANDOM`, `pattern_gap: float = 0.0`; `validate()`.
  - `StageDef.boss: BossDef` (optional; null = time up clears the stage).
  - `DefFactory.boss(scene: PackedScene = null, pattern_scenes: Array[PackedScene] = []) -> BossDef`: id `test_boss`, name "Test Boss", 500 HP, `contact_damage = 0` (tests see only pattern damage), `contact_interval = 1.0`, `body_radius = 20`, `pattern_gap = 2.0`, order SEQUENCE, one weight-1 `BossPatternDef` per scene.

- [ ] **Step 1: Add the factory helper**

Append to `tests/support/def_factory.gd`:
```gdscript
## A valid boss def with contact damage 0 (tests then see only pattern damage), SEQUENCE order, gap 2 s,
## and one weight-1 pattern per scene. `scene` defaults to an empty PackedScene (enough for validation).
static func boss(scene: PackedScene = null, pattern_scenes: Array[PackedScene] = []) -> BossDef:
	var def := BossDef.new()
	def.id = &"test_boss"
	def.display_name = "Test Boss"
	def.scene = scene if scene != null else PackedScene.new()
	def.visual = visual()
	def.stats = StatBlock.new()
	def.stats.max_health = 500.0
	def.contact_effect = GameplayEffect.new()
	def.contact_damage = 0.0
	def.contact_interval = 1.0
	def.body_radius = 20.0
	def.pattern_gap = 2.0
	def.order = BossDef.Order.SEQUENCE
	for pattern_scene in pattern_scenes:
		var pattern := BossPatternDef.new()
		pattern.ability_scene = pattern_scene
		pattern.weight = 1.0
		def.patterns.append(pattern)
	return def
```

- [ ] **Step 2: Write the failing tests**

`tests/defs/test_boss_def.gd`:
```gdscript
extends TestCase

const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")


func _valid() -> BossDef:
	var patterns: Array[PackedScene] = [PROBE_SCENE]
	return DefFactory.boss(null, patterns)


func test_factory_boss_is_valid() -> void:
	assert_no_errors(_valid().validate(), "boss")


func test_empty_boss_def_reports_every_missing_part() -> void:
	var errors := BossDef.new().validate()
	assert_eq(errors.size(), 8, "id, name, scene, contact effect, visual, stats, gap, patterns: %s" % ", ".join(errors))


func test_numbers_must_be_positive() -> void:
	var def := _valid()
	def.pattern_gap = 0.0
	assert_eq(def.validate().size(), 1, "pattern_gap")
	def = _valid()
	def.contact_interval = 0.0
	def.contact_damage = -1.0
	assert_eq(def.validate().size(), 1, "one combined actor-number error")


func test_nested_stats_and_visual_errors_are_reported() -> void:
	var def := _valid()
	def.stats.max_health = 0.0
	def.visual = VisualDef.new()
	assert_eq(def.validate().size(), 2)


func test_pattern_needs_a_gameplay_ability_scene_and_a_positive_weight() -> void:
	var pattern := BossPatternDef.new()
	assert_eq(pattern.validate().size(), 1, "no scene")
	pattern.ability_scene = PROBE_SCENE
	assert_no_errors(pattern.validate(), "probe")
	pattern.weight = 0.0
	assert_eq(pattern.validate().size(), 1, "weight 0")
	pattern.weight = 1.0
	pattern.ability_scene = DefFactory.packed(Node.new())
	assert_eq(pattern.validate().size(), 1, "root is not an ability")


func test_pattern_root_may_reject_itself() -> void:
	var probe := ProbeAbility.new()
	probe.reject_reason = "probe says no"
	var pattern := BossPatternDef.new()
	pattern.ability_scene = DefFactory.packed(probe)
	var errors := pattern.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))


func test_boss_reports_null_and_invalid_patterns() -> void:
	var def := _valid()
	def.patterns.append(null)
	def.patterns.append(BossPatternDef.new())
	assert_eq(def.validate().size(), 2)
```

Append to `tests/defs/test_stage_def.gd`:
```gdscript
func test_stage_validates_its_optional_boss() -> void:
	var def := DefFactory.stage()
	assert_null(def.boss, "no boss by default")
	def.boss = BossDef.new()
	assert_eq(def.validate().size(), 8, "the boss's errors are the stage's errors")
	var patterns: Array[PackedScene] = [preload("res://tests/support/probe_ability.tscn")]
	def.boss = DefFactory.boss(null, patterns)
	assert_no_errors(def.validate(), "valid boss")
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/defs/test_boss_def TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `BossDef`, `BossPatternDef` not found.

- [ ] **Step 4: Implement**

`src/defs/boss_pattern_def.gd`:
```gdscript
class_name BossPatternDef
extends Resource
## One attack in a boss's repertoire: the ability scene that performs it and its weight in RANDOM order.
## The root must extend ActorAbility with a unique ability_tag; defs/ cannot name ActorAbility, so that
## check is BossBrain.validate_def (run by Stage.start). Here: the root is a GameplayAbility and passes
## its own validate().

@export var ability_scene: PackedScene
@export var weight: float = 1.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if weight <= 0.0:
		errors.append("BossPatternDef: weight must be > 0")
	if ability_scene == null:
		errors.append("BossPatternDef: ability_scene is null")
		return errors
	var root := ability_scene.instantiate()
	if root == null:
		errors.append("BossPatternDef: ability_scene cannot be instantiated")
		return errors
	if not (root is GameplayAbility):
		errors.append("BossPatternDef: ability_scene root must extend GameplayAbility")
	elif root.has_method("validate"):
		errors.append_array(root.call("validate") as PackedStringArray)
	root.free()
	return errors
```

`src/defs/boss_def.gd`:
```gdscript
class_name BossDef
extends Resource
## The end-of-stage boss: a generic boss actor scene configured like an EnemyDef, plus its attack
## patterns and how the brain picks them (SEQUENCE = list order, RANDOM = weighted, no immediate repeats).

enum Order { SEQUENCE, RANDOM }

@export var id: StringName
@export var display_name: String = ""
@export var scene: PackedScene
@export var visual: VisualDef
@export var visual_scale: float = 1.0
@export var tint: Color = Color.WHITE
@export var stats: StatBlock
@export var contact_effect: GameplayEffect
@export var contact_damage: float = 0.0
@export var contact_interval: float = 1.0
@export var body_radius: float = 10.0
@export var patterns: Array[BossPatternDef] = []
@export var order: Order = Order.RANDOM
## Seconds of chasing between the end of one pattern and the start of the next.
@export var pattern_gap: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("BossDef: id is empty")
	if display_name.is_empty():
		errors.append("BossDef '%s': display_name is empty" % id)
	if scene == null:
		errors.append("BossDef '%s': scene is null" % id)
	if contact_effect == null:
		errors.append("BossDef '%s': contact_effect is null" % id)
	if body_radius <= 0.0 or contact_damage < 0.0 or contact_interval <= 0.0 or visual_scale <= 0.0:
		errors.append("BossDef '%s': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0" % id)
	if visual == null:
		errors.append("BossDef '%s': visual is null" % id)
	else:
		errors.append_array(visual.validate())
	if stats == null:
		errors.append("BossDef '%s': stats is null" % id)
	else:
		errors.append_array(stats.validate())
	if pattern_gap <= 0.0:
		errors.append("BossDef '%s': pattern_gap must be > 0" % id)
	if patterns.is_empty():
		errors.append("BossDef '%s': needs at least one pattern" % id)
	for pattern in patterns:
		if pattern == null:
			errors.append("BossDef '%s': null pattern" % id)
		else:
			for error in pattern.validate():
				errors.append("BossDef '%s': %s" % [id, error])
	return errors
```

`src/defs/stage_def.gd` — add after the `events` export:
```gdscript
## Optional end-of-stage boss (Phase 3); null = time up clears the stage.
@export var boss: BossDef
```
and append inside `validate()` before `return errors`:
```gdscript
	if boss != null:
		errors.append_array(boss.validate())
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/defs TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: `test_boss_def` 7 and `test_stage_def` 8 PASS. (`VisualDef.new().validate()` reports exactly one error, the missing idle strip, which is the "visual" half of `test_nested_stats_and_visual_errors_are_reported`.)

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/defs tests/defs tests/support
git commit -q -m "feat(defs): add BossDef and BossPatternDef; StageDef gains an optional boss

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Telegraph and AreaStrike components; MeleeStrike rebuilt on AreaStrike

**Files:**
- Create: `src/components/telegraph.gd`, `src/components/area_strike.gd`, `src/components/area_strike.tscn`
- Modify: `src/abilities/blade_sweep/melee_strike.gd` (scene `melee_strike.tscn` unchanged)
- Test: `tests/components/test_telegraph.gd` (new), `tests/components/test_area_strike.gd` (new); `tests/abilities/test_melee_strike.gd` and `tests/abilities/test_blade_sweep_ability.gd` must stay green unchanged

**Interfaces:**
- Consumes: `Hitbox` (`hurtbox_entered`, `once_per_target` default true), `EffectApplier.bind/hit(hurtbox, effect, amount, causer) -> bool` (false when the effect was refused, e.g. `State.Invulnerable`), `Lifetime.restart/expired`, `Hurtbox.actor`, `PhysicsLayers`, `ActorFixture.hurt_target(test, position, layer)`.
- Produces:
  - `Telegraph` (Node2D, script only — `Telegraph.new()`): `signal elapsed`; `enum Shape { CIRCLE, LINE }`; `shape`, `radius`, `line_offset`, `line_width`; `show_circle(radius: float, seconds: float)`, `show_line(offset: Vector2, width: float, seconds: float)`; `remaining() -> float`; `progress() -> float` (0 → 1); `step(delta)` (physics-driven); emits `elapsed` once and `queue_free`s when the time is up. Visual only: owners keep their own timers.
  - `AreaStrike` (Node2D scene `src/components/area_strike.tscn`: `Hitbox` + `CollisionShape2D` (local circle), `EffectApplier`, `Lifetime`): `signal struck(hurtbox: Hurtbox)` (only for hits that landed); `@export ring_color`; `strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float, target_mask: int = PhysicsLayers.ENEMY_HURT, only_actor: Node2D = null)` — call after it is in the tree; each hurtbox once; frees itself after `seconds`.
  - `MeleeStrike extends AreaStrike` (no own code; Blade Sweep still instantiates `melee_strike.tscn` and calls `strike(asc, effect, damage, radius, seconds)`).

- [ ] **Step 1: Write the failing tests**

`tests/components/test_telegraph.gd`:
```gdscript
extends TestCase


func _telegraph() -> Telegraph:
	var telegraph := adopt(Telegraph.new()) as Telegraph
	telegraph.set_physics_process(false)
	return telegraph


func test_circle_counts_down_then_announces_and_frees_itself() -> void:
	var telegraph := _telegraph()
	var elapsed: Array = []
	telegraph.elapsed.connect(func() -> void: elapsed.append(true))
	telegraph.show_circle(60.0, 1.0)
	assert_eq(telegraph.shape, Telegraph.Shape.CIRCLE)
	assert_almost_eq(telegraph.radius, 60.0)
	telegraph.step(0.4)
	assert_almost_eq(telegraph.remaining(), 0.6)
	assert_almost_eq(telegraph.progress(), 0.4)
	assert_true(elapsed.is_empty())
	telegraph.step(0.7)
	assert_eq(elapsed.size(), 1)
	assert_true(telegraph.is_queued_for_deletion())
	telegraph.step(0.5)
	assert_eq(elapsed.size(), 1, "announces once")


func test_line_keeps_its_offset_and_width() -> void:
	var telegraph := _telegraph()
	telegraph.show_line(Vector2(360, -40), 24.0, 0.7)
	assert_eq(telegraph.shape, Telegraph.Shape.LINE)
	assert_eq(telegraph.line_offset, Vector2(360, -40))
	assert_almost_eq(telegraph.line_width, 24.0)
	assert_almost_eq(telegraph.remaining(), 0.7)


func test_an_unarmed_telegraph_does_nothing() -> void:
	var telegraph := _telegraph()
	var elapsed: Array = []
	telegraph.elapsed.connect(func() -> void: elapsed.append(true))
	telegraph.step(5.0)
	assert_true(elapsed.is_empty())
	assert_false(telegraph.is_queued_for_deletion())
```

`tests/components/test_area_strike.gd`:
```gdscript
extends TestCase

const STRIKE_SCENE: PackedScene = preload("res://src/components/area_strike.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func _strike(at: Vector2) -> AreaStrike:
	var strike := adopt(STRIKE_SCENE.instantiate()) as AreaStrike
	strike.global_position = at
	return strike


func _settle() -> void:
	await next_physics_frame()
	await next_physics_frame()


func test_the_mask_selects_which_side_is_hit() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var ally := ActorFixture.hurt_target(self, Vector2(310, 200), PhysicsLayers.ALLY_HURT)
	var enemy := ActorFixture.hurt_target(self, Vector2(290, 200), PhysicsLayers.ENEMY_HURT)
	_strike(Vector2(300, 200)).strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await _settle()
	assert_almost_eq(_health(ally), 85.0)
	assert_almost_eq(_health(enemy), 100.0, 0.001, "enemy hurt layer not in the mask")


func test_only_actor_narrows_the_strike_to_one_actor() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var player := ActorFixture.hurt_target(self, Vector2(-120, 40), PhysicsLayers.ALLY_HURT)
	var building := ActorFixture.hurt_target(self, Vector2(-100, 40), PhysicsLayers.ALLY_HURT)
	var struck: Array = []
	var strike := _strike(Vector2(-110, 40))
	strike.struck.connect(func(hurtbox: Hurtbox) -> void: struck.append(hurtbox.actor))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 30.0, 60.0, 0.3, PhysicsLayers.ALLY_HURT, player)
	await _settle()
	assert_almost_eq(_health(player), 70.0)
	assert_almost_eq(_health(building), 100.0, 0.001, "the building is spared")
	assert_eq(struck, [player])


func test_a_refused_hit_is_not_reported_as_struck() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var dodger := ActorFixture.hurt_target(self, Vector2(50, -60), PhysicsLayers.ALLY_HURT)
	ActorFixture.asc_of(dodger).add_tag(GameplayTags.State_Invulnerable)
	var struck: Array = []
	var strike := _strike(Vector2(50, -60))
	strike.struck.connect(func(hurtbox: Hurtbox) -> void: struck.append(hurtbox))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await _settle()
	assert_almost_eq(_health(dodger), 100.0)
	assert_true(struck.is_empty(), "an invulnerable target was not struck")


func test_the_attackers_attack_scales_the_damage() -> void:
	var stats := StatBlock.new()
	stats.attack = 2.0
	var attacker := ActorFixture.bare_actor(self, stats)
	var victim := ActorFixture.hurt_target(self, Vector2(400, 0), PhysicsLayers.ALLY_HURT)
	_strike(Vector2(400, 0)).strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await _settle()
	assert_almost_eq(_health(victim), 70.0, 0.001, "15 x attack 2")


func test_the_strike_frees_itself_after_its_seconds() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var strike := _strike(Vector2(10, 10))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 20.0, 0.1)
	await wait_seconds(0.3)
	assert_false(is_instance_valid(strike))
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/components/test_telegraph TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `Telegraph` not found.

- [ ] **Step 3: Implement**

`src/components/telegraph.gd`:
```gdscript
class_name Telegraph
extends Node2D
## A warning drawn on the ground — a circle or a line that fills up over `seconds` — then `elapsed` and
## free. Purely visual: whoever shows it keeps its own timer and decides what happens when the warning ends.

signal elapsed

enum Shape { CIRCLE, LINE }

const SEGMENTS: int = 40
const EDGE_WIDTH: float = 2.0

@export var fill_color: Color = Color(1.0, 0.2, 0.15, 0.25)
@export var edge_color: Color = Color(1.0, 0.3, 0.2, 0.9)

var shape: Shape = Shape.CIRCLE
var radius: float = 0.0
var line_offset: Vector2 = Vector2.ZERO
var line_width: float = 0.0
var _duration: float = 0.0
var _left: float = 0.0
var _armed: bool = false


func show_circle(p_radius: float, seconds: float) -> void:
	shape = Shape.CIRCLE
	radius = p_radius
	_arm(seconds)


func show_line(offset: Vector2, width: float, seconds: float) -> void:
	shape = Shape.LINE
	line_offset = offset
	line_width = width
	_arm(seconds)


func remaining() -> float:
	return _left


## 0 when shown, 1 when the warning is over.
func progress() -> float:
	if _duration <= 0.0:
		return 1.0
	return clampf(1.0 - _left / _duration, 0.0, 1.0)


func step(delta: float) -> void:
	if not _armed:
		return
	_left = maxf(_left - delta, 0.0)
	queue_redraw()
	if _left <= 0.0:
		_armed = false
		elapsed.emit()
		queue_free()


func _arm(seconds: float) -> void:
	_duration = maxf(seconds, 0.0)
	_left = _duration
	_armed = true
	queue_redraw()


func _physics_process(delta: float) -> void:
	step(delta)


func _draw() -> void:
	match shape:
		Shape.CIRCLE:
			draw_circle(Vector2.ZERO, radius, fill_color)
			draw_circle(Vector2.ZERO, radius * progress(), fill_color)
			draw_arc(Vector2.ZERO, radius, 0.0, TAU, SEGMENTS, edge_color, EDGE_WIDTH)
		Shape.LINE:
			draw_line(Vector2.ZERO, line_offset, fill_color, line_width)
			draw_line(Vector2.ZERO, line_offset * progress(), edge_color, line_width * 0.4)
```

`src/components/area_strike.gd`:
```gdscript
class_name AreaStrike
extends Node2D
## A short-lived circular hit: damages every hurtbox on `target_mask` it overlaps, once each, draws a ring,
## then frees itself after `seconds`. `only_actor` narrows it to one actor (meteors and the boss's slam and
## charge hit the player but not the building). The one area-hit path: Blade Sweep (MeleeStrike), meteors
## and boss patterns all damage through Hitbox + EffectApplier, i.e. EffectBuilder.damage.

signal struck(hurtbox: Hurtbox)

const RING_WIDTH: float = 2.0
const RING_SEGMENTS: int = 32

@export var ring_color: Color = Color(1.0, 0.9, 0.6, 0.8)

var _effect: GameplayEffect
var _damage: float = 0.0
var _radius: float = 0.0
var _only_actor: Node2D
var _filtered: bool = false

@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the strike has entered the tree.
func strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float, target_mask: int = PhysicsLayers.ENEMY_HURT, only_actor: Node2D = null) -> void:
	_effect = effect
	_damage = damage
	_radius = radius
	_only_actor = only_actor
	_filtered = only_actor != null
	_hitbox.collision_mask = target_mask
	(_hit_shape.shape as CircleShape2D).radius = radius
	_applier.bind(attacker_asc)
	_lifetime.restart(seconds)
	queue_redraw()


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	if _filtered and (not is_instance_valid(_only_actor) or hurtbox.actor != _only_actor):
		return
	if _applier.hit(hurtbox, _effect, _damage, self):
		struck.emit(hurtbox)


func _draw() -> void:
	if _radius > 0.0:
		draw_arc(Vector2.ZERO, _radius, 0.0, TAU, RING_SEGMENTS, ring_color, RING_WIDTH)
```

`src/components/area_strike.tscn`:
```
[gd_scene load_steps=6 format=3]

[ext_resource type="Script" path="res://src/components/area_strike.gd" id="1_strike"]
[ext_resource type="Script" path="res://src/components/hitbox.gd" id="2_hitbox"]
[ext_resource type="Script" path="res://src/components/effect_applier.gd" id="3_applier"]
[ext_resource type="Script" path="res://src/components/lifetime.gd" id="4_lifetime"]

[sub_resource type="CircleShape2D" id="CircleShape2D_hit"]
resource_local_to_scene = true
radius = 40.0

[node name="AreaStrike" type="Node2D"]
script = ExtResource("1_strike")

[node name="Hitbox" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 16
monitorable = false
script = ExtResource("2_hitbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hitbox"]
shape = SubResource("CircleShape2D_hit")

[node name="EffectApplier" type="Node" parent="."]
script = ExtResource("3_applier")

[node name="Lifetime" type="Node" parent="."]
script = ExtResource("4_lifetime")
```

`src/abilities/blade_sweep/melee_strike.gd` (whole file):
```gdscript
class_name MeleeStrike
extends AreaStrike
## Blade Sweep's radial hit: an AreaStrike on the enemy hurt layer (its default target mask), so the sweep
## shares the single area-hit path with meteors and boss attacks.
```

- [ ] **Step 4: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/components TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` and `FILTER=tests/abilities TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: `test_telegraph` 3, `test_area_strike` 5 PASS; the Phase 2 `test_melee_strike` (2) and `test_blade_sweep_ability` (4) PASS unchanged. Likely snag: `melee_strike.tscn` still parses because its nodes match what `AreaStrike` expects (`Hitbox`, `Hitbox/CollisionShape2D`, `EffectApplier`, `Lifetime`); if `MeleeStrike` reports "Could not find base class AreaStrike", the scan has not run.

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/components src/abilities tests/components
git commit -q -m "feat(components): add Telegraph and AreaStrike; MeleeStrike becomes an AreaStrike

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: StageEvent base, EventContext, EventDirector

**Files:**
- Create: `src/stage/stage_event.gd`, `src/stage/event_context.gd`, `src/stage/event_director.gd`
- Create: `tests/support/probe_event.gd`, `tests/support/probe_event.tscn`
- Modify: `tests/support/actor_fixture.gd` (add `kill`, `event_context`)
- Test: `tests/stage/test_stage_event.gd` (new), `tests/stage/test_event_director.gd` (new), `tests/defs/test_event_def.gd` (append)

**Interfaces:**
- Consumes: `EventDef`, `EventSchedule.roll_times/roll_defs`, `DefFactory.event/schedule/packed/stage` (Task 3), `Timeline.new(duration, marks)` + `Timeline.crossed(from, to)`, `StageClock.ticked`, `EnemyFactory.bind(layer, targets, rng)`, `TargetProvider.building/player`.
- Produces:
  - `EventContext` (RefCounted): `layer: Node2D` (the controller is added here; its own objects are its children), `factory: EnemyFactory`, `targets: TargetProvider`, `player: Node2D`, `building: Node2D`, `rng: RunRng`, `bounds: Rect2`, `spawn_radius: float`, `def: EventDef`; `_init(layer, factory, targets, player, building, rng, bounds, spawn_radius, def)`.
  - `StageEvent` (`@abstract`, Node2D): signals `succeeded`, `failed`, `progress_changed(text: String)`; `context: EventContext`; `begin(context)`, `abort()`, `step(delta)`, `objectives() -> Array[Node2D]` (empty by default), `remaining_time() -> float`, `is_running() -> bool`, `has_ended() -> bool`, `report_progress(text)`. Subclass hooks: `_on_begin()`, `_on_step(delta)`, `_on_time_up()` (fails by default), `_cleanup()`; finishers `_succeed()` / `_fail()`. Ends exactly once; `_cleanup()` runs on success, failure and abort; abort emits nothing.
  - `EventDirector` (Node): signals `event_started(def: EventDef, event: StageEvent)`, `event_progressed(def: EventDef, text: String)`, `event_ended(def: EventDef, success: bool)`; `bind(stage_def: StageDef, clock: StageClock, context_factory: Callable, rng: RunRng, wave_times: PackedFloat32Array)` where `context_factory` is `func(def: EventDef) -> EventContext`; `event_times() -> PackedFloat32Array`; `planned_defs() -> Array[EventDef]`; `running() -> StageEvent`; `abort_running()` (emits `event_ended(def, false)`, drops waiting events); `static validate_defs(schedule: EventSchedule) -> PackedStringArray` (controller root must be a `StageEvent`).
  - `ProbeEvent` (test double, `tests/support/probe_event.tscn`): `endless` (ignores its time limit), `reject_reason` (→ `validate()`), `begins`, `cleanups`, `objective_nodes: Array[Node2D]`, `finish(success: bool)`; reports progress `"Probe <def id>"` on begin.
  - `ActorFixture.kill(test, target_asc)`; `ActorFixture.event_context(test, def, player: Node2D = null, building_at: Vector2 = Vector2.ZERO) -> EventContext` (events layer, enemy layer + bound factory, targets, a building stand-in at `building_at`, the given player or a Node2D at `building_at + (0, 150)`, `RunRng(7)`, bounds `Rect2(-1200, -800, 2400, 1600)`, spawn radius 300).

- [ ] **Step 1: Add the test double and fixture helpers**

`tests/support/probe_event.gd`:
```gdscript
class_name ProbeEvent
extends StageEvent
## Test double: counts begin/cleanup, finishes on demand, optionally ignores its time limit (`endless`),
## exposes objectives, and reports `reject_reason` from validate() so def validation can be tested.

@export var endless: bool = false
@export var reject_reason: String = ""

var begins: int = 0
var cleanups: int = 0
var objective_nodes: Array[Node2D] = []


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if reject_reason != "":
		errors.append(reject_reason)
	return errors


## Read by index into an untyped variable: a freed node must not be assigned to a typed loop variable.
func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for i in objective_nodes.size():
		var node: Variant = objective_nodes[i]
		if is_instance_valid(node):
			found.append(node)
	return found


func finish(success: bool) -> void:
	if success:
		_succeed()
	else:
		_fail()


func _on_begin() -> void:
	begins += 1
	report_progress("Probe %s" % context.def.id)


func _on_time_up() -> void:
	if not endless:
		super._on_time_up()


func _cleanup() -> void:
	cleanups += 1
```

`tests/support/probe_event.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://tests/support/probe_event.gd" id="1_probe"]

[node name="ProbeEvent" type="Node2D"]
script = ExtResource("1_probe")
```

Append to `tests/support/actor_fixture.gd`:
```gdscript
const KILL_EFFECT: GameplayEffect = preload("res://data/effects/damage_basic.tres")


## Lethal damage from a throwaway attacker, through the real damage pipeline.
static func kill(test: TestCase, target_asc: AbilitySystemComponent) -> void:
	var attacker := bare_actor(test, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, KILL_EFFECT, 99999.0)
	asc_of(attacker).apply_effect_spec_to_target(spec, target_asc)


## An EventContext inside the test tree: an events layer, an enemy layer with a bound EnemyFactory, a
## TargetProvider, a building stand-in at `building_at`, `player` (or a plain Node2D at building_at + (0, 150)),
## RunRng(7), a 2400x1600 arena centred on the origin and a 300 px spawn ring.
static func event_context(test: TestCase, def: EventDef, player: Node2D = null, building_at: Vector2 = Vector2.ZERO) -> EventContext:
	var layer := test.adopt(Node2D.new()) as Node2D
	var enemies := test.adopt(Node2D.new()) as Node2D
	var targets := test.adopt(TargetProvider.new()) as TargetProvider
	var building := test.adopt(Node2D.new()) as Node2D
	building.global_position = building_at
	if player == null:
		player = test.adopt(Node2D.new()) as Node2D
		player.global_position = building_at + Vector2(0, 150)
	targets.building = building
	targets.player = player
	var rng := RunRng.new(7)
	var factory := test.adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(enemies, targets, rng)
	return EventContext.new(layer, factory, targets, player, building, rng, Rect2(-1200, -800, 2400, 1600), 300.0, def)
```

- [ ] **Step 2: Write the failing tests**

`tests/stage/test_stage_event.gd`:
```gdscript
extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _begun(time_limit: float = 12.0) -> Dictionary:
	var def := DefFactory.event(PROBE, time_limit)
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	var log := {"succeeded": [], "failed": [], "progress": []}
	event.succeeded.connect(func() -> void: (log["succeeded"] as Array).append(true))
	event.failed.connect(func() -> void: (log["failed"] as Array).append(true))
	event.progress_changed.connect(func(text: String) -> void: (log["progress"] as Array).append(text))
	event.begin(ActorFixture.event_context(self, def))
	return {"event": event, "log": log, "def": def}


func test_begin_starts_the_time_limit_and_reports_progress() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	assert_true(event.is_running())
	assert_almost_eq(event.remaining_time(), 12.0)
	assert_eq(event.begins, 1)
	assert_eq(rig["log"]["progress"], ["Probe probe"])
	assert_true(event.context.def == rig["def"])


func test_running_out_of_time_fails_once_and_cleans_up() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	event.step(5.0)
	assert_almost_eq(event.remaining_time(), 7.0)
	assert_true(event.is_running())
	event.step(8.0)
	assert_eq((rig["log"]["failed"] as Array).size(), 1)
	assert_true((rig["log"]["succeeded"] as Array).is_empty())
	assert_eq(event.cleanups, 1)
	assert_false(event.is_running())
	assert_true(event.has_ended())
	event.step(5.0)
	assert_eq((rig["log"]["failed"] as Array).size(), 1, "no second ending")


func test_success_ends_exactly_once() -> void:
	var rig := _begun()
	var event: ProbeEvent = rig["event"]
	event.finish(true)
	event.finish(true)
	event.finish(false)
	event.step(100.0)
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1)
	assert_true((rig["log"]["failed"] as Array).is_empty())
	assert_eq(event.cleanups, 1)


func test_abort_cleans_up_without_a_result() -> void:
	var rig := _begun()
	var event: ProbeEvent = rig["event"]
	event.abort()
	event.abort()
	event.finish(true)
	assert_eq(event.cleanups, 1)
	assert_true((rig["log"]["succeeded"] as Array).is_empty())
	assert_true((rig["log"]["failed"] as Array).is_empty())
	assert_true(event.has_ended())


func test_begin_twice_is_ignored() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	event.step(4.0)
	event.begin(ActorFixture.event_context(self, DefFactory.event(PROBE, 99.0)))
	assert_eq(event.begins, 1)
	assert_almost_eq(event.remaining_time(), 8.0)


func test_an_override_of_time_up_keeps_the_event_running() -> void:
	var def := DefFactory.event(PROBE, 3.0)
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	event.endless = true
	event.begin(ActorFixture.event_context(self, def))
	event.step(10.0)
	assert_true(event.is_running(), "the subclass decided what time up means")
	assert_almost_eq(event.remaining_time(), 0.0)
```

`tests/stage/test_event_director.gd`:
```gdscript
extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _endless_scene() -> PackedScene:
	var probe := PROBE.instantiate() as ProbeEvent
	probe.endless = true
	return DefFactory.packed(probe)


## A director over `pool` with `count` events in [10, 60], gap 20 (time limits 20), stage 100 s.
func _rig(pool: Array[EventDef], count: int, run_seed: int = 5) -> Dictionary:
	var stage_def := DefFactory.stage(100.0)
	stage_def.events = DefFactory.schedule(pool, count, 10.0, 60.0, 20.0)
	var layer := adopt(Node2D.new()) as Node2D
	var clock := adopt(StageClock.new()) as StageClock
	clock.set_physics_process(false)
	var director := adopt(EventDirector.new()) as EventDirector
	var log := {"started": [], "progress": [], "ended": []}
	director.event_started.connect(func(def: EventDef, event: StageEvent) -> void: (log["started"] as Array).append([def, event]))
	director.event_progressed.connect(func(def: EventDef, text: String) -> void: (log["progress"] as Array).append([def, text]))
	director.event_ended.connect(func(def: EventDef, success: bool) -> void: (log["ended"] as Array).append([def, success]))
	var context_factory := func(def: EventDef) -> EventContext:
		return EventContext.new(layer, null, null, null, null, RunRng.new(1), Rect2(), 0.0, def)
	director.bind(stage_def, clock, context_factory, RunRng.new(run_seed), PackedFloat32Array())
	clock.start(100.0)
	return {"director": director, "clock": clock, "layer": layer, "log": log}


func _pool(scene: PackedScene = PROBE) -> Array[EventDef]:
	var pool: Array[EventDef] = [DefFactory.event(scene, 20.0)]
	return pool


func test_times_are_rolled_once_from_the_seeded_schedule() -> void:
	var first: EventDirector = _rig(_pool(), 2, 5)["director"]
	var times := first.event_times()
	assert_eq(times.size(), 2)
	assert_true(times[0] >= 10.0 and times[1] <= 60.0)
	assert_true(times[1] - times[0] >= 20.0)
	assert_eq((_rig(_pool(), 2, 5)["director"] as EventDirector).event_times(), times, "same seed, same times")
	assert_eq(first.planned_defs().size(), 2)


func test_an_event_starts_on_the_tick_that_crosses_its_time() -> void:
	var pool := _pool()
	var rig := _rig(pool, 1)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	var started: Array = rig["log"]["started"]
	var time := director.event_times()[0]
	clock.step(time - 0.5)
	assert_true(started.is_empty())
	clock.step(1.0)
	assert_eq(started.size(), 1)
	assert_true(started[0][0] == pool[0])
	var event := started[0][1] as ProbeEvent
	assert_eq(event.get_parent(), rig["layer"], "placed on the context's layer")
	assert_eq(event.begins, 1)
	assert_true(director.running() == event)
	assert_true(event.context.def == pool[0])
	assert_eq(rig["log"]["progress"], [[pool[0], "Probe probe"]], "progress is relayed")


func test_events_run_one_at_a_time() -> void:
	var rig := _rig(_pool(_endless_scene()), 2)
	var director: EventDirector = rig["director"]
	var started: Array = rig["log"]["started"]
	(rig["clock"] as StageClock).step(director.event_times()[1] + 1.0)
	assert_eq(started.size(), 1, "the second waits while the first runs")
	var first := started[0][1] as ProbeEvent
	first.finish(true)
	assert_eq(rig["log"]["ended"], [[started[0][0], true]])
	await next_frame()
	assert_eq(started.size(), 2, "the waiting event starts once the first has ended")
	assert_true(director.running() == started[1][1])
	assert_true(is_instance_valid(started[1][1]) and (started[1][1] as ProbeEvent).is_running())


func test_success_and_failure_are_relayed_and_the_event_is_freed() -> void:
	var rig := _rig(_pool(), 1)
	var director: EventDirector = rig["director"]
	(rig["clock"] as StageClock).step(director.event_times()[0] + 0.1)
	var event := (rig["log"]["started"] as Array)[0][1] as ProbeEvent
	event.finish(false)
	assert_eq((rig["log"]["ended"] as Array).size(), 1)
	assert_false((rig["log"]["ended"] as Array)[0][1], "failure")
	assert_null(director.running())
	assert_true(event.is_queued_for_deletion())


func test_abort_running_ends_without_reward_and_drops_waiting_events() -> void:
	var rig := _rig(_pool(_endless_scene()), 2)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	clock.step(director.event_times()[1] + 1.0)
	var first := (rig["log"]["started"] as Array)[0][1] as ProbeEvent
	director.abort_running()
	assert_eq(first.cleanups, 1)
	assert_eq((rig["log"]["ended"] as Array).size(), 1)
	assert_false((rig["log"]["ended"] as Array)[0][1], "an aborted event never succeeds")
	assert_null(director.running())
	director.abort_running()
	assert_eq((rig["log"]["ended"] as Array).size(), 1, "nothing left to abort")
	clock.step(5.0)
	await next_frame()
	assert_eq((rig["log"]["started"] as Array).size(), 1, "the waiting event was dropped")


func test_no_event_starts_in_overtime() -> void:
	var rig := _rig(_pool(), 1)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	clock.step(director.event_times()[0] + 0.1)
	((rig["log"]["started"] as Array)[0][1] as ProbeEvent).finish(true)
	for i in 10:
		clock.step(20.0)
	await next_frame()
	assert_true(clock.elapsed > 100.0, "well past the stage end")
	assert_eq((rig["log"]["started"] as Array).size(), 1)


func test_without_a_schedule_there_are_no_events() -> void:
	var stage_def := DefFactory.stage(100.0)
	var clock := adopt(StageClock.new()) as StageClock
	clock.set_physics_process(false)
	var director := adopt(EventDirector.new()) as EventDirector
	var started: Array = []
	director.event_started.connect(func(def: EventDef, _event: StageEvent) -> void: started.append(def))
	director.bind(stage_def, clock, func(_def: EventDef) -> EventContext: return null, RunRng.new(1), PackedFloat32Array())
	clock.start(100.0)
	clock.step(150.0)
	assert_eq(director.event_times().size(), 0)
	assert_true(started.is_empty())


func test_validate_defs_requires_a_stage_event_root() -> void:
	var good := _pool()
	assert_no_errors(EventDirector.validate_defs(DefFactory.schedule(good)), "probe")
	var bad: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"plain")]
	var errors := EventDirector.validate_defs(DefFactory.schedule(bad))
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("plain"))
	assert_no_errors(EventDirector.validate_defs(null), "no schedule")
```

Append to `tests/defs/test_event_def.gd`:
```gdscript
func test_controller_root_may_reject_itself() -> void:
	var probe := (load("res://tests/support/probe_event.tscn") as PackedScene).instantiate() as ProbeEvent
	probe.reject_reason = "probe says no"
	var errors := DefFactory.event(DefFactory.packed(probe)).validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/stage/test_stage_event TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `StageEvent`, `EventContext`, `EventDirector`, `ProbeEvent` not found.

- [ ] **Step 4: Implement**

`src/stage/event_context.gd`:
```gdscript
class_name EventContext
extends RefCounted
## What a StageEvent may use. The Stage builds one per event; event code never imports Stage.

## Where the event controller lives; the objects it places (statues, warnings, strikes) are its children.
var layer: Node2D
var factory: EnemyFactory
var targets: TargetProvider
var player: Node2D
var building: Node2D
var rng: RunRng
var bounds: Rect2
var spawn_radius: float
var def: EventDef


func _init(p_layer: Node2D, p_factory: EnemyFactory, p_targets: TargetProvider, p_player: Node2D, p_building: Node2D, p_rng: RunRng, p_bounds: Rect2, p_spawn_radius: float, p_def: EventDef) -> void:
	layer = p_layer
	factory = p_factory
	targets = p_targets
	player = p_player
	building = p_building
	rng = p_rng
	bounds = p_bounds
	spawn_radius = p_spawn_radius
	def = p_def
```

`src/stage/stage_event.gd`:
```gdscript
@abstract
class_name StageEvent
extends Node2D
## Base of an event controller (the root of an EventDef.controller_scene). The base owns the time limit
## (EventDef.time_limit) and guarantees the event ends exactly once: `_succeed()` / `_fail()` emit their
## signal after `_cleanup()`, `abort()` only cleans up. Running out of time calls `_on_time_up()`, which
## fails unless a subclass overrides it. Physics drives `step()` in play; tests call it by hand.

signal succeeded
signal failed
signal progress_changed(text: String)

var context: EventContext

var _time_left: float = 0.0
var _running: bool = false
var _ended: bool = false


func begin(p_context: EventContext) -> void:
	if _running or _ended:
		return
	context = p_context
	_time_left = p_context.def.time_limit
	_running = true
	_on_begin()


## Ends the event without a result (boss appearance, run end): cleanup only, no signal.
func abort() -> void:
	if not _running:
		return
	_running = false
	_ended = true
	_cleanup()


func step(delta: float) -> void:
	if not _running:
		return
	_time_left = maxf(_time_left - delta, 0.0)
	_on_step(delta)
	if _running and _time_left <= 0.0:
		_on_time_up()


## Things the HUD points at when they are off-screen.
func objectives() -> Array[Node2D]:
	var none: Array[Node2D] = []
	return none


func remaining_time() -> float:
	return _time_left


func is_running() -> bool:
	return _running


func has_ended() -> bool:
	return _ended


func report_progress(text: String) -> void:
	progress_changed.emit(text)


func _succeed() -> void:
	_finish(true)


func _fail() -> void:
	_finish(false)


## Subclass hooks.
func _on_begin() -> void:
	pass


func _on_step(_delta: float) -> void:
	pass


func _on_time_up() -> void:
	_fail()


func _cleanup() -> void:
	pass


func _finish(success: bool) -> void:
	if not _running:
		return
	_running = false
	_ended = true
	_cleanup()
	if success:
		succeeded.emit()
	else:
		failed.emit()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/stage/event_director.gd`:
```gdscript
class_name EventDirector
extends Node
## Rolls the stage's event times once (seeded), starts each event on the clock tick that crosses its time
## and relays its signals. At most one event runs: an event whose time comes while another runs waits and
## starts when that one ends. Events are EventDef.controller_scene instances; this file never names a
## concrete event.

signal event_started(def: EventDef, event: StageEvent)
signal event_progressed(def: EventDef, text: String)
signal event_ended(def: EventDef, success: bool)

var _timeline: Timeline = Timeline.new(0.0)
var _defs: Array[EventDef] = []
var _context_factory: Callable
var _previous_time: float = -1.0
var _waiting: Array[int] = []
var _running: StageEvent
var _running_def: EventDef


## `context_factory` is `func(def: EventDef) -> EventContext`.
func bind(stage_def: StageDef, clock: StageClock, context_factory: Callable, rng: RunRng, wave_times: PackedFloat32Array) -> void:
	_context_factory = context_factory
	var times := PackedFloat32Array()
	if stage_def.events != null:
		times = stage_def.events.roll_times(stage_def.duration, wave_times, rng)
		_defs = stage_def.events.roll_defs(times.size(), rng)
	_timeline = Timeline.new(stage_def.duration, times)
	clock.ticked.connect(_on_clock_ticked)


## Rolled start times, ascending; the HUD draws them on the timeline.
func event_times() -> PackedFloat32Array:
	return _timeline.marks


func planned_defs() -> Array[EventDef]:
	return _defs.duplicate()


func running() -> StageEvent:
	return _running


## Ends the running event without reward (boss appearance, run end) and drops any waiting one.
func abort_running() -> void:
	_waiting.clear()
	if _running == null:
		return
	var event := _running
	var def := _running_def
	_running = null
	_running_def = null
	event.abort()
	event.queue_free()
	event_ended.emit(def, false)


## Stage-layer check run by Stage.start: every controller root must extend StageEvent.
static func validate_defs(schedule: EventSchedule) -> PackedStringArray:
	var errors := PackedStringArray()
	if schedule == null:
		return errors
	for def in schedule.pool:
		if def == null or def.controller_scene == null:
			continue  # EventDef.validate reports these
		var root := def.controller_scene.instantiate()
		if root == null:
			continue
		if not (root is StageEvent):
			errors.append("event '%s': controller_scene root is not a StageEvent" % def.id)
		root.free()
	return errors


func _on_clock_ticked(elapsed: float) -> void:
	for index in _timeline.crossed(_previous_time, elapsed):
		_waiting.append(index)
	_previous_time = elapsed
	_start_next()


func _start_next() -> void:
	if _running != null or _waiting.is_empty():
		return
	var def := _defs[_waiting.pop_front()]
	var event := def.controller_scene.instantiate() as StageEvent
	var context := _context_factory.call(def) as EventContext
	context.layer.add_child(event)
	_running = event
	_running_def = def
	event.progress_changed.connect(_on_progress_changed.bind(def))
	event.succeeded.connect(_on_event_finished.bind(event, def, true))
	event.failed.connect(_on_event_finished.bind(event, def, false))
	event_started.emit(def, event)
	event.begin(context)


func _on_progress_changed(text: String, def: EventDef) -> void:
	event_progressed.emit(def, text)


func _on_event_finished(event: StageEvent, def: EventDef, success: bool) -> void:
	if event != _running:
		return
	_running = null
	_running_def = null
	event.queue_free()
	event_ended.emit(def, success)
	# Deferred: events usually end inside a physics callback (a hit), where new areas must not be added.
	_start_next.call_deferred()
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/stage/test_stage_event ...`, `FILTER=tests/stage/test_event_director ...`, `FILTER=tests/defs/test_event_def ...` (all with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: 6 + 8 + 6 PASS. Likely snags:
- `ProbeEvent.new()` refused because `StageEvent` is abstract: only the base is abstract; instantiate the probe through its scene (the tests do).
- `super._on_time_up()` refused by the parser: write `super()` inside `_on_time_up` instead.
- The relayed progress is missing: `progress_changed` must be connected **before** `event.begin(context)` (the probe reports during `_on_begin`).

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/stage tests/stage tests/defs tests/support
git commit -q -m "feat(stage): add StageEvent, EventContext and EventDirector (seeded times, one event at a time)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Statue actor and the statue event

**Files:**
- Create: `src/actors/statue/statue.gd`, `src/actors/statue/statue.tscn`, `src/events/statue_event.gd`, `src/events/statue_event.tscn`
- Test: `tests/actors/test_statue.gd` (new), `tests/events/test_statue_event.gd` (new)

**Interfaces:**
- Consumes: `StageEvent` hooks (`_on_begin`, `_cleanup`, `_succeed`, `report_progress`, `context`), `EventContext` (`bounds`, `rng`, `building`, `player`), `Health` (`bind`, `changed`, `died`, `current`, `maximum`), `Hurtbox.bind`, `StatBlock`, `Groups.ENEMIES`, `ActorFixture.kill/event_context` (Task 6), `DefFactory.event` (Task 3), `TargetProvider.nearest_enemy`.
- Produces:
  - `Statue` (Node2D, `src/actors/statue/statue.tscn`: `AbilitySystemComponent`, `Health`, `Hurtbox` on ENEMY_HURT): `signal destroyed(statue: Statue)`; public `asc`, `health`; `setup(stats: StatBlock)` (after entering the tree; joins `Groups.ENEMIES`); `remove()` (out of play without counting as destroyed; idempotent); `is_removed() -> bool`. Not an `Enemy`: no kill count, no `Event.Kill`, no drops.
  - `StatueEvent extends StageEvent` (`src/events/statue_event.tscn`): exports `statue_scene`, `statue_count` (4), `statue_stats` (60 HP), `min_distance` (250), `edge_margin` (48), `placement_tries` (32); `validate()`; `objectives()` = living statues; progress `"Statues k/N"`.

- [ ] **Step 1: Write the failing tests**

`tests/actors/test_statue.gd`:
```gdscript
extends TestCase

const STATUE_SCENE: PackedScene = preload("res://src/actors/statue/statue.tscn")


func _statue(at: Vector2, max_health: float = 60.0) -> Statue:
	var statue := adopt(STATUE_SCENE.instantiate()) as Statue
	statue.global_position = at
	var stats := StatBlock.new()
	stats.max_health = max_health
	stats.move_speed = 0.0
	statue.setup(stats)
	return statue


func test_setup_applies_stats_and_makes_it_a_target() -> void:
	var statue := _statue(Vector2(240, -130), 60.0)
	assert_almost_eq(statue.health.current(), 60.0)
	assert_true(statue.is_in_group(Groups.ENEMIES))
	var targets := adopt(TargetProvider.new()) as TargetProvider
	assert_eq(targets.nearest_enemy(Vector2(200, -100), 100.0), statue, "auto-aim sees it")


func test_destroying_it_announces_once_and_frees_it() -> void:
	var statue := _statue(Vector2(-300, 80))
	var destroyed: Array = []
	statue.destroyed.connect(func(found: Statue) -> void: destroyed.append(found))
	ActorFixture.kill(self, statue.asc)
	assert_eq(destroyed, [statue])
	assert_false(statue.is_in_group(Groups.ENEMIES))
	assert_true(statue.is_removed())
	ActorFixture.kill(self, statue.asc)
	assert_eq(destroyed.size(), 1, "dead once")
	await next_frame()
	await next_frame()
	assert_false(is_instance_valid(statue))


func test_remove_takes_it_out_without_counting_as_destroyed() -> void:
	var statue := _statue(Vector2(90, 410))
	var destroyed: Array = []
	statue.destroyed.connect(func(found: Statue) -> void: destroyed.append(found))
	statue.remove()
	statue.remove()
	assert_true(destroyed.is_empty())
	assert_false(statue.is_in_group(Groups.ENEMIES))
	await next_frame()
	await next_frame()
	assert_false(is_instance_valid(statue))


func test_a_projectile_on_the_enemy_layer_can_damage_it() -> void:
	var statue := _statue(Vector2(500, 300))
	var shooter := ActorFixture.bare_actor(self, StatBlock.new())
	var projectile := adopt(preload("res://src/actors/projectile/projectile.tscn").instantiate()) as Projectile
	projectile.global_position = Vector2(490, 284)
	projectile.launch(preload("res://data/projectiles/arrow.tres"), ActorFixture.asc_of(shooter), 12.0, Vector2.RIGHT)
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(statue.health.current(), 48.0)
```

`tests/events/test_statue_event.gd`:
```gdscript
extends TestCase

const STATUE_EVENT: PackedScene = preload("res://src/events/statue_event.tscn")
const BUILDING_AT: Vector2 = Vector2(100, -50)


func _rig(min_distance: float = -1.0) -> Dictionary:
	var def := DefFactory.event(STATUE_EVENT, 25.0, &"statues")
	var context := ActorFixture.event_context(self, def, null, BUILDING_AT)
	var event := adopt(STATUE_EVENT.instantiate()) as StatueEvent
	event.set_physics_process(false)
	if min_distance >= 0.0:
		event.min_distance = min_distance
	var log := {"succeeded": [], "failed": [], "progress": []}
	event.succeeded.connect(func() -> void: (log["succeeded"] as Array).append(true))
	event.failed.connect(func() -> void: (log["failed"] as Array).append(true))
	event.progress_changed.connect(func(text: String) -> void: (log["progress"] as Array).append(text))
	event.begin(context)
	return {"event": event, "context": context, "log": log}


func _statues(event: StatueEvent) -> Array[Statue]:
	var found: Array[Statue] = []
	for node in event.objectives():
		found.append(node as Statue)
	return found


func test_spawns_the_statues_away_from_the_building_and_the_player() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var context: EventContext = rig["context"]
	var statues := _statues(event)
	assert_eq(statues.size(), 4)
	var inner := context.bounds.grow(-48.0)
	for statue in statues:
		assert_true(statue.global_position.distance_to(context.building.global_position) >= 250.0, "far from the building")
		assert_true(statue.global_position.distance_to(context.player.global_position) >= 250.0, "far from the player")
		assert_true(inner.has_point(statue.global_position), "inside the arena margin")
		assert_almost_eq(statue.health.current(), 60.0)
		assert_eq(statue.get_parent(), event, "event-owned")
	assert_eq(rig["log"]["progress"], ["Statues 0/4"])


func test_destroying_every_statue_succeeds_once() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	for i in 3:
		ActorFixture.kill(self, statues[i].asc)
	assert_eq((rig["log"]["progress"] as Array).back(), "Statues 3/4")
	assert_eq(event.objectives().size(), 1)
	assert_true(event.is_running())
	ActorFixture.kill(self, statues[3].asc)
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1)
	assert_true((rig["log"]["failed"] as Array).is_empty())
	assert_false(event.is_running())


func test_time_up_removes_the_remaining_statues_and_fails() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	ActorFixture.kill(self, statues[0].asc)
	event.step(24.0)
	assert_true(event.is_running())
	event.step(1.5)
	assert_eq((rig["log"]["failed"] as Array).size(), 1)
	for i in range(1, 4):
		assert_true(statues[i].is_removed(), "statue %d removed" % i)
	assert_true(event.objectives().is_empty())
	await next_frame()
	await next_frame()
	assert_eq(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size(), 0)


func test_abort_removes_the_statues_without_a_result() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	event.abort()
	assert_true((rig["log"]["succeeded"] as Array).is_empty())
	assert_true((rig["log"]["failed"] as Array).is_empty())
	for statue in statues:
		assert_true(statue.is_removed())


func test_impossible_spacing_still_places_every_statue() -> void:
	var rig := _rig(100000.0)
	assert_eq(_statues(rig["event"]).size(), 4, "falls back to the best spot found, never hangs")


func test_validate_reports_missing_scene_and_bad_numbers() -> void:
	var event := STATUE_EVENT.instantiate() as StatueEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.statue_scene = null
	event.statue_count = 0
	event.min_distance = -1.0
	assert_eq(event.validate().size(), 3)
	event.free()
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/actors/test_statue TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `Statue` not found / `statue.tscn` missing.

- [ ] **Step 3: Implement the statue**

`src/actors/statue/statue.gd`:
```gdscript
class_name Statue
extends Node2D
## A destructible event objective: ASC + Health + a Hurtbox on ENEMY_HURT, in the enemy group so auto-aim
## shoots it. Not an Enemy: no kill count, no Event.Kill, no drops. Drawn as a simple stone pillar that
## darkens as it takes damage.

signal destroyed(statue: Statue)

@export var radius: float = 16.0
@export var stone_color: Color = Color(0.62, 0.6, 0.56)
@export var cracked_color: Color = Color(0.3, 0.26, 0.24)

var _removed: bool = false

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox


## Call after the statue has entered the tree.
func setup(stats: StatBlock) -> void:
	asc.initialize_attribute_overrides(stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	health.changed.connect(_on_health_changed)
	health.died.connect(_on_died)
	add_to_group(Groups.ENEMIES)
	queue_redraw()


## Takes the statue out of play without counting it as destroyed (event cleanup). Safe inside physics callbacks.
func remove() -> void:
	if _removed:
		return
	_removed = true
	remove_from_group(Groups.ENEMIES)
	_hurtbox.set_deferred("monitorable", false)
	asc.cleanup.call_deferred()
	queue_free()


func is_removed() -> bool:
	return _removed


func _on_died() -> void:
	if _removed:
		return
	remove()
	destroyed.emit(self)


func _on_health_changed(_current: float, _maximum: float) -> void:
	queue_redraw()


func _draw() -> void:
	var ratio := 1.0
	if health != null and health.maximum() > 0.0:
		ratio = clampf(health.current() / health.maximum(), 0.0, 1.0)
	var color := cracked_color.lerp(stone_color, ratio)
	draw_rect(Rect2(-radius, -radius * 0.4, radius * 2.0, radius * 0.6), color.darkened(0.25))
	draw_rect(Rect2(-radius * 0.6, -radius * 2.4, radius * 1.2, radius * 2.1), color)
	draw_circle(Vector2(0.0, -radius * 2.6), radius * 0.55, color)
```

`src/actors/statue/statue.tscn`:
```
[gd_scene load_steps=6 format=3]

[ext_resource type="Script" path="res://src/actors/statue/statue.gd" id="1_statue"]
[ext_resource type="PackedScene" path="res://src/gas/actor_asc.tscn" id="2_asc"]
[ext_resource type="Script" path="res://src/components/health.gd" id="3_health"]
[ext_resource type="Script" path="res://src/components/hurtbox.gd" id="4_hurtbox"]

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
radius = 20.0

[node name="Statue" type="Node2D"]
script = ExtResource("1_statue")

[node name="AbilitySystemComponent" parent="." instance=ExtResource("2_asc")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 16
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
position = Vector2(0, -16)
shape = SubResource("CircleShape2D_hurt")
```

- [ ] **Step 4: Implement the statue event**

`src/events/statue_event.gd`:
```gdscript
class_name StatueEvent
extends StageEvent
## "Destroy the statues": spawns statues at random arena points at least `min_distance` from the building
## and the player. All destroyed in time -> success; time up -> the rest are removed and the event fails.

@export var statue_scene: PackedScene
@export var statue_count: int = 0
@export var statue_stats: StatBlock
@export var min_distance: float = 0.0
## Keeps statues off the arena walls.
@export var edge_margin: float = 0.0
## Random points tried per statue before settling for the best one found.
@export var placement_tries: int = 0

var _statues: Array[Statue] = []
var _destroyed: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if statue_scene == null:
		errors.append("StatueEvent: statue_scene is null")
	if statue_count < 1:
		errors.append("StatueEvent: statue_count must be >= 1")
	if statue_stats == null:
		errors.append("StatueEvent: statue_stats is null")
	else:
		errors.append_array(statue_stats.validate())
	if min_distance < 0.0 or edge_margin < 0.0:
		errors.append("StatueEvent: min_distance and edge_margin must be >= 0")
	if placement_tries < 1:
		errors.append("StatueEvent: placement_tries must be >= 1")
	return errors


func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for statue in _statues:
		if is_instance_valid(statue):
			found.append(statue)
	return found


func _on_begin() -> void:
	for i in statue_count:
		var statue := statue_scene.instantiate() as Statue
		add_child(statue)
		statue.global_position = _pick_point()
		statue.setup(statue_stats)
		statue.destroyed.connect(_on_statue_destroyed)
		_statues.append(statue)
	_report()


func _pick_point() -> Vector2:
	var area := context.bounds.grow(-edge_margin)
	var best := area.get_center()
	var best_clearance := -INF
	for attempt in placement_tries:
		var point := Vector2(context.rng.randf_range(area.position.x, area.end.x), context.rng.randf_range(area.position.y, area.end.y))
		var clearance := _clearance(point)
		if clearance >= min_distance:
			return point
		if clearance > best_clearance:
			best = point
			best_clearance = clearance
	return best


## Distance from `point` to the nearer of the building and the player.
func _clearance(point: Vector2) -> float:
	var nearest := INF
	for anchor in [context.building, context.player]:
		if is_instance_valid(anchor):
			nearest = minf(nearest, point.distance_to((anchor as Node2D).global_position))
	return nearest


func _on_statue_destroyed(statue: Statue) -> void:
	if not is_running():
		return
	_statues.erase(statue)
	_destroyed += 1
	_report()
	if _destroyed >= statue_count:
		_succeed()


func _report() -> void:
	report_progress("Statues %d/%d" % [_destroyed, statue_count])


func _cleanup() -> void:
	for statue in _statues:
		if is_instance_valid(statue):
			statue.remove()
	_statues.clear()
```

`src/events/statue_event.tscn`:
```
[gd_scene load_steps=5 format=3]

[ext_resource type="Script" path="res://src/events/statue_event.gd" id="1_event"]
[ext_resource type="PackedScene" path="res://src/actors/statue/statue.tscn" id="2_statue"]
[ext_resource type="Script" path="res://src/defs/stat_block.gd" id="3_stats"]

[sub_resource type="Resource" id="Resource_statue_stats"]
script = ExtResource("3_stats")
max_health = 60.0
move_speed = 0.0

[node name="StatueEvent" type="Node2D"]
script = ExtResource("1_event")
statue_scene = ExtResource("2_statue")
statue_count = 4
statue_stats = SubResource("Resource_statue_stats")
min_distance = 250.0
edge_margin = 48.0
placement_tries = 32
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/actors/test_statue ...` and `FILTER=tests/events/test_statue_event ...` (with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: 4 + 6 PASS. Likely snags:
- The projectile test misses: the statue's hurt circle sits 16 px above its origin (`position = Vector2(0, -16)`, radius 20) — the projectile starts at `(490, 284)`, inside it; keep the numbers in sync if you move the shape.
- `test_time_up_...` counts other enemies-group nodes: the rig spawns none, so 0 is exact once the removed statues are freed.

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/actors/statue src/events tests/actors tests/events
git commit -q -m "feat(events): add the Statue actor and the destroy-the-statues event

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Meteor event

**Files:**
- Create: `src/events/meteor_event.gd`, `src/events/meteor_event.tscn`
- Test: `tests/events/test_meteor_event.gd` (new)

**Interfaces:**
- Consumes: `StageEvent` hooks and `is_running()` (Task 6), `Telegraph.show_circle` and `AreaStrike.strike(..., PhysicsLayers.ALLY_HURT, only_actor)` + `struck` (Task 5), `RunRng.offset_in_circle(radius)`, `src/gas/actor_asc.tscn`, `ActorFixture.player/event_context/hurt_target` (Player has public `health` and `asc`).
- Produces: `MeteorEvent extends StageEvent` (`src/events/meteor_event.tscn`): exports `strike_scene` (`area_strike.tscn`), `hit_effect` (`damage_basic.tres`), `interval` 1.2, `warning` 0.9, `radius` 60, `damage` 15, `landing_spread` 120, `max_hits` 3, `strike_seconds` 0.15; `hits() -> int`; `validate()`. The first meteor drops one `interval` after `begin`; each lands `warning` later at the point chosen when it dropped. Hits are counted only when the effect lands (an invulnerable dash dodges). More than `max_hits` → fail at once; time up (the def's `time_limit`, 20 s) → success. Progress `"Hits k/max"`. The event's own ASC (child named `AbilitySystemComponent`) is the damage source.

- [ ] **Step 1: Write the failing tests**

`tests/events/test_meteor_event.gd`:
```gdscript
extends TestCase

const METEOR_EVENT: PackedScene = preload("res://src/events/meteor_event.tscn")
const PLAYER_AT: Vector2 = Vector2(300, 200)


## The player stands at PLAYER_AT; landing_spread 0 makes every meteor land exactly on it.
func _rig(max_hits: int = 3) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var def := DefFactory.event(METEOR_EVENT, 20.0, &"meteors")
	var context := ActorFixture.event_context(self, def, player)
	var event := adopt(METEOR_EVENT.instantiate()) as MeteorEvent
	event.set_physics_process(false)
	event.landing_spread = 0.0
	event.max_hits = max_hits
	var log := {"succeeded": [], "failed": [], "progress": []}
	event.succeeded.connect(func() -> void: (log["succeeded"] as Array).append(true))
	event.failed.connect(func() -> void: (log["failed"] as Array).append(true))
	event.progress_changed.connect(func(text: String) -> void: (log["progress"] as Array).append(text))
	event.begin(context)
	return {"event": event, "player": player, "log": log}


func _telegraphs(event: MeteorEvent) -> Array[Telegraph]:
	var found: Array[Telegraph] = []
	for child in event.get_children():
		if child is Telegraph and not child.is_queued_for_deletion():
			found.append(child)
	return found


func _strikes(event: MeteorEvent) -> Array[AreaStrike]:
	var found: Array[AreaStrike] = []
	for child in event.get_children():
		if child is AreaStrike and not child.is_queued_for_deletion():
			found.append(child)
	return found


func _settle() -> void:
	await next_physics_frame()
	await next_physics_frame()


func test_a_warning_appears_every_interval_and_hits_only_after_it() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	var player: Player = rig["player"]
	assert_eq(rig["log"]["progress"], ["Hits 0/3"])
	event.step(1.1)
	assert_true(_telegraphs(event).is_empty(), "nothing before the first interval")
	event.step(0.15)
	var warnings := _telegraphs(event)
	assert_eq(warnings.size(), 1)
	assert_eq(warnings[0].global_position, PLAYER_AT)
	assert_almost_eq(warnings[0].radius, 60.0)
	event.step(0.5)
	await _settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "still only a warning")
	event.step(0.5)
	assert_eq(_strikes(event).size(), 1, "landed")
	assert_true(_telegraphs(event).is_empty(), "the warning is gone")
	await _settle()
	assert_almost_eq(player.health.current(), 85.0)
	assert_eq(event.hits(), 1)
	assert_eq((rig["log"]["progress"] as Array).back(), "Hits 1/3")


func test_more_than_max_hits_fails_at_once() -> void:
	var rig := _rig(1)
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	event.step(0.9)
	await _settle()
	assert_eq(event.hits(), 1)
	assert_true(event.is_running(), "max_hits itself is allowed")
	event.step(0.35)
	event.step(0.9)
	await _settle()
	assert_eq(event.hits(), 2)
	assert_eq((rig["log"]["failed"] as Array).size(), 1)
	assert_false(event.is_running())


func test_dodged_meteors_do_not_count_and_surviving_succeeds() -> void:
	var rig := _rig(0)
	var event: MeteorEvent = rig["event"]
	var player: Player = rig["player"]
	player.asc.add_tag(GameplayTags.State_Invulnerable)
	event.step(1.2)
	event.step(0.9)
	await _settle()
	assert_eq(event.hits(), 0, "an invulnerable dash dodges the meteor")
	assert_almost_eq(player.health.current(), 100.0)
	event.step(18.0)
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1, "survived the 20 s")
	assert_true((rig["log"]["failed"] as Array).is_empty())


func test_a_hit_after_the_event_ended_changes_nothing() -> void:
	var rig := _rig(0)
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	event.step(0.9)
	event.step(18.0)
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1, "time is up before the strike reports")
	await _settle()
	assert_true((rig["log"]["failed"] as Array).is_empty(), "a late hit cannot fail a finished event")
	assert_eq(event.hits(), 0)


func test_meteors_spare_the_building() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	var building := ActorFixture.hurt_target(self, PLAYER_AT + Vector2(15, 0), PhysicsLayers.ALLY_HURT)
	event.step(1.2)
	event.step(0.9)
	await _settle()
	assert_almost_eq((rig["player"] as Player).health.current(), 85.0)
	assert_almost_eq(ActorFixture.asc_of(building).get_attribute("health").current_value, 100.0, 0.001, "ally hurt layer, but not the player")


func test_abort_clears_pending_warnings_without_a_result() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	assert_eq(_telegraphs(event).size(), 1)
	event.abort()
	assert_true(_telegraphs(event).is_empty())
	event.step(5.0)
	assert_true(_strikes(event).is_empty(), "nothing lands after the abort")
	assert_true((rig["log"]["succeeded"] as Array).is_empty())
	assert_true((rig["log"]["failed"] as Array).is_empty())


func test_validate_flags_missing_parts_and_bad_numbers() -> void:
	var event := METEOR_EVENT.instantiate() as MeteorEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.strike_scene = null
	event.interval = 0.0
	event.max_hits = -1
	assert_eq(event.validate().size(), 3)
	event.free()
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/events/test_meteor_event TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `MeteorEvent` not found.

- [ ] **Step 3: Implement**

`src/events/meteor_event.gd`:
```gdscript
class_name MeteorEvent
extends StageEvent
## "Meteor shower": every `interval` seconds a warning circle appears within `landing_spread` of the player;
## `warning` seconds later an AreaStrike hits only the player there. More than `max_hits` landed hits fails
## at once; surviving the time limit succeeds. The event owns a small ASC so the damage has a GAS source.

const ASC_SCENE: PackedScene = preload("res://src/gas/actor_asc.tscn")

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var interval: float = 0.0
@export var warning: float = 0.0
@export var radius: float = 0.0
@export var damage: float = 0.0
@export var landing_spread: float = 0.0
@export var max_hits: int = 0
## How long each landed strike stays active; it must outlive a physics step so overlaps are reported.
@export var strike_seconds: float = 0.0


## A warned meteor that has not landed yet.
class Incoming:
	var position: Vector2
	var left: float
	var telegraph: Telegraph

	func _init(p_position: Vector2, p_left: float, p_telegraph: Telegraph) -> void:
		position = p_position
		left = p_left
		telegraph = p_telegraph


var _source_asc: AbilitySystemComponent
var _next_drop_in: float = 0.0
var _incoming: Array[Incoming] = []
var _hits: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null or hit_effect == null:
		errors.append("MeteorEvent: strike_scene and hit_effect are required")
	if interval <= 0.0 or warning <= 0.0 or radius <= 0.0 or strike_seconds <= 0.0:
		errors.append("MeteorEvent: interval, warning, radius and strike_seconds must be > 0")
	if damage < 0.0 or landing_spread < 0.0 or max_hits < 0:
		errors.append("MeteorEvent: damage, landing_spread and max_hits must be >= 0")
	return errors


func hits() -> int:
	return _hits


func _on_begin() -> void:
	_source_asc = ASC_SCENE.instantiate() as AbilitySystemComponent
	_source_asc.name = "AbilitySystemComponent"
	add_child(_source_asc)
	_next_drop_in = interval
	_report()


func _on_step(delta: float) -> void:
	for i in range(_incoming.size() - 1, -1, -1):
		var meteor := _incoming[i]
		meteor.left -= delta
		if meteor.left <= 0.0:
			_incoming.remove_at(i)
			_land(meteor)
	_next_drop_in -= delta
	while _next_drop_in <= 0.0:
		_drop()
		_next_drop_in += interval


func _drop() -> void:
	if not is_instance_valid(context.player):
		return
	var point: Vector2 = context.player.global_position + context.rng.offset_in_circle(landing_spread)
	var telegraph := Telegraph.new()
	add_child(telegraph)
	telegraph.global_position = point
	telegraph.show_circle(radius, warning)
	_incoming.append(Incoming.new(point, warning, telegraph))


func _land(meteor: Incoming) -> void:
	if is_instance_valid(meteor.telegraph):
		meteor.telegraph.queue_free()
	var strike := strike_scene.instantiate() as AreaStrike
	add_child(strike)
	strike.global_position = meteor.position
	strike.struck.connect(_on_player_struck)
	strike.strike(_source_asc, hit_effect, damage, radius, strike_seconds, PhysicsLayers.ALLY_HURT, context.player)


func _on_player_struck(_hurtbox: Hurtbox) -> void:
	if not is_running():
		return
	_hits += 1
	_report()
	if _hits > max_hits:
		_fail()


## Surviving the shower is the objective.
func _on_time_up() -> void:
	_succeed()


func _report() -> void:
	report_progress("Hits %d/%d" % [_hits, max_hits])


func _cleanup() -> void:
	for meteor in _incoming:
		if is_instance_valid(meteor.telegraph):
			meteor.telegraph.queue_free()
	_incoming.clear()
	if is_instance_valid(_source_asc):
		_source_asc.cleanup()
```
(`StageEvent.step` decrements the time limit before `_on_step`, then checks time up, so the meteors of the final step still land before the success; strikes that report after the end are ignored by the `is_running()` guard.)

`src/events/meteor_event.tscn`:
```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://src/events/meteor_event.gd" id="1_event"]
[ext_resource type="PackedScene" path="res://src/components/area_strike.tscn" id="2_strike"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[node name="MeteorEvent" type="Node2D"]
script = ExtResource("1_event")
strike_scene = ExtResource("2_strike")
hit_effect = ExtResource("3_damage")
interval = 1.2
warning = 0.9
radius = 60.0
damage = 15.0
landing_spread = 120.0
max_hits = 3
strike_seconds = 0.15
```

- [ ] **Step 4: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/events/test_meteor_event TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: 7 PASS. Likely snags:
- Float drift: step sums that should exactly reach a drop time (e.g. `0.9 + 0.3` vs `1.2`) can fall a hair short; the tests overshoot on purpose (`0.15`, `0.35`, `18.0`). Keep that when adding tests.
- The player is hit twice: the arrow ability of the fixture player is silenced; any second hit means a second meteor landed — check the step sums against `interval`.
- `class Incoming` inside a `class_name` script must come after the exports and before the vars that use it (as written).

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/events tests/events
git commit -q -m "feat(events): add the meteor shower event (warned AreaStrikes on the player, max hits)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Elite event and the elite orc

**Files:**
- Create: `src/events/elite_event.gd`, `src/events/elite_event.tscn`, `data/enemies/elite_orc.tres`
- Modify: `tests/data/test_game_data.gd` (`DATA_FILES`, the generic-scene list, one new test)
- Test: `tests/events/test_elite_event.gd` (new)

**Interfaces:**
- Consumes: `StageEvent` hooks (Task 6), `EventContext.factory/building/spawn_radius/bounds/rng`, `EnemyFactory.spawn(def, position) -> Enemy`, `Enemy.died(enemy)`, `SpawnGeometry.ring_point/clamp_to_rect`, `ActorFixture.kill/event_context`.
- Produces:
  - `EliteEvent extends StageEvent` (`src/events/elite_event.tscn`): exports `elite_enemy: EnemyDef` (`elite_orc.tres`), `count` 3, `edge_margin` 24; `validate()`; `objectives()` = living elites; progress `"Elites k/N"`. Elites come from the `EnemyFactory`, so kills, `Event.Kill` (Bounty) and drops work; on time up or abort the survivors stay in the fight.
  - `data/enemies/elite_orc.tres`: `id = &"elite_orc"`, 300 HP, speed 70, `visual_scale` 1.8, red tint, `target_policy` PLAYER, contact 15 every 0.9 s, `body_radius` 18, the orc's visual and drops.

- [ ] **Step 1: Write the failing tests**

`tests/events/test_elite_event.gd`:
```gdscript
extends TestCase

const ELITE_EVENT: PackedScene = preload("res://src/events/elite_event.tscn")
const BUILDING_AT: Vector2 = Vector2(50, 40)


func _rig() -> Dictionary:
	var def := DefFactory.event(ELITE_EVENT, 25.0, &"elites")
	var context := ActorFixture.event_context(self, def, null, BUILDING_AT)
	var event := adopt(ELITE_EVENT.instantiate()) as EliteEvent
	event.set_physics_process(false)
	var log := {"succeeded": [], "failed": [], "progress": []}
	event.succeeded.connect(func() -> void: (log["succeeded"] as Array).append(true))
	event.failed.connect(func() -> void: (log["failed"] as Array).append(true))
	event.progress_changed.connect(func(text: String) -> void: (log["progress"] as Array).append(text))
	event.begin(context)
	return {"event": event, "context": context, "log": log}


func _elites(event: EliteEvent) -> Array[Enemy]:
	var found: Array[Enemy] = []
	for node in event.objectives():
		found.append(node as Enemy)
	return found


func test_spawns_the_elites_through_the_factory_on_the_spawn_ring() -> void:
	var rig := _rig()
	var context: EventContext = rig["context"]
	var elites := _elites(rig["event"])
	assert_eq(elites.size(), 3)
	assert_eq(context.factory.alive_count(), 3, "the factory made them, so kills and drops work")
	for elite in elites:
		assert_eq(elite.def.id, &"elite_orc")
		assert_almost_eq(elite.global_position.distance_to(BUILDING_AT), 300.0, 0.5)
	assert_eq(rig["log"]["progress"], ["Elites 0/3"])


func test_killing_every_elite_succeeds() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	var elites := _elites(event)
	ActorFixture.kill(self, elites[0].get_node("AbilitySystemComponent"))
	ActorFixture.kill(self, elites[1].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["progress"] as Array).back(), "Elites 2/3")
	assert_eq(event.objectives().size(), 1)
	assert_true(event.is_running())
	ActorFixture.kill(self, elites[2].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1)
	assert_true((rig["log"]["failed"] as Array).is_empty())


func test_time_up_fails_and_the_survivors_stay() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	var elites := _elites(event)
	ActorFixture.kill(self, elites[0].get_node("AbilitySystemComponent"))
	event.step(25.5)
	assert_eq((rig["log"]["failed"] as Array).size(), 1)
	await next_frame()
	for i in range(1, 3):
		assert_true(is_instance_valid(elites[i]) and not elites[i].is_queued_for_deletion(), "elite %d keeps fighting" % i)
		assert_true(elites[i].is_in_group(Groups.ENEMIES))
	ActorFixture.kill(self, elites[1].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["progress"] as Array).back(), "Elites 1/3", "kills after the end no longer count")
	assert_true((rig["log"]["succeeded"] as Array).is_empty())


func test_abort_leaves_the_elites_in_the_fight() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	event.abort()
	assert_true((rig["log"]["failed"] as Array).is_empty())
	assert_eq((rig["context"] as EventContext).factory.alive_count(), 3)
	assert_true(event.objectives().is_empty())


func test_validate_reports_a_missing_or_invalid_elite_and_a_bad_count() -> void:
	var event := ELITE_EVENT.instantiate() as EliteEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.count = 0
	event.elite_enemy = null
	assert_eq(event.validate().size(), 2)
	event.free()
```

In `tests/data/test_game_data.gd` add `"res://data/enemies/elite_orc.tres",` to `DATA_FILES` (after `orc_swarmer.tres`), add `"res://data/enemies/elite_orc.tres"` to the path list in `test_enemy_defs_use_the_generic_enemy_scene`, and append:
```gdscript
func test_elite_orc_matches_the_spec() -> void:
	var elite := load("res://data/enemies/elite_orc.tres") as EnemyDef
	assert_no_errors(elite.validate(), "elite_orc")
	assert_eq(elite.id, &"elite_orc")
	assert_almost_eq(elite.stats.max_health, 300.0)
	assert_almost_eq(elite.stats.move_speed, 70.0)
	assert_almost_eq(elite.visual_scale, 1.8)
	assert_eq(elite.target_policy, EnemyDef.TargetPolicy.PLAYER)
	assert_almost_eq(elite.contact_damage, 15.0)
	assert_true(elite.tint.r > 0.9 and elite.tint.g < 0.5 and elite.tint.b < 0.5, "red tint")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/events/test_elite_event TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `EliteEvent` not found.

- [ ] **Step 3: Implement**

`src/events/elite_event.gd`:
```gdscript
class_name EliteEvent
extends StageEvent
## "Elite hunt": `count` elite enemies arrive on the spawn ring around the building through the EnemyFactory,
## so kills, Event.Kill and drops work as for any enemy. All dead in time -> success; time up -> failure,
## and the survivors stay in the fight (also on abort: once spawned they are ordinary enemies).

@export var elite_enemy: EnemyDef
@export var count: int = 0
## Keeps the spawn points off the arena walls.
@export var edge_margin: float = 0.0

var _alive: Array[Enemy] = []
var _killed: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if elite_enemy == null:
		errors.append("EliteEvent: elite_enemy is null")
	else:
		errors.append_array(elite_enemy.validate())
	if count < 1:
		errors.append("EliteEvent: count must be >= 1")
	if edge_margin < 0.0:
		errors.append("EliteEvent: edge_margin must be >= 0")
	return errors


func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for elite in _alive:
		if is_instance_valid(elite):
			found.append(elite)
	return found


func _on_begin() -> void:
	var center := context.building.global_position if is_instance_valid(context.building) else Vector2.ZERO
	var base_angle := context.rng.randf() * TAU
	for i in count:
		var angle := base_angle + TAU * float(i) / float(count)
		var point := SpawnGeometry.ring_point(center, context.spawn_radius, angle)
		var elite := context.factory.spawn(elite_enemy, SpawnGeometry.clamp_to_rect(point, context.bounds, edge_margin))
		elite.died.connect(_on_elite_died)
		_alive.append(elite)
	_report()


func _on_elite_died(elite: Enemy) -> void:
	if not is_running():
		return
	_alive.erase(elite)
	_killed += 1
	_report()
	if _killed >= count:
		_succeed()


func _report() -> void:
	report_progress("Elites %d/%d" % [_killed, count])


## The survivors are not removed: they keep fighting as ordinary enemies.
func _cleanup() -> void:
	for elite in _alive:
		if is_instance_valid(elite) and elite.died.is_connected(_on_elite_died):
			elite.died.disconnect(_on_elite_died)
	_alive.clear()
```

`src/events/elite_event.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/events/elite_event.gd" id="1_event"]
[ext_resource type="Resource" path="res://data/enemies/elite_orc.tres" id="2_elite"]

[node name="EliteEvent" type="Node2D"]
script = ExtResource("1_event")
elite_enemy = ExtResource("2_elite")
count = 3
edge_margin = 24.0
```

`data/enemies/elite_orc.tres`:
```
[gd_resource type="Resource" script_class="EnemyDef" format=3]

[ext_resource type="Script" uid="uid://7lu42npm7k84" path="res://src/defs/enemy_def.gd" id="1_def"]
[ext_resource type="Script" uid="uid://bpy31rqnwoa0w" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="PackedScene" path="res://src/actors/enemy/enemy.tscn" id="3_scene"]
[ext_resource type="Resource" uid="uid://y6rgouyb0s4i" path="res://data/enemies/orc_visual.tres" id="4_visual"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="5_damage"]
[ext_resource type="Resource" uid="uid://cuofrb5rwqfuf" path="res://data/drops/orc_drops.tres" id="6_drops"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
max_health = 300.0
move_speed = 70.0

[resource]
script = ExtResource("1_def")
id = &"elite_orc"
scene = ExtResource("3_scene")
visual = ExtResource("4_visual")
visual_scale = 1.8
tint = Color(1, 0.3, 0.3, 1)
stats = SubResource("Resource_stats")
contact_effect = ExtResource("5_damage")
contact_damage = 15.0
contact_interval = 0.9
target_policy = 1
body_radius = 18.0
drops = ExtResource("6_drops")
```

- [ ] **Step 4: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/events/test_elite_event ...` and `FILTER=tests/data ...` (with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: 5 PASS and every data test PASS (one new). Likely snag: the ring distance is off when a spawn point is clamped — the rig's 2400x1600 arena keeps a 300 px ring around `(50, 40)` well inside, so any clamp means `spawn_radius` was not taken from the context.

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/events data/enemies tests/events tests/data
git commit -q -m "feat(events): add the elite hunt event and the elite orc

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Boss actor, BossPattern base, AbilityContext.summon, State.Casting

**Files:**
- Create: `src/actors/boss/boss.gd`, `src/actors/boss/boss.tscn`, `src/boss/patterns/boss_pattern.gd`
- Create: `tests/support/probe_pattern.gd`, `tests/support/probe_pattern.tscn`
- Modify: `src/components/ability_context.gd` (+ `summon`), `tools/sync_tags.gd` (+ `State.Casting`), `tests/support/actor_fixture.gd` (add `boss`)
- Test: `tests/actors/test_boss.gd` (new), `tests/boss/test_boss_pattern.gd` (new)

**Interfaces:**
- Consumes: `BossDef`/`BossPatternDef` and `DefFactory.boss(scene, pattern_scenes)` (Task 4), `ActorAbility.bind_context/context`, GAS `GameplayAbility` (`try_activate`, `is_active`, `ability_ended(was_cancelled)`, `abort_ability`, `end_ability`, `activation_owned_tags`, `owner_asc`; `_ready` self-grants under an ASC parent), `AbilitySystemComponent.try_activate_abilities_by_tag(tag) -> bool`, `has_tag`, `cleanup`, the enemy-style components (`Health`, `Hurtbox`, `Hitbox`, `EffectApplier`, `ContactDamage`, `Mover`, `ActorVisual`), `ActorFixture.player/kill`.
- Produces:
  - `AbilityContext.summon: Callable` — `func(def: EnemyDef, position: Vector2) -> void` injected by the Stage for the boss; empty for the player (constructor unchanged).
  - `Boss` (CharacterBody2D, `src/actors/boss/boss.tscn`): `signal died(boss: Boss)`; `def: BossDef`; `ability_context: AbilityContext`; public `asc`, `health`; `setup(def: BossDef, target: Node2D, targets: TargetProvider, projectile_layer: Node, summon: Callable = Callable())` (after entering the tree; builds its own `AbilityContext`, grants one `ActorAbility` per `def.patterns` entry, joins `Groups.ENEMIES`); `patterns() -> Array[ActorAbility]` (def order); `pattern_weights() -> Array[float]`; `is_casting() -> bool` (`asc.has_tag(GameplayTags.State_Casting)`). Chases `target` unless casting. Death: leaves the group, stops every child's physics (mover, contact damage, the brain), aborts its patterns, `asc.cleanup.call_deferred()`, emits `died`, frees itself after 1 s.
  - `BossPattern` (`@abstract`, `src/boss/patterns/boss_pattern.gd`, extends `ActorAbility`): one activation = one attack. `signal completed`; `step(delta)` (physics-driven; tests call it after `set_physics_process(false)`); `is_running() -> bool`. Hooks: `_begin() -> bool` (false = cannot run now, nothing happens), `_advance(delta)`, `_abort()`; finisher `_complete()`. Aborting the ability (boss death, `asc.cleanup`) calls `_abort()` and releases the activation.
  - `ProbePattern` (test double, `tests/support/probe_pattern.tscn`, tag `Ability.Probe.Pattern`, owns `State.Casting`): `hold` (stays active until `release()`), `refuse` (`_begin` returns false), counters `starts`, `aborts`, `stepped`.
  - `ActorFixture.boss(test, player: Player, def: BossDef, position: Vector2, summon: Callable = Callable()) -> Boss` — targets and projectile layer come from `player.ability_context`.
  - `GameplayTags.State_Casting`.

- [ ] **Step 1: Register the tag**

In `tools/sync_tags.gd` add `"State.Casting",` to `TAGS` (keep the list sorted: after `"Event.Kill",`, before `"State.Invulnerable",`). Run `"$GODOT_BIN" --headless --path . -s res://tools/sync_tags.gd`, check that `godot_gas/gameplay_tags.gd` now has `const State_Casting: StringName = &"State.Casting"`, then godot-ai `filesystem_manage` `{"op": "scan"}`.

- [ ] **Step 2: Add the test double and fixture helper**

`tests/support/probe_pattern.gd`:
```gdscript
class_name ProbePattern
extends BossPattern
## Test double: counts starts, completes at once unless `hold` (then `release()` ends it), can refuse to
## start, and records aborts and stepped time.

@export var hold: bool = false
@export var refuse: bool = false

var starts: int = 0
var aborts: int = 0
var stepped: float = 0.0


func release() -> void:
	_complete()


func _begin() -> bool:
	if refuse:
		return false
	starts += 1
	if not hold:
		_complete()
	return true


func _advance(delta: float) -> void:
	stepped += delta


func _abort() -> void:
	aborts += 1
```

`tests/support/probe_pattern.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://tests/support/probe_pattern.gd" id="1_probe"]

[node name="ProbePattern" type="Node"]
script = ExtResource("1_probe")
ability_name = "Probe Pattern"
ability_tag = &"Ability.Probe.Pattern"
activation_owned_tags = Array[StringName]([&"State.Casting"])
```

Append to `tests/support/actor_fixture.gd`:
```gdscript
## A set-up Boss from `def` (its scene must be boss.tscn) at `position`, chasing `player`, sharing the
## player's TargetProvider and projectile layer. No brain is attached: tests activate patterns by tag.
static func boss(test: TestCase, player: Player, def: BossDef, position: Vector2, summon: Callable = Callable()) -> Boss:
	var result := test.adopt(def.scene.instantiate()) as Boss
	result.global_position = position
	result.setup(def, player, player.ability_context.targets, player.ability_context.projectile_layer, summon)
	return result
```

- [ ] **Step 3: Write the failing tests**

`tests/boss/test_boss_pattern.gd`:
```gdscript
extends TestCase

const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")


func _pattern(hold: bool) -> Dictionary:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	var asc := ActorFixture.asc_of(actor)
	var pattern := PROBE_PATTERN.instantiate() as ProbePattern
	pattern.hold = hold
	pattern.bind_context(AbilityContext.new(actor, null, null, null, null))
	asc.add_child(pattern)
	pattern.set_physics_process(false)
	var ended: Array = []
	pattern.ability_ended.connect(func(was_cancelled: bool) -> void: ended.append(was_cancelled))
	return {"pattern": pattern, "asc": asc, "ended": ended}


func test_an_instant_pattern_runs_once_and_ends() -> void:
	var rig := _pattern(false)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	assert_true(asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"))
	assert_eq(pattern.starts, 1)
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [false])


func test_a_held_pattern_casts_until_it_completes() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	assert_true(pattern.is_active)
	assert_true(pattern.is_running())
	assert_true(asc.has_tag(GameplayTags.State_Casting), "owned tag while running")
	pattern.step(0.25)
	pattern.step(0.5)
	assert_almost_eq(pattern.stepped, 0.75)
	pattern.release()
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [false])
	pattern.step(1.0)
	assert_almost_eq(pattern.stepped, 0.75, 0.001, "a finished pattern does not advance")


func test_abort_calls_the_abort_hook_and_strips_the_casting_tag() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	pattern.abort_ability()
	assert_eq(pattern.aborts, 1)
	assert_false(pattern.is_running())
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [true])
	pattern.release()
	assert_eq(rig["ended"], [true], "releasing after the abort changes nothing")
	assert_true(asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"), "can run again")
	assert_eq(pattern.starts, 2)


func test_a_refusing_pattern_never_casts() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	pattern.refuse = true
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	assert_eq(pattern.starts, 0)
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [true], "ends as cancelled")
```

`tests/actors/test_boss.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")


func _def() -> BossDef:
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def := DefFactory.boss(BOSS_SCENE, patterns)
	def.stats.move_speed = 120.0
	return def


func _rig(summon: Callable = Callable()) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(300, 60)
	var boss := ActorFixture.boss(self, player, _def(), Vector2(-200, 60), summon)
	var pattern := boss.patterns()[0] as ProbePattern
	pattern.set_physics_process(false)
	return {"player": player, "boss": boss, "pattern": pattern}


func test_setup_applies_stats_joins_the_enemy_group_and_grants_the_patterns() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var pattern: ProbePattern = rig["pattern"]
	assert_almost_eq(boss.health.current(), 500.0)
	assert_true(boss.is_in_group(Groups.ENEMIES))
	assert_eq(boss.patterns().size(), 1)
	assert_eq(boss.pattern_weights().size(), 1)
	assert_almost_eq(boss.pattern_weights()[0], 1.0)
	assert_true(pattern.owner_asc == boss.asc, "granted to the boss's ASC")
	assert_true(pattern.context == boss.ability_context)
	assert_true(pattern.context.actor == boss)
	assert_true(pattern.context.targets == player.ability_context.targets)
	assert_null(pattern.context.wallet, "the boss has no wallet")
	assert_false(pattern.context.summon.is_valid(), "no summon injected")


func test_the_injected_summon_reaches_the_patterns() -> void:
	var calls: Array = []
	var summon := func(def: EnemyDef, position: Vector2) -> void: calls.append([def, position])
	var rig := _rig(summon)
	var pattern: ProbePattern = rig["pattern"]
	pattern.context.summon.call(null, Vector2(7, 8))
	assert_eq(calls, [[null, Vector2(7, 8)]])


func test_chases_its_target_and_stands_still_while_casting() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	await wait_seconds(0.3)
	assert_true(boss.global_position.x > -190.0, "moved toward the player on +x")
	boss.asc.add_tag(GameplayTags.State_Casting)
	await next_physics_frame()
	var held := boss.global_position
	await wait_seconds(0.3)
	assert_almost_eq(boss.global_position.distance_to(held), 0.0, 0.5, "no chasing while casting")
	assert_true(boss.is_casting())


func test_death_aborts_patterns_leaves_the_group_and_frees_itself() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var pattern: ProbePattern = rig["pattern"]
	pattern.hold = true
	assert_true(boss.asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"))
	assert_true(boss.is_casting())
	var deaths: Array = []
	boss.died.connect(func(found: Boss) -> void: deaths.append(found))
	ActorFixture.kill(self, boss.asc)
	assert_eq(deaths, [boss])
	assert_eq(pattern.aborts, 1)
	assert_false(pattern.is_active)
	assert_false(boss.is_casting(), "State.Casting stripped by the abort")
	assert_false(boss.is_in_group(Groups.ENEMIES))
	assert_false(pattern.is_physics_processing())
	ActorFixture.kill(self, boss.asc)
	assert_eq(deaths.size(), 1, "dies once")
	await wait_seconds(1.3)
	assert_false(is_instance_valid(boss))
```

- [ ] **Step 4: Run to verify failure**

Run: `FILTER=tests/boss/test_boss_pattern TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `BossPattern`, `ProbePattern`, `Boss` not found.

- [ ] **Step 5: Implement**

`src/components/ability_context.gd` — add after the `wallet` member:
```gdscript
## Boss summons: `func(def: EnemyDef, position: Vector2) -> void`, injected by the Stage (it spawns through
## the EnemyFactory so summoned enemies count as kills and drop loot). Empty for the player.
var summon: Callable = Callable()
```

`src/boss/patterns/boss_pattern.gd`:
```gdscript
@abstract
class_name BossPattern
extends ActorAbility
## Base of a boss attack. One activation runs one attack: `_begin()` starts it (false = cannot run now,
## nothing happens), `step(delta)` advances it through `_advance()` (physics in play, by hand in tests),
## `_complete()` ends it. Aborting the ability (boss death, ASC cleanup) calls `_abort()` so telegraphs and
## strikes are removed. Scenes set `activation_owned_tags = [State.Casting]`, which stops the boss chasing.

signal completed

var _running: bool = false


func step(delta: float) -> void:
	if _running:
		_advance(delta)


func is_running() -> bool:
	return _running


func _activate_ability() -> bool:
	if context == null:
		return false
	_running = true
	if not _begin():
		_running = false
		return false
	if _running:
		ability_ended.connect(_on_ability_ended, CONNECT_ONE_SHOT)
		await completed
	return true


## Starts the attack; return false when it cannot run now (no target, no summon callable).
func _begin() -> bool:
	return true


func _advance(_delta: float) -> void:
	pass


## Removes whatever the attack placed (telegraphs, strikes) when it is interrupted.
func _abort() -> void:
	pass


func _complete() -> void:
	if not _running:
		return
	_running = false
	completed.emit()


func _on_ability_ended(_was_cancelled: bool) -> void:
	if not _running:
		return  # normal completion: try_activate ended the ability after `completed`
	_running = false
	_abort()
	completed.emit()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/actors/boss/boss.gd`:
```gdscript
class_name Boss
extends CharacterBody2D
## The end-of-stage boss, configured by a BossDef: chases its target, damages allies it touches and owns
## its pattern abilities (granted from the def). It stands still while it owns State.Casting. When a
## pattern runs is decided by a BossBrain the Stage attaches. Not an Enemy: no kill count, no drops.

signal died(boss: Boss)

const FREE_DELAY_AFTER_DEATH: float = 1.0
const HURTBOX_PADDING: float = 2.0
const CONTACT_PADDING: float = 4.0

var def: BossDef
var ability_context: AbilityContext
var _target: Node2D
var _patterns: Array[ActorAbility] = []
var _weights: Array[float] = []

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _contact_hitbox: Hitbox = $ContactHitbox
@onready var _applier: EffectApplier = $EffectApplier
@onready var _contact: ContactDamage = $ContactDamage
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _body_shape: CollisionShape2D = $CollisionShape2D
@onready var _hurt_shape: CollisionShape2D = $Hurtbox/CollisionShape2D
@onready var _contact_shape: CollisionShape2D = $ContactHitbox/CollisionShape2D


## Call after the boss has entered the tree. `summon` is `func(def: EnemyDef, position: Vector2) -> void`.
func setup(p_def: BossDef, target: Node2D, targets: TargetProvider, projectile_layer: Node, summon: Callable = Callable()) -> void:
	def = p_def
	_target = target
	asc.initialize_attribute_overrides(def.stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	_applier.bind(asc)
	_mover.bind(self, asc)
	_contact.bind(_contact_hitbox, _applier, def.contact_effect, def.contact_damage, def.contact_interval)
	_visual.apply(def.visual, def.visual_scale, def.tint)
	(_body_shape.shape as CircleShape2D).radius = def.body_radius
	(_hurt_shape.shape as CircleShape2D).radius = def.body_radius + HURTBOX_PADDING
	(_contact_shape.shape as CircleShape2D).radius = def.body_radius + CONTACT_PADDING
	ability_context = AbilityContext.new(self, _mover, targets, projectile_layer, null)
	ability_context.summon = summon
	for pattern_def in def.patterns:
		if pattern_def == null or pattern_def.ability_scene == null:
			continue
		var ability := pattern_def.ability_scene.instantiate() as ActorAbility
		if ability == null:
			push_error("Boss '%s': a pattern scene root is not an ActorAbility" % def.id)
			continue
		ability.bind_context(ability_context)
		asc.add_child(ability)  # GameplayAbility._ready grants it to the ASC
		_patterns.append(ability)
		_weights.append(pattern_def.weight)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	add_to_group(Groups.ENEMIES)


func patterns() -> Array[ActorAbility]:
	return _patterns.duplicate()


func pattern_weights() -> Array[float]:
	return _weights.duplicate()


func is_casting() -> bool:
	return asc.has_tag(GameplayTags.State_Casting)


func _physics_process(_delta: float) -> void:
	if is_casting() or not is_instance_valid(_target):
		_mover.set_direction(Vector2.ZERO)
		_visual.set_moving(_mover.is_dashing())
		return
	var to_target := _target.global_position - global_position
	_mover.set_direction(to_target.normalized())
	_visual.face(to_target.x)
	_visual.set_moving(true)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	remove_from_group(Groups.ENEMIES)
	set_physics_process(false)
	for child in get_children():
		child.set_physics_process(false)  # mover, contact damage, the attached brain
	for ability in _patterns:
		if is_instance_valid(ability):
			ability.abort_ability()
			ability.set_physics_process(false)
	_hurtbox.set_deferred("monitorable", false)
	_contact_hitbox.set_deferred("monitoring", false)
	_body_shape.set_deferred("disabled", true)
	asc.cleanup.call_deferred()
	_visual.play_once(AnimNames.DEATH)
	died.emit(self)
	get_tree().create_timer(FREE_DELAY_AFTER_DEATH).timeout.connect(queue_free)
```

`src/actors/boss/boss.tscn`:
```
[gd_scene load_steps=13 format=3]

[ext_resource type="Script" path="res://src/actors/boss/boss.gd" id="1_boss"]
[ext_resource type="PackedScene" path="res://src/gas/actor_asc.tscn" id="2_asc"]
[ext_resource type="Script" path="res://src/components/health.gd" id="3_health"]
[ext_resource type="Script" path="res://src/components/hurtbox.gd" id="4_hurtbox"]
[ext_resource type="Script" path="res://src/components/hitbox.gd" id="5_hitbox"]
[ext_resource type="Script" path="res://src/components/effect_applier.gd" id="6_applier"]
[ext_resource type="Script" path="res://src/components/contact_damage.gd" id="7_contact"]
[ext_resource type="Script" path="res://src/components/mover.gd" id="8_mover"]
[ext_resource type="Script" path="res://src/components/actor_visual.gd" id="9_visual"]

[sub_resource type="CircleShape2D" id="CircleShape2D_body"]
resource_local_to_scene = true
radius = 30.0

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
resource_local_to_scene = true
radius = 32.0

[sub_resource type="CircleShape2D" id="CircleShape2D_contact"]
resource_local_to_scene = true
radius = 34.0

[node name="Boss" type="CharacterBody2D"]
collision_layer = 4
collision_mask = 1
motion_mode = 1
script = ExtResource("1_boss")

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_body")

[node name="AbilitySystemComponent" parent="." instance=ExtResource("2_asc")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 16
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
shape = SubResource("CircleShape2D_hurt")

[node name="ContactHitbox" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 8
monitorable = false
script = ExtResource("5_hitbox")
once_per_target = false

[node name="CollisionShape2D" type="CollisionShape2D" parent="ContactHitbox"]
shape = SubResource("CircleShape2D_contact")

[node name="EffectApplier" type="Node" parent="."]
script = ExtResource("6_applier")

[node name="ContactDamage" type="Node" parent="."]
script = ExtResource("7_contact")

[node name="Mover" type="Node" parent="."]
script = ExtResource("8_mover")

[node name="Visual" type="AnimatedSprite2D" parent="."]
script = ExtResource("9_visual")
```

- [ ] **Step 6: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/boss/test_boss_pattern ...` and `FILTER=tests/actors/test_boss ...` (with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: 4 + 4 PASS. Likely snags:
- The activation hangs (test timeout): `_complete()` was called inside `_begin()` but `_activate_ability` still awaited `completed` — the `if _running:` guard before the `await` is what prevents that.
- `GAS: Ability ... was forcefully aborted.` lines in the output come from the plugin's `abort_ability` and are expected.

- [ ] **Step 7: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/actors/boss src/boss src/components tools godot_gas tests/actors tests/boss tests/support
git commit -q -m "feat(boss): add the Boss actor, the BossPattern ability base and State.Casting

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: BossBrain

**Files:**
- Create: `src/boss/boss_brain.gd`
- Test: `tests/boss/test_boss_brain.gd` (new)

**Interfaces:**
- Consumes: `Boss.asc/patterns()/pattern_weights()/def` (Task 10), `BossDef.Order`, `BossDef.pattern_gap` (Task 4), `ActorAbility.ability_tag/is_active/ability_ended`, `AbilitySystemComponent.try_activate_abilities_by_tag(tag) -> bool`, `RunRng.weighted_index(weights: Array[float]) -> int`, `ProbePattern` (Task 10), `BareGameplayAbility`, `DefFactory.boss/packed`, `ActorFixture.player/boss`.
- Produces: `BossBrain` (Node, `src/boss/boss_brain.gd`):
  - `signal pattern_started(ability: ActorAbility)`;
  - `bind(asc: AbilitySystemComponent, patterns: Array[ActorAbility], weights: Array[float], order: BossDef.Order, pattern_gap: float, rng: RunRng)` — the first pattern starts after one `pattern_gap`;
  - `static attach_to(boss: Boss, rng: RunRng) -> BossBrain` (adds a child named `Brain` under the boss and binds it to the boss's patterns, weights, order and gap);
  - `step(delta)` (physics-driven; tests call it after `set_physics_process(false)`), `stop()`, `casting() -> ActorAbility`;
  - `static validate_def(def: BossDef) -> PackedStringArray` — every pattern scene root must be an `ActorAbility` with a non-default `ability_tag` unique within the boss (the brain activates by tag). `null` → no errors.
  - Rules: SEQUENCE = list order, wrapping; RANDOM = weighted, never the same pattern twice in a row when there are alternatives; after a pattern ends (completed, refused or aborted) the next gap starts.

- [ ] **Step 1: Write the failing tests**

`tests/boss/test_boss_brain.gd`:
```gdscript
extends TestCase

const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")


## `count` probe patterns (tags Ability.Probe.P0..), instantly completing unless `hold`, on a bare actor;
## the brain uses gap 2 s and RunRng(9). `started` records the index of every pattern the brain starts.
func _rig(count: int, order: BossDef.Order, weights: Array = [], hold: bool = false) -> Dictionary:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	var asc := ActorFixture.asc_of(actor)
	var context := AbilityContext.new(actor, null, null, null, null)
	var patterns: Array[ActorAbility] = []
	var typed_weights: Array[float] = []
	for i in count:
		var pattern := PROBE_PATTERN.instantiate() as ProbePattern
		pattern.ability_tag = StringName("Ability.Probe.P%d" % i)
		pattern.hold = hold
		pattern.bind_context(context)
		asc.add_child(pattern)
		pattern.set_physics_process(false)
		patterns.append(pattern)
		typed_weights.append(float(weights[i]) if i < weights.size() else 1.0)
	var brain := adopt(BossBrain.new()) as BossBrain
	brain.set_physics_process(false)
	var started: Array = []
	brain.pattern_started.connect(func(ability: ActorAbility) -> void: started.append(patterns.find(ability)))
	brain.bind(asc, patterns, typed_weights, order, 2.0, RunRng.new(9))
	return {"brain": brain, "patterns": patterns, "started": started, "asc": asc}


func test_waits_one_gap_before_the_first_pattern() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	brain.step(1.9)
	assert_true((rig["started"] as Array).is_empty())
	brain.step(0.2)
	assert_eq(rig["started"], [0])
	assert_eq((rig["patterns"][0] as ProbePattern).starts, 1, "activated through the ASC")


func test_sequence_order_wraps_around() -> void:
	var rig := _rig(3, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	for i in 5:
		brain.step(2.0)
	assert_eq(rig["started"], [0, 1, 2, 0, 1])


func test_waits_for_the_running_pattern_before_the_next_gap() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE, [], true)
	var brain: BossBrain = rig["brain"]
	var asc: AbilitySystemComponent = rig["asc"]
	var first: ProbePattern = rig["patterns"][0]
	brain.step(2.0)
	assert_true(asc.has_tag(GameplayTags.State_Casting))
	assert_true(brain.casting() == first)
	brain.step(10.0)
	assert_eq(rig["started"], [0], "nothing new while a pattern runs")
	first.release()
	assert_null(brain.casting())
	brain.step(1.9)
	assert_eq(rig["started"], [0], "the gap starts when the pattern ends")
	brain.step(0.2)
	assert_eq(rig["started"], [0, 1])


func test_random_order_never_repeats_and_follows_the_weights() -> void:
	var rig := _rig(3, BossDef.Order.RANDOM, [6.0, 1.0, 1.0])
	var brain: BossBrain = rig["brain"]
	for i in 60:
		brain.step(2.0)
	var started: Array = rig["started"]
	assert_eq(started.size(), 60)
	var counts := [0, 0, 0]
	for i in started.size():
		counts[started[i]] += 1
		if i > 0:
			assert_false(started[i] == started[i - 1], "pick %d repeats pattern %d" % [i, started[i]])
	assert_true(counts[0] > counts[1] and counts[0] > counts[2], "weight 6 beats weight 1: %s" % str(counts))
	assert_true(counts[1] > 0 and counts[2] > 0, "every pattern gets a turn: %s" % str(counts))


func test_random_with_a_single_pattern_repeats_it() -> void:
	var rig := _rig(1, BossDef.Order.RANDOM)
	var brain: BossBrain = rig["brain"]
	for i in 3:
		brain.step(2.0)
	assert_eq(rig["started"], [0, 0, 0])


func test_a_pattern_that_cannot_start_does_not_stall_the_brain() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	(rig["patterns"][0] as ProbePattern).refuse = true
	brain.step(2.0)
	assert_null(brain.casting(), "the refusal ended the attempt")
	brain.step(1.0)
	assert_eq(rig["started"], [0], "a full gap follows a refusal")
	brain.step(1.0)
	assert_eq(rig["started"], [0, 1])
	assert_eq((rig["patterns"][1] as ProbePattern).starts, 1)


func test_stop_halts_the_brain() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	brain.step(2.0)
	brain.stop()
	for i in 5:
		brain.step(2.0)
	assert_eq(rig["started"], [0])


func test_attach_to_binds_a_boss_and_runs_its_patterns() -> void:
	var player := ActorFixture.player(self)
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def := DefFactory.boss(BOSS_SCENE, patterns)
	def.pattern_gap = 1.5
	var boss := ActorFixture.boss(self, player, def, Vector2(-250, 120))
	var pattern := boss.patterns()[0] as ProbePattern
	pattern.set_physics_process(false)
	var brain := BossBrain.attach_to(boss, RunRng.new(1))
	brain.set_physics_process(false)
	assert_eq(brain.get_parent(), boss)
	assert_eq(brain.name, "Brain")
	brain.step(1.4)
	assert_eq(pattern.starts, 0, "the def's gap, not a default")
	brain.step(0.2)
	assert_eq(pattern.starts, 1)


func test_validate_def_requires_actor_ability_roots_with_distinct_tags() -> void:
	var one: Array[PackedScene] = [PROBE_PATTERN]
	assert_no_errors(BossBrain.validate_def(DefFactory.boss(null, one)), "one probe")
	var twice: Array[PackedScene] = [PROBE_PATTERN, PROBE_PATTERN]
	var errors := BossBrain.validate_def(DefFactory.boss(null, twice))
	assert_eq(errors.size(), 1, "two patterns share Ability.Probe.Pattern")
	assert_true(errors[0].contains("Ability.Probe.Pattern"))
	var bare: Array[PackedScene] = [DefFactory.packed(BareGameplayAbility.new())]
	assert_eq(BossBrain.validate_def(DefFactory.boss(null, bare)).size(), 1, "not an ActorAbility")
	var untagged := PROBE_PATTERN.instantiate() as ProbePattern
	untagged.ability_tag = &"Ability.None"
	var no_tag: Array[PackedScene] = [DefFactory.packed(untagged)]
	assert_eq(BossBrain.validate_def(DefFactory.boss(null, no_tag)).size(), 1, "the default tag cannot be activated")
	assert_no_errors(BossBrain.validate_def(null), "no boss")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/boss/test_boss_brain TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `BossBrain` not found.

- [ ] **Step 3: Implement**

`src/boss/boss_brain.gd`:
```gdscript
class_name BossBrain
extends Node
## Decides when the boss attacks: after `pattern_gap` seconds of chasing it picks the next pattern
## (SEQUENCE = list order, wrapping; RANDOM = weighted, never the same twice in a row when there are
## alternatives), activates it through the ASC by its ability tag, waits for that ability to end, then
## starts the next gap. Plain code, not GAS: the patterns are the abilities.

signal pattern_started(ability: ActorAbility)

var _asc: AbilitySystemComponent
var _patterns: Array[ActorAbility] = []
var _weights: Array[float] = []
var _order: BossDef.Order = BossDef.Order.SEQUENCE
var _gap: float = 0.0
var _rng: RunRng
var _gap_left: float = 0.0
var _next_index: int = 0
var _last_index: int = -1
var _casting: ActorAbility
var _stopped: bool = false


## Adds a brain named "Brain" under a set-up boss and binds it to the boss's patterns and def.
static func attach_to(boss: Boss, rng: RunRng) -> BossBrain:
	var brain := BossBrain.new()
	brain.name = "Brain"
	boss.add_child(brain)
	brain.bind(boss.asc, boss.patterns(), boss.pattern_weights(), boss.def.order, boss.def.pattern_gap, rng)
	return brain


## Boss-layer check run by Stage.start (defs/ cannot name ActorAbility): every pattern root is an
## ActorAbility whose ability_tag is set and unique within the boss.
static func validate_def(def: BossDef) -> PackedStringArray:
	var errors := PackedStringArray()
	if def == null:
		return errors
	var seen: Dictionary[StringName, bool] = {}
	for pattern in def.patterns:
		if pattern == null or pattern.ability_scene == null:
			continue  # BossDef.validate reports these
		var root := pattern.ability_scene.instantiate()
		if root == null:
			continue
		if not (root is ActorAbility):
			errors.append("boss '%s': a pattern scene root is not an ActorAbility" % def.id)
		else:
			var tag := (root as ActorAbility).ability_tag
			if tag == &"" or tag == &"Ability.None":
				errors.append("boss '%s': pattern '%s' has no ability_tag" % [def.id, (root as ActorAbility).ability_name])
			elif seen.has(tag):
				errors.append("boss '%s': two patterns share the ability_tag '%s' (the brain activates by tag)" % [def.id, tag])
			seen[tag] = true
		root.free()
	return errors


func bind(asc: AbilitySystemComponent, patterns: Array[ActorAbility], weights: Array[float], order: BossDef.Order, pattern_gap: float, rng: RunRng) -> void:
	_asc = asc
	_patterns = patterns.duplicate()
	_weights = weights.duplicate()
	_order = order
	_gap = pattern_gap
	_rng = rng
	_gap_left = pattern_gap
	_next_index = 0
	_last_index = -1
	_casting = null
	_stopped = false
	for ability in _patterns:
		if not ability.ability_ended.is_connected(_on_pattern_ended):
			ability.ability_ended.connect(_on_pattern_ended)


func stop() -> void:
	_stopped = true


## The pattern the brain is waiting on, or null while it counts down a gap.
func casting() -> ActorAbility:
	return _casting


func step(delta: float) -> void:
	if _stopped or _casting != null or _patterns.is_empty():
		return
	_gap_left -= delta
	if _gap_left > 0.0:
		return
	_activate(_pick())


func _pick() -> int:
	var index := 0
	if _order == BossDef.Order.SEQUENCE:
		index = _next_index % _patterns.size()
		_next_index = index + 1
	else:
		var weights := _weights.duplicate()
		if _patterns.size() > 1 and _last_index >= 0:
			weights[_last_index] = 0.0
		index = maxi(_rng.weighted_index(weights), 0)
	_last_index = index
	return index


func _activate(index: int) -> void:
	var ability := _patterns[index]
	_casting = ability
	_gap_left = _gap
	pattern_started.emit(ability)
	# A pattern that finishes (or refuses) synchronously has already cleared `_casting` through ability_ended.
	var activated := _asc.try_activate_abilities_by_tag(ability.ability_tag)
	if not activated and _casting == ability and not ability.is_active:
		_casting = null


func _on_pattern_ended(_was_cancelled: bool) -> void:
	if _casting == null or _casting.is_active:
		return
	_casting = null
	_gap_left = _gap


func _physics_process(delta: float) -> void:
	step(delta)
```

- [ ] **Step 4: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/boss/test_boss_brain TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: 9 PASS. Likely snags:
- `Dictionary[StringName, bool]` with `seen[tag] = true` where `tag` is typed `StringName` — fine; if the inferred `tag` type is Variant, declare `var tag: StringName = ...`.
- In `test_random_order_never_repeats_and_follows_the_weights`, `counts[started[i]] += 1` on an untyped array works; if the seed happens to give a weight-1 pattern more turns than the weight-6 one, the brain ignores weights — check that `_weights` reached `weighted_index`, do not change the seed.

- [ ] **Step 5: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/boss tests/boss
git commit -q -m "feat(boss): add BossBrain (gap, sequence/weighted random without repeats, activation by tag)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Charge and Ground Slam patterns

**Files:**
- Create: `src/boss/patterns/charge_pattern.gd`, `src/boss/patterns/charge_pattern.tscn`, `src/boss/patterns/ground_slam_pattern.gd`, `src/boss/patterns/ground_slam_pattern.tscn`
- Modify: `tools/sync_tags.gd` (+ `Ability.Boss.Charge`, `Ability.Boss.Slam`)
- Test: `tests/boss/test_charge_pattern.gd` (new), `tests/boss/test_ground_slam_pattern.gd` (new)

**Interfaces:**
- Consumes: `BossPattern` hooks (`_begin`, `_advance`, `_complete`, `_abort`, `step`, `context`, `owner_asc`) (Task 10), `Telegraph.show_line/show_circle` and `AreaStrike.strike(..., PhysicsLayers.ALLY_HURT, only_actor)` (Task 5), `Mover.dash(direction, speed, duration)`, `AbilityContext.actor/mover/targets/projectile_layer`, `TargetProvider.player`, `Boss.is_casting()`, `ActorFixture.player/boss/hurt_target`, `DefFactory.boss`.
- Produces:
  - `ChargePattern extends BossPattern` (`charge_pattern.tscn`, tag `Ability.Boss.Charge`, owns `State.Casting`): exports `strike_scene` (`area_strike.tscn`), `hit_effect` (`damage_basic.tres`), `warning` 0.7, `speed` 520, `distance` 360, `damage` 25, `hit_radius` 40, `line_width` 24; `charge_seconds() -> float` (= distance / speed); `validate()`. Warning line from the boss toward the player for `warning`; then `Mover.dash` along it for `charge_seconds()` with an `AreaStrike` riding on the boss that hits the player only, once per charge; completes when the dash time is over. No player → refuses.
  - `GroundSlamPattern extends BossPattern` (`ground_slam_pattern.tscn`, tag `Ability.Boss.Slam`, owns `State.Casting`): exports `strike_scene`, `hit_effect`, `warning` 1.0, `radius` 90, `damage` 30, `strike_seconds` 0.15; `validate()`. Warning circle at the player's position when the slam starts; after `warning` an `AreaStrike` there hitting the player only; completes when it strikes.
  - `GameplayTags.Ability_Boss_Charge`, `GameplayTags.Ability_Boss_Slam`.

- [ ] **Step 1: Register the tags**

Add `"Ability.Boss.Charge",` and `"Ability.Boss.Slam",` to `TAGS` in `tools/sync_tags.gd` (sorted: after `"Ability.Arrow",`). Run `"$GODOT_BIN" --headless --path . -s res://tools/sync_tags.gd`, check `godot_gas/gameplay_tags.gd` lists `Ability_Boss_Charge` and `Ability_Boss_Slam`, then godot-ai `filesystem_manage` `{"op": "scan"}`.

- [ ] **Step 2: Write the failing tests**

`tests/boss/test_charge_pattern.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const CHARGE_SCENE: PackedScene = preload("res://src/boss/patterns/charge_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(-100, 40)
const PLAYER_AT: Vector2 = Vector2(150, 40)


func _rig() -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var patterns: Array[PackedScene] = [CHARGE_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT)
	var charge := boss.patterns()[0] as ChargePattern
	charge.set_physics_process(false)
	return {"player": player, "boss": boss, "charge": charge, "layer": player.ability_context.projectile_layer}


func _telegraphs(layer: Node) -> Array[Telegraph]:
	var found: Array[Telegraph] = []
	for child in layer.get_children():
		if child is Telegraph and not child.is_queued_for_deletion():
			found.append(child)
	return found


func test_warns_along_a_line_then_charges_through_the_player_once() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var charge: ChargePattern = rig["charge"]
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge))
	assert_true(boss.is_casting())
	var warnings := _telegraphs(rig["layer"])
	assert_eq(warnings.size(), 1)
	assert_eq(warnings[0].shape, Telegraph.Shape.LINE)
	assert_eq(warnings[0].global_position, BOSS_AT)
	assert_almost_eq(warnings[0].line_offset.x, 360.0, 0.5, "toward the player, full distance")
	assert_almost_eq(warnings[0].line_offset.y, 0.0, 0.5)
	charge.step(0.5)
	await wait_seconds(0.2)
	assert_almost_eq(boss.global_position.x, BOSS_AT.x, 0.5, "no movement during the warning")
	assert_almost_eq(player.health.current(), 100.0)
	charge.step(0.3)
	assert_true(_telegraphs(rig["layer"]).is_empty(), "the warning is gone once the charge starts")
	await wait_seconds(0.9)
	assert_true(boss.global_position.x > 200.0, "dashed about 360 px (now at %s)" % boss.global_position.x)
	assert_almost_eq(player.health.current(), 75.0, 0.001, "25 damage, exactly once")
	charge.step(charge.charge_seconds())
	assert_false(charge.is_active)
	assert_false(boss.is_casting())


func test_abort_during_the_warning_removes_the_telegraph() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var charge: ChargePattern = rig["charge"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge)
	charge.abort_ability()
	assert_true(_telegraphs(rig["layer"]).is_empty())
	assert_false(boss.is_casting())
	charge.step(1.0)
	await wait_seconds(0.3)
	assert_true(boss.global_position.distance_to(BOSS_AT) < 60.0, "no dash after the abort (only the chase, about 30 px)")


func test_without_a_player_the_charge_does_not_start() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	(rig["player"] as Player).ability_context.targets.player = null
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge)
	assert_false((rig["charge"] as ChargePattern).is_active)
	assert_true(_telegraphs(rig["layer"]).is_empty())


func test_validate_reports_missing_parts_and_bad_numbers() -> void:
	var charge := CHARGE_SCENE.instantiate() as ChargePattern
	assert_no_errors(charge.validate(), "scene as shipped")
	assert_almost_eq(charge.charge_seconds(), 360.0 / 520.0)
	charge.strike_scene = null
	charge.speed = 0.0
	assert_eq(charge.validate().size(), 2)
	charge.free()
```

`tests/boss/test_ground_slam_pattern.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const SLAM_SCENE: PackedScene = preload("res://src/boss/patterns/ground_slam_pattern.tscn")
const PLAYER_AT: Vector2 = Vector2(150, -60)


func _rig() -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var patterns: Array[PackedScene] = [SLAM_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), Vector2(-400, 0))
	var slam := boss.patterns()[0] as GroundSlamPattern
	slam.set_physics_process(false)
	return {"player": player, "boss": boss, "slam": slam, "layer": player.ability_context.projectile_layer}


func _count(layer: Node, script_class: String) -> int:
	var total := 0
	for child in layer.get_children():
		if child.is_queued_for_deletion():
			continue
		if (script_class == "Telegraph" and child is Telegraph) or (script_class == "AreaStrike" and child is AreaStrike):
			total += 1
	return total


func _settle() -> void:
	await next_physics_frame()
	await next_physics_frame()


func test_slam_hits_the_player_only_after_the_warning() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var slam: GroundSlamPattern = rig["slam"]
	var building := ActorFixture.hurt_target(self, PLAYER_AT + Vector2(30, 0), PhysicsLayers.ALLY_HURT)
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Slam))
	assert_eq(_count(rig["layer"], "Telegraph"), 1)
	slam.step(0.6)
	await _settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "still a warning")
	assert_eq(_count(rig["layer"], "AreaStrike"), 0)
	assert_true(boss.is_casting())
	slam.step(0.5)
	assert_eq(_count(rig["layer"], "AreaStrike"), 1)
	assert_eq(_count(rig["layer"], "Telegraph"), 0)
	assert_false(boss.is_casting(), "the slam completes when it strikes")
	await _settle()
	assert_almost_eq(player.health.current(), 70.0)
	assert_almost_eq(ActorFixture.asc_of(building).get_attribute("health").current_value, 100.0, 0.001, "the building is spared")


func test_slam_lands_where_the_player_was() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var slam: GroundSlamPattern = rig["slam"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Slam)
	player.global_position = PLAYER_AT + Vector2(400, 0)
	slam.step(1.1)
	await _settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "stepped out of the circle")


func test_validate_reports_missing_parts_and_bad_numbers() -> void:
	var slam := SLAM_SCENE.instantiate() as GroundSlamPattern
	assert_no_errors(slam.validate(), "scene as shipped")
	slam.hit_effect = null
	slam.radius = 0.0
	assert_eq(slam.validate().size(), 2)
	slam.free()
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/boss/test_charge_pattern TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `ChargePattern` not found.

- [ ] **Step 4: Implement**

`src/boss/patterns/charge_pattern.gd`:
```gdscript
class_name ChargePattern
extends BossPattern
## Charge: a warning line from the boss toward the player for `warning` seconds, then the boss dashes
## `distance` at `speed`. An AreaStrike riding on the boss hits the player (only) once per charge.

enum Phase { WARNING, CHARGING }

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var warning: float = 0.0
@export var speed: float = 0.0
@export var distance: float = 0.0
@export var damage: float = 0.0
## Contact radius of the charging boss.
@export var hit_radius: float = 0.0
@export var line_width: float = 0.0

var _phase: Phase = Phase.WARNING
var _left: float = 0.0
var _direction: Vector2 = Vector2.RIGHT
var _telegraph: Telegraph
var _strike: AreaStrike


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null or hit_effect == null:
		errors.append("Charge: strike_scene and hit_effect are required")
	if warning <= 0.0 or speed <= 0.0 or distance <= 0.0 or hit_radius <= 0.0 or line_width <= 0.0:
		errors.append("Charge: warning, speed, distance, hit_radius and line_width must be > 0")
	if damage < 0.0:
		errors.append("Charge: damage must be >= 0")
	return errors


func charge_seconds() -> float:
	return distance / speed if speed > 0.0 else 0.0


func _begin() -> bool:
	var target: Node2D = context.targets.player if context.targets != null else null
	if not is_instance_valid(target):
		return false
	var origin := context.actor.global_position
	_direction = (target.global_position - origin).normalized()
	if _direction == Vector2.ZERO:
		_direction = Vector2.RIGHT
	_telegraph = Telegraph.new()
	context.projectile_layer.add_child(_telegraph)
	_telegraph.global_position = origin
	_telegraph.show_line(_direction * distance, line_width, warning)
	_phase = Phase.WARNING
	_left = warning
	return true


func _advance(delta: float) -> void:
	_left -= delta
	if _left > 0.0:
		return
	if _phase == Phase.WARNING:
		_start_charge()
	else:
		_complete()


func _start_charge() -> void:
	_free_telegraph()
	_phase = Phase.CHARGING
	_left = charge_seconds()
	context.mover.dash(_direction, speed, _left)
	_strike = strike_scene.instantiate() as AreaStrike
	context.actor.add_child(_strike)  # rides along with the boss
	_strike.strike(owner_asc, hit_effect, damage, hit_radius, _left, PhysicsLayers.ALLY_HURT, context.targets.player)


func _abort() -> void:
	_free_telegraph()
	if is_instance_valid(_strike):
		_strike.queue_free()
	_strike = null


func _free_telegraph() -> void:
	if is_instance_valid(_telegraph):
		_telegraph.queue_free()
	_telegraph = null
```

`src/boss/patterns/charge_pattern.tscn`:
```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://src/boss/patterns/charge_pattern.gd" id="1_charge"]
[ext_resource type="PackedScene" path="res://src/components/area_strike.tscn" id="2_strike"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[node name="ChargePattern" type="Node"]
script = ExtResource("1_charge")
ability_name = "Charge"
ability_tag = &"Ability.Boss.Charge"
activation_owned_tags = Array[StringName]([&"State.Casting"])
strike_scene = ExtResource("2_strike")
hit_effect = ExtResource("3_damage")
warning = 0.7
speed = 520.0
distance = 360.0
damage = 25.0
hit_radius = 40.0
line_width = 24.0
```

`src/boss/patterns/ground_slam_pattern.gd`:
```gdscript
class_name GroundSlamPattern
extends BossPattern
## Ground slam: a warning circle where the player stands when the slam starts; `warning` seconds later
## an AreaStrike there hits the player (only). Stepping out of the circle in time avoids it.

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var warning: float = 0.0
@export var radius: float = 0.0
@export var damage: float = 0.0
## How long the strike stays active; it must outlive a physics step so overlaps are reported.
@export var strike_seconds: float = 0.0

var _left: float = 0.0
var _point: Vector2 = Vector2.ZERO
var _telegraph: Telegraph


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null or hit_effect == null:
		errors.append("Ground slam: strike_scene and hit_effect are required")
	if warning <= 0.0 or radius <= 0.0 or strike_seconds <= 0.0:
		errors.append("Ground slam: warning, radius and strike_seconds must be > 0")
	if damage < 0.0:
		errors.append("Ground slam: damage must be >= 0")
	return errors


func _begin() -> bool:
	var target: Node2D = context.targets.player if context.targets != null else null
	if not is_instance_valid(target):
		return false
	_point = target.global_position
	_telegraph = Telegraph.new()
	context.projectile_layer.add_child(_telegraph)
	_telegraph.global_position = _point
	_telegraph.show_circle(radius, warning)
	_left = warning
	return true


func _advance(delta: float) -> void:
	_left -= delta
	if _left > 0.0:
		return
	_free_telegraph()
	var strike := strike_scene.instantiate() as AreaStrike
	context.projectile_layer.add_child(strike)
	strike.global_position = _point
	strike.strike(owner_asc, hit_effect, damage, radius, strike_seconds, PhysicsLayers.ALLY_HURT, context.targets.player)
	_complete()


func _abort() -> void:
	_free_telegraph()


func _free_telegraph() -> void:
	if is_instance_valid(_telegraph):
		_telegraph.queue_free()
	_telegraph = null
```

`src/boss/patterns/ground_slam_pattern.tscn`:
```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://src/boss/patterns/ground_slam_pattern.gd" id="1_slam"]
[ext_resource type="PackedScene" path="res://src/components/area_strike.tscn" id="2_strike"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[node name="GroundSlamPattern" type="Node"]
script = ExtResource("1_slam")
ability_name = "Ground Slam"
ability_tag = &"Ability.Boss.Slam"
activation_owned_tags = Array[StringName]([&"State.Casting"])
strike_scene = ExtResource("2_strike")
hit_effect = ExtResource("3_damage")
warning = 1.0
radius = 90.0
damage = 30.0
strike_seconds = 0.15
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/boss/test_charge_pattern ...` and `FILTER=tests/boss/test_ground_slam_pattern ...` (with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: 4 + 3 PASS. Likely snags:
- The charge hits twice (player at 50): the boss's contact damage is 0 in `DefFactory.boss`, so a second 25 means the strike was not `once_per_target` — `area_strike.tscn` must leave the Hitbox default (`once_per_target = true`).
- The boss stops short of 200: the dash runs on the boss's `Mover` in real physics time (`360 / 520 ≈ 0.69 s`); the 0.9 s wait covers it. If the boss chased afterwards the x only grows — fine.
- `test_abort_during_the_warning_removes_the_telegraph` sees the boss far away: the aborted pattern still dashed — `_abort()` must run before `_advance` can reach `_start_charge` (the base clears `_running` first).

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/boss tools godot_gas tests/boss
git commit -q -m "feat(boss): add the charge and ground slam patterns with telegraphs

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Radial Burst and Summon patterns; projectiles that hit allies

**Files:**
- Create: `src/boss/patterns/radial_burst_pattern.gd`, `src/boss/patterns/radial_burst_pattern.tscn`, `src/boss/patterns/summon_pattern.gd`, `src/boss/patterns/summon_pattern.tscn`, `data/projectiles/boss_bolt.tres`
- Modify: `src/defs/projectile_def.gd` (+ `Team`, `hits`, `hit_mask()`), `src/actors/projectile/projectile.gd` (mask from the def), `tools/sync_tags.gd` (+ `Ability.Boss.Burst`, `Ability.Boss.Summon`), `tests/data/test_game_data.gd` (`DATA_FILES`)
- Test: `tests/actors/test_projectile.gd` (append), `tests/boss/test_radial_burst_pattern.gd` (new), `tests/boss/test_summon_pattern.gd` (new)

**Interfaces:**
- Consumes: `BossPattern` (Task 10), `Projectile.launch(def, shooter_asc, damage, direction)`, `AbilityContext.summon` (Task 10), `SpawnGeometry.ring_point`, `EnemyFactory.spawn/alive_count`, `ActorFixture.player/boss/hurt_target`, `DefFactory.boss/enemy`.
- Produces:
  - `ProjectileDef.Team { ENEMIES, ALLIES }`, `ProjectileDef.hits: Team = ENEMIES`, `ProjectileDef.hit_mask() -> int` (`ENEMY_HURT` / `ALLY_HURT`); `Projectile.launch` sets its Hitbox mask from it (arrows unchanged).
  - `data/projectiles/boss_bolt.tres`: arrow texture, `damage_basic`, speed 220, lifetime 2.0, pierce 0, hit radius 6, `hits = ALLIES`.
  - `RadialBurstPattern extends BossPattern` (`radial_burst_pattern.tscn`, tag `Ability.Boss.Burst`, owns `State.Casting`): exports `projectile_scene`, `projectile_def` (`boss_bolt.tres`), `count` 12, `damage` 10; fires `count` projectiles evenly around the boss and completes at once; `validate()` requires `projectile_def.hits == ALLIES`.
  - `SummonPattern extends BossPattern` (`summon_pattern.tscn`, tag `Ability.Boss.Summon`, owns `State.Casting`): exports `enemy` (`orc_swarmer.tres`), `count` 4, `radius` 80; calls `context.summon.call(enemy, point)` for `count` points evenly on the ring and completes; refuses without a valid `summon`.
  - `GameplayTags.Ability_Boss_Burst`, `GameplayTags.Ability_Boss_Summon`.

- [ ] **Step 1: Register the tags**

Add `"Ability.Boss.Burst",` and `"Ability.Boss.Summon",` to `TAGS` in `tools/sync_tags.gd` (sorted: `Ability.Boss.Burst`, `Ability.Boss.Charge`, `Ability.Boss.Slam`, `Ability.Boss.Summon`). Run `"$GODOT_BIN" --headless --path . -s res://tools/sync_tags.gd`, check `godot_gas/gameplay_tags.gd`, then godot-ai `filesystem_manage` `{"op": "scan"}`.

- [ ] **Step 2: Write the failing tests**

Append to `tests/actors/test_projectile.gd`:
```gdscript
func _victim_on(layer: int, at: Vector2) -> Node2D:
	return ActorFixture.hurt_target(self, at, layer)


func test_an_ally_hitting_projectile_damages_allies_and_passes_enemies() -> void:
	var shooter := _actor()
	var def := _def(1)
	def.hits = ProjectileDef.Team.ALLIES
	var ally := _victim_on(PhysicsLayers.ALLY_HURT, Vector2(4, 0))
	var enemy := _victim_on(PhysicsLayers.ENEMY_HURT, Vector2(-4, 0))
	var projectile := _fire(shooter, def)
	assert_eq((projectile.get_node("Hitbox") as Area2D).collision_mask, PhysicsLayers.ALLY_HURT)
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(ally), 90.0)
	assert_almost_eq(_health(enemy), 100.0, 0.001, "pierce 1 would have hit it too if the mask matched")


func test_projectiles_hit_enemies_by_default() -> void:
	assert_eq(ProjectileDef.new().hit_mask(), PhysicsLayers.ENEMY_HURT)
	var projectile := _fire(_actor(), _def())
	assert_eq((projectile.get_node("Hitbox") as Area2D).collision_mask, PhysicsLayers.ENEMY_HURT)
```

`tests/boss/test_radial_burst_pattern.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const BURST_SCENE: PackedScene = preload("res://src/boss/patterns/radial_burst_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(40, -30)


func _rig(player_at: Vector2) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = player_at
	var patterns: Array[PackedScene] = [BURST_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT)
	(boss.patterns()[0] as BossPattern).set_physics_process(false)
	return {"player": player, "boss": boss, "layer": player.ability_context.projectile_layer}


func _bolts(layer: Node) -> Array[Projectile]:
	var found: Array[Projectile] = []
	for child in layer.get_children():
		if child is Projectile:
			found.append(child)
	return found


func test_fires_count_ally_hitting_bolts_evenly_around_the_boss() -> void:
	var rig := _rig(Vector2(600, 600))
	var boss: Boss = rig["boss"]
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Burst))
	assert_false(boss.is_casting(), "the burst completes at once")
	var bolts := _bolts(rig["layer"])
	assert_eq(bolts.size(), 12)
	var angles: Array[float] = []
	for bolt in bolts:
		assert_eq(bolt.global_position, BOSS_AT)
		assert_eq((bolt.get_node("Hitbox") as Area2D).collision_mask, PhysicsLayers.ALLY_HURT)
		angles.append(fposmod(bolt.rotation, TAU))
	angles.sort()
	for i in range(1, angles.size()):
		assert_almost_eq(angles[i] - angles[i - 1], TAU / 12.0, 0.001, "even spacing")


func test_a_bolt_damages_the_player_but_not_the_boss() -> void:
	var rig := _rig(BOSS_AT + Vector2(45, 0))
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Burst)
	await wait_seconds(0.4)
	assert_almost_eq(player.health.current(), 90.0, 0.001, "one bolt flies along +x into the player")
	assert_almost_eq(boss.health.current(), 500.0, 0.001, "bolts ignore the enemy hurt layer")


func test_validate_requires_an_ally_hitting_projectile() -> void:
	var burst := BURST_SCENE.instantiate() as RadialBurstPattern
	assert_no_errors(burst.validate(), "scene as shipped")
	burst.projectile_def = load("res://data/projectiles/arrow.tres")
	assert_eq(burst.validate().size(), 1, "the player's arrow hits enemies")
	burst.count = 0
	assert_eq(burst.validate().size(), 2)
	burst.free()
```

`tests/boss/test_summon_pattern.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const SUMMON_SCENE: PackedScene = preload("res://src/boss/patterns/summon_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(100, 50)


func _boss(summon: Callable) -> Boss:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(-500, 0)
	var patterns: Array[PackedScene] = [SUMMON_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT, summon)
	(boss.patterns()[0] as BossPattern).set_physics_process(false)
	return boss


func test_summons_count_enemies_on_the_ring_through_the_context() -> void:
	var calls: Array = []
	var boss := _boss(func(def: EnemyDef, position: Vector2) -> void: calls.append([def, position]))
	var pattern := boss.patterns()[0] as SummonPattern
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon))
	assert_eq(calls.size(), 4)
	var seen: Array = []
	for call_args in calls:
		assert_true(call_args[0] == pattern.enemy)
		assert_almost_eq((call_args[1] as Vector2).distance_to(BOSS_AT), 80.0, 0.01)
		assert_false(seen.has(call_args[1]), "distinct points")
		seen.append(call_args[1])
	assert_false(boss.is_casting())


func test_without_a_summon_callable_the_pattern_does_not_start() -> void:
	var boss := _boss(Callable())
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon)
	assert_false((boss.patterns()[0] as SummonPattern).is_active)
	assert_false(boss.is_casting())


func test_summoned_enemies_come_from_a_real_factory() -> void:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, RunRng.new(3))
	var boss := _boss(func(def: EnemyDef, position: Vector2) -> void: factory.spawn(def, position))
	var pattern := boss.patterns()[0] as SummonPattern
	pattern.enemy = DefFactory.enemy(ENEMY_SCENE)
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon)
	assert_eq(factory.alive_count(), 4)
	assert_eq(layer.get_child_count(), 4)


func test_validate_reports_a_missing_enemy_and_bad_numbers() -> void:
	var summon := SUMMON_SCENE.instantiate() as SummonPattern
	assert_no_errors(summon.validate(), "scene as shipped")
	assert_eq(summon.enemy.id, &"orc_swarmer")
	summon.enemy = null
	summon.radius = 0.0
	assert_eq(summon.validate().size(), 2)
	summon.free()
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/boss/test_radial_burst_pattern TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `RadialBurstPattern`, `ProjectileDef.Team` not found.

- [ ] **Step 4: Implement the projectile team**

`src/defs/projectile_def.gd` — add right after the class comment (before the first `@export`):
```gdscript
## Which side a projectile damages: the player's arrows hit ENEMIES; boss bolts hit ALLIES (the player
## and the building).
enum Team { ENEMIES, ALLIES }
```
after `@export var hit_radius: float = 6.0`:
```gdscript
@export var hits: Team = Team.ENEMIES
```
and before `validate()`:
```gdscript
func hit_mask() -> int:
	return PhysicsLayers.ALLY_HURT if hits == Team.ALLIES else PhysicsLayers.ENEMY_HURT
```

`src/actors/projectile/projectile.gd` — in `launch`, after `(_hit_shape.shape as CircleShape2D).radius = def.hit_radius` add:
```gdscript
	_hitbox.collision_mask = def.hit_mask()
```

`data/projectiles/boss_bolt.tres`:
```
[gd_resource type="Resource" script_class="ProjectileDef" format=3]

[ext_resource type="Script" path="res://src/defs/projectile_def.gd" id="1_def"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/arrow.png" id="2_tex"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[resource]
script = ExtResource("1_def")
texture = ExtResource("2_tex")
hit_effect = ExtResource("3_damage")
speed = 220.0
lifetime = 2.0
pierce = 0
hit_radius = 6.0
hits = 1
```
Add `"res://data/projectiles/boss_bolt.tres",` to `DATA_FILES` in `tests/data/test_game_data.gd` (after `arrow.tres`).

- [ ] **Step 5: Implement the two patterns**

`src/boss/patterns/radial_burst_pattern.gd`:
```gdscript
class_name RadialBurstPattern
extends BossPattern
## Radial burst: `count` projectiles fired evenly around the boss. The projectile def must hit ALLIES
## (the player and the building); speed and lifetime live in that def.

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var count: int = 0
@export var damage: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if projectile_scene == null or projectile_def == null:
		errors.append("Radial burst: projectile_scene and projectile_def are required")
	elif projectile_def.hits != ProjectileDef.Team.ALLIES:
		errors.append("Radial burst: projectile_def must hit ALLIES")
	if count < 1:
		errors.append("Radial burst: count must be >= 1")
	if damage < 0.0:
		errors.append("Radial burst: damage must be >= 0")
	return errors


func _begin() -> bool:
	var origin := context.actor.global_position
	for i in count:
		var direction := Vector2.from_angle(TAU * float(i) / float(count))
		var projectile := projectile_scene.instantiate() as Projectile
		context.projectile_layer.add_child(projectile)
		projectile.global_position = origin
		projectile.launch(projectile_def, owner_asc, damage, direction)
	_complete()
	return true
```

`src/boss/patterns/radial_burst_pattern.tscn`:
```
[gd_scene load_steps=4 format=3]

[ext_resource type="Script" path="res://src/boss/patterns/radial_burst_pattern.gd" id="1_burst"]
[ext_resource type="PackedScene" path="res://src/actors/projectile/projectile.tscn" id="2_projectile"]
[ext_resource type="Resource" path="res://data/projectiles/boss_bolt.tres" id="3_bolt"]

[node name="RadialBurstPattern" type="Node"]
script = ExtResource("1_burst")
ability_name = "Radial Burst"
ability_tag = &"Ability.Boss.Burst"
activation_owned_tags = Array[StringName]([&"State.Casting"])
projectile_scene = ExtResource("2_projectile")
projectile_def = ExtResource("3_bolt")
count = 12
damage = 10.0
```

`src/boss/patterns/summon_pattern.gd`:
```gdscript
class_name SummonPattern
extends BossPattern
## Summon: `count` enemies of `enemy` evenly on a ring of `radius` around the boss, through the Stage's
## AbilityContext.summon (it spawns with the EnemyFactory, so summoned enemies count as kills and drop loot).

@export var enemy: EnemyDef
@export var count: int = 0
@export var radius: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if enemy == null:
		errors.append("Summon: enemy is null")
	else:
		errors.append_array(enemy.validate())
	if count < 1 or radius <= 0.0:
		errors.append("Summon: count must be >= 1 and radius > 0")
	return errors


func _begin() -> bool:
	if not context.summon.is_valid():
		return false
	var origin := context.actor.global_position
	for i in count:
		context.summon.call(enemy, SpawnGeometry.ring_point(origin, radius, TAU * float(i) / float(count)))
	_complete()
	return true
```

`src/boss/patterns/summon_pattern.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/boss/patterns/summon_pattern.gd" id="1_summon"]
[ext_resource type="Resource" path="res://data/enemies/orc_swarmer.tres" id="2_swarmer"]

[node name="SummonPattern" type="Node"]
script = ExtResource("1_summon")
ability_name = "Summon"
ability_tag = &"Ability.Boss.Summon"
activation_owned_tags = Array[StringName]([&"State.Casting"])
enemy = ExtResource("2_swarmer")
count = 4
radius = 80.0
```

- [ ] **Step 6: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/actors/test_projectile ...`, `FILTER=tests/boss ...`, `FILTER=tests/data ...` (with `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`)
Expected: projectile 8, burst 3, summon 4 PASS; data tests PASS. Likely snags:
- Even spacing fails at the wrap: `fposmod(rotation, TAU)` gives 0 … 11·TAU/12 after sorting; if a rotation is `-0.0000001` it maps to almost TAU — compare with a 0.001 tolerance as written, or round each angle to 4 decimals first.
- The player takes 20: two bolts hit — the player's hurt circle (10 px) at 45 px from the boss is only on the +x ray; any second hit means the projectile pierce is not 0 in `boss_bolt.tres`.

- [ ] **Step 7: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/boss src/defs src/actors/projectile data/projectiles tools godot_gas tests/actors tests/boss tests/data
git commit -q -m "feat(boss): add radial burst and summon patterns; projectiles can hit allies

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: BossDirector and Stage wiring (events, rewards, boss, brain, summons) with integration tests

**Files:**
- Create: `src/stage/boss_director.gd`
- Modify: `src/stage/stage.gd` (whole file below), `src/stage/stage.tscn`
- Test: `tests/stage/test_boss_director.gd` (new), `tests/stage/test_stage.gd` (append)

**Interfaces:**
- Consumes: `EventDirector.bind/event_times/abort_running/event_ended/running` + `validate_defs` (Task 6), `EventContext` (Task 6), `StageFlow.transition_to/is_terminal/state`, `StageClock.ended` (overtime, Task 2), `Boss.setup/died/health/asc/def` (Task 10), `BossBrain.attach_to/validate_def` (Task 11), `SummonPattern` (Task 13), `RewardDef`, `Wallet.Kind.TICKET` (Task 1), `SpawnGeometry`, `ProbeEvent`/`ProbePattern`, `DefFactory.boss/event/schedule/reward/packed`, `ActorFixture.kill`.
- Produces:
  - `BossDirector` (Node): signals `boss_spawned(boss: Boss)`, `boss_appeared(boss_health: Health, display_name: String)`, `boss_defeated`; `boss: Boss`; `bind(boss_def: BossDef, flow: StageFlow, events: EventDirector, layer: Node2D, anchor: Node2D, spawn_radius: float, bounds: Rect2, rng: RunRng, targets: TargetProvider, projectile_layer: Node, summon: Callable)`; `has_boss() -> bool`; `appear() -> Boss` — only from RUNNING, once: flow → BOSS, `events.abort_running()`, boss on the spawn ring around `anchor` (clamped into `bounds`), `setup(def, targets.player, targets, projectile_layer, summon)`, then `boss_spawned`, then `boss_appeared`. Boss `died` → `boss_defeated`.
  - `Stage`: `signal boss_appeared(boss_health: Health, display_name: String)` (relayed); public `events: EventDirector`; `flow_state() -> StageFlow.State`; `current_boss() -> Boss`. `start()` also runs `EventDirector.validate_defs(stage_def.events)` and `BossBrain.validate_def(stage_def.boss)`. Clock `ended` → boss fight when the stage has a boss, else CLEARED. Boss defeated → CLEARED. Event success → `RewardDef` credited to the run wallet (gold, stone, tickets); failure/abort pays nothing. `_finish` aborts a running event. Boss summons go through the `EnemyFactory` (kills, `Event.Kill`, drops), clamped into the arena. The `BossBrain` is attached on `boss_spawned`.
  - `stage.tscn`: `World/Events` (Node2D, y-sorted) and `Systems/EventDirector`, `Systems/BossDirector`.

- [ ] **Step 1: Write the failing tests**

`tests/stage/test_boss_director.gd`:
```gdscript
extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const ANCHOR_AT: Vector2 = Vector2(80, -40)


func _rig(with_boss: bool = true) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(80, 110)
	var targets := player.ability_context.targets
	var anchor := adopt(Node2D.new()) as Node2D
	anchor.global_position = ANCHOR_AT
	var layer := adopt(Node2D.new()) as Node2D
	var flow := StageFlow.new()
	flow.transition_to(StageFlow.State.RUNNING)
	var events := adopt(EventDirector.new()) as EventDirector
	var director := adopt(BossDirector.new()) as BossDirector
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def: BossDef = DefFactory.boss(BOSS_SCENE, patterns) if with_boss else null
	var log := {"spawned": [], "appeared": [], "defeated": []}
	director.boss_spawned.connect(func(boss: Boss) -> void: (log["spawned"] as Array).append(boss))
	director.boss_appeared.connect(func(health: Health, display_name: String) -> void: (log["appeared"] as Array).append([health, display_name]))
	director.boss_defeated.connect(func() -> void: (log["defeated"] as Array).append(true))
	director.bind(def, flow, events, layer, anchor, 300.0, Rect2(-1200, -800, 2400, 1600), RunRng.new(6), targets, player.ability_context.projectile_layer, Callable())
	return {"director": director, "flow": flow, "layer": layer, "player": player, "log": log}


func test_appear_switches_to_boss_and_spawns_it_on_the_ring() -> void:
	var rig := _rig()
	var director: BossDirector = rig["director"]
	var boss := director.appear()
	assert_not_null(boss)
	assert_eq((rig["flow"] as StageFlow).state, StageFlow.State.BOSS)
	assert_eq(boss.get_parent(), rig["layer"])
	assert_almost_eq(boss.global_position.distance_to(ANCHOR_AT), 300.0, 0.5)
	assert_eq(rig["log"]["spawned"], [boss])
	assert_eq(rig["log"]["appeared"], [[boss.health, "Test Boss"]])
	assert_true(boss.ability_context.targets.player == rig["player"], "chases and targets the player")


func test_appear_happens_once_and_only_from_running() -> void:
	var rig := _rig()
	var director: BossDirector = rig["director"]
	var first := director.appear()
	assert_true(director.appear() == first, "a second call returns the same boss")
	assert_eq((rig["log"]["spawned"] as Array).size(), 1)
	var finished := _rig()
	(finished["flow"] as StageFlow).transition_to(StageFlow.State.FAILED)
	assert_null((finished["director"] as BossDirector).appear(), "the run is over")
	assert_true((finished["log"]["spawned"] as Array).is_empty())


func test_boss_death_is_reported_as_defeated() -> void:
	var rig := _rig()
	var boss := (rig["director"] as BossDirector).appear()
	ActorFixture.kill(self, boss.asc)
	assert_eq((rig["log"]["defeated"] as Array).size(), 1)


func test_without_a_boss_nothing_appears() -> void:
	var rig := _rig(false)
	var director: BossDirector = rig["director"]
	assert_false(director.has_boss())
	assert_null(director.appear())
	assert_eq((rig["flow"] as StageFlow).state, StageFlow.State.RUNNING, "flow untouched")
```

Append to `tests/stage/test_stage.gd` (constants next to the existing ones):
```gdscript
const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const PROBE_EVENT: PackedScene = preload("res://tests/support/probe_event.tscn")
const SUMMON_SCENE: PackedScene = preload("res://src/boss/patterns/summon_pattern.tscn")


## A stage without trickle or waves whose clock the test drives by hand.
func _manual_stage(def: StageDef, results: Array) -> Stage:
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	assert_true(stage.start(def, _profile(), 11))
	stage.clock.set_physics_process(false)
	return stage


func _quiet_def(duration: float) -> StageDef:
	var def := _stage_def(duration)
	def.trickle_enemies.clear()
	def.waves.entries.clear()
	return def


func _boss_def(pattern: PackedScene = PROBE_PATTERN) -> BossDef:
	var patterns: Array[PackedScene] = [pattern]
	return DefFactory.boss(BOSS_SCENE, patterns)


## One endless probe event somewhere in [window_start, window_end]. Its time limit is 1 s so the schedule
## passes the window_end + time_limit <= duration rule even on a 5 s stage; being endless, it keeps running
## past that limit until the test (or the boss) ends it.
func _probe_events(window_start: float, window_end: float) -> EventSchedule:
	var probe := PROBE_EVENT.instantiate() as ProbeEvent
	probe.endless = true
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(probe), 1.0, &"probe")]
	return DefFactory.schedule(pool, 1, window_start, window_end, 10.0)


func test_time_up_brings_the_boss_and_its_death_clears_the_stage() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	var results: Array = []
	var stage := _manual_stage(def, results)
	var appeared: Array = []
	stage.boss_appeared.connect(func(health: Health, display_name: String) -> void: appeared.append([health, display_name]))
	stage.clock.step(5.0)
	assert_true(results.is_empty(), "time up starts the boss fight instead of clearing")
	assert_eq(stage.flow_state(), StageFlow.State.BOSS)
	var boss := stage.current_boss()
	assert_not_null(boss)
	assert_eq(appeared, [[boss.health, "Test Boss"]])
	assert_almost_eq(boss.global_position.distance_to(stage.building.global_position), def.spawn_radius, 1.0)
	assert_true(boss.get_node_or_null("Brain") is BossBrain, "the Stage attached the brain")
	stage.clock.step(30.0)
	assert_true(results.is_empty(), "the boss fight has no time limit")
	assert_almost_eq(stage.clock.elapsed, 35.0)
	_kill(stage, boss.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_eq(stage.run_state.kills, 0, "the boss is not a kill")


func test_without_a_boss_time_up_clears_at_once() -> void:
	var results: Array = []
	var stage := _manual_stage(_quiet_def(5.0), results)
	stage.clock.step(5.0)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_null(stage.current_boss())
	assert_eq(stage.flow_state(), StageFlow.State.CLEARED)


func test_player_death_during_the_boss_fight_fails_the_run() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(5.0)
	_kill(stage, stage.player.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_boss_appearance_aborts_a_running_event_without_reward() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	def.events = _probe_events(1.0, 3.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	var ended: Array = []
	stage.events.event_ended.connect(func(event_def: EventDef, success: bool) -> void: ended.append([event_def.id, success]))
	stage.clock.step(3.5)
	var probe := stage.events.running() as ProbeEvent
	assert_not_null(probe, "the event is running")
	assert_eq(probe.get_parent(), stage.get_node("World/Events"))
	stage.clock.step(1.5)
	assert_not_null(stage.current_boss())
	assert_eq(ended, [[&"probe", false]])
	assert_eq(probe.cleanups, 1, "its objects were removed")
	assert_null(stage.events.running())
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_eq(stage.run_state.wallet.tickets, 0)


func test_a_successful_event_credits_its_reward() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	(stage.events.running() as ProbeEvent).finish(true)
	assert_eq(stage.run_state.wallet.gold, 150)
	assert_eq(stage.run_state.wallet.stone, 2)
	assert_eq(stage.run_state.wallet.tickets, 1)


func test_a_failed_event_pays_nothing() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	(stage.events.running() as ProbeEvent).finish(false)
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_eq(stage.run_state.wallet.tickets, 0)


func test_the_run_ending_aborts_a_running_event() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	var probe := stage.events.running() as ProbeEvent
	_kill(stage, stage.building.asc)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)
	assert_eq(probe.cleanups, 1)
	assert_null(stage.events.running())


func test_boss_summons_spawn_through_the_factory_and_count_as_kills() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def(SUMMON_SCENE)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(5.0)
	var boss := stage.current_boss()
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon))
	var summoned: Array = []
	for node in host.get_tree().get_nodes_in_group(Groups.ENEMIES):
		if node is Enemy:
			summoned.append(node)
	assert_eq(summoned.size(), 4)
	assert_eq((summoned[0] as Enemy).get_parent(), stage.get_node("World/Enemies"))
	_kill(stage, (summoned[0] as Enemy).get_node("AbilitySystemComponent"))
	assert_eq(stage.run_state.kills, 1, "summons are ordinary kills")


func test_event_controllers_must_be_stage_events() -> void:
	var def := _quiet_def(60.0)
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 10.0, &"plain")]
	def.events = DefFactory.schedule(pool, 1, 5.0, 10.0, 10.0)
	assert_no_errors(def.validate(), "the def alone cannot see the problem")
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, _profile(), 11))
	assert_null(stage.run_state)


func test_boss_patterns_must_be_actor_abilities() -> void:
	var def := _quiet_def(60.0)
	def.boss = _boss_def(DefFactory.packed(BareGameplayAbility.new()))
	assert_no_errors(def.validate(), "the def alone cannot see the problem")
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, _profile(), 11))
	assert_null(stage.run_state)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/stage/test_boss_director TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse error — `BossDirector` not found.

- [ ] **Step 3: Implement the BossDirector**

`src/stage/boss_director.gd`:
```gdscript
class_name BossDirector
extends Node
## Brings the boss in when the stage time is up: switches the flow to BOSS, aborts a running event, spawns
## the boss on the spawn ring around the anchor (the building) and announces it. The boss's death is
## reported as `boss_defeated`; the Stage turns that into CLEARED. The boss is a scene from BossDef.scene.

signal boss_spawned(boss: Boss)
signal boss_appeared(boss_health: Health, display_name: String)
signal boss_defeated

const EDGE_MARGIN: float = 24.0

var boss: Boss

var _def: BossDef
var _flow: StageFlow
var _events: EventDirector
var _layer: Node2D
var _anchor: Node2D
var _spawn_radius: float = 0.0
var _bounds: Rect2
var _rng: RunRng
var _targets: TargetProvider
var _projectile_layer: Node
var _summon: Callable


func bind(boss_def: BossDef, flow: StageFlow, events: EventDirector, layer: Node2D, anchor: Node2D, spawn_radius: float, bounds: Rect2, rng: RunRng, targets: TargetProvider, projectile_layer: Node, summon: Callable) -> void:
	_def = boss_def
	_flow = flow
	_events = events
	_layer = layer
	_anchor = anchor
	_spawn_radius = spawn_radius
	_bounds = bounds
	_rng = rng
	_targets = targets
	_projectile_layer = projectile_layer
	_summon = summon


func has_boss() -> bool:
	return _def != null


## Spawns the boss once, from RUNNING only. Returns it, or null when there is none or the run is over.
func appear() -> Boss:
	if boss != null:
		return boss
	if _def == null or not _flow.transition_to(StageFlow.State.BOSS):
		return null
	_events.abort_running()
	boss = _def.scene.instantiate() as Boss
	_layer.add_child(boss)
	var center := _anchor.global_position if is_instance_valid(_anchor) else Vector2.ZERO
	var point := SpawnGeometry.ring_point(center, _spawn_radius, _rng.randf() * TAU)
	boss.global_position = SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN)
	boss.setup(_def, _targets.player, _targets, _projectile_layer, _summon)
	boss.died.connect(_on_boss_died)
	boss_spawned.emit(boss)
	boss_appeared.emit(boss.health, _def.display_name)
	return boss


func _on_boss_died(_boss: Boss) -> void:
	boss_defeated.emit()
```

- [ ] **Step 4: Wire the Stage**

`src/stage/stage.tscn` — change `load_steps=12` to `load_steps=14`, add after the installer ext resource:
```
[ext_resource type="Script" path="res://src/stage/event_director.gd" id="12_events"]
[ext_resource type="Script" path="res://src/stage/boss_director.gd" id="13_boss"]
```
add after the `Player` node (before `Projectiles`):
```
[node name="Events" type="Node2D" parent="World"]
y_sort_enabled = true
```
and append at the end:
```
[node name="EventDirector" type="Node" parent="Systems"]
script = ExtResource("12_events")

[node name="BossDirector" type="Node" parent="Systems"]
script = ExtResource("13_boss")
```

`src/stage/stage.gd` (whole file):
```gdscript
class_name Stage
extends Node2D
## Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the
## signals between modules, and reports a RunResult. Modules never look each other up.
## The stage knows nothing about UI: whoever presents it binds to the public models below.
## As the composition root it may reference abilities/ (the shop) and boss/ (the brain it attaches).

signal finished(result: RunResult)
## The boss arrived; relayed from the BossDirector for the HUD.
signal boss_appeared(boss_health: Health, display_name: String)

const PLAYER_START: Vector2 = Vector2(0.0, 150.0)
## Keeps summoned enemies inside the arena walls.
const SUMMON_EDGE_MARGIN: float = 24.0

var run_state: RunState
## The ability slots and the shop rules over them; the HUD binds to these through Main.
var loadout: Loadout
var grade_service: GradeService

var _flow := StageFlow.new()

@onready var player: Player = $World/Player
@onready var building: Building = $World/Building
@onready var clock: StageClock = $Systems/StageClock
@onready var waves: WaveDirector = $Systems/WaveDirector
## The stage's events: their times for the timeline and start/progress/end signals for the HUD.
@onready var events: EventDirector = $Systems/EventDirector
@onready var _bosses: BossDirector = $Systems/BossDirector
@onready var _world: Node2D = $World
@onready var _arena: Arena = $Arena
@onready var _enemy_layer: Node2D = $World/Enemies
@onready var _event_layer: Node2D = $World/Events
@onready var _projectile_layer: Node2D = $World/Projectiles
@onready var _pickup_layer: Node2D = $World/Pickups
@onready var _targets: TargetProvider = $Systems/TargetProvider
@onready var _factory: EnemyFactory = $Systems/EnemyFactory
@onready var _spawner: SpawnDirector = $Systems/SpawnDirector
@onready var _loot: LootDirector = $Systems/LootDirector
@onready var _installer: AbilityInstaller = $Systems/AbilityInstaller


## Returns false, after logging every problem, when a def is invalid; nothing is started then.
func start(stage_def: StageDef, profile: PlayerProfile, run_seed: int = 0) -> bool:
	if run_state != null:
		push_error("Stage.start: already started; a Stage runs once")
		return false
	var errors := stage_def.validate()
	errors.append_array(profile.validate())
	errors.append_array(AbilityInstaller.validate_defs(profile.abilities))
	errors.append_array(EventDirector.validate_defs(stage_def.events))
	errors.append_array(BossBrain.validate_def(stage_def.boss))
	if not errors.is_empty():
		for error in errors:
			push_error("Stage.start: %s" % error)
		return false
	run_state = RunState.new(stage_def, profile, run_seed)
	_arena.build(stage_def.arena_size, stage_def.ground_texture)
	building.setup(stage_def.building_texture, stage_def.building_stats)
	player.global_position = PLAYER_START
	player.setup(profile, _targets, _projectile_layer, run_state.wallet)
	player.limit_camera(_arena.bounds)
	_targets.building = building
	_targets.player = player
	_factory.bind(_enemy_layer, _targets, run_state.rng)
	# Enemies appear on a ring around the defended building, not around the roaming player.
	_spawner.bind(stage_def, clock, _factory, building, run_state.rng, _arena.bounds)
	waves.bind(stage_def, clock, _factory, building, run_state.rng, _arena.bounds)
	events.bind(stage_def, clock, _event_context, run_state.rng, waves.mark_times())
	_bosses.bind(stage_def.boss, _flow, events, _enemy_layer, building, stage_def.spawn_radius, _arena.bounds, run_state.rng, _targets, _projectile_layer, _summon)
	_loot.bind(_factory, _pickup_layer, player, run_state.wallet, stage_def.drop_multiplier)
	# The shop: pure rules over the run's wallet and rng; the installer mirrors slots onto the player.
	loadout = Loadout.new(profile.slot_count)
	grade_service = GradeService.new(loadout, AbilityPool.new(profile.abilities), stage_def.grade_table, run_state.wallet, run_state.rng)
	_installer.bind(loadout, player)
	_wire()
	_flow.transition_to(StageFlow.State.RUNNING)
	clock.start(stage_def.duration)
	return true


func flow_state() -> StageFlow.State:
	return _flow.state


## The boss once it has appeared; null before (or on a stage without one).
func current_boss() -> Boss:
	return _bosses.boss


func _wire() -> void:
	building.destroyed.connect(_finish.bind(RunResult.Outcome.FAILED))
	player.died.connect(_finish.bind(RunResult.Outcome.FAILED))
	clock.ended.connect(_on_clock_ended)
	_factory.spawned.connect(_on_enemy_spawned)
	events.event_ended.connect(_on_event_ended)
	_bosses.boss_spawned.connect(_on_boss_spawned)
	_bosses.boss_appeared.connect(boss_appeared.emit)
	_bosses.boss_defeated.connect(_finish.bind(RunResult.Outcome.CLEARED))


## Time up: the boss fight starts when the stage has a boss; otherwise the stage is cleared.
func _on_clock_ended() -> void:
	if _flow.is_terminal():
		return
	if _bosses.has_boss():
		_bosses.appear()
	else:
		_finish(RunResult.Outcome.CLEARED)


func _event_context(def: EventDef) -> EventContext:
	return EventContext.new(_event_layer, _factory, _targets, player, building, run_state.rng, _arena.bounds, run_state.stage_def.spawn_radius, def)


## Boss summons go through the factory like every enemy, so they count as kills and drop loot.
func _summon(def: EnemyDef, position: Vector2) -> void:
	_factory.spawn(def, SpawnGeometry.clamp_to_rect(position, _arena.bounds, SUMMON_EDGE_MARGIN))


func _on_boss_spawned(boss: Boss) -> void:
	BossBrain.attach_to(boss, run_state.rng)


## Only success pays; a failed or aborted event credits nothing.
func _on_event_ended(def: EventDef, success: bool) -> void:
	if not success or def.reward == null:
		return
	run_state.wallet.add(Wallet.Kind.GOLD, def.reward.gold)
	run_state.wallet.add(Wallet.Kind.STONE, def.reward.stone)
	run_state.wallet.add(Wallet.Kind.TICKET, def.reward.tickets)


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.died.connect(_on_enemy_died)


## Every kill is also a gameplay event on the player's ASC, so on-kill abilities react through GAS.
func _on_enemy_died(enemy: Enemy) -> void:
	run_state.kills += 1
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, enemy)


func _finish(outcome: RunResult.Outcome) -> void:
	if _flow.is_terminal():
		return
	var end_state := StageFlow.State.CLEARED if outcome == RunResult.Outcome.CLEARED else StageFlow.State.FAILED
	_flow.transition_to(end_state)
	events.abort_running()
	clock.stop()
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	finished.emit(run_state.to_result(outcome, clock.elapsed))
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/stage TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: `test_boss_director` 4 PASS, `test_stage` 26 PASS (16 existing + 10 new), every other stage test PASS. Likely snags:
- `$Systems/EventDirector` / `$World/Events` null: the nodes must be declared in `stage.tscn` with the parents shown.
- `test_time_up_brings_the_boss...` sees CLEARED at once: `clock.ended` is still connected to `_finish` somewhere — the only connection must be `_on_clock_ended`.
- `test_boss_summons_...` counts 5: the boss is in the enemies group but is not an `Enemy` — the loop filters on `is Enemy`; a 5th `Enemy` means the trickle was not cleared (`_quiet_def`).
- The `Summon` tag is not activatable because the brain is already running a pattern: the brain waits `pattern_gap` (2 s) in real time before its first pick, and the test activates synchronously right after the clock step.

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/stage tests/stage
git commit -q -m "feat(stage): wire events, rewards and the boss fight (BossDirector, brain, summons) into the Stage

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: HUD — timeline event marks and BOSS, EventBanner, ObjectiveArrows, BossBar; Main binds them

**Files:**
- Create: `src/ui/hud/event_banner.gd`, `src/ui/hud/objective_arrows.gd`, `src/ui/hud/boss_bar.gd`
- Modify: `src/ui/hud/timeline_bar.gd` (whole file below), `src/ui/hud/hud.gd` (whole file below), `src/ui/hud/hud.tscn`, `src/app/main.gd`
- Test: `tests/ui/test_timeline_bar.gd` (append), `tests/ui/test_event_banner.gd` (new), `tests/ui/test_objective_arrows.gd` (new), `tests/ui/test_boss_bar.gd` (new), `tests/ui/test_hud.gd` (modify `_rig`, append)

**Interfaces:**
- Consumes: `StageEvent.remaining_time/objectives` and `ProbeEvent` (Task 6), `EventDef.display_name/reward`, `RewardDef`, `Health.changed/current/maximum`, `Stage.events` (`event_started(def, event)`, `event_progressed(def, text)`, `event_ended(def, success)`, `event_times()`) and `Stage.boss_appeared(boss_health, display_name)` (Task 14), `ActorFixture.event_context/bare_actor/asc_of`, `DefFactory.event/reward`.
- Produces:
  - `TimelineBar.configure(duration, marks, event_marks: PackedFloat32Array = PackedFloat32Array())` — waves stay diamonds, events are circles in `event_mark_color`; `event_marks() -> PackedFloat32Array`; `show_boss()` reveals a "BOSS" label over the bar; `is_showing_boss() -> bool`.
  - `EventBanner` (VBoxContainer, builds its labels in `_ready`, hidden until used): `show_event(def: EventDef, event: StageEvent)`, `show_progress(text: String)`, `show_result(def: EventDef, success: bool)` (`"<name>: Success +150 gold +2 stone +1 ticket"` / `"<name>: Failed"`, hidden after `result_seconds`); `step(delta)` (process-driven; refreshes the remaining time as `"<ceil>s"`); `static reward_text(reward: RewardDef) -> String`; `title_text()`, `progress_text()`, `timer_text()`.
  - `ObjectiveArrows` (Control, full rect, mouse-transparent): `track(event: StageEvent)`, `clear()`, `refresh(canvas_transform: Transform2D, view_size: Vector2)` (process-driven with the viewport's canvas transform), `arrow_positions() -> PackedVector2Array`, `static edge_point(target: Vector2, view: Rect2, edge_margin: float) -> Vector2`.
  - `BossBar` (VBoxContainer, hidden until `show_boss`): `show_boss(health: Health, display_name: String)`, `boss_name() -> String`, `value() -> float`, `maximum() -> float`.
  - `Hud.bind(player_health, building_health, wallet, clock, timeline, event_times: PackedFloat32Array = PackedFloat32Array())`; `Hud.show_event(def, event)`, `Hud.show_event_progress(def, text)`, `Hud.show_event_result(def, success)`, `Hud.show_boss(boss_health, display_name)`.
  - `Main` passes `_stage.events.event_times()` to `Hud.bind` and connects the three event signals and `boss_appeared` to those Hud methods.

- [ ] **Step 1: Write the failing tests**

Append to `tests/ui/test_timeline_bar.gd`:
```gdscript
func test_event_marks_are_kept_apart_from_wave_marks() -> void:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(100.0, PackedFloat32Array([25.0]), PackedFloat32Array([40.0, 70.0]))
	assert_eq(bar.event_marks(), PackedFloat32Array([40.0, 70.0]))
	assert_almost_eq(bar.mark_x(70.0), 280.0)
	bar.configure(100.0, PackedFloat32Array([25.0]))
	assert_eq(bar.event_marks().size(), 0, "reconfiguring without events clears them")


func test_the_boss_label_appears_once_the_boss_does() -> void:
	var bar := _bar()
	assert_false(bar.is_showing_boss())
	bar.show_boss()
	assert_true(bar.is_showing_boss())
	var label := bar.get_node("BossLabel") as Label
	assert_true(label.visible)
	assert_eq(label.text, "BOSS")
```

`tests/ui/test_event_banner.gd`:
```gdscript
extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _banner() -> EventBanner:
	var banner := adopt(EventBanner.new()) as EventBanner
	banner.set_process(false)
	return banner


func _running_probe(def: EventDef) -> ProbeEvent:
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	event.begin(ActorFixture.event_context(self, def))
	return event


func test_hidden_until_an_event_starts() -> void:
	assert_false(_banner().visible)


func test_shows_name_progress_and_remaining_time_of_the_running_event() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"statues")
	var event := _running_probe(def)
	banner.show_event(def, event)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Statues")
	assert_eq(banner.timer_text(), "25s")
	banner.show_progress("Statues 1/4")
	assert_eq(banner.progress_text(), "Statues 1/4")
	event.step(5.2)
	banner.step(0.016)
	assert_eq(banner.timer_text(), "20s", "ceil of 19.8")


func test_success_shows_the_rewards_then_hides() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"meteors")
	banner.show_event(def, _running_probe(def))
	banner.show_result(def, true)
	assert_eq(banner.title_text(), "Meteors: Success +150 gold +2 stone +1 ticket")
	assert_eq(banner.timer_text(), "")
	banner.step(1.0)
	assert_true(banner.visible)
	banner.step(2.0)
	assert_false(banner.visible, "hidden after result_seconds (2.5 s)")


func test_failure_shows_failed_without_rewards() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0, &"elites")
	banner.show_result(def, false)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Elites: Failed")


func test_reward_text_omits_zero_amounts_and_pluralises_tickets() -> void:
	assert_eq(EventBanner.reward_text(DefFactory.reward(0, 3, 2)), "+3 stone +2 tickets")
	assert_eq(EventBanner.reward_text(DefFactory.reward(200, 0, 0)), "+200 gold")


func test_a_freed_event_does_not_break_the_banner() -> void:
	var banner := _banner()
	var def := DefFactory.event(PROBE, 25.0)
	var event := _running_probe(def)
	banner.show_event(def, event)
	event.free()
	banner.step(0.1)
	assert_eq(banner.timer_text(), "25s", "keeps the last value")
```

`tests/ui/test_objective_arrows.gd`:
```gdscript
extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")
const VIEW: Vector2 = Vector2(800, 600)


func _arrows() -> ObjectiveArrows:
	var arrows := adopt(ObjectiveArrows.new()) as ObjectiveArrows
	arrows.set_process(false)
	arrows.margin = 20.0
	return arrows


func _probe_with(points: Array[Vector2]) -> ProbeEvent:
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	for point in points:
		var node := adopt(Node2D.new()) as Node2D
		node.global_position = point
		event.objective_nodes.append(node)
	return event


func test_edge_point_clamps_to_the_inner_rect_along_the_ray_from_the_centre() -> void:
	var view := Rect2(Vector2.ZERO, VIEW)
	assert_eq(ObjectiveArrows.edge_point(Vector2(1600, 300), view, 20.0), Vector2(780, 300))
	assert_eq(ObjectiveArrows.edge_point(Vector2(400, -900), view, 20.0), Vector2(400, 20))
	var corner := ObjectiveArrows.edge_point(Vector2(1200, 1200), view, 20.0)
	assert_almost_eq(corner.x, 400.0 + 800.0 * 280.0 / 900.0, 0.01)
	assert_almost_eq(corner.y, 580.0, 0.01)


func test_only_off_screen_objectives_get_arrows() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(100, 100), Vector2(2000, 300)]
	arrows.track(_probe_with(points))
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions(), PackedVector2Array([Vector2(780, 300)]))


func test_the_camera_transform_decides_what_is_on_screen() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(1400, 300)]
	arrows.track(_probe_with(points))
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 1, "off-screen without a camera offset")
	arrows.refresh(Transform2D(0.0, Vector2(-1000, 0)), VIEW)
	assert_eq(arrows.arrow_positions().size(), 0, "the camera moved right: now on screen")


func test_cleared_or_freed_objectives_have_no_arrows() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(-900, 300)]
	var event := _probe_with(points)
	arrows.track(event)
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 1)
	event.objective_nodes[0].free()
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 0)
	arrows.clear()
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 0)
```

`tests/ui/test_boss_bar.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func test_hidden_until_the_boss_appears_then_follows_its_health() -> void:
	var bar := adopt(BossBar.new()) as BossBar
	assert_false(bar.visible)
	var stats := StatBlock.new()
	stats.max_health = 2500.0
	var boss := ActorFixture.bare_actor(self, stats)
	var health := Health.new()
	boss.add_child(health)
	health.bind(ActorFixture.asc_of(boss))
	bar.show_boss(health, "Orc Warlord")
	assert_true(bar.visible)
	assert_eq(bar.boss_name(), "Orc Warlord")
	assert_almost_eq(bar.maximum(), 2500.0)
	assert_almost_eq(bar.value(), 2500.0)
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(EffectBuilder.damage(attacker, attacker, DAMAGE, 400.0), ActorFixture.asc_of(boss))
	assert_almost_eq(bar.value(), 2100.0)
```

In `tests/ui/test_hud.gd` change the `hud.bind(...)` line in `_rig()` to pass event times:
```gdscript
	hud.bind(_health_for(player_actor), _health_for(building_actor), wallet, clock, Timeline.new(100.0, PackedFloat32Array([30.0])), PackedFloat32Array([45.0, 80.0]))
```
and append:
```gdscript
const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func test_bind_passes_the_event_times_to_the_timeline() -> void:
	var hud: Hud = _rig()["hud"]
	assert_eq((hud.get_node("%Timeline") as TimelineBar).event_marks(), PackedFloat32Array([45.0, 80.0]))


func test_event_signals_drive_the_banner_and_the_arrows() -> void:
	var hud: Hud = _rig()["hud"]
	var banner := hud.get_node("%EventBanner") as EventBanner
	var arrows := hud.get_node("%ObjectiveArrows") as ObjectiveArrows
	arrows.set_process(false)
	var def := DefFactory.event(PROBE, 20.0, &"meteors")
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	var far := adopt(Node2D.new()) as Node2D
	far.global_position = Vector2(5000, 0)
	event.objective_nodes.append(far)
	event.begin(ActorFixture.event_context(self, def))
	hud.show_event(def, event)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Meteors")
	hud.show_event_progress(def, "Hits 1/3")
	assert_eq(banner.progress_text(), "Hits 1/3")
	arrows.refresh(Transform2D.IDENTITY, Vector2(800, 600))
	assert_eq(arrows.arrow_positions().size(), 1, "the HUD tracks the running event's objectives")
	hud.show_event_result(def, true)
	assert_true(banner.title_text().contains("Success"))
	arrows.refresh(Transform2D.IDENTITY, Vector2(800, 600))
	assert_eq(arrows.arrow_positions().size(), 0, "no arrows once the event ended")


func test_boss_appearance_shows_the_bar_and_the_timeline_label() -> void:
	var rig := _rig()
	var hud: Hud = rig["hud"]
	var boss_bar := hud.get_node("%BossBar") as BossBar
	assert_false(boss_bar.visible)
	var stats := StatBlock.new()
	stats.max_health = 2500.0
	hud.show_boss(_health_for(ActorFixture.bare_actor(self, stats)), "Orc Warlord")
	assert_true(boss_bar.visible)
	assert_eq(boss_bar.boss_name(), "Orc Warlord")
	assert_true((hud.get_node("%Timeline") as TimelineBar).is_showing_boss())
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/ui TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: parse errors — `EventBanner`, `ObjectiveArrows`, `BossBar` not found.

- [ ] **Step 3: Implement the widgets**

`src/ui/hud/timeline_bar.gd` (whole file):
```gdscript
class_name TimelineBar
extends Control
## Stage progress: a diamond for every wave, a circle for every event (passed marks turn grey), and a
## "BOSS" label once the boss fight starts.

const MARK_HALF_WIDTH: float = 5.0

@export var track_color: Color = Color(0.12, 0.12, 0.16, 0.85)
@export var fill_color: Color = Color(0.35, 0.7, 1.0)
@export var mark_color: Color = Color(1.0, 0.75, 0.2)
@export var event_mark_color: Color = Color(0.55, 1.0, 0.45)
@export var passed_mark_color: Color = Color(0.55, 0.55, 0.55)
@export var boss_text_color: Color = Color(1.0, 0.35, 0.3)

var _duration: float = 1.0
var _marks: PackedFloat32Array = PackedFloat32Array()
var _event_marks: PackedFloat32Array = PackedFloat32Array()
var _elapsed: float = 0.0
var _boss_label: Label


func _ready() -> void:
	_boss_label = Label.new()
	_boss_label.name = "BossLabel"
	_boss_label.text = "BOSS"
	_boss_label.visible = false
	_boss_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_boss_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_boss_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_boss_label.add_theme_color_override("font_color", boss_text_color)
	add_child(_boss_label)
	_boss_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


func configure(duration: float, marks: PackedFloat32Array, p_event_marks: PackedFloat32Array = PackedFloat32Array()) -> void:
	_duration = duration
	_marks = marks
	_event_marks = p_event_marks
	_elapsed = 0.0
	queue_redraw()


func event_marks() -> PackedFloat32Array:
	return _event_marks


func set_elapsed(elapsed: float) -> void:
	_elapsed = elapsed
	queue_redraw()


func show_boss() -> void:
	_boss_label.visible = true
	queue_redraw()


func is_showing_boss() -> bool:
	return _boss_label != null and _boss_label.visible


func mark_x(mark: float) -> float:
	if _duration <= 0.0:
		return 0.0
	return size.x * clampf(mark / _duration, 0.0, 1.0)


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), track_color)
	var ratio := 1.0 if _duration <= 0.0 else clampf(_elapsed / _duration, 0.0, 1.0)
	draw_rect(Rect2(Vector2.ZERO, Vector2(size.x * ratio, size.y)), fill_color)
	for mark in _marks:
		var x := mark_x(mark)
		var color := passed_mark_color if mark <= _elapsed else mark_color
		draw_colored_polygon(PackedVector2Array([
			Vector2(x, 0.0),
			Vector2(x + MARK_HALF_WIDTH, size.y * 0.5),
			Vector2(x, size.y),
			Vector2(x - MARK_HALF_WIDTH, size.y * 0.5),
		]), color)
	for mark in _event_marks:
		var color := passed_mark_color if mark <= _elapsed else event_mark_color
		draw_circle(Vector2(mark_x(mark), size.y * 0.5), MARK_HALF_WIDTH, color)
```

`src/ui/hud/event_banner.gd`:
```gdscript
class_name EventBanner
extends VBoxContainer
## The running event's name, progress and remaining time; after it ends, "<name>: Success +rewards" or
## "<name>: Failed" for `result_seconds`. A view: it reads the event and never changes it.

@export var result_seconds: float = 2.5
@export var success_color: Color = Color(0.55, 1.0, 0.5)
@export var failure_color: Color = Color(1.0, 0.45, 0.4)

var _event: StageEvent
var _tracking: bool = false
var _result_left: float = 0.0
var _title := Label.new()
var _progress := Label.new()
var _timer := Label.new()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	for label in [_title, _progress, _timer]:
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(label)
	visible = false


func show_event(def: EventDef, event: StageEvent) -> void:
	_event = event
	_tracking = true
	_result_left = 0.0
	_title.text = def.display_name
	_title.modulate = Color.WHITE
	_progress.text = ""
	_timer.text = ""
	visible = true
	step(0.0)


func show_progress(text: String) -> void:
	_progress.text = text


func show_result(def: EventDef, success: bool) -> void:
	_event = null
	_tracking = false
	if success:
		_title.text = "%s: Success %s" % [def.display_name, reward_text(def.reward)]
		_title.modulate = success_color
	else:
		_title.text = "%s: Failed" % def.display_name
		_title.modulate = failure_color
	_progress.text = ""
	_timer.text = ""
	_result_left = result_seconds
	visible = true


static func reward_text(reward: RewardDef) -> String:
	if reward == null:
		return ""
	var parts := PackedStringArray()
	if reward.gold > 0:
		parts.append("+%d gold" % reward.gold)
	if reward.stone > 0:
		parts.append("+%d stone" % reward.stone)
	if reward.tickets > 0:
		parts.append("+%d %s" % [reward.tickets, "ticket" if reward.tickets == 1 else "tickets"])
	return " ".join(parts)


func title_text() -> String:
	return _title.text


func progress_text() -> String:
	return _progress.text


func timer_text() -> String:
	return _timer.text


func step(delta: float) -> void:
	if _tracking:
		if is_instance_valid(_event):
			_timer.text = "%ds" % ceili(_event.remaining_time())
		return
	if _result_left > 0.0:
		_result_left -= delta
		if _result_left <= 0.0:
			visible = false


func _process(delta: float) -> void:
	step(delta)
```

`src/ui/hud/objective_arrows.gd`:
```gdscript
class_name ObjectiveArrows
extends Control
## Arrows at the screen edge pointing at the running event's objectives that are off-screen.

@export var margin: float = 28.0
@export var arrow_size: float = 12.0
@export var arrow_color: Color = Color(1.0, 0.85, 0.3, 0.9)

var _event: StageEvent
var _positions := PackedVector2Array()
var _directions := PackedVector2Array()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func track(event: StageEvent) -> void:
	_event = event


func clear() -> void:
	_event = null
	_positions.clear()
	_directions.clear()
	queue_redraw()


func arrow_positions() -> PackedVector2Array:
	return _positions


## `canvas_transform` maps world positions to the screen (the camera); `view_size` is the screen size.
func refresh(canvas_transform: Transform2D, view_size: Vector2) -> void:
	_positions.clear()
	_directions.clear()
	if is_instance_valid(_event):
		var view := Rect2(Vector2.ZERO, view_size)
		for objective in _event.objectives():
			var screen := canvas_transform * objective.global_position
			if view.has_point(screen):
				continue
			_positions.append(edge_point(screen, view, margin))
			_directions.append((screen - view.get_center()).normalized())
	queue_redraw()


## Where the ray from the view centre to `target` leaves the view shrunk by `edge_margin`.
static func edge_point(target: Vector2, view: Rect2, edge_margin: float) -> Vector2:
	var inner := view.grow(-edge_margin)
	var center := inner.get_center()
	var offset := target - center
	var limit := 1.0
	if absf(offset.x) > 0.0:
		limit = minf(limit, inner.size.x * 0.5 / absf(offset.x))
	if absf(offset.y) > 0.0:
		limit = minf(limit, inner.size.y * 0.5 / absf(offset.y))
	return center + offset * limit


func _process(_delta: float) -> void:
	refresh(get_viewport().get_canvas_transform(), get_viewport_rect().size)


func _draw() -> void:
	for i in _positions.size():
		var tip := _positions[i]
		var direction := _directions[i]
		var side := direction.orthogonal() * arrow_size * 0.6
		var back := tip - direction * arrow_size
		draw_colored_polygon(PackedVector2Array([tip, back + side, back - side]), arrow_color)
```

`src/ui/hud/boss_bar.gd`:
```gdscript
class_name BossBar
extends VBoxContainer
## The boss's name and health bar, shown once the boss appears. A view over its Health.

@export var fill_color: Color = Color(0.85, 0.2, 0.2)
@export var bar_size: Vector2 = Vector2(400, 14)

var _name_label := Label.new()
var _bar := ProgressBar.new()
var _health: Health


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_bar.custom_minimum_size = bar_size
	_bar.show_percentage = false
	_bar.self_modulate = fill_color
	add_child(_name_label)
	add_child(_bar)
	visible = false


func show_boss(health: Health, display_name: String) -> void:
	if is_instance_valid(_health) and _health.changed.is_connected(_on_health_changed):
		_health.changed.disconnect(_on_health_changed)
	_health = health
	_name_label.text = display_name
	health.changed.connect(_on_health_changed)
	_on_health_changed(health.current(), health.maximum())
	visible = true


func boss_name() -> String:
	return _name_label.text


func value() -> float:
	return _bar.value


func maximum() -> float:
	return _bar.max_value


func _on_health_changed(current: float, maximum_value: float) -> void:
	_bar.max_value = maxf(maximum_value, 1.0)
	_bar.value = current
```

- [ ] **Step 4: Host them in the HUD and bind them in Main**

`src/ui/hud/hud.tscn` — change `load_steps=4` to `load_steps=7`, add after the `3_bar` ext resource:
```
[ext_resource type="Script" path="res://src/ui/hud/event_banner.gd" id="4_banner"]
[ext_resource type="Script" path="res://src/ui/hud/boss_bar.gd" id="5_boss_bar"]
[ext_resource type="Script" path="res://src/ui/hud/objective_arrows.gd" id="6_arrows"]
```
add directly after the `Root` node (first child of `Root`, so the arrows draw beneath the rest):
```
[node name="ObjectiveArrows" type="Control" parent="Root"]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2
script = ExtResource("6_arrows")
```
and append at the end:
```
[node name="BossBar" type="VBoxContainer" parent="Root"]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 5
anchor_left = 0.5
anchor_right = 0.5
offset_left = -200.0
offset_top = 48.0
offset_right = 200.0
offset_bottom = 88.0
grow_horizontal = 2
mouse_filter = 2
script = ExtResource("5_boss_bar")

[node name="EventBanner" type="VBoxContainer" parent="Root"]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 1
anchor_left = 1.0
anchor_right = 1.0
offset_left = -296.0
offset_top = 16.0
offset_right = -16.0
offset_bottom = 96.0
grow_horizontal = 0
mouse_filter = 2
script = ExtResource("4_banner")
```

`src/ui/hud/hud.gd` (whole file):
```gdscript
class_name Hud
extends CanvasLayer
## Read-only view of the stage. It binds to models through signals and never mutates them.
## Main binds it (the stage layer must not know the HUD) and forwards the event and boss signals.

## Bound by Main; empty until then.
@onready var ability_bar: AbilityBar = %AbilityBar
@onready var _player_bar: ProgressBar = %PlayerBar
@onready var _building_bar: ProgressBar = %BuildingBar
@onready var _wallet_label: Label = %WalletLabel
@onready var _timeline: TimelineBar = %Timeline
@onready var _result_label: Label = %ResultLabel
@onready var _event_banner: EventBanner = %EventBanner
@onready var _objective_arrows: ObjectiveArrows = %ObjectiveArrows
@onready var _boss_bar: BossBar = %BossBar


func bind(player_health: Health, building_health: Health, wallet: Wallet, clock: StageClock, timeline: Timeline, event_times: PackedFloat32Array = PackedFloat32Array()) -> void:
	player_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_player_bar, current, maximum))
	building_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_building_bar, current, maximum))
	_set_bar(_player_bar, player_health.current(), player_health.maximum())
	_set_bar(_building_bar, building_health.current(), building_health.maximum())
	wallet.changed.connect(_on_wallet_changed)
	_on_wallet_changed(wallet.gold, wallet.stone, wallet.tickets)
	_timeline.configure(timeline.duration, timeline.marks, event_times)
	clock.ticked.connect(_timeline.set_elapsed)


## Stage.events.event_started.
func show_event(def: EventDef, event: StageEvent) -> void:
	_event_banner.show_event(def, event)
	_objective_arrows.track(event)


## Stage.events.event_progressed.
func show_event_progress(_def: EventDef, text: String) -> void:
	_event_banner.show_progress(text)


## Stage.events.event_ended.
func show_event_result(def: EventDef, success: bool) -> void:
	_event_banner.show_result(def, success)
	_objective_arrows.clear()


## Stage.boss_appeared.
func show_boss(boss_health: Health, display_name: String) -> void:
	_boss_bar.show_boss(boss_health, display_name)
	_timeline.show_boss()


func show_result(result: RunResult) -> void:
	var title := "STAGE CLEARED" if result.outcome == RunResult.Outcome.CLEARED else "GAME OVER"
	_result_label.text = "%s\nKills %d   Gold %d   Stone %d\nPress R to restart" % [title, result.kills, result.gold, result.stone]
	_result_label.visible = true


func _set_bar(bar: ProgressBar, current: float, maximum: float) -> void:
	bar.max_value = maxf(maximum, 1.0)
	bar.value = current


func _on_wallet_changed(gold: int, stone: int, tickets: int) -> void:
	_wallet_label.text = "Gold %d   Stone %d   Ticket %d" % [gold, stone, tickets]
```

`src/app/main.gd` — in `_start_run`, replace the `_hud.bind(...)` line with:
```gdscript
	_hud.bind(_stage.player.health, _stage.building.health, _stage.run_state.wallet, _stage.clock, _stage.waves.timeline, _stage.events.event_times())
	_stage.events.event_started.connect(_hud.show_event)
	_stage.events.event_progressed.connect(_hud.show_event_progress)
	_stage.events.event_ended.connect(_hud.show_event_result)
	_stage.boss_appeared.connect(_hud.show_boss)
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/ui TEST_SUITE_TIMEOUT=240 tools/run_tests.sh`
Expected: timeline 4, event banner 6, objective arrows 4, boss bar 1, hud 7, ability bar 8 PASS. Likely snags:
- The banner timer shows `"25s"` before any step because `show_event` calls `step(0.0)`; `test_a_freed_event_does_not_break_the_banner` relies on that.

- [ ] **Step 6: Run the whole suite and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS.
```bash
git add src/ui src/app tests/ui
git commit -q -m "feat(ui): show event marks, an event banner, objective arrows and a boss bar; Main binds them

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: Ship the stage_01 events and boss, in-editor acceptance, documentation sync

**Files:**
- Create: `data/events/statues.tres`, `data/events/meteors.tres`, `data/events/elites.tres`, `data/events/stage_01_events.tres`, `data/bosses/orc_warlord.tres`
- Modify: `data/stages/stage_01.tres` (+ `events`, `boss`), `tests/data/test_game_data.gd`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`, `docs/superpowers/specs/2026-10-13-phase3-events-boss-design.md` (an "As built" section)

**Interfaces:**
- Consumes: everything above — `EventDef`/`RewardDef`/`EventSchedule` (Task 3), `BossDef`/`BossPatternDef` (Task 4), the controller scenes (Tasks 7–9), the pattern scenes (Tasks 12–13), `boss.tscn` (Task 10), `EventDirector.validate_defs` (Task 6), `BossBrain.validate_def` (Task 11); godot-ai `project_run`, `logs_read`, `editor_screenshot` / game screenshot, `project_manage` stop.
- Produces: `stage_01.tres` with the three events (count 3, window 20–155 s per the controller ruling, `min_gap` 30, `wave_clearance` 5) and the Orc Warlord (spec §3 numbers); docs that match the code.

- [ ] **Step 1: Write the failing data tests**

In `tests/data/test_game_data.gd` add to `DATA_FILES`:
```gdscript
	"res://data/events/statues.tres",
	"res://data/events/meteors.tres",
	"res://data/events/elites.tres",
	"res://data/events/stage_01_events.tres",
	"res://data/bosses/orc_warlord.tres",
```
and append:
```gdscript
func test_stage_01_schedules_three_events_and_the_orc_warlord() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	var events := stage.events
	assert_not_null(events)
	assert_eq(events.count, 3)
	assert_almost_eq(events.window_start, 20.0)
	assert_almost_eq(events.window_end, 155.0)
	assert_almost_eq(events.min_gap, 30.0)
	assert_almost_eq(events.wave_clearance, 5.0)
	var ids: Array = []
	for def in events.pool:
		ids.append(String(def.id))
	ids.sort()
	assert_eq(ids, ["elites", "meteors", "statues"])
	assert_no_errors(EventDirector.validate_defs(events), "event controllers")
	assert_no_errors(BossBrain.validate_def(stage.boss), "boss patterns")
	for run_seed in range(1, 11):
		assert_eq(events.roll_times(stage.duration, stage.waves.times(), RunRng.new(run_seed)).size(), 3, "seed %d" % run_seed)
	assert_eq(stage.boss.display_name, "Orc Warlord")


func test_event_limits_and_rewards_match_the_spec() -> void:
	var expected := {
		"statues": [25.0, 150, 2, 1],
		"meteors": [20.0, 100, 3, 1],
		"elites": [25.0, 200, 2, 1],
	}
	for id: String in expected:
		var def := load("res://data/events/%s.tres" % id) as EventDef
		var row: Array = expected[id]
		assert_no_errors(def.validate(), id)
		assert_eq(String(def.id), id)
		assert_almost_eq(def.time_limit, row[0], 0.001, id)
		assert_eq([def.reward.gold, def.reward.stone, def.reward.tickets], [row[1], row[2], row[3]], id)


func test_event_controllers_carry_the_spec_numbers() -> void:
	var statues := (load("res://src/events/statue_event.tscn") as PackedScene).instantiate() as StatueEvent
	assert_eq(statues.statue_count, 4)
	assert_almost_eq(statues.statue_stats.max_health, 60.0)
	assert_almost_eq(statues.min_distance, 250.0)
	statues.free()
	var meteors := (load("res://src/events/meteor_event.tscn") as PackedScene).instantiate() as MeteorEvent
	assert_eq([meteors.interval, meteors.warning, meteors.radius, meteors.damage, meteors.landing_spread], [1.2, 0.9, 60.0, 15.0, 120.0])
	assert_eq(meteors.max_hits, 3)
	meteors.free()
	var elites := (load("res://src/events/elite_event.tscn") as PackedScene).instantiate() as EliteEvent
	assert_eq(elites.count, 3)
	assert_eq(elites.elite_enemy.id, &"elite_orc")
	elites.free()


func test_orc_warlord_matches_the_spec() -> void:
	var boss := load("res://data/bosses/orc_warlord.tres") as BossDef
	assert_no_errors(boss.validate(), "orc_warlord")
	assert_almost_eq(boss.stats.max_health, 2500.0)
	assert_almost_eq(boss.stats.move_speed, 60.0)
	assert_almost_eq(boss.visual_scale, 3.0)
	assert_almost_eq(boss.contact_damage, 20.0)
	assert_almost_eq(boss.pattern_gap, 2.0)
	assert_eq(boss.order, BossDef.Order.RANDOM)
	assert_eq(boss.scene.resource_path, "res://src/actors/boss/boss.tscn")
	assert_true(boss.tint.v < 0.7, "dark tint")
	var tags: Array = []
	for pattern in boss.patterns:
		assert_almost_eq(pattern.weight, 1.0)
		var ability := pattern.ability_scene.instantiate() as BossPattern
		tags.append(String(ability.ability_tag))
		assert_true(ability.activation_owned_tags.has(GameplayTags.State_Casting), String(ability.ability_tag))
		ability.free()
	tags.sort()
	assert_eq(tags, ["Ability.Boss.Burst", "Ability.Boss.Charge", "Ability.Boss.Slam", "Ability.Boss.Summon"])


func test_boss_patterns_carry_the_spec_numbers() -> void:
	var charge := (load("res://src/boss/patterns/charge_pattern.tscn") as PackedScene).instantiate() as ChargePattern
	assert_eq([charge.warning, charge.speed, charge.distance, charge.damage], [0.7, 520.0, 360.0, 25.0])
	charge.free()
	var burst := (load("res://src/boss/patterns/radial_burst_pattern.tscn") as PackedScene).instantiate() as RadialBurstPattern
	assert_eq(burst.count, 12)
	assert_almost_eq(burst.damage, 10.0)
	assert_almost_eq(burst.projectile_def.speed, 220.0)
	assert_almost_eq(burst.projectile_def.lifetime, 2.0)
	assert_eq(burst.projectile_def.hits, ProjectileDef.Team.ALLIES)
	burst.free()
	var summon := (load("res://src/boss/patterns/summon_pattern.tscn") as PackedScene).instantiate() as SummonPattern
	assert_eq(summon.count, 4)
	assert_almost_eq(summon.radius, 80.0)
	assert_eq(summon.enemy.id, &"orc_swarmer")
	summon.free()
	var slam := (load("res://src/boss/patterns/ground_slam_pattern.tscn") as PackedScene).instantiate() as GroundSlamPattern
	assert_eq([slam.warning, slam.radius, slam.damage], [1.0, 90.0, 30.0])
	slam.free()
```
Run: `FILTER=tests/data TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → `test_every_data_file_loads`, `test_stage_01_schedules_three_events_and_the_orc_warlord`, `test_event_limits_and_rewards_match_the_spec` and `test_orc_warlord_matches_the_spec` FAIL (files missing); `test_event_controllers_carry_the_spec_numbers` and `test_boss_patterns_carry_the_spec_numbers` already PASS (they pin the scene numbers from Tasks 7–13).

- [ ] **Step 2: Write the data**

`data/events/statues.tres`:
```
[gd_resource type="Resource" script_class="EventDef" format=3]

[ext_resource type="Script" path="res://src/defs/event_def.gd" id="1_def"]
[ext_resource type="Script" path="res://src/defs/reward_def.gd" id="2_reward"]
[ext_resource type="PackedScene" path="res://src/events/statue_event.tscn" id="3_scene"]

[sub_resource type="Resource" id="Resource_reward"]
script = ExtResource("2_reward")
gold = 150
stone = 2
tickets = 1

[resource]
script = ExtResource("1_def")
id = &"statues"
display_name = "Destroy the Statues"
controller_scene = ExtResource("3_scene")
time_limit = 25.0
reward = SubResource("Resource_reward")
```

`data/events/meteors.tres`:
```
[gd_resource type="Resource" script_class="EventDef" format=3]

[ext_resource type="Script" path="res://src/defs/event_def.gd" id="1_def"]
[ext_resource type="Script" path="res://src/defs/reward_def.gd" id="2_reward"]
[ext_resource type="PackedScene" path="res://src/events/meteor_event.tscn" id="3_scene"]

[sub_resource type="Resource" id="Resource_reward"]
script = ExtResource("2_reward")
gold = 100
stone = 3
tickets = 1

[resource]
script = ExtResource("1_def")
id = &"meteors"
display_name = "Meteor Shower"
controller_scene = ExtResource("3_scene")
time_limit = 20.0
reward = SubResource("Resource_reward")
```

`data/events/elites.tres`:
```
[gd_resource type="Resource" script_class="EventDef" format=3]

[ext_resource type="Script" path="res://src/defs/event_def.gd" id="1_def"]
[ext_resource type="Script" path="res://src/defs/reward_def.gd" id="2_reward"]
[ext_resource type="PackedScene" path="res://src/events/elite_event.tscn" id="3_scene"]

[sub_resource type="Resource" id="Resource_reward"]
script = ExtResource("2_reward")
gold = 200
stone = 2
tickets = 1

[resource]
script = ExtResource("1_def")
id = &"elites"
display_name = "Elite Hunt"
controller_scene = ExtResource("3_scene")
time_limit = 25.0
reward = SubResource("Resource_reward")
```

`data/events/stage_01_events.tres`:
```
[gd_resource type="Resource" script_class="EventSchedule" format=3]

[ext_resource type="Script" path="res://src/defs/event_schedule.gd" id="1_schedule"]
[ext_resource type="Script" path="res://src/defs/event_def.gd" id="2_event"]
[ext_resource type="Resource" path="res://data/events/statues.tres" id="3_statues"]
[ext_resource type="Resource" path="res://data/events/meteors.tres" id="4_meteors"]
[ext_resource type="Resource" path="res://data/events/elites.tres" id="5_elites"]

[resource]
script = ExtResource("1_schedule")
count = 3
window_start = 20.0
window_end = 155.0
min_gap = 30.0
wave_clearance = 5.0
pool = Array[ExtResource("2_event")]([ExtResource("3_statues"), ExtResource("4_meteors"), ExtResource("5_elites")])
```

`data/bosses/orc_warlord.tres`:
```
[gd_resource type="Resource" script_class="BossDef" format=3]

[ext_resource type="Script" path="res://src/defs/boss_def.gd" id="1_def"]
[ext_resource type="Script" path="res://src/defs/boss_pattern_def.gd" id="2_pattern"]
[ext_resource type="Script" uid="uid://bpy31rqnwoa0w" path="res://src/defs/stat_block.gd" id="3_stats"]
[ext_resource type="PackedScene" path="res://src/actors/boss/boss.tscn" id="4_scene"]
[ext_resource type="Resource" uid="uid://y6rgouyb0s4i" path="res://data/enemies/orc_visual.tres" id="5_visual"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="6_damage"]
[ext_resource type="PackedScene" path="res://src/boss/patterns/charge_pattern.tscn" id="7_charge"]
[ext_resource type="PackedScene" path="res://src/boss/patterns/radial_burst_pattern.tscn" id="8_burst"]
[ext_resource type="PackedScene" path="res://src/boss/patterns/summon_pattern.tscn" id="9_summon"]
[ext_resource type="PackedScene" path="res://src/boss/patterns/ground_slam_pattern.tscn" id="10_slam"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("3_stats")
max_health = 2500.0
move_speed = 60.0

[sub_resource type="Resource" id="Resource_charge"]
script = ExtResource("2_pattern")
ability_scene = ExtResource("7_charge")
weight = 1.0

[sub_resource type="Resource" id="Resource_burst"]
script = ExtResource("2_pattern")
ability_scene = ExtResource("8_burst")
weight = 1.0

[sub_resource type="Resource" id="Resource_summon"]
script = ExtResource("2_pattern")
ability_scene = ExtResource("9_summon")
weight = 1.0

[sub_resource type="Resource" id="Resource_slam"]
script = ExtResource("2_pattern")
ability_scene = ExtResource("10_slam")
weight = 1.0

[resource]
script = ExtResource("1_def")
id = &"orc_warlord"
display_name = "Orc Warlord"
scene = ExtResource("4_scene")
visual = ExtResource("5_visual")
visual_scale = 3.0
tint = Color(0.45, 0.4, 0.55, 1)
stats = SubResource("Resource_stats")
contact_effect = ExtResource("6_damage")
contact_damage = 20.0
contact_interval = 1.0
body_radius = 30.0
patterns = Array[ExtResource("2_pattern")]([SubResource("Resource_charge"), SubResource("Resource_burst"), SubResource("Resource_summon"), SubResource("Resource_slam")])
order = 1
pattern_gap = 2.0
```

`data/stages/stage_01.tres` — keep the header and uids; add after the `9_grades` ext resource:
```
[ext_resource type="Resource" path="res://data/events/stage_01_events.tres" id="10_events"]
[ext_resource type="Resource" path="res://data/bosses/orc_warlord.tres" id="11_boss"]
```
and at the end of `[resource]` (after `grade_table = ExtResource("9_grades")`):
```
events = ExtResource("10_events")
boss = ExtResource("11_boss")
```

godot-ai `filesystem_manage` `{"op": "scan"}`, then run `FILTER=tests/data TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS, then `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS. If `test_stage_01_validates` fails, the printed error names the broken file — fix the `.tres`, not the code.

- [ ] **Step 3: Automated smoke run in the editor**

1. godot-ai `project_run` (main scene); wait ~5 s; `logs_read` → **no** errors or warnings from our scripts (a `Stage.start:` line means a data file is wrong).
2. Screenshot: the timeline shows five wave diamonds and three green event circles between 20 s and 144 s, none within 5 s of a diamond; the wallet reads `Gold 200   Stone 0   Ticket 0`.
3. Wait until the timeline fill passes the first circle (≤ 144 s, usually much sooner), screenshot: the event banner (top right) shows the event name, its progress line and a countdown; for statues/elites, yellow arrows at the screen edge point toward off-screen objectives.
4. Keep the run going to 180 s (the player may need to stay alive: it is unattended, so a GAME OVER before 180 s is acceptable here — note it and leave the boss check to Step 4). If it reaches 180 s: screenshot shows "BOSS" on the timeline and the "Orc Warlord" bar under it; `logs_read` clean.
5. Stop the run (`project_manage` stop).

- [ ] **Step 4: Manual acceptance checklist** (owner plays ~4 minutes; every line is pass/fail — record failures and fix them inside this task before committing)

- The three event circles sit on the timeline from the start; their positions change between runs (restart with R) and never overlap a wave diamond.
- Statues: four grey pillars appear far from the house and the soldier; arrows auto-target them; the banner counts `Statues k/4`; destroying all four in 25 s shows `Destroy the Statues: Success +150 gold +2 stone +1 ticket` and the wallet's `Ticket` goes up by one; letting the time run out removes the remaining statues and shows `Failed`.
- Meteors: red circles appear near the soldier every ~1.2 s and fill up; a dash out of the circle avoids the hit; `Hits k/3` counts only real hits; the 4th hit fails the event at once; surviving 20 s succeeds.
- Elites: three large red orcs arrive around the house and chase the soldier; arrows point at them when off-screen; killing all three in 25 s succeeds; on failure they stay and keep fighting; their kills pay Bounty gold if Bounty is held.
- With a ticket in the wallet every slot label reads `1T` (except `MAX`), any click spends the ticket instead of gold/stone; with no ticket the labels return to gold/stone prices.
- At 180 s the timeline shows "BOSS", the boss bar appears, and a huge dark orc arrives from the ring around the house; normal orcs keep trickling in; a running event (if any) ends as `Failed`.
- The boss chases the soldier; every ~2 s it uses one of: a red line then a charge along it (25 damage once), a ring of 12 bolts (10 each, they also hit the house), 4 small orcs around itself, a red circle under the soldier that slams after 1 s (30 damage). It never repeats the same pattern twice in a row; it stands still while winding up.
- Killing the boss shows STAGE CLEARED (kills do not include the boss); dying or losing the house during the fight shows GAME OVER; R restarts with fresh event times.
- `logs_read` shows no script errors after the session; the frame rate stays smooth during the boss fight with the trickle running.

- [ ] **Step 5: Sync the documentation**

Edit `CLAUDE.md`:
1. In **Architecture rules**, replace the sentence "It does not know the HUD: `Main` (`src/app/main.gd`) instantiates Stage and Hud and calls `Hud.bind(player_health, building_health, wallet, clock, timeline)`." with:
   "It does not know the HUD: `Main` (`src/app/main.gd`) instantiates Stage and Hud, calls `Hud.bind(player_health, building_health, wallet, clock, timeline, event_times)` and connects `Stage.events` (`event_started`/`event_progressed`/`event_ended`) and `Stage.boss_appeared` to `Hud.show_event`/`show_event_progress`/`show_event_result`/`show_boss`. `Stage` may reference `abilities/` and `boss/` (it attaches `BossBrain`, runs `BossBrain.validate_def`); no other `stage/` file may, and `stage/` never imports `events/` or `boss/` scripts — it instantiates the scenes named by defs."
2. Append to **Architecture rules**:
```markdown
- Where GAS belongs: an *ability* is an action an actor with an ASC performs, triggered by its controller (input,
  AI brain, gameplay event) and constrained by tags/cooldowns; an *effect* is a state change applied to an actor;
  stage progression (waves, events, when the boss appears) is plain stage code. So boss patterns are
  `BossPattern` (`ActorAbility`) scenes owning `State.Casting`, activated by tag from a `BossBrain`; events are
  `StageEvent` controllers that place GAS actors/effects (statues, `AreaStrike`s) and watch them.
- Events: `EventDirector` rolls `StageDef.events` (an `EventSchedule`) once from `RunRng` on a 1-second grid and runs
  one `StageEvent` at a time (a later one waits); success credits the `RewardDef` (gold, stone, promotion tickets)
  in `Stage`. `StageClock` emits `ended` once and keeps ticking (overtime): with a `StageDef.boss` the
  `BossDirector` switches to BOSS, aborts a running event and spawns the boss; its death clears the stage.
- A held promotion ticket pays any applicable shop action (`GradeService` prices it as `Price(TICKET, 1)` first).
```
3. Add after **Adding an enemy**:
```markdown
## Adding an event (scene + data)
1. Write a controller: a scene whose root script extends `StageEvent` (`src/events/`), numbers as exported values
   in the scene, `validate()` on the root, objects it places as its children, `objectives()` for HUD arrows,
   `report_progress(text)`, `_succeed()`/`_fail()`; override `_on_time_up()` if surviving is the goal.
2. Add an `EventDef` in `data/events/` (id, name, `controller_scene`, `time_limit`, `RewardDef`) and put it in the
   stage's `EventSchedule.pool` (`data/events/stage_01_events.tres`); `min_gap` must cover the longest time limit.
   Add the file to `DATA_FILES` in `tests/data/test_game_data.gd`.

## Adding a boss pattern
1. Write a scene whose root extends `BossPattern` (`src/boss/patterns/`): a unique `Ability.Boss.<Word>` tag
   (add it to `TAGS` in `tools/sync_tags.gd`), `activation_owned_tags = [State.Casting]`, numbers as exported
   values, `_begin()`/`_advance()`/`_complete()`/`_abort()`, damage through `AreaStrike`/`Projectile` (boss ASC as
   source).
2. Add a `BossPatternDef` (scene + weight) to the boss's `patterns` in `data/bosses/`.
```

Edit `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`:
1. §2 "Do not use GAS for": change "Wave/spawn scheduling, stage events, boss sequencing, enemy movement/AI." to "Wave/spawn scheduling, stage events (controllers; the things they place are GAS actors/effects), boss sequencing (`BossBrain` is plain code; its patterns are abilities), enemy movement/AI."
2. §3.1 folder tree: `defs/` `[P3]` list → `event_def, reward_def, event_schedule, boss_def, boss_pattern_def`; `components/` gains `telegraph, area_strike`; `stage/` `[P3]` list → `stage_event, event_context, event_director, boss_director` (drop `fx_spawner`: not built); `events/` → `statue_event, meteor_event, elite_event (extend stage/stage_event)`; `boss/` → `boss_brain, patterns/ (boss_pattern, charge, radial_burst, summon, ground_slam)`; `ui/hud/` gains `event_banner, objective_arrows, boss_bar`; `data/` gains `events/`, `bosses/`.
3. §3.2: add rows — `core/Wallet` (now `changed(gold, stone, tickets)`, `Kind.TICKET`); `defs/EventDef`/`RewardDef`/`EventSchedule` (`roll_times`, `roll_defs`, `validate_against`); `defs/BossDef`/`BossPatternDef`; `components/Telegraph` (`show_circle/show_line`, `elapsed`); `components/AreaStrike` (`strike(..., target_mask, only_actor)`, `struck`); `actors/statue/Statue`, `actors/boss/Boss` (`setup(def, target, targets, projectile_layer, summon)`, `patterns()`, `is_casting()`); `stage/StageEvent`, `stage/EventContext`, `stage/EventDirector` (`event_started/progressed/ended`, `event_times()`, `abort_running()`, `validate_defs`), `stage/BossDirector` (`appear()`, `boss_appeared`, `boss_defeated`); `boss/BossBrain` (`attach_to`, `validate_def`), `boss/patterns/BossPattern`; `ui/hud/EventBanner`, `ObjectiveArrows`, `BossBar`. Extend `stage/StageClock` with "keeps ticking after `ended` (overtime)", `stage/Stage` with `events`, `boss_appeared`, `flow_state()`, `current_boss()`, `components/ActorAbility / AbilityContext` with `summon`, `defs/ProjectileDef` with `hits`.
4. §3.3: in "behaviors referenced from data" replace `BossPatternDef.script` with `BossPatternDef.ability_scene`; append the decision "**Where GAS belongs:** an ability is an action an actor with an ASC performs (triggered by input, an AI brain or a gameplay event; constrained by tags/cooldowns); an effect is a state change applied to an actor; stage progression is plain stage code. Boss patterns are abilities driven by `BossBrain`; events are controllers placing GAS actors/effects."
5. §3.4: change "`stage/Stage` (composition root) may reference `abilities/`; no other `stage/` file may." to "`stage/Stage` (composition root) may reference `abilities/` and `boss/`; no other `stage/` file may."
6. §4 Phase 3: replace the paragraph with "**Phase 3 — Events + boss (built):** `EventDef`, `RewardDef`, `EventSchedule` (seeded 1-second-grid times with gap/clearance feasibility), `BossDef`, `BossPatternDef`; `StageEvent` + `EventContext` + `EventDirector` (one event at a time, rewards on success), `BossDirector`; `StageClock` overtime; `Telegraph`, `AreaStrike` (Blade Sweep's `MeleeStrike` is one); events statues, meteors, elites; the `Boss` actor, `BossBrain` and four `BossPattern` abilities (charge, radial burst, summon, ground slam); promotion tickets in the wallet and shop; HUD event marks, banner, objective arrows, boss bar. Content: three events (stage_01: 3 per run, 20–155 s) and the Orc Warlord."
7. Add "## 9. Phase 3 notes" after §8: acceptance evidence from Steps 3–4 (one or two lines, honest about what was verified automatically and what by the owner), and known follow-ups: "`fx_spawner` (hit/death FX) is still not built"; "boss bolts also hit the building (ALLY team)"; "elites stay in the fight when their event fails or is aborted"; "with validated data no event can still run at 180 s (window_end + longest limit <= duration); the boss's abort and the overlap queue remain safety nets only"; "statues draw auto-aim away from nearby enemies"; "`drop_multiplier` 10000 makes event gold/stone irrelevant while prototyping; tickets still matter"; the Phase 2 pre-heal follow-ups stay open (no heal source yet).

Append to `docs/superpowers/specs/2026-10-13-phase3-events-boss-design.md` a "## 14. As built (2026-10-13)" section listing the decisions from this plan's File Structure note (Boss.setup signature, BossBrain attached by Stage on `boss_spawned`, the StageEvent-root check in `EventDirector.validate_defs`, waiting instead of dropping a crossed event, elites staying on abort, `only_actor` filtering, the `1T` label, first meteor after one interval), the two controller rulings — `EventSchedule.validate_against` rejects `window_end + longest time_limit > duration`, and stage_01's window is 20–155 s instead of §3's 20–160 s (allowed seconds end at 144; 144 + 25 = 169 < 180) — and that the overlap queue is a safety net that never triggers with validated data, plus `Stage.flow_state()`/`current_boss()` and `Hud.show_event*`/`show_boss` as the HUD entry points.

- [ ] **Step 6: Verify and commit**

Run: `TEST_SUITE_TIMEOUT=240 tools/run_tests.sh` → all PASS, then:
```bash
git add data tests CLAUDE.md docs
git commit -q -m "feat(data): ship stage_01 events and the Orc Warlord; docs: sync CLAUDE.md and specs with Phase 3

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (Phase 3 spec §2–§13):**
- §2 owner decisions — tickets banked and spent first for buy/promote/reveal/reroll (Task 1; credited by events in Task 14); event times rolled from the run seed and shown on the timeline (Tasks 3, 6, 15); time limits, only success pays, statues/meteors/elites rules (Tasks 6–9, 14); the four boss patterns (Tasks 12–13); the boss chases the player, spawning continues, no time limit in the boss fight (Tasks 2, 10, 14); three events and one boss as content (Task 16); the ability/effect/stage-code rule (Global Constraints, Tasks 10–13 design, Task 16 docs).
- §3 numbers — controller scenes (Tasks 7–9), pattern scenes (Tasks 12–13), `elite_orc.tres` (Task 9), `boss_bolt.tres` (Task 13), event defs / schedule / boss / stage (Task 16), each pinned by a data or scene test.
- Controller rulings — window rule in `validate_against` with its pass/fail test (Task 3), window 20–155 s in data and tests (Tasks 3, 16), both recorded in the "As built" notes (Task 16).
- §4 flow and time — BOSS/CLEARED/FAILED transitions and no-boss backward compatibility (Task 14), overtime clock (Task 2), abort on boss appearance (Tasks 6, 14), `EventSchedule` rolling, greedy-feasible picks, validation rules and non-repeating defs (Task 3), event marks + "BOSS" (Task 15).
- §5 events — `StageEvent` contract incl. exactly-once ending, `_on_time_up`, `_cleanup` on every end (Task 6); `EventContext`, `EventDirector` API (Task 6); rewards (Task 14); `EventDef`/`RewardDef` (Task 3); statues incl. the `Statue` actor (Task 7), meteors with the environment ASC (Task 8), elites through the factory (Task 9); `Telegraph` and `AreaStrike` with `MeleeStrike` rebuilt on it (Task 5).
- §6 boss — actor, chase/casting, death cleanup, not a kill (Tasks 10, 14); `BossBrain` SEQUENCE/RANDOM/gap/activation by tag (Task 11); patterns (Tasks 12–13); `ProjectileDef.hits` (Task 13); `BossDef`/`BossPatternDef` (Task 4); the ActorAbility check at `Stage.start` (Tasks 11, 14); `BossDirector` (Task 14); `AbilityContext.summon` (Task 10).
- §7 tickets — wallet signal, ticket-first pricing, labels/dimming, `Ticket Z` (Task 1).
- §8 UI — timeline marks and BOSS, `EventBanner`, `ObjectiveArrows`, `BossBar`, `Main` wiring (Task 15).
- §9 modules/layers/tags — every listed file has a task; tags added in Tasks 10, 12, 13.
- §10 errors — infeasible schedule, non-StageEvent controller, non-ActorAbility pattern, empty patterns, non-positive weights/limits, bad rewards, invalid elite/boss stats all fail `Stage.start` (Tasks 3, 4, 6, 9, 11, 14).
- §11 testing — every item listed there has a test (pure logic in Tasks 1, 3, 11; events by `step()` in Tasks 6–9; patterns in Tasks 12–13; integration in Task 14; UI in Tasks 1, 15; editor + owner acceptance in Task 16). §12 out of scope stays out (no phases, heals, sound, gems, art). §13 risks are recorded in the Phase 3 notes (Task 16); the overtime risk is pinned (Review Focus 4).

**Review Focus mapping:** 1 → Task 6 `test_events_run_one_at_a_time`, `test_abort_running_ends_without_reward_and_drops_waiting_events`; Task 14 `test_boss_appearance_aborts_a_running_event_without_reward`, `test_the_run_ending_aborts_a_running_event`. 2 → Task 6 `test_success_ends_exactly_once`, `test_abort_cleans_up_without_a_result`; Task 8 `test_a_hit_after_the_event_ended_changes_nothing`; Task 9 `test_time_up_fails_and_the_survivors_stay`. 3 → Task 1 `test_a_ticket_pays_for_a_purchase_before_any_gold`, `test_one_ticket_pays_any_shop_action_even_without_currency`, `test_tickets_are_not_spent_on_refused_actions`, `test_a_held_ticket_prices_every_applicable_slot_at_one_ticket`. 4 → Task 2 `test_ended_fires_once_and_the_clock_keeps_ticking_into_overtime`, `test_waves_never_fire_again_in_overtime`, `test_trickle_continues_in_overtime_at_the_end_interval`; Task 6 `test_no_event_starts_in_overtime`; Task 14 `test_time_up_brings_the_boss_and_its_death_clears_the_stage`, `test_without_a_boss_time_up_clears_at_once`. 5 → Task 10 `test_death_aborts_patterns_leaves_the_group_and_frees_itself`, `test_abort_calls_the_abort_hook_and_strips_the_casting_tag`; Task 11 `test_stop_halts_the_brain`; Task 12 `test_abort_during_the_warning_removes_the_telegraph`.

**Type consistency checked across tasks:** `Wallet.Kind.TICKET` / `tickets` / `changed(gold, stone, tickets)` (1 → 14, 15); `GradeService.promote_cost/reroll_cost` (1); `StageClock.has_ended` (2); `RewardDef`, `EventDef(id, display_name, controller_scene, time_limit, reward)`, `EventSchedule.roll_times/roll_defs/earliest_times/validate_against` (3 → 6, 14, 16); `DefFactory.packed/reward/event(scene, time_limit, id)/schedule(pool, count, start, end, gap, clearance)/boss(scene, pattern_scenes)` (3, 4 → 6–16); `BossDef.Order`, `BossPatternDef.ability_scene/weight` (4 → 10, 11, 16); `Telegraph.show_circle/show_line/shape/radius/line_offset` and `AreaStrike.strike(asc, effect, damage, radius, seconds, target_mask, only_actor)` + `struck` (5 → 8, 12); `StageEvent.begin/abort/step/objectives/remaining_time/is_running/has_ended/report_progress/_succeed/_fail/_on_time_up/_cleanup`, `EventContext(layer, factory, targets, player, building, rng, bounds, spawn_radius, def)`, `EventDirector.bind/event_times/running/abort_running/validate_defs` + three signals (6 → 7–9, 14, 15); `ActorFixture.kill/event_context(test, def, player, building_at)/boss(test, player, def, position, summon)` (6, 10 → 7–15); `Statue.setup/remove/is_removed/destroyed` (7); `MeteorEvent.hits` (8); `Boss.setup(def, target, targets, projectile_layer, summon)/patterns/pattern_weights/is_casting/died/health/asc/def`, `AbilityContext.summon`, `BossPattern._begin/_advance/_complete/_abort/step/is_running/completed`, `ProbePattern.hold/refuse/release/starts/aborts/stepped` (10 → 11–14); `BossBrain.bind/attach_to/validate_def/step/stop/casting/pattern_started` (11 → 14, 16); `ChargePattern.charge_seconds`, `RadialBurstPattern`, `SummonPattern`, `GroundSlamPattern` exports (12, 13 → 16); `ProjectileDef.Team/hits/hit_mask` (13 → 16); `BossDirector.bind(11 args)/appear/has_boss/boss` + signals, `Stage.events/boss_appeared/flow_state/current_boss` (14 → 15); `TimelineBar.configure(duration, marks, p_event_marks)/event_marks/show_boss/is_showing_boss`, `EventBanner.show_event/show_progress/show_result/reward_text/title_text/progress_text/timer_text/step`, `ObjectiveArrows.track/clear/refresh/arrow_positions/edge_point`, `BossBar.show_boss/boss_name/value/maximum`, `Hud.bind(..., event_times)/show_event/show_event_progress/show_event_result/show_boss` (15).

**GAS behaviours the code relies on (verified in `addons/GodotGAS`):** `GameplayAbility._ready` self-grants under an ASC parent; `try_activate` adds `activation_owned_tags`, awaits `_activate_ability()`, then calls `end_ability(not success)` only while still active; `end_ability` strips the owned tags and emits `ability_ended(was_cancelled)`; `abort_ability` = `end_ability(true)` while active (Tasks 10–12). `try_activate_abilities_by_tag` returns true when `can_activate_ability` passed, even if `_activate_ability` then returned false (Task 11 handles both). Effects whose `application_query` rejects the target (`State.Invulnerable` on `damage_basic`) make `apply_effect_spec_to_target` return null, so `EffectApplier.hit` returns false and `AreaStrike` does not report `struck` (Tasks 5, 8). `ASC.cleanup()` aborts active abilities and runs on `NOTIFICATION_PREDELETE` too (Tasks 7, 10). Signal emission resumes an awaiting coroutine synchronously, which `BossPattern` relies on for instant patterns and aborts.

**Known risks the executor should expect:** hand-written `.tscn`/`.tres` text may need small fixes on first load (each task lists the exact nodes and ids); float sums in step-driven tests deliberately overshoot thresholds (Task 8 notes it); physics-overlap tests wait two physics frames as the Phase 2 strike tests do; the boss charge test relies on real-time `Mover.dash` (0.69 s of 0.9 s waited); the RANDOM-order test asserts only the weight ordering and no repeats, never exact counts, so the seed does not matter.
