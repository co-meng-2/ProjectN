# Phase 2 — Ability System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** During a stage the player buys, promotes and rerolls abilities through six HUD slots, paying with the gold and stone that enemies drop; seven data-defined abilities (four stat passives, two attacks, one on-kill hybrid) scale with their grade through GodotGAS.

**Architecture:** A new `src/abilities/` module (layer 6, beside `stage/`) holds the pure shop logic — `Loadout`/`AbilitySlot` (state), `AbilityPool` (which defs a grade can roll), `GradeService` (promote/reroll rules over `Wallet` + `RunRng`) — and `AbilityInstaller`, the one place that mirrors slot changes onto the player's ASC (INFINITE passive effects held by handle, attack abilities granted as `ActorAbility` nodes through `Player.grant_ability`). `Stage` composes it and exposes `loadout`/`grade_service`; `Main` binds the HUD's `AbilityBar` and routes its request signals to `grade_service`, so `ui/` only emits requests and `stage/` never sees `ui/`. Abilities are `.tres` `AbilityDef`s whose numbers are `Curve`s sampled at the grade level (COMMON 1 … REVEALED 6).

**Tech Stack:** Godot 4.7.2 (typed GDScript), GodotGAS v1.1.2 (read-only plugin symlinked into `addons/`), the in-repo headless test runner (`tests/run_tests.tscn`, `tools/run_tests.sh`), godot-ai MCP bridge for the open editor.

**Spec:** `docs/superpowers/specs/2026-10-13-phase2-ability-system-design.md` (the Phase 2 design; its §2/§3 numbers and §7 rules are binding) and the parent spec `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md` (§3.4 dependency rules, §2 GAS usage). Read both first.

## Global Constraints

- Godot **4.7.2**; typed GDScript everywhere (`var x: T`, `-> T`); **tabs** for indentation.
- **Never edit `addons/GodotGAS` or `addons/godot_ai`** (symlinks to `D:\CM\Godot\plugins\`). Files in `godot_gas/` are only changed by `tools/sync_tags.gd`.
- Layer rule (a file may reference only layers below it): `0 addons/godot_gas` → `1 core` → `2 defs` → `3 gas` → `4 components` → `5 actors` → `6 stage / abilities` → `7 ui` → `8 app`. Upward communication = signals. `defs/` may reference GAS resources and `core/`, nothing above. No `get_node("/root/...")`. No autoloads of ours.
- Components never look up their neighbours: actors inject dependencies through `bind(...)`. Anything an installer or ability may bind/connect twice guards with `is_connected` / an early return.
- The ASC is a **direct child named exactly `AbilitySystemComponent`** of its actor root (instance `src/gas/actor_asc.tscn`); stats are applied only after the node is in the tree; `asc.cleanup()` is called deferred before freeing an actor (as `player.gd`/`enemy.gd` do).
- Physics objects (areas, bodies, projectiles, pickups) are added from physics callbacks with `call_deferred`; adding them from `_physics_process` itself (as `ArrowAbility` does) is fine.
- All content is `.tres` in `data/`, typed by `src/defs/`; **no balance numbers in code**. Ability numbers are `Curve`s in the ability's `.tres` (passives) or in the ability scene's exported curves (attacks/hybrids). Every def has `validate() -> PackedStringArray` and `Stage.start` runs it; `validate()` must never crash on bad data (null checks, bounds, divisors) and must report duplicates.
- Gameplay tags only through `GameplayTags.X` constants. Phase 2 adds `Ability.Volley`, `Ability.BladeSweep`, `Ability.Bounty`, `Event.Kill` to the Phase 1 set (`Ability.Arrow`, `Ability.Dash`, `Cooldown.Arrow`, `Cooldown.Dash`, `Damage.Amount`, `State.Invulnerable`).
- Owner-approved shop numbers (data, never code): none→common **100 gold**, common→rare **150**, rare→epic **250**, epic→unique **400**, unique→legendary **600**, legendary→revealed **10 stone**, reroll **2 stone** (not allowed on NONE or REVEALED); run starts with **200 gold / 0 stone**; **6 slots**; `stage_01.tres` `drop_multiplier = 10000` (set back to 1 for balanced play).
- Damage always flows through `EffectBuilder.damage` → GAS `GameplayEffect` (`Hitbox` + `EffectApplier`); Blade Sweep adds **no new damage path**.
- Do not shadow `Object`/`Node`/`Control` virtuals or built-ins with your own members (no `_set`, `_get`, `get_index`-style names, no `index`/`show`/`update`/`size` on Nodes; `size()` on a RefCounted is fine).
- Tests that drive time call an exposed `step(delta)` after `set_physics_process(false)`; tests must be able to fail (non-default multipliers, ordering-sensitive cases, state not already true before the action).
- The UI never mutates models: `AbilityBar`/`AbilitySlotView` emit `promote_requested`/`reroll_requested`; `Main` routes them.
- Assets are copied from `D:\CM\Godot\res` into `res://assets/art/icons/` with a row in `assets/SOURCES.md`; never reference the external path from the project.
- Commit after every task, with the generated `*.gd.uid` and `*.png.import` files of new scripts/assets. Commit messages end with: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## How tests run

```bash
timeout 120 tools/run_tests.sh                               # whole suite (≈163 Phase 1 tests + the new ones)
FILTER=tests/abilities/test_grade_service tools/run_tests.sh  # one file (substring of its path)
```
The Godot editor is **open** during this work (MCP bridge `godot-ai`). After creating a new `class_name` script, scene or asset, call godot-ai `filesystem_manage` with `{"op": "scan"}` and wait for it to finish **before** running the headless suite; otherwise the runner fails with `Could not find type ...`/`Parse Error` or a missing texture import. Never run `IMPORT=1` / `--import` while the editor is open. Output lines are `PASS  file::test` / `FAIL  file::test` + reasons, then `---- N passed, M failed ----`; any `SCRIPT ERROR`/`Parse Error` fails the run. Project-level file edits (`project.godot`, `godot_gas/`) are done by the named tool, never by the editor UI.

Rebuilding tags: `/d/CM/Godot/Godot_v4.7.2-stable_win64.exe --headless --path . -s res://tools/sync_tags.gd` (regenerates `godot_gas/tag_registry.tres` and `godot_gas/gameplay_tags.gd`), then a `filesystem_manage` scan.

## Review Focus

Failure modes the spec implies but a happy-path test would miss; each has a pinning test in the owning task:

1. **Refused actions change nothing** — insufficient currency or a non-applicable action (empty slot reroll, promoting REVEALED, out-of-range slot) must leave wallet, slot and signals untouched (Task 4 `test_promote_without_enough_gold_changes_nothing`, `test_reroll_is_refused_on_empty_and_revealed_slots_and_without_stone`, `test_out_of_range_slot_is_not_applicable`).
2. **Empty and single-ability pools** — promoting into a grade nothing can roll refuses with `EMPTY_POOL` and spends nothing; rerolling when the pool holds only the current ability returns the same ability, still charges, and re-emits `slot_changed` so the installer reinstalls a fresh instance (Task 3 `test_roll_with_only_the_excluded_def_returns_it`, `test_empty_pool_rolls_null`; Task 4 `test_promote_into_an_empty_pool_spends_nothing`, `test_reroll_on_a_single_ability_pool_keeps_the_ability_but_still_charges`).
3. **No leftovers on the ASC** — after dozens of promotes/rerolls and a full clear, the player holds exactly the installed effects and abilities; changing a slot whose ability is mid-activation strips its owned tags and frees it (Task 5 `test_revoke_mid_activation_strips_its_owned_tags`; Task 6 `test_many_changes_leave_no_leftovers`, `test_changing_a_slot_mid_activation_strips_its_tags`, `test_rebinding_does_not_double_install`).
4. **Legendary → revealed is deterministic and terminal** — same def, never rolled, costs stone, and REVEALED cannot be promoted or rerolled (Task 4 `test_reveal_keeps_the_same_ability_and_costs_stone`; Task 12 `test_buying_promoting_and_revealing_through_the_service_changes_the_player`).
5. **Click vs Ctrl+click and view purity** — a plain click promotes, Ctrl+click rerolls, right-click/release do nothing, the bar never touches models, and clicks after the run ended are ignored (Task 11 `test_click_emits_promote_and_ctrl_click_emits_reroll`, `test_the_bar_only_emits_requests`; Task 12 `Main._on_promote_requested` guard, exercised in the Task 13 acceptance run).
6. **Bad data fails at `Stage.start`, never mid-run** — a passive modifier with `magnitude = 0` (the curve output is multiplied by it, so every grade would be 0), a missing curve, a scene whose root is not a `GameplayAbility`, duplicate ability ids, a short grade table (Task 1 `test_passive_must_be_infinite_with_curved_nonzero_modifiers`, `test_scene_root_must_be_a_gameplay_ability_and_may_reject_itself`, `test_validate_reports_wrong_size_and_negative_costs_without_crashing`; Task 2 `test_profile_rejects_zero_slots_null_and_duplicate_abilities`).

---

## File Structure

```
src/core/grade.gd                      Grade.Value ladder, level(), display_name(), color()
src/core/price.gd                      Price(kind, amount) + is_affordable(wallet)
src/defs/ability_def.gd                AbilityDef: id, names, icons, min_grade, passive_effect, ability_scene, validate(), icon_for(grade)
src/defs/grade_table.gd                GradeTable: promote_gold[5], reveal_stone, reroll_stone, promote_price(), reroll_price(), validate()
src/defs/stage_def.gd        (modify)  + start_gold, start_stone, drop_multiplier, grade_table
src/defs/player_profile.gd   (modify)  + slot_count, abilities (duplicate-id validation)
src/components/ability_context.gd (modify)  + wallet
src/components/target_provider.gd (modify)  + nearest_enemies(origin, range, count)
src/actors/player/player.gd  (modify)  public asc, ability_context, grant_ability(), revoke_ability(); setup(..., wallet)
src/stage/run_state.gd       (modify)  wallet seeded with start_gold/start_stone
src/stage/loot_director.gd   (modify)  drop_multiplier applied when a pickup is spawned
src/stage/stage.gd/.tscn     (modify)  builds Loadout/AbilityPool/GradeService, AbilityInstaller node, sends Event.Kill, exposes loadout + grade_service
src/abilities/ability_slot.gd          AbilitySlot (def, grade)
src/abilities/loadout.gd               Loadout: slot_changed, slot(), set_slot(), size()
src/abilities/ability_pool.gd          AbilityPool: defs_for(grade), roll(grade, rng, exclude)
src/abilities/grade_result.gd          GradeResult.Outcome {OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL}
src/abilities/grade_service.gd         GradeService: promote(), reroll(), promote_cost(), reroll_cost(), signal resolved
src/abilities/ability_installer.gd     AbilityInstaller (Node): bind(loadout, player); handles per slot
src/abilities/periodic_ability.gd      PeriodicAbility (abstract ActorAbility): cooldown curve, step(delta), start_cooldown(), validate()
src/abilities/volley/volley_ability.gd/.tscn        Volley: N arrows at N nearest enemies
src/abilities/blade_sweep/melee_strike.gd/.tscn    MeleeStrike: Hitbox + EffectApplier + Lifetime radial hit
src/abilities/blade_sweep/blade_sweep_ability.gd/.tscn   Blade Sweep: periodic MeleeStrike around the player
src/abilities/bounty/bounty_ability.gd/.tscn       Bounty: trigger_event_tag Event.Kill → wallet gold
src/ui/hud/ability_slot_view.gd/.tscn  one slot: icon, grade frame, cost label, click/Ctrl+click → request signals
src/ui/hud/ability_bar.gd/.tscn        the row of slots; bind(loadout, wallet, service)
src/ui/hud/hud.gd/.tscn      (modify)  hosts the AbilityBar (%AbilityBar), exposes `ability_bar`
src/app/main.gd              (modify)  binds the bar, routes requests to stage.grade_service
tools/sync_tags.gd           (modify)  + Ability.Volley, Ability.BladeSweep, Ability.Bounty, Event.Kill
data/grade_table.tres                  the owner's prices
data/abilities/{prospector,swift_boots,vitality,might,volley,blade_sweep,bounty}.tres
data/stages/stage_01.tres    (modify)  start_gold 200, start_stone 0, drop_multiplier 10000, grade_table
data/characters/default_profile.tres (modify)  slot_count 6, the 7 abilities
assets/art/icons/*.png                 copied 16x16 food icons + 128x128 rpg icons (CC0), rows in assets/SOURCES.md
tests/support/def_factory.gd (modify)  curve(), passive_effect(), ability(), vitality(), grade_table(); stage() gets a grade_table
tests/support/probe_ability.gd/.tscn   ProbeAbility test double (counts activations, optional hold, validate() rejection)
tests/core/test_grade.gd  tests/defs/test_ability_def.gd  tests/defs/test_grade_table.gd  tests/defs/test_player_profile.gd
tests/abilities/test_loadout.gd  test_ability_pool.gd  test_grade_service.gd  test_ability_installer.gd
tests/abilities/test_volley_ability.gd  test_melee_strike.gd  test_blade_sweep_ability.gd  test_bounty_ability.gd
tests/data/test_ability_data.gd        every ability file loads, validates, scales per level
tests/ui/test_ability_bar.gd           views, costs, clicks, dimming, flashing
tests/stage/test_stage.gd    (modify)  Event.Kill + shop integration
```

Deviations from the spec worth knowing (Task 13 records them in the spec): `GradeService.Result` is its own class `GradeResult` (GDScript inner classes cannot reference the outer enum cleanly); costs are returned as `Price` objects (kind + amount) because a promotion may cost gold or stone; attack cooldowns are curves inside `PeriodicAbility` (its own timer, exposed as `step(delta)` for deterministic tests) instead of level-blind GAS cooldown effects; `Player.asc` becomes public (like `Building.asc`) so the installer can hold effect handles; the `AbilityBar` lives inside `hud.tscn` and is bound by `Main` through `Hud.ability_bar`.

---

### Task 1: Grade, Price, AbilityDef, GradeTable

**Files:**
- Create: `src/core/grade.gd`, `src/core/price.gd`, `src/defs/ability_def.gd`, `src/defs/grade_table.gd`
- Create: `tests/support/probe_ability.gd`, `tests/support/probe_ability.tscn`
- Modify: `tests/support/def_factory.gd` (add `curve`, `passive_effect`, `ability`, `vitality`, `grade_table`)
- Test: `tests/core/test_grade.gd`, `tests/defs/test_ability_def.gd`, `tests/defs/test_grade_table.gd`

**Interfaces:**
- Consumes: `Wallet.Kind` (`src/core/wallet.gd`), `ActorAbility` (`src/components/actor_ability.gd`, abstract `GameplayAbility` with `bind_context`), GAS `GameplayEffect` (`policy`, `modifiers`), `GameplayEffectModifier` (`attribute_name`, `operation`, `magnitude`, `scaling_curve`; `calculate_magnitude(level) = scaling_curve.sample(level) * magnitude`), `GameplayAbility` (`task_wait_delay`).
- Produces:
  - `Grade` (RefCounted, static): `enum Value { NONE, COMMON, RARE, EPIC, UNIQUE, LEGENDARY, REVEALED }`; `next(grade) -> Value` (REVEALED stays), `is_rollable(grade) -> bool` (false for NONE/REVEALED), `level(grade) -> float` (`float(grade)`: COMMON 1.0 … REVEALED 6.0), `display_name(grade) -> String`, `color(grade) -> Color`.
  - `Price` (RefCounted): `_init(kind: Wallet.Kind, amount: int)`; `kind`, `amount`; `is_affordable(wallet: Wallet) -> bool`.
  - `AbilityDef` (Resource): `id: StringName`, `display_name: String`, `description: String`, `icon: Texture2D`, `revealed_icon: Texture2D`, `min_grade: Grade.Value = COMMON`, `passive_effect: GameplayEffect`, `ability_scene: PackedScene`; `icon_for(grade) -> Texture2D`; `validate() -> PackedStringArray`. A scene root may implement `validate() -> PackedStringArray`; `AbilityDef.validate()` instantiates the scene (not in the tree), calls it, and frees the node.
  - `GradeTable` (Resource): `promote_gold: PackedInt32Array` (5 entries indexed by the grade being left, NONE..UNIQUE), `reveal_stone: int`, `reroll_stone: int`; `promote_price(grade) -> Price` (null for REVEALED or a short table), `reroll_price(grade) -> Price` (null for NONE/REVEALED); `validate()`.
  - `ProbeAbility` (test double, `tests/support/probe_ability.tscn`, root script extends `ActorAbility`): `@export hold_seconds := 0.0`, `@export reject_reason := ""`, `activations: int`; `validate()` returns `[reject_reason]` when set; `_activate_ability` counts and optionally awaits `task_wait_delay(hold_seconds)`. Scene sets `ability_tag = &"Ability.Probe"` (deliberately unregistered: the ASC compares StringNames at runtime, the tag registry is only an editor aid).
  - `DefFactory.curve(values: PackedFloat32Array) -> Curve` (point i at x = i+1, domain 1..6), `DefFactory.passive_effect(attribute, operation, per_level) -> GameplayEffect` (INFINITE, one modifier, `magnitude = 1.0`), `DefFactory.ability(id, passive = null, scene = null, min_grade = COMMON) -> AbilityDef` (with a 16x16 icon), `DefFactory.vitality(per_level = [20,40,70,110,160,240]) -> AbilityDef` (max_health ADD), `DefFactory.grade_table() -> GradeTable` (100/150/250/400/600, 10, 2).

- [ ] **Step 1: Write the failing tests**

`tests/core/test_grade.gd`:
```gdscript
extends TestCase


func test_next_climbs_the_ladder_and_stops_at_revealed() -> void:
	assert_eq(Grade.next(Grade.Value.NONE), Grade.Value.COMMON)
	assert_eq(Grade.next(Grade.Value.UNIQUE), Grade.Value.LEGENDARY)
	assert_eq(Grade.next(Grade.Value.LEGENDARY), Grade.Value.REVEALED)
	assert_eq(Grade.next(Grade.Value.REVEALED), Grade.Value.REVEALED)


func test_level_maps_common_to_one_and_revealed_to_six() -> void:
	assert_almost_eq(Grade.level(Grade.Value.NONE), 0.0)
	assert_almost_eq(Grade.level(Grade.Value.COMMON), 1.0)
	assert_almost_eq(Grade.level(Grade.Value.EPIC), 3.0)
	assert_almost_eq(Grade.level(Grade.Value.REVEALED), 6.0)


func test_is_rollable_excludes_none_and_revealed() -> void:
	assert_false(Grade.is_rollable(Grade.Value.NONE))
	assert_true(Grade.is_rollable(Grade.Value.COMMON))
	assert_true(Grade.is_rollable(Grade.Value.LEGENDARY))
	assert_false(Grade.is_rollable(Grade.Value.REVEALED))


func test_every_grade_has_a_name_and_a_distinct_color() -> void:
	var colors: Array = []
	for grade in Grade.Value.values():
		assert_false(Grade.display_name(grade).is_empty(), "name for grade %d" % grade)
		assert_false(colors.has(Grade.color(grade)), "colour reused by %s" % Grade.display_name(grade))
		colors.append(Grade.color(grade))
	assert_eq(colors.size(), 7)
```

`tests/defs/test_grade_table.gd`:
```gdscript
extends TestCase


func test_factory_table_validates() -> void:
	assert_no_errors(DefFactory.grade_table().validate(), "table")


func test_promote_price_follows_the_ladder() -> void:
	var table := DefFactory.grade_table()
	var first := table.promote_price(Grade.Value.NONE)
	assert_eq(first.kind, Wallet.Kind.GOLD)
	assert_eq(first.amount, 100)
	assert_eq(table.promote_price(Grade.Value.COMMON).amount, 150)
	assert_eq(table.promote_price(Grade.Value.UNIQUE).amount, 600)
	var reveal := table.promote_price(Grade.Value.LEGENDARY)
	assert_eq(reveal.kind, Wallet.Kind.STONE)
	assert_eq(reveal.amount, 10)
	assert_null(table.promote_price(Grade.Value.REVEALED), "revealed is the top")


func test_reroll_price_is_stone_except_on_empty_and_revealed() -> void:
	var table := DefFactory.grade_table()
	assert_null(table.reroll_price(Grade.Value.NONE))
	assert_null(table.reroll_price(Grade.Value.REVEALED))
	var price := table.reroll_price(Grade.Value.EPIC)
	assert_eq(price.kind, Wallet.Kind.STONE)
	assert_eq(price.amount, 2)


func test_validate_reports_wrong_size_and_negative_costs_without_crashing() -> void:
	var table := GradeTable.new()
	assert_eq(table.validate().size(), 1, "empty promote_gold")
	assert_null(table.promote_price(Grade.Value.RARE), "short table must not crash")
	table.promote_gold = PackedInt32Array([100, 150, -1, 400, 600])
	table.reveal_stone = -5
	assert_eq(table.validate().size(), 2)


func test_price_is_affordable_compares_the_right_currency() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 100)
	assert_true(Price.new(Wallet.Kind.GOLD, 100).is_affordable(wallet))
	assert_false(Price.new(Wallet.Kind.GOLD, 101).is_affordable(wallet))
	assert_false(Price.new(Wallet.Kind.STONE, 1).is_affordable(wallet))
```

`tests/defs/test_ability_def.gd`:
```gdscript
extends TestCase

const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")


func _packed(node: Node) -> PackedScene:
	var scene := PackedScene.new()
	scene.pack(node)
	node.free()
	return scene


func test_valid_passive_scene_and_hybrid_defs_report_no_errors() -> void:
	assert_no_errors(DefFactory.vitality().validate(), "passive")
	assert_no_errors(DefFactory.ability(&"probe", null, PROBE_SCENE).validate(), "scene")
	assert_no_errors(DefFactory.ability(&"hybrid", DefFactory.vitality().passive_effect, PROBE_SCENE).validate(), "hybrid")


func test_def_needs_id_name_icon_and_some_content() -> void:
	var errors := AbilityDef.new().validate()
	assert_eq(errors.size(), 4, ", ".join(errors))


func test_min_grade_must_be_rollable() -> void:
	var def := DefFactory.vitality()
	def.min_grade = Grade.Value.NONE
	assert_eq(def.validate().size(), 1)
	def.min_grade = Grade.Value.REVEALED
	assert_eq(def.validate().size(), 1)


func test_passive_must_be_infinite_with_curved_nonzero_modifiers() -> void:
	var def := DefFactory.vitality()
	def.passive_effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(def.validate().size(), 1, "policy")
	def = DefFactory.vitality()
	var modifier: GameplayEffectModifier = def.passive_effect.modifiers[0]
	modifier.magnitude = 0.0
	assert_eq(def.validate().size(), 1, "zero magnitude would zero every grade")
	modifier.magnitude = 1.0
	modifier.scaling_curve = null
	assert_eq(def.validate().size(), 1, "missing curve")
	modifier.scaling_curve = DefFactory.curve(PackedFloat32Array([1.0, 2.0]))
	modifier.attribute_name = ""
	assert_eq(def.validate().size(), 1, "missing attribute")
	modifier.attribute_name = "max_health"
	def.passive_effect.modifiers.append(null)
	assert_eq(def.validate().size(), 1, "null modifier")


func test_scene_root_must_be_a_gameplay_ability_and_may_reject_itself() -> void:
	var def := DefFactory.ability(&"bad", null, _packed(Node2D.new()))
	assert_eq(def.validate().size(), 1, "root is not an ability")
	var probe := ProbeAbility.new()
	probe.reject_reason = "probe says no"
	def = DefFactory.ability(&"rejecting", null, _packed(probe))
	var errors := def.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))


func test_icon_for_uses_the_revealed_icon_only_when_revealed_and_present() -> void:
	var def := DefFactory.vitality()
	assert_eq(def.icon_for(Grade.Value.REVEALED), def.icon, "no revealed icon: fall back")
	def.revealed_icon = DefFactory.texture(16, 16)
	assert_eq(def.icon_for(Grade.Value.LEGENDARY), def.icon)
	assert_eq(def.icon_for(Grade.Value.REVEALED), def.revealed_icon)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/core/test_grade tools/run_tests.sh` and `FILTER=tests/defs/test_ability_def tools/run_tests.sh`
Expected: parse errors — `Grade`, `Price`, `AbilityDef`, `GradeTable`, `ProbeAbility` not found (the run fails with `!! script errors detected`).

- [ ] **Step 3: Implement core and defs**

`src/core/grade.gd`:
```gdscript
class_name Grade
extends RefCounted
## The quality ladder of an ability. REVEALED is the enhanced form of the same LEGENDARY ability,
## so only COMMON..LEGENDARY are rolled from pools. `level` is the GAS ability_level and the curve X.

enum Value { NONE, COMMON, RARE, EPIC, UNIQUE, LEGENDARY, REVEALED }

const DISPLAY_NAMES: PackedStringArray = ["Empty", "Common", "Rare", "Epic", "Unique", "Legendary", "Revealed"]
const COLORS: Array[Color] = [
	Color(0.35, 0.35, 0.38),  # NONE
	Color(0.80, 0.80, 0.80),  # COMMON
	Color(0.30, 0.60, 1.00),  # RARE
	Color(0.70, 0.35, 1.00),  # EPIC
	Color(1.00, 0.55, 0.20),  # UNIQUE
	Color(1.00, 0.85, 0.25),  # LEGENDARY
	Color(0.55, 1.00, 0.95),  # REVEALED
]


static func next(grade: Value) -> Value:
	if grade == Value.REVEALED:
		return Value.REVEALED
	return (grade + 1) as Value


static func is_rollable(grade: Value) -> bool:
	return grade != Value.NONE and grade != Value.REVEALED


static func level(grade: Value) -> float:
	return float(grade)


static func display_name(grade: Value) -> String:
	return DISPLAY_NAMES[grade]


static func color(grade: Value) -> Color:
	return COLORS[grade]
```

`src/core/price.gd`:
```gdscript
class_name Price
extends RefCounted
## What one shop action costs: an amount of a single currency.

var kind: Wallet.Kind
var amount: int


func _init(p_kind: Wallet.Kind, p_amount: int) -> void:
	kind = p_kind
	amount = p_amount


func is_affordable(wallet: Wallet) -> bool:
	return wallet.balance(kind) >= amount
```

`src/defs/ability_def.gd`:
```gdscript
class_name AbilityDef
extends Resource
## One ability the player can hold in a slot. A passive is an INFINITE GameplayEffect whose modifiers
## scale with the grade level through `scaling_curve`; an active ability is a scene whose root extends
## GameplayAbility (ActorAbility in this project). A hybrid has both.

@export var id: StringName
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var icon: Texture2D
## Optional icon of the revealed form; without it the view tints `icon`.
@export var revealed_icon: Texture2D
## Lowest grade whose pool offers this ability.
@export var min_grade: Grade.Value = Grade.Value.COMMON
@export var passive_effect: GameplayEffect
@export var ability_scene: PackedScene


func icon_for(grade: Grade.Value) -> Texture2D:
	if grade == Grade.Value.REVEALED and revealed_icon != null:
		return revealed_icon
	return icon


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("AbilityDef: id is empty")
	if display_name.is_empty():
		errors.append("AbilityDef '%s': display_name is empty" % id)
	if icon == null:
		errors.append("AbilityDef '%s': icon is null" % id)
	if not Grade.is_rollable(min_grade):
		errors.append("AbilityDef '%s': min_grade must be COMMON..LEGENDARY" % id)
	if passive_effect == null and ability_scene == null:
		errors.append("AbilityDef '%s': needs a passive_effect and/or an ability_scene" % id)
	if passive_effect != null:
		errors.append_array(_validate_passive())
	if ability_scene != null:
		errors.append_array(_validate_scene())
	return errors


func _validate_passive() -> PackedStringArray:
	var errors := PackedStringArray()
	if passive_effect.policy != GameplayEffect.DurationPolicy.INFINITE:
		errors.append("AbilityDef '%s': passive_effect must be INFINITE" % id)
	for modifier in passive_effect.modifiers:
		if modifier == null:
			errors.append("AbilityDef '%s': null modifier" % id)
		elif modifier.attribute_name == "":
			errors.append("AbilityDef '%s': modifier without attribute_name" % id)
		elif modifier.scaling_curve == null:
			errors.append("AbilityDef '%s': modifier '%s' needs a scaling_curve over the grade level" % [id, modifier.attribute_name])
		elif is_zero_approx(modifier.magnitude):
			# GAS multiplies the curve sample by `magnitude`; 0 would silently zero every grade.
			errors.append("AbilityDef '%s': modifier '%s' has magnitude 0 (use 1.0 with a curve)" % [id, modifier.attribute_name])
	return errors


func _validate_scene() -> PackedStringArray:
	var errors := PackedStringArray()
	var root := ability_scene.instantiate()
	if root == null:
		errors.append("AbilityDef '%s': ability_scene cannot be instantiated" % id)
		return errors
	if not (root is GameplayAbility):
		errors.append("AbilityDef '%s': ability_scene root must extend GameplayAbility" % id)
	elif root.has_method("validate"):
		errors.append_array(root.call("validate") as PackedStringArray)
	root.free()
	return errors
```

`src/defs/grade_table.gd`:
```gdscript
class_name GradeTable
extends Resource
## Prices of the ability shop. `promote_gold` is indexed by the grade being left (NONE..UNIQUE);
## LEGENDARY -> REVEALED costs stone, and every reroll costs stone.

const PROMOTE_STEPS: int = 5

@export var promote_gold: PackedInt32Array = PackedInt32Array()
@export var reveal_stone: int = 0
@export var reroll_stone: int = 0


## null when the grade cannot be promoted (REVEALED) or the table is too short.
func promote_price(grade: Grade.Value) -> Price:
	if grade == Grade.Value.REVEALED:
		return null
	if grade == Grade.Value.LEGENDARY:
		return Price.new(Wallet.Kind.STONE, reveal_stone)
	if grade >= promote_gold.size():
		return null
	return Price.new(Wallet.Kind.GOLD, promote_gold[grade])


## null when the grade cannot be rerolled (NONE, REVEALED).
func reroll_price(grade: Grade.Value) -> Price:
	if not Grade.is_rollable(grade):
		return null
	return Price.new(Wallet.Kind.STONE, reroll_stone)


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if promote_gold.size() != PROMOTE_STEPS:
		errors.append("GradeTable: promote_gold needs %d entries (NONE..UNIQUE), has %d" % [PROMOTE_STEPS, promote_gold.size()])
	for cost in promote_gold:
		if cost < 0:
			errors.append("GradeTable: negative promote cost %d" % cost)
			break
	if reveal_stone < 0 or reroll_stone < 0:
		errors.append("GradeTable: reveal_stone and reroll_stone must be >= 0")
	return errors
```

- [ ] **Step 4: Add the test double and the factory helpers**

`tests/support/probe_ability.gd`:
```gdscript
class_name ProbeAbility
extends ActorAbility
## Test double: counts activations, optionally stays active for `hold_seconds`, and reports
## `reject_reason` from validate() so def validation can be tested.

@export var hold_seconds: float = 0.0
@export var reject_reason: String = ""

var activations: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if reject_reason != "":
		errors.append(reject_reason)
	return errors


func _activate_ability() -> bool:
	activations += 1
	if hold_seconds > 0.0:
		await task_wait_delay(hold_seconds)
	return true
```

`tests/support/probe_ability.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://tests/support/probe_ability.gd" id="1_probe"]

[node name="ProbeAbility" type="Node"]
script = ExtResource("1_probe")
ability_name = "Probe"
ability_tag = &"Ability.Probe"
```

Append to `tests/support/def_factory.gd` (keep the existing functions):
```gdscript
## A curve with one point per grade level: values[0] at x = 1 ... values[n-1] at x = n.
static func curve(values: PackedFloat32Array) -> Curve:
	var result := Curve.new()
	var lowest := 0.0
	var highest := 0.0
	for value in values:
		lowest = minf(lowest, value)
		highest = maxf(highest, value)
	result.min_domain = 1.0
	result.max_domain = float(maxi(values.size(), 2))
	result.min_value = lowest
	result.max_value = maxf(highest, lowest + 1.0)
	for i in values.size():
		result.add_point(Vector2(float(i + 1), values[i]))
	return result


## An INFINITE effect with one curved modifier (magnitude 1.0: GAS multiplies the curve sample by it).
static func passive_effect(attribute: String, operation: GameplayEffectModifier.Operation, per_level: PackedFloat32Array) -> GameplayEffect:
	var modifier := GameplayEffectModifier.new()
	modifier.attribute_name = attribute
	modifier.operation = operation
	modifier.magnitude = 1.0
	modifier.scaling_curve = curve(per_level)
	var effect := GameplayEffect.new()
	effect.policy = GameplayEffect.DurationPolicy.INFINITE
	effect.modifiers.append(modifier)
	return effect


static func ability(id: StringName, passive: GameplayEffect = null, scene: PackedScene = null, min_grade: Grade.Value = Grade.Value.COMMON) -> AbilityDef:
	var def := AbilityDef.new()
	def.id = id
	def.display_name = String(id).capitalize()
	def.icon = texture(16, 16)
	def.min_grade = min_grade
	def.passive_effect = passive
	def.ability_scene = scene
	return def


## max_health +20/40/70/110/160/240 over the six grade levels.
static func vitality(per_level: PackedFloat32Array = PackedFloat32Array([20.0, 40.0, 70.0, 110.0, 160.0, 240.0])) -> AbilityDef:
	return ability(&"vitality", passive_effect("max_health", GameplayEffectModifier.Operation.ADD, per_level))


static func grade_table() -> GradeTable:
	var table := GradeTable.new()
	table.promote_gold = PackedInt32Array([100, 150, 250, 400, 600])
	table.reveal_stone = 10
	table.reroll_stone = 2
	return table
```

- [ ] **Step 5: Scan and run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}` (new `class_name` scripts), then:
Run: `FILTER=tests/core/test_grade tools/run_tests.sh`, `FILTER=tests/defs/test_grade_table tools/run_tests.sh`, `FILTER=tests/defs/test_ability_def tools/run_tests.sh`
Expected: 4 + 5 + 6 PASS. Likely snags:
- `(grade + 1) as Value` refused by the parser: write `return Value.values()[grade + 1]` instead (the enum values are 0..6 in order).
- `ProbeAbility.new()` complains that `ActorAbility` is abstract: it is only the base that is abstract; the subclass instantiates. If the class is not found, the scan has not run.
- `Curve.max_domain` unknown: the property exists in 4.7 (verified in the binary); make sure the project really runs `Godot_v4.7.2`.

- [ ] **Step 6: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS (the Phase 1 163 + 15 new).
```bash
git add src/core src/defs tests/core tests/defs tests/support
git commit -q -m "feat(defs): add Grade ladder, Price, AbilityDef and GradeTable with validation

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Stage and profile data — economy fields, validation, wallet seeding, drop multiplier

**Files:**
- Modify: `src/defs/stage_def.gd`, `src/defs/player_profile.gd`, `src/stage/run_state.gd`, `src/stage/loot_director.gd`, `src/stage/stage.gd` (one line: the loot bind), `tests/support/def_factory.gd` (`stage()` sets a grade table), `data/stages/stage_01.tres`, `tests/data/test_game_data.gd`
- Create: `data/grade_table.tres`
- Test: `tests/defs/test_stage_def.gd` (append), `tests/defs/test_player_profile.gd` (new), `tests/stage/test_run_state.gd` (append), `tests/stage/test_loot_director.gd` (append)

**Interfaces:**
- Consumes: Task 1 (`GradeTable`, `AbilityDef`, `DefFactory.grade_table()`, `DefFactory.vitality()`), `RunState._init(stage_def, profile, run_seed)`, `LootDirector.bind(factory, pickup_layer, player, wallet)`, `Stage.start` (`src/stage/stage.gd:31-57`).
- Produces:
  - `StageDef` gains `start_gold: int = 0`, `start_stone: int = 0`, `drop_multiplier: float = 1.0`, `grade_table: GradeTable` (required; validated).
  - `PlayerProfile` gains `slot_count: int = 6`, `abilities: Array[AbilityDef] = []` (nulls, duplicate ids and nested errors reported).
  - `RunState`: the wallet starts at `start_gold`/`start_stone`.
  - `LootDirector.bind(factory, pickup_layer, player, wallet, drop_multiplier: float = 1.0)`; each spawned pickup carries `maxi(1, roundi(amount * drop_multiplier))`.
  - `data/grade_table.tres` (owner prices) referenced by `stage_01.tres`, which now has `start_gold = 200`, `start_stone = 0`, `drop_multiplier = 1.0` (Task 13 raises it to 10000 together with the acceptance run).

- [ ] **Step 1: Write the failing tests**

Append to `tests/defs/test_stage_def.gd`:
```gdscript
func test_stage_requires_a_grade_table_and_a_sane_economy() -> void:
	var def := DefFactory.stage()
	def.grade_table = null
	assert_eq(def.validate().size(), 1, "grade_table required")
	def.grade_table = DefFactory.grade_table()
	def.start_gold = -1
	def.drop_multiplier = 0.0
	assert_eq(def.validate().size(), 2, "negative start gold, zero multiplier")


func test_stage_collects_grade_table_errors() -> void:
	var def := DefFactory.stage()
	def.grade_table.promote_gold = PackedInt32Array([1])
	assert_eq(def.validate().size(), 1)
```

`tests/defs/test_player_profile.gd`:
```gdscript
extends TestCase


func _profile() -> PlayerProfile:
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	return profile


func test_profile_defaults_to_six_empty_slots() -> void:
	var profile := _profile()
	assert_eq(profile.slot_count, 6)
	assert_true(profile.abilities.is_empty())
	assert_no_errors(profile.validate(), "default profile")


func test_profile_rejects_zero_slots_null_and_duplicate_abilities() -> void:
	var profile := _profile()
	profile.slot_count = 0
	assert_eq(profile.validate().size(), 1, "slot_count")
	profile.slot_count = 6
	profile.abilities.append(null)
	assert_eq(profile.validate().size(), 1, "null ability")
	profile.abilities.clear()
	profile.abilities.append(DefFactory.vitality())
	profile.abilities.append(DefFactory.vitality())
	var errors := profile.validate()
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("vitality"), "duplicate id is named")


func test_profile_collects_nested_ability_errors() -> void:
	var profile := _profile()
	var broken := DefFactory.vitality()
	broken.icon = null
	profile.abilities.append(broken)
	assert_eq(profile.validate().size(), 1)
```

Append to `tests/stage/test_run_state.gd`:
```gdscript
func test_wallet_is_seeded_from_the_stage_def() -> void:
	var def := DefFactory.stage()
	def.start_gold = 200
	def.start_stone = 3
	var state := RunState.new(def, PlayerProfile.new(), 7)
	assert_eq(state.wallet.gold, 200)
	assert_eq(state.wallet.stone, 3)
```

In `tests/stage/test_loot_director.gd` change the rig signature to `func _rig(drop_multiplier: float = 1.0) -> Dictionary:` and its bind line to `loot.bind(factory, pickup_layer, player, wallet, drop_multiplier)`; then append:
```gdscript
func test_drop_multiplier_scales_pickup_amounts() -> void:
	var rig := _rig(3.0)
	var factory: EnemyFactory = rig["factory"]
	var pickups: Node2D = rig["pickups"]
	var entry := DropEntry.new()
	entry.chance = 1.0
	entry.min_amount = 2
	entry.max_amount = 2
	entry.icon = DefFactory.texture(8, 8)
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.contact_effect = DAMAGE
	def.drops = DropTable.new()
	def.drops.entries.append(entry)
	var enemy := factory.spawn(def, Vector2(50, 50))
	var killer := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 9999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, enemy.get_node("AbilitySystemComponent"))
	await next_frame()
	await next_frame()
	assert_eq(pickups.get_child_count(), 1)
	assert_eq((pickups.get_child(0) as Pickup).amount, 6, "2 x 3.0")
```

Append to `tests/data/test_game_data.gd` (and add `"res://data/grade_table.tres"` to `DATA_FILES`):
```gdscript
func test_stage_01_starts_with_the_owner_economy() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_eq(stage.start_gold, 200)
	assert_eq(stage.start_stone, 0)
	assert_not_null(stage.grade_table)
	assert_eq(stage.grade_table.promote_price(Grade.Value.NONE).amount, 100)
	assert_eq(stage.grade_table.promote_price(Grade.Value.LEGENDARY).amount, 10)
	assert_eq(stage.grade_table.reroll_price(Grade.Value.COMMON).amount, 2)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/defs tools/run_tests.sh`
Expected: `test_stage_requires_a_grade_table...` FAIL (0 errors instead of 1), `test_player_profile` FAIL (no `slot_count`), `test_loot_director` parse error (5th argument), data test FAIL.

- [ ] **Step 3: Implement**

`src/defs/stage_def.gd` — add after `@export var waves: WaveSchedule`:
```gdscript
@export var start_gold: int = 0
@export var start_stone: int = 0
## Scales every enemy drop; 1.0 for balanced play, very large for prototype tuning.
@export var drop_multiplier: float = 1.0
@export var grade_table: GradeTable
```
and before `return errors` in `validate()`:
```gdscript
	if start_gold < 0 or start_stone < 0:
		errors.append("StageDef '%s': start_gold and start_stone must be >= 0" % id)
	if drop_multiplier <= 0.0:
		errors.append("StageDef '%s': drop_multiplier must be > 0" % id)
	if grade_table == null:
		errors.append("StageDef '%s': grade_table is null" % id)
	else:
		errors.append_array(grade_table.validate())
```

`src/defs/player_profile.gd` (whole file):
```gdscript
class_name PlayerProfile
extends Resource
## The player's starting stats, look and ability catalog for a run. Phase 4 derives this from the skill tree.

@export var stats: StatBlock
@export var visual: VisualDef
## Ability slots shown in the HUD.
@export var slot_count: int = 6
## The catalog the grade pools are built from.
@export var abilities: Array[AbilityDef] = []


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if stats == null:
		errors.append("PlayerProfile: stats is null")
	else:
		errors.append_array(stats.validate())
	if visual == null:
		errors.append("PlayerProfile: visual is null")
	else:
		errors.append_array(visual.validate())
	if slot_count < 1:
		errors.append("PlayerProfile: slot_count must be >= 1")
	var seen: Dictionary[StringName, bool] = {}
	for ability in abilities:
		if ability == null:
			errors.append("PlayerProfile: null ability")
			continue
		if seen.has(ability.id):
			errors.append("PlayerProfile: duplicate ability id '%s'" % ability.id)
		seen[ability.id] = true
		errors.append_array(ability.validate())
	return errors
```

`src/stage/run_state.gd` — in `_init`, after `rng = RunRng.new(run_seed)`:
```gdscript
	wallet.add(Wallet.Kind.GOLD, p_stage_def.start_gold)
	wallet.add(Wallet.Kind.STONE, p_stage_def.start_stone)
```

`src/stage/loot_director.gd` (whole file):
```gdscript
class_name LootDirector
extends Node
## Turns enemy drops into pickups and collected pickups into wallet credit.

const PICKUP_SCENE: PackedScene = preload("res://src/actors/pickup/pickup.tscn")

var _pickup_layer: Node2D
var _player: Player
var _wallet: Wallet
var _drop_multiplier: float = 1.0


func bind(factory: EnemyFactory, pickup_layer: Node2D, player: Player, wallet: Wallet, drop_multiplier: float = 1.0) -> void:
	_pickup_layer = pickup_layer
	_player = player
	_wallet = wallet
	_drop_multiplier = drop_multiplier
	factory.spawned.connect(_on_enemy_spawned)
	player.loot_collected.connect(_on_loot_collected)


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.loot_dropped.connect(_on_loot_dropped)


## Deferred: pickups are collision objects and must not be added while physics signals are flushing.
func _on_loot_dropped(roll: DropRoll, position: Vector2) -> void:
	_spawn_pickup.call_deferred(roll, position)


func _spawn_pickup(roll: DropRoll, position: Vector2) -> void:
	# The stage's drop multiplier is applied to the drop itself, so the pickup shows what it pays.
	roll.amount = maxi(1, roundi(roll.amount * _drop_multiplier))
	var pickup := PICKUP_SCENE.instantiate() as Pickup
	_pickup_layer.add_child(pickup)
	pickup.global_position = position
	pickup.setup(roll)


func _on_loot_collected(kind: Wallet.Kind, amount: int) -> void:
	_wallet.add(kind, roundi(amount * _player.gain_multiplier(kind)))
```

`src/stage/stage.gd` — replace the loot bind line with:
```gdscript
	_loot.bind(_factory, _pickup_layer, player, run_state.wallet, stage_def.drop_multiplier)
```

`tests/support/def_factory.gd` — in `stage()`, before `return def`:
```gdscript
	def.grade_table = grade_table()
```

`data/grade_table.tres`:
```
[gd_resource type="Resource" script_class="GradeTable" load_steps=2 format=3]

[ext_resource type="Script" path="res://src/defs/grade_table.gd" id="1_table"]

[resource]
script = ExtResource("1_table")
promote_gold = PackedInt32Array(100, 150, 250, 400, 600)
reveal_stone = 10
reroll_stone = 2
```

`data/stages/stage_01.tres` — change `load_steps=11` to `load_steps=12`, add after the waves ext_resource:
```
[ext_resource type="Resource" path="res://data/grade_table.tres" id="9_grades"]
```
and append to the `[resource]` block:
```
start_gold = 200
start_stone = 0
drop_multiplier = 1.0
grade_table = ExtResource("9_grades")
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/defs tools/run_tests.sh`, `FILTER=tests/stage/test_run_state tools/run_tests.sh`, `FILTER=tests/stage/test_loot_director tools/run_tests.sh`, `FILTER=tests/data tools/run_tests.sh`
Expected: all PASS. If `stage_01.tres` fails to load, open it in the editor (`scene_open`/`resource_manage` or the FileSystem dock) — a wrong `load_steps` count or ext id is the usual cause.

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS (`test_stage` still passes: `DefFactory.stage()` carries a grade table, and the default profile validates with `slot_count = 6`).
```bash
git add src/defs src/stage tests data
git commit -q -m "feat(data): add stage economy fields, profile slots/abilities, wallet seeding and drop multiplier

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: AbilitySlot, Loadout, AbilityPool

**Files:**
- Create: `src/abilities/ability_slot.gd`, `src/abilities/loadout.gd`, `src/abilities/ability_pool.gd`
- Test: `tests/abilities/test_loadout.gd`, `tests/abilities/test_ability_pool.gd`

**Interfaces:**
- Consumes: `Grade` (Task 1), `AbilityDef.min_grade` (Task 1), `RunRng.pick(items: Array) -> Variant` (`src/core/run_rng.gd:30`), `DefFactory.ability`.
- Produces:
  - `AbilitySlot` (RefCounted): `def: AbilityDef` (null = empty), `grade: Grade.Value = NONE`, `is_empty() -> bool`.
  - `Loadout` (RefCounted): `_init(slot_count: int)`; `signal slot_changed(index: int, slot: AbilitySlot)`; `size() -> int`; `slot(index) -> AbilitySlot` (null out of range); `set_slot(index, def, grade) -> bool` (false out of range; `def == null` forces grade NONE; always emits on success, even when nothing differs — the installer relies on it to reinstall a fresh instance).
  - `AbilityPool` (RefCounted): `_init(defs: Array[AbilityDef])` (nulls skipped); `defs_for(grade) -> Array[AbilityDef]` (every def with `min_grade <= grade`; empty for NONE/REVEALED); `roll(grade, rng, exclude: AbilityDef = null) -> AbilityDef` (null when the pool is empty; `exclude` is dropped only when another candidate exists).

- [ ] **Step 1: Write the failing tests**

`tests/abilities/test_loadout.gd`:
```gdscript
extends TestCase


func test_loadout_starts_with_empty_slots() -> void:
	var loadout := Loadout.new(4)
	assert_eq(loadout.size(), 4)
	for i in 4:
		assert_true(loadout.slot(i).is_empty(), "slot %d" % i)
		assert_eq(loadout.slot(i).grade, Grade.Value.NONE)
	assert_null(loadout.slot(4))
	assert_null(loadout.slot(-1))


func test_set_slot_writes_and_emits_once() -> void:
	var loadout := Loadout.new(2)
	var changes: Array = []
	loadout.slot_changed.connect(func(index: int, slot: AbilitySlot) -> void: changes.append([index, slot]))
	var def := DefFactory.ability(&"a")
	assert_true(loadout.set_slot(1, def, Grade.Value.RARE))
	assert_eq(changes.size(), 1)
	assert_eq(changes[0][0], 1)
	assert_eq((changes[0][1] as AbilitySlot).def, def)
	assert_eq(loadout.slot(1).grade, Grade.Value.RARE)
	assert_true(loadout.slot(0).is_empty(), "other slot untouched")


func test_rewriting_the_same_content_still_emits() -> void:
	var loadout := Loadout.new(1)
	var def := DefFactory.ability(&"a")
	loadout.set_slot(0, def, Grade.Value.RARE)
	var changes: Array = []
	loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	loadout.set_slot(0, def, Grade.Value.RARE)
	assert_eq(changes, [0], "a reroll onto the same ability must reinstall it")


func test_clearing_a_slot_forces_grade_none() -> void:
	var loadout := Loadout.new(1)
	loadout.set_slot(0, DefFactory.ability(&"a"), Grade.Value.EPIC)
	loadout.set_slot(0, null, Grade.Value.EPIC)
	assert_true(loadout.slot(0).is_empty())
	assert_eq(loadout.slot(0).grade, Grade.Value.NONE)


func test_out_of_range_set_is_refused_without_emitting() -> void:
	var loadout := Loadout.new(1)
	var changes: Array = []
	loadout.slot_changed.connect(func(_index: int, _slot: AbilitySlot) -> void: changes.append(true))
	assert_false(loadout.set_slot(1, DefFactory.ability(&"a"), Grade.Value.COMMON))
	assert_false(loadout.set_slot(-1, DefFactory.ability(&"a"), Grade.Value.COMMON))
	assert_true(changes.is_empty())
```

`tests/abilities/test_ability_pool.gd`:
```gdscript
extends TestCase


func _rig() -> Dictionary:
	var common := DefFactory.ability(&"common_one")
	var rare := DefFactory.ability(&"rare_one", null, null, Grade.Value.RARE)
	var epic := DefFactory.ability(&"epic_one", null, null, Grade.Value.EPIC)
	var defs: Array[AbilityDef] = [common, rare, epic]
	return {"pool": AbilityPool.new(defs), "common": common, "rare": rare, "epic": epic}


func test_defs_for_includes_everything_at_or_below_the_grade() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var at_common := pool.defs_for(Grade.Value.COMMON)
	assert_eq(at_common.size(), 1)
	assert_true(at_common.has(rig["common"]))
	var at_rare := pool.defs_for(Grade.Value.RARE)
	assert_eq(at_rare.size(), 2)
	assert_true(at_rare.has(rig["rare"]))
	assert_false(at_rare.has(rig["epic"]))
	assert_eq(pool.defs_for(Grade.Value.LEGENDARY).size(), 3)


func test_none_and_revealed_have_no_pool() -> void:
	var pool: AbilityPool = _rig()["pool"]
	assert_true(pool.defs_for(Grade.Value.NONE).is_empty())
	assert_true(pool.defs_for(Grade.Value.REVEALED).is_empty())
	assert_null(pool.roll(Grade.Value.NONE, RunRng.new(1)))
	assert_null(pool.roll(Grade.Value.REVEALED, RunRng.new(1)))


func test_roll_is_seeded_and_only_returns_pool_members() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var first: Array = []
	var second: Array = []
	var rng_a := RunRng.new(5)
	var rng_b := RunRng.new(5)
	for i in 20:
		first.append(pool.roll(Grade.Value.RARE, rng_a))
		second.append(pool.roll(Grade.Value.RARE, rng_b))
	assert_eq(first, second, "same seed, same sequence")
	for def in first:
		assert_true(def == rig["common"] or def == rig["rare"], "only RARE-pool members")
	assert_true(first.has(rig["common"]) and first.has(rig["rare"]), "both members appear over 20 rolls")


func test_roll_prefers_a_different_def_than_the_excluded_one() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	var rng := RunRng.new(8)
	for i in 20:
		assert_eq(pool.roll(Grade.Value.RARE, rng, rig["common"]), rig["rare"])


func test_roll_with_only_the_excluded_def_returns_it() -> void:
	var rig := _rig()
	var pool: AbilityPool = rig["pool"]
	assert_eq(pool.roll(Grade.Value.COMMON, RunRng.new(8), rig["common"]), rig["common"])


func test_empty_pool_rolls_null_and_nulls_are_skipped() -> void:
	var nothing: Array[AbilityDef] = []
	assert_null(AbilityPool.new(nothing).roll(Grade.Value.COMMON, RunRng.new(1)))
	var with_hole: Array[AbilityDef] = [null, DefFactory.ability(&"a")]
	var pool := AbilityPool.new(with_hole)
	assert_eq(pool.defs_for(Grade.Value.COMMON).size(), 1)
	assert_not_null(pool.roll(Grade.Value.COMMON, RunRng.new(1)))
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities tools/run_tests.sh`
Expected: parse errors — `Loadout`, `AbilitySlot`, `AbilityPool` not found.

- [ ] **Step 3: Implement**

`src/abilities/ability_slot.gd`:
```gdscript
class_name AbilitySlot
extends RefCounted
## One HUD slot's content: which ability (null = empty) at which grade.

var def: AbilityDef
var grade: Grade.Value = Grade.Value.NONE


func is_empty() -> bool:
	return def == null
```

`src/abilities/loadout.gd`:
```gdscript
class_name Loadout
extends RefCounted
## The player's N ability slots. Writing a slot announces it; the installer and the HUD react to the signal.

signal slot_changed(index: int, slot: AbilitySlot)

var _slots: Array[AbilitySlot] = []


func _init(slot_count: int) -> void:
	for i in maxi(slot_count, 0):
		_slots.append(AbilitySlot.new())


func size() -> int:
	return _slots.size()


## null when `index` is out of range.
func slot(index: int) -> AbilitySlot:
	if index < 0 or index >= _slots.size():
		return null
	return _slots[index]


## Returns false (and changes nothing) for an out-of-range index. `def == null` empties the slot.
## Always emits on success, even when the content is identical: a reroll onto the same ability reinstalls it.
func set_slot(index: int, def: AbilityDef, grade: Grade.Value) -> bool:
	var target := slot(index)
	if target == null:
		return false
	target.def = def
	target.grade = grade if def != null else Grade.Value.NONE
	slot_changed.emit(index, target)
	return true
```

`src/abilities/ability_pool.gd`:
```gdscript
class_name AbilityPool
extends RefCounted
## Which abilities a grade can roll: every def whose `min_grade` is at or below that grade.

var _defs: Array[AbilityDef] = []


func _init(defs: Array[AbilityDef]) -> void:
	for def in defs:
		if def != null:
			_defs.append(def)


func defs_for(grade: Grade.Value) -> Array[AbilityDef]:
	var result: Array[AbilityDef] = []
	if not Grade.is_rollable(grade):
		return result
	for def in _defs:
		if def.min_grade <= grade:
			result.append(def)
	return result


## A random def of the grade's pool, or null when it is empty. With `exclude`, a different def is
## preferred, but the excluded one is returned when it is the only candidate.
func roll(grade: Grade.Value, rng: RunRng, exclude: AbilityDef = null) -> AbilityDef:
	var candidates := defs_for(grade)
	if exclude != null and candidates.size() > 1:
		candidates.erase(exclude)
	return rng.pick(candidates) as AbilityDef
```

- [ ] **Step 4: Scan, run to verify pass, run the suite, commit**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities tools/run_tests.sh` → 5 + 6 PASS; `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/abilities tests/abilities
git commit -q -m "feat(abilities): add AbilitySlot, Loadout and AbilityPool

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: GradeService — promote, reroll, reveal

**Files:**
- Create: `src/abilities/grade_result.gd`, `src/abilities/grade_service.gd`
- Test: `tests/abilities/test_grade_service.gd`

**Interfaces:**
- Consumes: `Loadout`, `AbilityPool` (Task 3), `GradeTable.promote_price/reroll_price`, `Price.is_affordable` (Task 1), `Wallet.try_spend(kind, amount) -> bool`, `RunRng`.
- Produces:
  - `GradeResult` (RefCounted): `enum Outcome { OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL }`; `outcome`, `def: AbilityDef`, `grade: Grade.Value`; static `failed(outcome)`, `ok(def, grade)`.
  - `GradeService` (RefCounted): `_init(loadout, pool, table, wallet, rng)`; `signal resolved(index: int, result: GradeResult)` (every attempt, success or not); `promote(index) -> GradeResult`; `reroll(index) -> GradeResult`; `promote_cost(index) -> Price` (null when impossible); `reroll_cost(index) -> Price`. Atomic order: applicability → affordability → roll → spend → write slot. Only `OK` changes anything.

- [ ] **Step 1: Write the failing tests**

`tests/abilities/test_grade_service.gd`:
```gdscript
extends TestCase

var _loadout: Loadout
var _wallet: Wallet
var _service: GradeService


func _build(defs: Array[AbilityDef], gold: int, stone: int, run_seed: int = 3) -> GradeService:
	_loadout = Loadout.new(2)
	_wallet = Wallet.new()
	_wallet.add(Wallet.Kind.GOLD, gold)
	_wallet.add(Wallet.Kind.STONE, stone)
	_service = GradeService.new(_loadout, AbilityPool.new(defs), DefFactory.grade_table(), _wallet, RunRng.new(run_seed))
	return _service


func _single() -> Array[AbilityDef]:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"only")]
	return defs


func _pair() -> Array[AbilityDef]:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"first"), DefFactory.ability(&"second")]
	return defs


func test_buying_an_empty_slot_costs_100_gold_and_rolls_a_common() -> void:
	var service := _build(_single(), 250, 0)
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.OK)
	assert_eq(result.grade, Grade.Value.COMMON)
	assert_eq(result.def.id, &"only")
	assert_eq(_loadout.slot(0).def.id, &"only")
	assert_eq(_loadout.slot(0).grade, Grade.Value.COMMON)
	assert_eq(_wallet.gold, 150)
	assert_true(_loadout.slot(1).is_empty(), "other slot untouched")


func test_promotion_climbs_with_rising_gold_costs() -> void:
	var service := _build(_single(), 1500, 0)
	for expected_grade in [Grade.Value.COMMON, Grade.Value.RARE, Grade.Value.EPIC, Grade.Value.UNIQUE, Grade.Value.LEGENDARY]:
		assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, expected_grade)
	assert_eq(_wallet.gold, 0, "100 + 150 + 250 + 400 + 600")


func test_promote_cost_is_the_price_of_the_next_step() -> void:
	var defs := _single()
	var service := _build(defs, 0, 0)
	assert_eq(service.promote_cost(0).amount, 100)
	_loadout.set_slot(0, defs[0], Grade.Value.EPIC)
	assert_eq(service.promote_cost(0).amount, 400)
	assert_eq(service.promote_cost(0).kind, Wallet.Kind.GOLD)
	_loadout.set_slot(0, defs[0], Grade.Value.LEGENDARY)
	assert_eq(service.promote_cost(0).kind, Wallet.Kind.STONE)
	assert_eq(service.reroll_cost(0).amount, 2)


func test_promote_without_enough_gold_changes_nothing() -> void:
	var service := _build(_single(), 99, 5)
	var changes: Array = []
	_loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_null(result.def)
	assert_true(_loadout.slot(0).is_empty())
	assert_eq(_wallet.gold, 99)
	assert_eq(_wallet.stone, 5)
	assert_true(changes.is_empty())


func test_promote_into_an_empty_pool_spends_nothing() -> void:
	var defs: Array[AbilityDef] = [DefFactory.ability(&"rare_only", null, null, Grade.Value.RARE)]
	var service := _build(defs, 500, 0)
	var result := service.promote(0)
	assert_eq(result.outcome, GradeResult.Outcome.EMPTY_POOL, "nothing can be COMMON")
	assert_eq(_wallet.gold, 500)
	assert_true(_loadout.slot(0).is_empty())


func test_reveal_keeps_the_same_ability_and_costs_stone() -> void:
	var defs := _pair()
	var service := _build(defs, 0, 25)
	_loadout.set_slot(0, defs[1], Grade.Value.LEGENDARY)
	for i in 10:
		var result := service.promote(0)
		if i == 0:
			assert_eq(result.outcome, GradeResult.Outcome.OK)
		else:
			assert_eq(result.outcome, GradeResult.Outcome.NOT_APPLICABLE, "revealed is the top")
	assert_eq(_loadout.slot(0).def, defs[1], "same ability, never rolled")
	assert_eq(_loadout.slot(0).grade, Grade.Value.REVEALED)
	assert_eq(_wallet.stone, 15, "charged exactly once")
	assert_null(service.promote_cost(0))


func test_reveal_without_enough_stone_is_refused() -> void:
	var defs := _single()
	var service := _build(defs, 1000, 9)
	_loadout.set_slot(0, defs[0], Grade.Value.LEGENDARY)
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_eq(_loadout.slot(0).grade, Grade.Value.LEGENDARY)
	assert_eq(_wallet.gold, 1000, "gold is not an alternative currency")


func test_promotion_rerolls_from_the_target_grade_pool() -> void:
	var defs := _pair()
	var service := _build(defs, 100000, 0)
	var seen: Dictionary[StringName, bool] = {}
	for i in 16:
		_loadout.set_slot(0, defs[0], Grade.Value.COMMON)
		assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, Grade.Value.RARE)
		seen[_loadout.slot(0).def.id] = true
	assert_true(seen.has(&"second"), "promotion may change the ability (16 seeded tries)")


func test_reroll_costs_stone_and_prefers_a_different_ability() -> void:
	var defs := _pair()
	var service := _build(defs, 0, 10)
	_loadout.set_slot(0, defs[0], Grade.Value.EPIC)
	var expected := [defs[1], defs[0], defs[1]]
	for i in 3:
		var result := service.reroll(0)
		assert_eq(result.outcome, GradeResult.Outcome.OK)
		assert_eq(_loadout.slot(0).grade, Grade.Value.EPIC, "same grade")
		assert_eq(_loadout.slot(0).def, expected[i], "alternates between the two")
	assert_eq(_wallet.stone, 4)


func test_reroll_on_a_single_ability_pool_keeps_the_ability_but_still_charges() -> void:
	var defs := _single()
	var service := _build(defs, 0, 4)
	_loadout.set_slot(0, defs[0], Grade.Value.RARE)
	var changes: Array = []
	_loadout.slot_changed.connect(func(index: int, _slot: AbilitySlot) -> void: changes.append(index))
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.OK)
	assert_eq(_loadout.slot(0).def, defs[0])
	assert_eq(_wallet.stone, 2)
	assert_eq(changes, [0], "re-emitted so the installer reinstalls a fresh instance")


func test_reroll_is_refused_on_empty_and_revealed_slots_and_without_stone() -> void:
	var defs := _single()
	var service := _build(defs, 0, 1)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_APPLICABLE, "empty slot")
	assert_null(service.reroll_cost(0))
	_loadout.set_slot(0, defs[0], Grade.Value.REVEALED)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_APPLICABLE, "revealed")
	_loadout.set_slot(0, defs[0], Grade.Value.RARE)
	assert_eq(service.reroll(0).outcome, GradeResult.Outcome.NOT_ENOUGH, "1 stone < 2")
	assert_eq(_wallet.stone, 1)
	assert_eq(_loadout.slot(0).grade, Grade.Value.RARE)


func test_out_of_range_slot_is_not_applicable() -> void:
	var service := _build(_single(), 1000, 100)
	assert_eq(service.promote(7).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_eq(service.reroll(-1).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_null(service.promote_cost(7))
	assert_null(service.reroll_cost(-1))
	assert_eq(_wallet.gold, 1000)


func test_resolved_is_emitted_for_every_attempt() -> void:
	var service := _build(_single(), 100, 0)
	var log: Array = []
	service.resolved.connect(func(index: int, result: GradeResult) -> void: log.append([index, result.outcome]))
	service.promote(0)
	service.promote(0)
	service.reroll(1)
	assert_eq(log, [[0, GradeResult.Outcome.OK], [0, GradeResult.Outcome.NOT_ENOUGH], [1, GradeResult.Outcome.NOT_APPLICABLE]])
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities/test_grade_service tools/run_tests.sh`
Expected: parse errors — `GradeService`, `GradeResult` not found.

- [ ] **Step 3: Implement**

`src/abilities/grade_result.gd`:
```gdscript
class_name GradeResult
extends RefCounted
## What a promote/reroll attempt produced. Only OK changed anything.

enum Outcome { OK, NOT_ENOUGH, NOT_APPLICABLE, EMPTY_POOL }

var outcome: Outcome = Outcome.NOT_APPLICABLE
var def: AbilityDef
var grade: Grade.Value = Grade.Value.NONE


static func failed(p_outcome: Outcome) -> GradeResult:
	var result := GradeResult.new()
	result.outcome = p_outcome
	return result


static func ok(p_def: AbilityDef, p_grade: Grade.Value) -> GradeResult:
	var result := GradeResult.new()
	result.outcome = Outcome.OK
	result.def = p_def
	result.grade = p_grade
	return result
```

`src/abilities/grade_service.gd`:
```gdscript
class_name GradeService
extends RefCounted
## The shop rules: promote (NONE->COMMON buys; COMMON..UNIQUE re-roll from the next grade's pool;
## LEGENDARY->REVEALED keeps the ability) and reroll (same grade, a different ability preferred).
## Pure logic over Wallet, RunRng, Loadout, AbilityPool and GradeTable. Every action is atomic:
## check applicability, check the price, roll, spend, then write the slot. A refused action changes nothing.

## Emitted after every attempt, successful or not, so a view can react (flash a refused slot).
signal resolved(index: int, result: GradeResult)

var _loadout: Loadout
var _pool: AbilityPool
var _table: GradeTable
var _wallet: Wallet
var _rng: RunRng


func _init(loadout: Loadout, pool: AbilityPool, table: GradeTable, wallet: Wallet, rng: RunRng) -> void:
	_loadout = loadout
	_pool = pool
	_table = table
	_wallet = wallet
	_rng = rng


## Price of the next promotion of slot `index`; null when none is possible.
func promote_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	return _table.promote_price(slot.grade) if slot != null else null


## Price of rerolling slot `index`; null when the slot is empty or revealed.
func reroll_cost(index: int) -> Price:
	var slot := _loadout.slot(index)
	if slot == null or slot.is_empty():
		return null
	return _table.reroll_price(slot.grade)


func promote(index: int) -> GradeResult:
	return _resolve(index, _promote(index))


func reroll(index: int) -> GradeResult:
	return _resolve(index, _reroll(index))


func _promote(index: int) -> GradeResult:
	var slot := _loadout.slot(index)
	var price := promote_cost(index)
	if slot == null or price == null:
		return GradeResult.failed(GradeResult.Outcome.NOT_APPLICABLE)
	if not price.is_affordable(_wallet):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	var target := Grade.next(slot.grade)
	var def: AbilityDef = null
	if target == Grade.Value.REVEALED:
		def = slot.def  # the enhanced form of the same ability: deterministic, never rolled
	else:
		def = _pool.roll(target, _rng)
	if def == null:
		return GradeResult.failed(GradeResult.Outcome.EMPTY_POOL)
	return _commit(index, price, def, target)


func _reroll(index: int) -> GradeResult:
	var slot := _loadout.slot(index)
	var price := reroll_cost(index)
	if slot == null or price == null:
		return GradeResult.failed(GradeResult.Outcome.NOT_APPLICABLE)
	if not price.is_affordable(_wallet):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	var def := _pool.roll(slot.grade, _rng, slot.def)
	if def == null:
		return GradeResult.failed(GradeResult.Outcome.EMPTY_POOL)
	return _commit(index, price, def, slot.grade)


func _commit(index: int, price: Price, def: AbilityDef, grade: Grade.Value) -> GradeResult:
	if not _wallet.try_spend(price.kind, price.amount):
		return GradeResult.failed(GradeResult.Outcome.NOT_ENOUGH)
	_loadout.set_slot(index, def, grade)
	return GradeResult.ok(def, grade)


func _resolve(index: int, result: GradeResult) -> GradeResult:
	resolved.emit(index, result)
	return result
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities/test_grade_service tools/run_tests.sh`
Expected: 13 PASS. If `test_promotion_rerolls_from_the_target_grade_pool` or `test_reroll_costs_stone_and_prefers...` fail only on which def came out, the seeded sequence differs from the plan's assumption: for the reroll test the alternation is deterministic (the excluded def is dropped whenever another exists), so a failure there is a real bug; for the promotion test raise the loop count to 32 before suspecting the roll.

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/abilities tests/abilities
git commit -q -m "feat(abilities): add GradeService with atomic promote, reveal and reroll rules

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Player hosts granted abilities; AbilityContext carries the wallet

**Files:**
- Modify: `src/components/ability_context.gd`, `src/actors/player/player.gd`, `src/stage/stage.gd` (the `player.setup` call), `tests/actors/test_player.gd`, `tests/stage/test_loot_director.gd` (the `player.setup` call in `_rig`)

**Interfaces:**
- Consumes: GAS `AbilitySystemComponent.grant_ability(node)` (`ability_system_component.gd:279`: adds the node as a child if needed, sets `owner_asc`, tracks it), `GameplayAbility._ready()` (`gameplay_ability.gd:76`: an ability whose parent is an ASC grants itself — so a plain `add_child` is a grant), `AbilitySystemComponent.remove_ability(ability)` (`:310`: untracks + `queue_free`, **does not abort** an active ability), `GameplayAbility.abort_ability()` (`:191`: `end_ability(true)` strips `activation_owned_tags`), `ProbeAbility` (Task 1), `Wallet`.
- Produces:
  - `AbilityContext._init(p_actor: Node2D, p_mover: Mover, p_targets: TargetProvider, p_projectile_layer: Node, p_wallet: Wallet)`; new field `wallet: Wallet`.
  - `Player`: public `asc: AbilitySystemComponent` (was `_asc`), `ability_context: AbilityContext` (built once in `setup`, shared by every ability), `setup(profile, targets, projectile_layer, wallet: Wallet)`, `grant_ability(ability: ActorAbility)` (binds the context, connects `animation_requested` once, adds it under the ASC which self-grants; a node that already has a parent — granted before, or owned elsewhere — is ignored), `revoke_ability(ability: ActorAbility)` (aborts if active, `asc.remove_ability`, removes the child immediately; unknown nodes are ignored). Phase 1 behaviour (arrow, dash, death, camera, loot) is unchanged.

- [ ] **Step 1: Update the existing rigs and write the failing tests**

In `tests/actors/test_player.gd` replace `_player()` with:
```gdscript
func _player() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	var wallet := Wallet.new()
	player.setup(_profile(), targets, layer, wallet)
	targets.player = player
	return [player, layer, targets, wallet]
```
add the constant `const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")` and append:
```gdscript
func test_context_carries_the_wallet_and_is_shared_by_every_ability() -> void:
	var rig := _player()
	var player: Player = rig[0]
	assert_eq(player.ability_context.wallet, rig[3])
	assert_eq(player.ability_context.actor, player)
	var arrow := player.asc.get_node("ArrowAbility") as ArrowAbility
	assert_true(arrow.context == player.ability_context, "same context object")


func test_grant_ability_registers_it_with_the_asc_and_binds_the_context() -> void:
	var player: Player = _player()[0]
	var granted: Array = []
	player.asc.ability_granted.connect(func(ability: GameplayAbility) -> void: granted.append(ability))
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	assert_eq(probe.owner_asc, player.asc)
	assert_eq(probe.get_parent(), player.asc)
	assert_true(probe.context == player.ability_context)
	assert_eq(granted, [probe], "granted exactly once")
	assert_true(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"))
	await next_frame()
	assert_eq(probe.activations, 1)


func test_granting_the_same_ability_twice_is_ignored() -> void:
	var player: Player = _player()[0]
	var granted: Array = []
	player.asc.ability_granted.connect(func(ability: GameplayAbility) -> void: granted.append(ability))
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	player.grant_ability(probe)
	assert_eq(granted.size(), 1)
	assert_eq(probe.get_parent(), player.asc)


func test_revoke_ability_untracks_and_frees_it() -> void:
	var player: Player = _player()[0]
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	player.revoke_ability(probe)
	assert_null(player.asc.get_node_or_null("ProbeAbility"), "gone from the ASC immediately")
	assert_false(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"), "no longer granted")
	await next_frame()
	assert_false(is_instance_valid(probe), "freed")


func test_revoke_mid_activation_strips_its_owned_tags() -> void:
	var player: Player = _player()[0]
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	probe.hold_seconds = 1.0
	probe.activation_owned_tags.append(GameplayTags.State_Invulnerable)
	player.grant_ability(probe)
	assert_true(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"))
	await next_frame()
	assert_true(player.asc.has_tag(GameplayTags.State_Invulnerable), "active and holding the tag")
	player.revoke_ability(probe)
	assert_false(player.asc.has_tag(GameplayTags.State_Invulnerable), "aborted before removal")
	await wait_seconds(0.1)
	assert_false(is_instance_valid(probe))


func test_revoking_an_unknown_ability_is_a_no_op() -> void:
	var player: Player = _player()[0]
	var stranger := adopt(PROBE_SCENE.instantiate()) as ProbeAbility
	player.revoke_ability(stranger)
	await next_frame()
	assert_true(is_instance_valid(stranger))
	assert_eq(stranger.get_parent(), host)


func test_abilities_granted_after_death_do_not_process() -> void:
	var player: Player = _player()[0]
	_hit_player(player, 9999.0)
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	assert_false(probe.is_physics_processing())
```

In `tests/stage/test_loot_director.gd` `_rig()`, create the wallet before the player and pass it: move `var wallet := Wallet.new()` above `player.setup(...)` and change that call to `player.setup(profile, targets, projectile_layer, wallet)`.

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/actors/test_player tools/run_tests.sh`
Expected: parse errors — `setup` takes 3 arguments, `ability_context`/`asc`/`grant_ability` not found.

- [ ] **Step 3: Implement**

`src/components/ability_context.gd`:
```gdscript
class_name AbilityContext
extends RefCounted
## What an actor offers its abilities. The actor builds it once and hands it to every ActorAbility on its ASC.

var actor: Node2D
var mover: Mover
var targets: TargetProvider
var projectile_layer: Node
## The run's currency; on-kill/economy abilities credit it directly.
var wallet: Wallet


func _init(p_actor: Node2D, p_mover: Mover, p_targets: TargetProvider, p_projectile_layer: Node, p_wallet: Wallet) -> void:
	actor = p_actor
	mover = p_mover
	targets = p_targets
	projectile_layer = p_projectile_layer
	wallet = p_wallet
```

`src/actors/player/player.gd` (whole file):
```gdscript
class_name Player
extends CharacterBody2D
## The player's actor: input -> Mover, dash key -> ability tag, loot magnet, camera.
## It does not know which abilities it has: every ActorAbility on its ASC gets the same context,
## whether placed in the scene (arrow, dash) or granted at runtime by the loadout.

signal died
signal loot_collected(kind: Wallet.Kind, amount: int)

var ability_context: AbilityContext

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _collector: LootCollector = $Collector
@onready var _camera: Camera2D = $Camera2D


## Call after the player has entered the tree.
func setup(profile: PlayerProfile, targets: TargetProvider, projectile_layer: Node, wallet: Wallet) -> void:
	asc.initialize_attribute_overrides(profile.stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	_mover.bind(self, asc)
	_visual.apply(profile.visual)
	_collector.bind(self)
	ability_context = AbilityContext.new(self, _mover, targets, projectile_layer, wallet)
	for child in asc.get_children():
		var ability := child as ActorAbility
		if ability != null:
			_attach(ability)
	_collector.collected.connect(loot_collected.emit)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	add_to_group(Groups.PLAYER)


func limit_camera(bounds: Rect2) -> void:
	_camera.limit_left = int(bounds.position.x)
	_camera.limit_top = int(bounds.position.y)
	_camera.limit_right = int(bounds.end.x)
	_camera.limit_bottom = int(bounds.end.y)


func gain_multiplier(kind: Wallet.Kind) -> float:
	var attribute := "gold_gain" if kind == Wallet.Kind.GOLD else "stone_gain"
	return asc.get_attribute(attribute).current_value


## Adds a runtime ability under the ASC. GameplayAbility._ready grants itself when its parent is an ASC,
## so no explicit grant call is needed (that would emit `ability_granted` twice).
func grant_ability(ability: ActorAbility) -> void:
	if ability.get_parent() != null:
		return  # already granted here, or owned by someone else: never re-parent
	_attach(ability)
	asc.add_child(ability)
	if ability.owner_asc == null:
		asc.grant_ability(ability)
	if health.is_dead:
		ability.set_physics_process(false)


## Ends and removes a runtime ability. ASC.remove_ability only untracks and queue_frees: an active
## ability must be aborted first so its activation_owned_tags come off.
func revoke_ability(ability: ActorAbility) -> void:
	if not is_instance_valid(ability) or ability.get_parent() != asc:
		return
	ability.abort_ability()
	asc.remove_ability(ability)
	asc.remove_child(ability)


func _physics_process(_delta: float) -> void:
	if health.is_dead:
		_mover.set_direction(Vector2.ZERO)
		return
	var input := InputActions.move_vector()
	_mover.set_direction(input)
	_visual.set_moving(_mover.is_moving())
	_visual.face(input.x)
	if Input.is_action_just_pressed(InputActions.DASH):
		asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash)


func _attach(ability: ActorAbility) -> void:
	ability.bind_context(ability_context)
	if not ability.animation_requested.is_connected(_on_animation_requested):
		ability.animation_requested.connect(_on_animation_requested)


func _on_animation_requested(animation: StringName, facing: Vector2) -> void:
	_visual.face(facing.x)
	_visual.play_once(animation)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	for child in asc.get_children():
		child.set_physics_process(false)
	_visual.play_once(AnimNames.DEATH)
	asc.cleanup.call_deferred()
	died.emit()
```

`src/stage/stage.gd` — change the player setup line to:
```gdscript
	player.setup(profile, _targets, _projectile_layer, run_state.wallet)
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/actors/test_player tools/run_tests.sh`, `FILTER=tests/stage tools/run_tests.sh`
Expected: 16 player tests PASS, stage/loot tests PASS. Likely snags:
- `granted` has two entries: you called `asc.grant_ability` on a node that was already in the tree — only call it when `owner_asc` is still null after `add_child`.
- `State.Invulnerable` still present after revoke: `abort_ability()` must run before `remove_ability`; also check `probe.is_active` was true (the activation needs one frame: the test awaits `next_frame()` first).
- `get_node_or_null("ProbeAbility")` not null: `remove_child` must follow `remove_ability` (queue_free alone leaves it in the tree until the frame ends).

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/components src/actors src/stage tests/actors tests/stage
git commit -q -m "feat(player): expose asc and ability_context, add grant/revoke_ability, wallet in AbilityContext

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: AbilityInstaller — the loadout mirrored onto the ASC

**Files:**
- Create: `src/abilities/ability_installer.gd`
- Test: `tests/abilities/test_ability_installer.gd`

**Interfaces:**
- Consumes: `Loadout.slot_changed/slot/size` (Task 3), `Grade.level` (Task 1), `Player.asc/grant_ability/revoke_ability/ability_context` (Task 5), GAS `apply_gameplay_effect(effect, source_asc, level) -> ActiveGameplayEffect` (`ability_system_component.gd:633`; for an INFINITE effect this is the persistent handle in `_active_effects`), `remove_active_effect(handle)` (`:825`; silently ignores a handle that is no longer active, e.g. after `cleanup()`), INFINITE effects under the default `FREE` stacking (two applications = two active effects whose modifiers add), `DefFactory.vitality/passive_effect/ability`, `ProbeAbility`.
- Produces: `AbilityInstaller` (Node): `bind(loadout: Loadout, player: Player)` (rebinding disconnects the old loadout and uninstalls everything first, then installs every already-filled slot); `installed_effect(index) -> ActiveGameplayEffect` and `installed_ability(index) -> ActorAbility` (null when none; for tests and debugging); `uninstall_all()`. On `slot_changed(index, slot)`: uninstall the slot's old effect handle and ability, then apply `def.passive_effect` at `Grade.level(grade)` and/or grant a fresh `def.ability_scene` instance with `ability_level = level`.

- [ ] **Step 1: Write the failing tests**

`tests/abilities/test_ability_installer.gd`:
```gdscript
extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")

var _player: Player
var _loadout: Loadout
var _installer: AbilityInstaller


func _rig(slot_count: int = 3, bind_now: bool = true) -> void:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	_player = adopt(PLAYER_SCENE.instantiate()) as Player
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	_player.setup(profile, targets, layer, Wallet.new())
	_loadout = Loadout.new(slot_count)
	_installer = adopt(AbilityInstaller.new()) as AbilityInstaller
	if bind_now:
		_installer.bind(_loadout, _player)


func _attr(attribute: String) -> float:
	return _player.asc.get_attribute(attribute).current_value


func _swift() -> AbilityDef:
	var per_level := PackedFloat32Array([0.1, 0.2, 0.3, 0.4, 0.5, 0.6])
	return DefFactory.ability(&"swift", DefFactory.passive_effect("move_speed", GameplayEffectModifier.Operation.PERCENT_ADD, per_level))


func _probe_def() -> AbilityDef:
	return DefFactory.ability(&"probe", null, PROBE_SCENE)


func _hybrid() -> AbilityDef:
	return DefFactory.ability(&"hybrid", DefFactory.vitality().passive_effect, PROBE_SCENE)


func _runtime_abilities() -> Array[ActorAbility]:
	var found: Array[ActorAbility] = []
	for child in _player.asc.get_children():
		if child is ActorAbility and not (child is ArrowAbility) and not (child is DashAbility):
			found.append(child)
	return found


func test_setting_a_slot_applies_the_passive_at_the_grade_level() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	assert_almost_eq(_attr("max_health"), 120.0)
	assert_not_null(_installer.installed_effect(0))
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 140.0, 0.001, "replaced, not stacked")
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.REVEALED)
	assert_almost_eq(_attr("max_health"), 340.0)


func test_binding_installs_slots_that_were_filled_before() -> void:
	_rig(3, false)
	_loadout.set_slot(1, DefFactory.vitality(), Grade.Value.EPIC)
	assert_almost_eq(_attr("max_health"), 100.0, 0.001, "nothing until bound")
	_installer.bind(_loadout, _player)
	assert_almost_eq(_attr("max_health"), 170.0)


func test_changing_the_ability_removes_the_old_passive() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_loadout.set_slot(0, _swift(), Grade.Value.COMMON)
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_almost_eq(_attr("move_speed"), 110.0)


func test_two_slots_with_the_same_passive_stack() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_loadout.set_slot(1, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 160.0, 0.001, "20 + 40")
	_loadout.set_slot(1, null, Grade.Value.NONE)
	assert_almost_eq(_attr("max_health"), 120.0)


func test_ability_scenes_are_granted_and_replaced() -> void:
	_rig()
	_loadout.set_slot(0, _probe_def(), Grade.Value.COMMON)
	var first := _installer.installed_ability(0) as ProbeAbility
	assert_not_null(first)
	assert_eq(first.owner_asc, _player.asc)
	assert_almost_eq(first.ability_level, 1.0)
	assert_true(first.context == _player.ability_context)
	_loadout.set_slot(0, _probe_def(), Grade.Value.RARE)
	var second := _installer.installed_ability(0) as ProbeAbility
	assert_true(second != first, "a fresh instance")
	assert_almost_eq(second.ability_level, 2.0)
	assert_eq(_runtime_abilities().size(), 1, "the old one left the ASC at once")
	await next_frame()
	assert_false(is_instance_valid(first))
	_player.asc.try_activate_abilities_by_tag(&"Ability.Probe")
	await next_frame()
	assert_eq(second.activations, 1)


func test_clearing_a_slot_uninstalls_everything() -> void:
	_rig()
	_loadout.set_slot(0, _hybrid(), Grade.Value.EPIC)
	assert_almost_eq(_attr("max_health"), 170.0)
	assert_not_null(_installer.installed_ability(0))
	_loadout.set_slot(0, null, Grade.Value.NONE)
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_null(_installer.installed_ability(0))
	assert_null(_installer.installed_effect(0))
	assert_true(_runtime_abilities().is_empty())


func test_many_changes_leave_no_leftovers() -> void:
	_rig()
	var rng := RunRng.new(9)
	var defs: Array[AbilityDef] = [DefFactory.vitality(), _swift(), _hybrid(), _probe_def()]
	var added := 0
	var removed := 0
	_player.asc.active_effect_added.connect(func(_effect: ActiveGameplayEffect) -> void: added += 1)
	_player.asc.active_effect_removed.connect(func(_effect: ActiveGameplayEffect) -> void: removed += 1)
	for i in 40:
		var grade := rng.randi_range(1, 6) as Grade.Value
		_loadout.set_slot(rng.randi_range(0, 2), rng.pick(defs) as AbilityDef, grade)
	for i in 3:
		_loadout.set_slot(i, null, Grade.Value.NONE)
	await next_frame()
	assert_almost_eq(_attr("max_health"), 100.0)
	assert_almost_eq(_attr("move_speed"), 100.0)
	assert_eq(added, removed, "every applied handle was removed")
	assert_true(added > 0)
	assert_true(_runtime_abilities().is_empty())


func test_changing_a_slot_mid_activation_strips_its_tags() -> void:
	_rig()
	_loadout.set_slot(0, _probe_def(), Grade.Value.COMMON)
	var probe := _installer.installed_ability(0) as ProbeAbility
	probe.hold_seconds = 1.0
	probe.activation_owned_tags.append(GameplayTags.State_Invulnerable)
	_player.asc.try_activate_abilities_by_tag(&"Ability.Probe")
	await next_frame()
	assert_true(_player.asc.has_tag(GameplayTags.State_Invulnerable))
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	assert_false(_player.asc.has_tag(GameplayTags.State_Invulnerable))
	assert_almost_eq(_attr("max_health"), 120.0)
	await wait_seconds(0.1)
	assert_false(is_instance_valid(probe))


func test_rebinding_does_not_double_install() -> void:
	_rig()
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.COMMON)
	_installer.bind(_loadout, _player)
	assert_almost_eq(_attr("max_health"), 120.0, 0.001, "reinstalled once, not stacked")
	_loadout.set_slot(0, DefFactory.vitality(), Grade.Value.RARE)
	assert_almost_eq(_attr("max_health"), 140.0, 0.001, "one listener")


func test_uninstall_survives_a_cleaned_up_asc() -> void:
	_rig()
	_loadout.set_slot(0, _hybrid(), Grade.Value.COMMON)
	_player.asc.cleanup()
	_loadout.set_slot(0, null, Grade.Value.NONE)
	assert_null(_installer.installed_effect(0))
	assert_true(_runtime_abilities().is_empty())
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities/test_ability_installer tools/run_tests.sh`
Expected: parse error — `AbilityInstaller` not found.

- [ ] **Step 3: Implement**

`src/abilities/ability_installer.gd`:
```gdscript
class_name AbilityInstaller
extends Node
## Mirrors a Loadout onto the player's ASC: a slot's passive_effect is held as an active-effect handle,
## its ability_scene as a granted ActorAbility. Changing a slot always uninstalls the old content first,
## so nothing stacks or leaks; two slots holding the same ability stack on purpose.

var _loadout: Loadout
var _player: Player
var _effects: Dictionary[int, ActiveGameplayEffect] = {}
var _abilities: Dictionary[int, ActorAbility] = {}


func bind(loadout: Loadout, player: Player) -> void:
	if _loadout != null and _loadout.slot_changed.is_connected(_on_slot_changed):
		_loadout.slot_changed.disconnect(_on_slot_changed)
	uninstall_all()
	_loadout = loadout
	_player = player
	loadout.slot_changed.connect(_on_slot_changed)
	for index in loadout.size():
		_install(index, loadout.slot(index))


func installed_effect(index: int) -> ActiveGameplayEffect:
	return _effects.get(index)


func installed_ability(index: int) -> ActorAbility:
	return _abilities.get(index)


func uninstall_all() -> void:
	for index in _effects.keys().duplicate():
		_uninstall(index)
	for index in _abilities.keys().duplicate():
		_uninstall(index)


func _on_slot_changed(index: int, slot: AbilitySlot) -> void:
	_uninstall(index)
	_install(index, slot)


func _install(index: int, slot: AbilitySlot) -> void:
	if slot == null or slot.is_empty() or not is_instance_valid(_player):
		return
	var level := Grade.level(slot.grade)
	if slot.def.passive_effect != null:
		var handle := _player.asc.apply_gameplay_effect(slot.def.passive_effect, _player.asc, level)
		if handle != null:
			_effects[index] = handle
	if slot.def.ability_scene != null:
		var ability := slot.def.ability_scene.instantiate() as ActorAbility
		if ability == null:
			push_error("AbilityInstaller: '%s' ability_scene root is not an ActorAbility" % slot.def.id)
			return
		ability.ability_level = level
		_player.grant_ability(ability)
		_abilities[index] = ability


func _uninstall(index: int) -> void:
	if _effects.has(index):
		var handle: ActiveGameplayEffect = _effects[index]
		_effects.erase(index)
		if is_instance_valid(_player):
			_player.asc.remove_active_effect(handle)
	if _abilities.has(index):
		var ability: ActorAbility = _abilities[index]
		_abilities.erase(index)
		if is_instance_valid(_player) and is_instance_valid(ability):
			_player.revoke_ability(ability)
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities/test_ability_installer tools/run_tests.sh`
Expected: 10 PASS. Likely snags:
- `max_health` 160 instead of 140 after the second `set_slot`: the old handle was not removed — check `_uninstall` runs before `_install` and that `apply_gameplay_effect` returned the persistent handle (INFINITE policy; a DURATION/INSTANT effect returns something else).
- `added != removed`: the Arrow cooldown fired because an enemy got into range — there are no enemies in this rig, so look for a passive applied twice (double bind).
- `Dictionary[int, ActiveGameplayEffect]` parse error: typed dictionaries need 4.4+; the project is 4.7, so the editor scan is stale.

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/abilities tests/abilities
git commit -q -m "feat(abilities): add AbilityInstaller mirroring the loadout onto the player's ASC

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Event.Kill tag, kill events from the Stage, and the four stat passives as data

**Files:**
- Modify: `tools/sync_tags.gd` (TAGS), `godot_gas/tag_registry.tres` + `godot_gas/gameplay_tags.gd` (regenerated by the tool only), `src/stage/stage.gd` (`_on_enemy_died`), `assets/SOURCES.md`
- Create: `data/abilities/prospector.tres`, `data/abilities/swift_boots.tres`, `data/abilities/vitality.tres`, `data/abilities/might.tres`; copied icons `assets/art/icons/{honey_pot,chili_pepper_red,chili_pepper_green,apple_red,apple_yellow,steak_grilled}.png`
- Test: `tests/stage/test_stage.gd` (append), `tests/data/test_ability_data.gd` (new)

**Interfaces:**
- Consumes: `Stage._on_enemy_died(enemy: Enemy)` (`src/stage/stage.gd:71`), `Player.asc` (Task 5), GAS `send_gameplay_event(tag, payload)` (`ability_system_component.gd:1209`: emits `gameplay_event_received(tag, payload)` and calls `try_activate(payload)` on every granted ability whose `trigger_event_tag` matches), `GameplayEffectModifier.calculate_magnitude(level) = scaling_curve.sample(level) * magnitude`, `AbilityDef` (Task 1), `ActorFixture.bare_actor`.
- Produces:
  - Tags `Ability.Volley`, `Ability.BladeSweep`, `Ability.Bounty`, `Event.Kill` → `GameplayTags.Ability_Volley`, `GameplayTags.Ability_BladeSweep`, `GameplayTags.Ability_Bounty`, `GameplayTags.Event_Kill`.
  - `Stage` sends `GameplayTags.Event_Kill` with the dead `Enemy` as payload to `player.asc` on every enemy death (after counting the kill). Any on-kill ability sets `trigger_event_tag = &"Event.Kill"` and reads `current_event_payload`.
  - Four `AbilityDef` files, each with an INFINITE `passive_effect` holding one modifier with `magnitude = 1.0` and a 6-point `Curve` (x = grade level 1..6): Prospector `gold_gain` ADD 0.25/0.5/0.8/1.2/1.7/2.5; Swift Boots `move_speed` PERCENT_ADD 0.1/0.2/0.32/0.45/0.6/0.85; Vitality `max_health` ADD 20/40/70/110/160/240; Might `attack` PERCENT_ADD 0.1/0.2/0.35/0.5/0.7/1.0.
  - Curve serialisation used by every `.tres`/`.tscn` of this plan: `_limits = [min_value, max_value, min_domain, max_domain]` then `_data = [Vector2(x, y), left_tangent, right_tangent, left_mode, right_mode, ...]` and `point_count` (Godot 4.7 stores `Curve` this way; sampling at an exact point x returns that point's y regardless of tangents).

- [ ] **Step 1: Register the tags**

In `tools/sync_tags.gd` set:
```gdscript
const TAGS: PackedStringArray = [
	"Ability.Arrow",
	"Ability.BladeSweep",
	"Ability.Bounty",
	"Ability.Dash",
	"Ability.Volley",
	"Cooldown.Arrow",
	"Cooldown.Dash",
	"Damage.Amount",
	"Event.Kill",
	"State.Invulnerable",
]
```
Run: `/d/CM/Godot/Godot_v4.7.2-stable_win64.exe --headless --path . -s res://tools/sync_tags.gd`, then godot-ai `filesystem_manage` `{"op": "scan"}`.
Expected: `godot_gas/gameplay_tags.gd` now contains `const Event_Kill: StringName = &"Event.Kill"` and the three `Ability_*` constants; `godot_gas/tag_registry.tres` lists 10 tags.

- [ ] **Step 2: Copy the icons and record their source**

```bash
mkdir -p assets/art/icons
src=/d/CM/Godot/res/opengameart/2D/16x16-food/food_0/food
cp "$src/honey_pot.png" "$src/chili_pepper_red.png" "$src/chili_pepper_green.png" "$src/apple_red.png" "$src/apple_yellow.png" "$src/steak_grilled.png" assets/art/icons/
```
(All six are 16x16 RGBA PNGs; verified to exist.) Append to the table in `assets/SOURCES.md`:
```
| `art/icons/{honey_pot,chili_pepper_red,chili_pepper_green,apple_red,apple_yellow,steak_grilled}.png` | OpenGameArt "16x16 Food" by ARoachIFoundOnMyPillow (`opengameart/2D/16x16-food`) | CC0 |
```
godot-ai `filesystem_manage` `{"op": "scan"}` so the editor imports them (this creates the `.png.import` files — commit them).

- [ ] **Step 3: Write the failing tests**

Append to `tests/stage/test_stage.gd`:
```gdscript
func test_every_enemy_death_sends_event_kill_to_the_player() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	var events: Array = []
	stage.player.asc.gameplay_event_received.connect(func(tag: StringName, payload: Variant) -> void: events.append([tag, payload]))
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		_kill(stage, victim.get_node("AbilitySystemComponent"))
	assert_eq(events.size(), victims.size())
	assert_eq(events[0][0], GameplayTags.Event_Kill)
	assert_true(events[0][1] is Enemy, "payload is the dead enemy")
	assert_eq(stage.run_state.kills, victims.size())
```

`tests/data/test_ability_data.gd`:
```gdscript
extends TestCase

## path -> [attribute, value at level 1, value at level 6] on a StatBlock.new() actor
## (max_health 100, move_speed 100, attack 1.0, gold_gain 1.0).
const PASSIVES: Dictionary = {
	"res://data/abilities/prospector.tres": ["gold_gain", 1.25, 3.5],
	"res://data/abilities/swift_boots.tres": ["move_speed", 110.0, 185.0],
	"res://data/abilities/vitality.tres": ["max_health", 120.0, 340.0],
	"res://data/abilities/might.tres": ["attack", 1.1, 2.0],
}


func _def(path: String) -> AbilityDef:
	var def := load(path) as AbilityDef
	assert_not_null(def, path)
	return def


func test_passive_defs_load_and_validate() -> void:
	for path: String in PASSIVES:
		var def := _def(path)
		assert_no_errors(def.validate(), path)
		assert_not_null(def.passive_effect, path)
		assert_null(def.ability_scene, "%s is a pure passive" % path)


func test_passives_scale_with_the_grade_level() -> void:
	for path: String in PASSIVES:
		var expected: Array = PASSIVES[path]
		var attribute: String = expected[0]
		var def := _def(path)
		var actor := ActorFixture.bare_actor(self, StatBlock.new())
		var asc := ActorFixture.asc_of(actor)
		var base: float = asc.get_attribute(attribute).current_value
		var handle := asc.apply_gameplay_effect(def.passive_effect, asc, Grade.level(Grade.Value.COMMON))
		assert_not_null(handle, path)
		assert_almost_eq(asc.get_attribute(attribute).current_value, expected[1], 0.001, "%s at level 1" % path)
		asc.remove_active_effect(handle)
		assert_almost_eq(asc.get_attribute(attribute).current_value, base, 0.001, "%s removed" % path)
		asc.apply_gameplay_effect(def.passive_effect, asc, Grade.level(Grade.Value.REVEALED))
		assert_almost_eq(asc.get_attribute(attribute).current_value, expected[2], 0.001, "%s at level 6" % path)


func test_passive_curves_are_strictly_increasing_over_levels_one_to_six() -> void:
	for path: String in PASSIVES:
		var modifier: GameplayEffectModifier = _def(path).passive_effect.modifiers[0]
		var previous := -INF
		for level in range(1, 7):
			var value := modifier.calculate_magnitude(float(level))
			assert_true(value > previous, "%s level %d (%f <= %f)" % [path, level, value, previous])
			previous = value
```

Run: `FILTER=tests/data/test_ability_data tools/run_tests.sh` → FAIL (files do not load); `FILTER=tests/stage/test_stage tools/run_tests.sh` → the new test FAIL (0 events).

- [ ] **Step 4: Send the kill event from the Stage**

In `src/stage/stage.gd` replace `_on_enemy_died` with:
```gdscript
## Every kill is also a gameplay event on the player's ASC, so on-kill abilities react through GAS.
func _on_enemy_died(enemy: Enemy) -> void:
	run_state.kills += 1
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, enemy)
```
(Called from the enemy's `died` signal, i.e. inside a physics callback: an on-kill ability must not add physics objects without `call_deferred`.)

- [ ] **Step 5: Write the four ability files**

`data/abilities/vitality.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=8 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]
[ext_resource type="Texture2D" path="res://assets/art/icons/apple_red.png" id="4_icon"]
[ext_resource type="Texture2D" path="res://assets/art/icons/apple_yellow.png" id="5_revealed"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 250.0, 1.0, 6.0]
_data = [Vector2(1, 20), 0.0, 0.0, 0, 0, Vector2(2, 40), 0.0, 0.0, 0, 0, Vector2(3, 70), 0.0, 0.0, 0, 0, Vector2(4, 110), 0.0, 0.0, 0, 0, Vector2(5, 160), 0.0, 0.0, 0, 0, Vector2(6, 240), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Resource" id="Modifier_max_health"]
script = ExtResource("3_modifier")
attribute_name = "max_health"
operation = 0
magnitude = 1.0
scaling_curve = SubResource("Curve_levels")

[sub_resource type="Resource" id="Effect_vitality"]
script = ExtResource("2_effect")
policy = 2
modifiers = Array[ExtResource("3_modifier")]([SubResource("Modifier_max_health")])

[resource]
script = ExtResource("1_def")
id = &"vitality"
display_name = "Vitality"
description = "More maximum health. Does not heal."
icon = ExtResource("4_icon")
revealed_icon = ExtResource("5_revealed")
min_grade = 1
passive_effect = SubResource("Effect_vitality")
```

`data/abilities/swift_boots.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=8 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]
[ext_resource type="Texture2D" path="res://assets/art/icons/chili_pepper_red.png" id="4_icon"]
[ext_resource type="Texture2D" path="res://assets/art/icons/chili_pepper_green.png" id="5_revealed"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 1.0, 1.0, 6.0]
_data = [Vector2(1, 0.1), 0.0, 0.0, 0, 0, Vector2(2, 0.2), 0.0, 0.0, 0, 0, Vector2(3, 0.32), 0.0, 0.0, 0, 0, Vector2(4, 0.45), 0.0, 0.0, 0, 0, Vector2(5, 0.6), 0.0, 0.0, 0, 0, Vector2(6, 0.85), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Resource" id="Modifier_move_speed"]
script = ExtResource("3_modifier")
attribute_name = "move_speed"
operation = 1
magnitude = 1.0
scaling_curve = SubResource("Curve_levels")

[sub_resource type="Resource" id="Effect_swift"]
script = ExtResource("2_effect")
policy = 2
modifiers = Array[ExtResource("3_modifier")]([SubResource("Modifier_move_speed")])

[resource]
script = ExtResource("1_def")
id = &"swift_boots"
display_name = "Swift Boots"
description = "Move faster."
icon = ExtResource("4_icon")
revealed_icon = ExtResource("5_revealed")
min_grade = 1
passive_effect = SubResource("Effect_swift")
```

`data/abilities/prospector.tres` (no revealed icon: the view tints it):
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=7 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]
[ext_resource type="Texture2D" path="res://assets/art/icons/honey_pot.png" id="4_icon"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 3.0, 1.0, 6.0]
_data = [Vector2(1, 0.25), 0.0, 0.0, 0, 0, Vector2(2, 0.5), 0.0, 0.0, 0, 0, Vector2(3, 0.8), 0.0, 0.0, 0, 0, Vector2(4, 1.2), 0.0, 0.0, 0, 0, Vector2(5, 1.7), 0.0, 0.0, 0, 0, Vector2(6, 2.5), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Resource" id="Modifier_gold_gain"]
script = ExtResource("3_modifier")
attribute_name = "gold_gain"
operation = 0
magnitude = 1.0
scaling_curve = SubResource("Curve_levels")

[sub_resource type="Resource" id="Effect_prospector"]
script = ExtResource("2_effect")
policy = 2
modifiers = Array[ExtResource("3_modifier")]([SubResource("Modifier_gold_gain")])

[resource]
script = ExtResource("1_def")
id = &"prospector"
display_name = "Prospector"
description = "Collected gold is worth more."
icon = ExtResource("4_icon")
min_grade = 1
passive_effect = SubResource("Effect_prospector")
```

`data/abilities/might.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=7 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]
[ext_resource type="Texture2D" path="res://assets/art/icons/steak_grilled.png" id="4_icon"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 1.2, 1.0, 6.0]
_data = [Vector2(1, 0.1), 0.0, 0.0, 0, 0, Vector2(2, 0.2), 0.0, 0.0, 0, 0, Vector2(3, 0.35), 0.0, 0.0, 0, 0, Vector2(4, 0.5), 0.0, 0.0, 0, 0, Vector2(5, 0.7), 0.0, 0.0, 0, 0, Vector2(6, 1.0), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Resource" id="Modifier_attack"]
script = ExtResource("3_modifier")
attribute_name = "attack"
operation = 1
magnitude = 1.0
scaling_curve = SubResource("Curve_levels")

[sub_resource type="Resource" id="Effect_might"]
script = ExtResource("2_effect")
policy = 2
modifiers = Array[ExtResource("3_modifier")]([SubResource("Modifier_attack")])

[resource]
script = ExtResource("1_def")
id = &"might"
display_name = "Might"
description = "All your hits deal more damage."
icon = ExtResource("4_icon")
min_grade = 1
passive_effect = SubResource("Effect_might")
```
(`operation = 0` is ADD, `1` is PERCENT_ADD; `policy = 2` is INFINITE; `min_grade = 1` is COMMON.)

- [ ] **Step 6: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/data/test_ability_data tools/run_tests.sh` and `FILTER=tests/stage/test_stage tools/run_tests.sh`
Expected: 3 + 11 PASS. Likely snags:
- A level-6 value is wrong or every value is 0: the curve was clamped on load — replace `_limits = [...]` by the four explicit properties placed before `_data` (`min_value = 0.0`, `max_value = 250.0`, `min_domain = 1.0`, `max_domain = 6.0`) and re-run; or `magnitude` is missing (defaults to 0).
- `Texture2D` cannot load: the editor has not imported the PNGs yet — scan again and wait a few seconds; make sure the file is a real copy (no symlink).
- `GameplayTags.Event_Kill` unknown: `sync_tags` did not run or the scan is stale.

- [ ] **Step 7: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add tools godot_gas src/stage assets data/abilities tests
git commit -q -m "feat(abilities): add Event.Kill from the stage and the four stat passives as data

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Volley — PeriodicAbility base and N arrows at the N nearest enemies

**Files:**
- Modify: `src/components/target_provider.gd` (`nearest_enemies`), `assets/SOURCES.md`, `tests/data/test_ability_data.gd` (append)
- Create: `src/abilities/periodic_ability.gd`, `src/abilities/volley/volley_ability.gd`, `src/abilities/volley/volley_ability.tscn`, `data/abilities/volley.tres`; icons `assets/art/icons/prismatic_knives_green.png`, `assets/art/icons/arcane_knives_purple.png`
- Test: `tests/components/test_target_provider.gd` (append), `tests/abilities/test_volley_ability.gd`

**Interfaces:**
- Consumes: `ActorAbility` (`context`, `animation_requested`), `AbilityContext.targets/actor/projectile_layer` (Task 5), `Projectile.launch(def, shooter_asc, damage, direction)` (`src/actors/projectile/projectile.gd:24`), `res://src/actors/projectile/projectile.tscn`, `res://data/projectiles/arrow.tres`, `AnimNames.ATTACK`, GAS `GameplayAbility.try_activate()` (may be called without `await` when `_activate_ability` never suspends — `ArrowAbility` does the same), `is_active`, `ability_level`.
- Produces:
  - `TargetProvider.nearest_enemies(origin: Vector2, max_range: float, count: int) -> Array[Node2D]` — up to `count` living enemies within range, nearest first (empty for `count <= 0`).
  - `PeriodicAbility` (abstract `ActorAbility`): `@export cooldown: Curve`, `@export auto_fire := true`; `cooldown_seconds() -> float` (`cooldown.sample(ability_level)`); `step(delta)` (counts the timer down and calls `try_activate()` when idle, bound, not active and off cooldown — `_physics_process` just calls it); `start_cooldown()` (subclasses call it after actually firing, so a miss costs nothing); `validate() -> PackedStringArray` (reports a missing curve; subclasses extend it with `super.validate()`).
  - `VolleyAbility` (`PeriodicAbility`, scene `res://src/abilities/volley/volley_ability.tscn`, `ability_tag = &"Ability.Volley"`): `@export projectile_scene, projectile_def, arrow_count: Curve, damage: Curve, attack_range := 300.0`; fires one arrow per target at the `roundi(arrow_count.sample(level))` nearest enemies within range; no target → returns false without cooldown. Scene curves: cooldown 2.4/2.1/1.8/1.5/1.2/0.9 s, arrow_count 2/3/4/5/6/8, damage 8/10/13/17/22/30.
  - `data/abilities/volley.tres`: `id = &"volley"`, `ability_scene = volley_ability.tscn`, icon `prismatic_knives_green.png`, revealed icon `arcane_knives_purple.png` (both 128x128 CC0 from `rpg-icons-3`).

- [ ] **Step 1: Write the failing tests**

Append to `tests/components/test_target_provider.gd`:
```gdscript
func test_nearest_enemies_returns_up_to_count_sorted_by_distance() -> void:
	var provider := _provider()
	var fifty := _enemy(Vector2(50, 0))
	_enemy(Vector2(150, 0))
	_enemy(Vector2(400, 0))
	var ninety := _enemy(Vector2(0, 90))
	var two := provider.nearest_enemies(Vector2.ZERO, 300.0, 2)
	assert_eq(two.size(), 2)
	assert_eq(two[0], fifty)
	assert_eq(two[1], ninety)
	assert_eq(provider.nearest_enemies(Vector2.ZERO, 300.0, 10).size(), 3, "the one at 400 is out of range")
	assert_true(provider.nearest_enemies(Vector2.ZERO, 300.0, 0).is_empty())


func test_nearest_enemies_skips_doomed_nodes() -> void:
	var provider := _provider()
	var doomed := _enemy(Vector2(10, 0))
	var other := _enemy(Vector2(80, 0))
	doomed.queue_free()
	assert_eq(provider.nearest_enemies(Vector2.ZERO, 300.0, 5), [other])
```

`tests/abilities/test_volley_ability.gd`:
```gdscript
extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const VOLLEY_SCENE: PackedScene = preload("res://src/abilities/volley/volley_ability.tscn")


## Player rig with the scene arrow silenced so only the volley spawns projectiles.
func _player() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	player.setup(profile, targets, layer, Wallet.new())
	targets.player = player
	(player.asc.get_node("ArrowAbility") as ArrowAbility).auto_fire = false
	return [player, layer, targets]


func _volley(player: Player, level: float) -> VolleyAbility:
	var ability := VOLLEY_SCENE.instantiate() as VolleyAbility
	ability.ability_level = level
	ability.set_physics_process(false)  # tests drive step() by hand
	player.grant_ability(ability)
	return ability


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _victim_at(position: Vector2) -> Node2D:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	actor.global_position = position
	actor.add_to_group(Groups.ENEMIES)
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = PhysicsLayers.ENEMY_HURT
	hurtbox.collision_mask = 0
	hurtbox.monitoring = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	hurtbox.add_child(shape)
	actor.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(actor), actor)
	return actor


func _projectiles(layer: Node2D) -> Array[Projectile]:
	var found: Array[Projectile] = []
	for child in layer.get_children():
		if child is Projectile:
			found.append(child)
	return found


func test_fires_one_arrow_at_each_of_the_nearest_enemies_up_to_the_level_count() -> void:
	var rig := _player()
	var volley := _volley(rig[0], 2.0)  # arrow_count 3
	_enemy_at(Vector2(100, 0))
	_enemy_at(Vector2(-120, 0))
	_enemy_at(Vector2(0, 140))
	_enemy_at(Vector2(0, -160))
	_enemy_at(Vector2(500, 0))
	var animations: Array = []
	volley.animation_requested.connect(func(animation: StringName, _facing: Vector2) -> void: animations.append(animation))
	volley.step(0.1)
	var arrows := _projectiles(rig[1])
	assert_eq(arrows.size(), 3)
	var headings: Array = []
	for arrow in arrows:
		headings.append(snappedf(rad_to_deg(arrow.rotation), 1.0))
	headings.sort()
	assert_eq(headings, [0.0, 90.0, 180.0], "toward the three nearest (the one at -160 and the far one are skipped)")
	assert_eq(animations, [AnimNames.ATTACK])


func test_respects_the_cooldown_curve() -> void:
	var rig := _player()
	var volley := _volley(rig[0], 2.0)  # cooldown 2.1 s
	_enemy_at(Vector2(100, 0))
	volley.step(0.1)
	assert_eq(_projectiles(rig[1]).size(), 3)
	volley.step(1.0)
	assert_eq(_projectiles(rig[1]).size(), 3, "still cooling down")
	volley.step(1.2)
	assert_eq(_projectiles(rig[1]).size(), 6, "fired again after 2.3 s")


func test_does_not_fire_or_start_a_cooldown_without_targets() -> void:
	var rig := _player()
	var volley := _volley(rig[0], 1.0)
	volley.step(0.1)
	volley.step(0.1)
	assert_true(_projectiles(rig[1]).is_empty())
	_enemy_at(Vector2(100, 0))
	_enemy_at(Vector2(0, 100))
	volley.step(0.0)
	assert_eq(_projectiles(rig[1]).size(), 2, "fires at once: a miss started no cooldown")


func test_level_six_fires_eight_arrows() -> void:
	var rig := _player()
	var volley := _volley(rig[0], 6.0)
	for i in 9:
		_enemy_at(Vector2.from_angle(i * TAU / 9.0) * 120.0)
	volley.step(0.1)
	assert_eq(_projectiles(rig[1]).size(), 8)


func test_arrow_damage_follows_the_damage_curve() -> void:
	var rig := _player()
	var volley := _volley(rig[0], 3.0)  # damage 13
	var victim := _victim_at(Vector2(60, 0))
	volley.step(0.1)
	await wait_seconds(0.4)
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 87.0)


func test_validate_reports_missing_curves_and_scene_data() -> void:
	var volley := VOLLEY_SCENE.instantiate() as VolleyAbility
	assert_no_errors(volley.validate(), "scene as shipped")
	volley.cooldown = null
	assert_eq(volley.validate().size(), 1)
	volley.arrow_count = null
	volley.projectile_def = null
	assert_eq(volley.validate().size(), 3)
	volley.free()
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities/test_volley tools/run_tests.sh`, `FILTER=tests/components/test_target_provider tools/run_tests.sh`
Expected: parse errors — `VolleyAbility`, the scene and `nearest_enemies` do not exist.

- [ ] **Step 3: Implement**

Append to `src/components/target_provider.gd`:
```gdscript
## Up to `count` living enemies within `max_range`, nearest first.
func nearest_enemies(origin: Vector2, max_range: float, count: int) -> Array[Node2D]:
	var found: Array[Node2D] = []
	if count <= 0:
		return found
	var limit := max_range * max_range
	for node in get_tree().get_nodes_in_group(Groups.ENEMIES):
		var enemy := node as Node2D
		if enemy == null or enemy.is_queued_for_deletion():
			continue
		if origin.distance_squared_to(enemy.global_position) <= limit:
			found.append(enemy)
	found.sort_custom(func(a: Node2D, b: Node2D) -> bool:
		return origin.distance_squared_to(a.global_position) < origin.distance_squared_to(b.global_position))
	if found.size() > count:
		found.resize(count)
	return found
```

`src/abilities/periodic_ability.gd`:
```gdscript
@abstract
class_name PeriodicAbility
extends ActorAbility
## An auto-firing ability whose cooldown is a Curve sampled at `ability_level`. It keeps its own timer
## instead of a GAS cooldown effect, so the cooldown can scale with the grade and tests can drive it
## through `step(delta)` after `set_physics_process(false)`.

@export var cooldown: Curve
@export var auto_fire: bool = true

var _cooldown_left: float = 0.0


func cooldown_seconds() -> float:
	return cooldown.sample(ability_level) if cooldown != null else INF


## Called by AbilityDef.validate() on an instance of the scene (not in the tree).
func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if cooldown == null:
		errors.append("%s: cooldown curve is null" % ability_name)
	return errors


func step(delta: float) -> void:
	_cooldown_left = maxf(_cooldown_left - delta, 0.0)
	if not auto_fire or context == null or is_active or _cooldown_left > 0.0:
		return
	try_activate()


## Subclasses call this once they have actually fired; a miss costs nothing.
func start_cooldown() -> void:
	_cooldown_left = cooldown_seconds()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/abilities/volley/volley_ability.gd`:
```gdscript
class_name VolleyAbility
extends PeriodicAbility
## Fires one arrow at each of the N nearest enemies in range. N and damage are curves over the grade level.

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var arrow_count: Curve
@export var damage: Curve
@export var attack_range: float = 300.0


func validate() -> PackedStringArray:
	var errors := super.validate()
	if projectile_scene == null or projectile_def == null:
		errors.append("Volley: projectile_scene and projectile_def are required")
	if arrow_count == null or damage == null:
		errors.append("Volley: arrow_count and damage curves are required")
	return errors


func _activate_ability() -> bool:
	var origin := context.actor.global_position
	var targets := context.targets.nearest_enemies(origin, attack_range, roundi(arrow_count.sample(ability_level)))
	if targets.is_empty():
		return false
	start_cooldown()
	var amount := damage.sample(ability_level)
	for target in targets:
		var projectile := projectile_scene.instantiate() as Projectile
		context.projectile_layer.add_child(projectile)
		projectile.global_position = origin
		projectile.launch(projectile_def, owner_asc, amount, (target.global_position - origin).normalized())
	animation_requested.emit(AnimNames.ATTACK, (targets[0].global_position - origin).normalized())
	return true
```

`src/abilities/volley/volley_ability.tscn`:
```
[gd_scene load_steps=7 format=3]

[ext_resource type="Script" path="res://src/abilities/volley/volley_ability.gd" id="1_volley"]
[ext_resource type="PackedScene" path="res://src/actors/projectile/projectile.tscn" id="2_projectile"]
[ext_resource type="Resource" path="res://data/projectiles/arrow.tres" id="3_arrow"]

[sub_resource type="Curve" id="Curve_cooldown"]
_limits = [0.0, 3.0, 1.0, 6.0]
_data = [Vector2(1, 2.4), 0.0, 0.0, 0, 0, Vector2(2, 2.1), 0.0, 0.0, 0, 0, Vector2(3, 1.8), 0.0, 0.0, 0, 0, Vector2(4, 1.5), 0.0, 0.0, 0, 0, Vector2(5, 1.2), 0.0, 0.0, 0, 0, Vector2(6, 0.9), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Curve" id="Curve_count"]
_limits = [0.0, 10.0, 1.0, 6.0]
_data = [Vector2(1, 2), 0.0, 0.0, 0, 0, Vector2(2, 3), 0.0, 0.0, 0, 0, Vector2(3, 4), 0.0, 0.0, 0, 0, Vector2(4, 5), 0.0, 0.0, 0, 0, Vector2(5, 6), 0.0, 0.0, 0, 0, Vector2(6, 8), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Curve" id="Curve_damage"]
_limits = [0.0, 40.0, 1.0, 6.0]
_data = [Vector2(1, 8), 0.0, 0.0, 0, 0, Vector2(2, 10), 0.0, 0.0, 0, 0, Vector2(3, 13), 0.0, 0.0, 0, 0, Vector2(4, 17), 0.0, 0.0, 0, 0, Vector2(5, 22), 0.0, 0.0, 0, 0, Vector2(6, 30), 0.0, 0.0, 0, 0]
point_count = 6

[node name="VolleyAbility" type="Node"]
script = ExtResource("1_volley")
ability_name = "Volley"
ability_tag = &"Ability.Volley"
cooldown = SubResource("Curve_cooldown")
projectile_scene = ExtResource("2_projectile")
projectile_def = ExtResource("3_arrow")
arrow_count = SubResource("Curve_count")
damage = SubResource("Curve_damage")
attack_range = 300.0
```

Icons and data:
```bash
cp /d/CM/Godot/res/opengameart/2D/rpg-icons-3/icons128/prismatic_knives_green.png /d/CM/Godot/res/opengameart/2D/rpg-icons-3/icons128/arcane_knives_purple.png assets/art/icons/
```
Append to `assets/SOURCES.md`:
```
| `art/icons/{prismatic_knives_green,arcane_knives_purple,whirlwind_orange,dual_swords}.png` | OpenGameArt "RPG Icons" by Viktor Hahn (`opengameart/2D/rpg-icons-3/icons128`, 128x128, opaque background) | CC0 (`license.txt` in the pack) |
```

`data/abilities/volley.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=5 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="PackedScene" path="res://src/abilities/volley/volley_ability.tscn" id="2_scene"]
[ext_resource type="Texture2D" path="res://assets/art/icons/prismatic_knives_green.png" id="3_icon"]
[ext_resource type="Texture2D" path="res://assets/art/icons/arcane_knives_purple.png" id="4_revealed"]

[resource]
script = ExtResource("1_def")
id = &"volley"
display_name = "Volley"
description = "Periodically looses extra arrows at the nearest enemies."
icon = ExtResource("3_icon")
revealed_icon = ExtResource("4_revealed")
min_grade = 1
ability_scene = ExtResource("2_scene")
```

Append to `tests/data/test_ability_data.gd`:
```gdscript
func test_volley_def_validates_and_carries_the_scene() -> void:
	var def := _def("res://data/abilities/volley.tres")
	assert_no_errors(def.validate(), "volley")
	assert_null(def.passive_effect)
	var root := def.ability_scene.instantiate()
	assert_true(root is VolleyAbility)
	root.free()
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities/test_volley tools/run_tests.sh`, `FILTER=tests/components/test_target_provider tools/run_tests.sh`, `FILTER=tests/data tools/run_tests.sh`
Expected: 6 + 7 + 4 (+ Task 2's) PASS. Likely snags:
- 4 projectiles instead of 3 in the first test: the scene `ArrowAbility` fired too — the rig must set `auto_fire = false` on it.
- `headings` differ by sign: `Projectile.launch` sets `rotation = direction.angle()`; `Vector2(-1, 0).angle()` is `+PI` (180°), `Vector2(0, 1).angle()` is 90° (y points down in 2D) — if a platform reports `-180.0`, map with `fposmod(deg, 360.0)` before sorting and keep the expectation.
- Health 87 not reached: the arrow needs physics frames — `wait_seconds(0.4)` covers 60 px at 420 px/s; if the victim's hurtbox never fires, check the `Hurtbox` layer (`ENEMY_HURT = 16`) and that `monitorable` stays true.
- `validate()` counts: as shipped 0; `cooldown = null` → 1 (base); `arrow_count`/`projectile_def` null → +2 (one line each).

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/components src/abilities assets data/abilities tests
git commit -q -m "feat(abilities): add PeriodicAbility base and the Volley attack

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Blade Sweep — MeleeStrike and a radial periodic hit

**Files:**
- Create: `src/abilities/blade_sweep/melee_strike.gd`, `src/abilities/blade_sweep/melee_strike.tscn`, `src/abilities/blade_sweep/blade_sweep_ability.gd`, `src/abilities/blade_sweep/blade_sweep_ability.tscn`, `data/abilities/blade_sweep.tres`; icons `assets/art/icons/whirlwind_orange.png`, `assets/art/icons/dual_swords.png`
- Modify: `tests/data/test_ability_data.gd` (append)
- Test: `tests/abilities/test_melee_strike.gd`, `tests/abilities/test_blade_sweep_ability.gd`

**Interfaces:**
- Consumes: `Hitbox` (`hurtbox_entered(hurtbox)`, `once_per_target`, `src/components/hitbox.gd`), `EffectApplier.bind(source_asc)` / `hit(hurtbox, effect, amount, causer)`, `Lifetime.restart(seconds)` / `expired`, `PeriodicAbility` (Task 8), `TargetProvider.nearest_enemy(origin, range)`, `res://data/effects/damage_basic.tres`, `PhysicsLayers.ENEMY_HURT = 16`.
- Produces:
  - `MeleeStrike` (Node2D scene `res://src/abilities/blade_sweep/melee_strike.tscn`): `strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float)` after entering the tree; damages every enemy hurtbox it overlaps once (hitbox mask `ENEMY_HURT`), draws a ring, frees itself when its lifetime expires.
  - `BladeSweepAbility` (`PeriodicAbility`, scene `res://src/abilities/blade_sweep/blade_sweep_ability.tscn`, `ability_tag = &"Ability.BladeSweep"`): `@export strike_scene, hit_effect, radius: Curve, damage: Curve, strike_seconds := 0.2`; fires only when an enemy is within the level's radius. Scene curves: cooldown 1.6/1.4/1.2/1.0/0.85/0.7 s, radius 40/48/56/66/78/95 px, damage 10/14/19/25/33/45.
  - `data/abilities/blade_sweep.tres`: `id = &"blade_sweep"`, icon `whirlwind_orange.png`, revealed `dual_swords.png`.

- [ ] **Step 1: Write the failing tests**

`tests/abilities/test_melee_strike.gd`:
```gdscript
extends TestCase

const STRIKE_SCENE: PackedScene = preload("res://src/abilities/blade_sweep/melee_strike.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _victim_at(position: Vector2) -> Node2D:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	actor.global_position = position
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = PhysicsLayers.ENEMY_HURT
	hurtbox.collision_mask = 0
	hurtbox.monitoring = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	hurtbox.add_child(shape)
	actor.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(actor), actor)
	return actor


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func test_strike_damages_each_hurtbox_in_radius_once_then_expires() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var near_a := _victim_at(Vector2(20, 0))
	var near_b := _victim_at(Vector2(-30, 10))
	var far := _victim_at(Vector2(200, 0))
	var strike := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 50.0, 0.3)
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(near_a), 85.0)
	assert_almost_eq(_health(near_b), 85.0)
	assert_almost_eq(_health(far), 100.0)
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(near_a), 85.0, 0.001, "once per target")
	await wait_seconds(0.4)
	assert_false(is_instance_valid(strike), "freed when the lifetime expires")


func test_strike_radius_is_applied_to_its_own_shape() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var first := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	var second := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	first.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 95.0, 0.3)
	second.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 40.0, 0.3)
	assert_almost_eq((first.get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 95.0)
	assert_almost_eq((second.get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 40.0, 0.001, "shapes are local to each instance")
```

`tests/abilities/test_blade_sweep_ability.gd`:
```gdscript
extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const SWEEP_SCENE: PackedScene = preload("res://src/abilities/blade_sweep/blade_sweep_ability.tscn")


func _player() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	player.setup(profile, targets, layer, Wallet.new())
	targets.player = player
	(player.asc.get_node("ArrowAbility") as ArrowAbility).auto_fire = false
	return [player, layer, targets]


func _sweep(player: Player, level: float) -> BladeSweepAbility:
	var ability := SWEEP_SCENE.instantiate() as BladeSweepAbility
	ability.ability_level = level
	ability.set_physics_process(false)
	player.grant_ability(ability)
	return ability


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _victim_at(position: Vector2) -> Node2D:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	actor.global_position = position
	actor.add_to_group(Groups.ENEMIES)
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = PhysicsLayers.ENEMY_HURT
	hurtbox.collision_mask = 0
	hurtbox.monitoring = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	hurtbox.add_child(shape)
	actor.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(actor), actor)
	return actor


func _strikes(layer: Node2D) -> Array[MeleeStrike]:
	var found: Array[MeleeStrike] = []
	for child in layer.get_children():
		if child is MeleeStrike:
			found.append(child)
	return found


func test_sweeps_only_when_an_enemy_is_within_the_level_radius() -> void:
	var rig := _player()
	var sweep := _sweep(rig[0], 1.0)  # radius 40
	var enemy := _enemy_at(Vector2(60, 0))
	sweep.step(0.1)
	assert_true(_strikes(rig[1]).is_empty(), "60 px is outside a 40 px sweep")
	enemy.global_position = Vector2(30, 0)
	sweep.step(0.1)
	var strikes := _strikes(rig[1])
	assert_eq(strikes.size(), 1)
	assert_almost_eq((strikes[0].get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 40.0)
	assert_almost_eq(strikes[0].global_position.x, 0.0, 0.001, "centred on the player")


func test_cooldown_follows_the_curve() -> void:
	var rig := _player()
	var sweep := _sweep(rig[0], 2.0)  # cooldown 1.4 s
	_enemy_at(Vector2(30, 0))
	sweep.step(0.1)
	assert_eq(_strikes(rig[1]).size(), 1)
	sweep.step(1.0)
	assert_eq(_strikes(rig[1]).size(), 1, "still cooling down")
	sweep.step(0.5)
	assert_eq(_strikes(rig[1]).size(), 2)


func test_damage_and_radius_scale_with_level() -> void:
	var rig := _player()
	var sweep := _sweep(rig[0], 6.0)  # radius 95, damage 45
	var victim := _victim_at(Vector2(80, 0))
	sweep.step(0.1)
	assert_eq(_strikes(rig[1]).size(), 1, "80 px is inside the level-6 radius")
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 55.0)


func test_validate_reports_missing_data() -> void:
	var sweep := SWEEP_SCENE.instantiate() as BladeSweepAbility
	assert_no_errors(sweep.validate(), "scene as shipped")
	sweep.radius = null
	sweep.hit_effect = null
	assert_eq(sweep.validate().size(), 2)
	sweep.free()
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities/test_melee_strike tools/run_tests.sh`, `FILTER=tests/abilities/test_blade_sweep tools/run_tests.sh`
Expected: parse errors — scenes and classes missing.

- [ ] **Step 3: Implement**

`src/abilities/blade_sweep/melee_strike.gd`:
```gdscript
class_name MeleeStrike
extends Node2D
## A short-lived circular hit area: damages every enemy hurtbox it overlaps once, draws a ring, then
## frees itself. Reuses Hitbox + EffectApplier + Lifetime so damage takes the same path as projectiles.

const RING_COLOR: Color = Color(1.0, 0.9, 0.6, 0.8)
const RING_WIDTH: float = 2.0
const RING_SEGMENTS: int = 32

var _effect: GameplayEffect
var _damage: float = 0.0
var _radius: float = 0.0

@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the strike has entered the tree.
func strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float) -> void:
	_effect = effect
	_damage = damage
	_radius = radius
	(_hit_shape.shape as CircleShape2D).radius = radius
	_applier.bind(attacker_asc)
	_lifetime.restart(seconds)
	queue_redraw()


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	_applier.hit(hurtbox, _effect, _damage, self)


func _draw() -> void:
	if _radius > 0.0:
		draw_arc(Vector2.ZERO, _radius, 0.0, TAU, RING_SEGMENTS, RING_COLOR, RING_WIDTH)
```

`src/abilities/blade_sweep/melee_strike.tscn`:
```
[gd_scene load_steps=6 format=3]

[ext_resource type="Script" path="res://src/abilities/blade_sweep/melee_strike.gd" id="1_strike"]
[ext_resource type="Script" path="res://src/components/hitbox.gd" id="2_hitbox"]
[ext_resource type="Script" path="res://src/components/effect_applier.gd" id="3_applier"]
[ext_resource type="Script" path="res://src/components/lifetime.gd" id="4_lifetime"]

[sub_resource type="CircleShape2D" id="CircleShape2D_hit"]
resource_local_to_scene = true
radius = 40.0

[node name="MeleeStrike" type="Node2D"]
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

`src/abilities/blade_sweep/blade_sweep_ability.gd`:
```gdscript
class_name BladeSweepAbility
extends PeriodicAbility
## A radial melee hit around the owner whenever an enemy is within reach. Radius and damage are curves
## over the grade level; the strike itself is a MeleeStrike spawned on the projectile layer.

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var radius: Curve
@export var damage: Curve
## How long the strike area stays active; it must outlive one physics step so overlaps are reported.
@export var strike_seconds: float = 0.2


func validate() -> PackedStringArray:
	var errors := super.validate()
	if strike_scene == null or hit_effect == null:
		errors.append("Blade Sweep: strike_scene and hit_effect are required")
	if radius == null or damage == null:
		errors.append("Blade Sweep: radius and damage curves are required")
	if strike_seconds <= 0.0:
		errors.append("Blade Sweep: strike_seconds must be > 0")
	return errors


func _activate_ability() -> bool:
	var origin := context.actor.global_position
	var reach := radius.sample(ability_level)
	var target := context.targets.nearest_enemy(origin, reach)
	if target == null:
		return false
	start_cooldown()
	var strike := strike_scene.instantiate() as MeleeStrike
	context.projectile_layer.add_child(strike)
	strike.global_position = origin
	strike.strike(owner_asc, hit_effect, damage.sample(ability_level), reach, strike_seconds)
	animation_requested.emit(AnimNames.ATTACK, (target.global_position - origin).normalized())
	return true
```

`src/abilities/blade_sweep/blade_sweep_ability.tscn`:
```
[gd_scene load_steps=7 format=3]

[ext_resource type="Script" path="res://src/abilities/blade_sweep/blade_sweep_ability.gd" id="1_sweep"]
[ext_resource type="PackedScene" path="res://src/abilities/blade_sweep/melee_strike.tscn" id="2_strike"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[sub_resource type="Curve" id="Curve_cooldown"]
_limits = [0.0, 2.0, 1.0, 6.0]
_data = [Vector2(1, 1.6), 0.0, 0.0, 0, 0, Vector2(2, 1.4), 0.0, 0.0, 0, 0, Vector2(3, 1.2), 0.0, 0.0, 0, 0, Vector2(4, 1.0), 0.0, 0.0, 0, 0, Vector2(5, 0.85), 0.0, 0.0, 0, 0, Vector2(6, 0.7), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Curve" id="Curve_radius"]
_limits = [0.0, 100.0, 1.0, 6.0]
_data = [Vector2(1, 40), 0.0, 0.0, 0, 0, Vector2(2, 48), 0.0, 0.0, 0, 0, Vector2(3, 56), 0.0, 0.0, 0, 0, Vector2(4, 66), 0.0, 0.0, 0, 0, Vector2(5, 78), 0.0, 0.0, 0, 0, Vector2(6, 95), 0.0, 0.0, 0, 0]
point_count = 6

[sub_resource type="Curve" id="Curve_damage"]
_limits = [0.0, 50.0, 1.0, 6.0]
_data = [Vector2(1, 10), 0.0, 0.0, 0, 0, Vector2(2, 14), 0.0, 0.0, 0, 0, Vector2(3, 19), 0.0, 0.0, 0, 0, Vector2(4, 25), 0.0, 0.0, 0, 0, Vector2(5, 33), 0.0, 0.0, 0, 0, Vector2(6, 45), 0.0, 0.0, 0, 0]
point_count = 6

[node name="BladeSweepAbility" type="Node"]
script = ExtResource("1_sweep")
ability_name = "Blade Sweep"
ability_tag = &"Ability.BladeSweep"
cooldown = SubResource("Curve_cooldown")
strike_scene = ExtResource("2_strike")
hit_effect = ExtResource("3_damage")
radius = SubResource("Curve_radius")
damage = SubResource("Curve_damage")
strike_seconds = 0.2
```

Icons and data:
```bash
cp /d/CM/Godot/res/opengameart/2D/rpg-icons-3/icons128/whirlwind_orange.png /d/CM/Godot/res/opengameart/2D/rpg-icons-3/icons128/dual_swords.png assets/art/icons/
```
(The `SOURCES.md` row added in Task 8 already names both files.)

`data/abilities/blade_sweep.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=5 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="PackedScene" path="res://src/abilities/blade_sweep/blade_sweep_ability.tscn" id="2_scene"]
[ext_resource type="Texture2D" path="res://assets/art/icons/whirlwind_orange.png" id="3_icon"]
[ext_resource type="Texture2D" path="res://assets/art/icons/dual_swords.png" id="4_revealed"]

[resource]
script = ExtResource("1_def")
id = &"blade_sweep"
display_name = "Blade Sweep"
description = "A spinning strike that hits every enemy around you."
icon = ExtResource("3_icon")
revealed_icon = ExtResource("4_revealed")
min_grade = 1
ability_scene = ExtResource("2_scene")
```

Append to `tests/data/test_ability_data.gd`:
```gdscript
func test_blade_sweep_def_validates_and_carries_the_scene() -> void:
	var def := _def("res://data/abilities/blade_sweep.tres")
	assert_no_errors(def.validate(), "blade_sweep")
	var root := def.ability_scene.instantiate()
	assert_true(root is BladeSweepAbility)
	root.free()
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities/test_melee_strike tools/run_tests.sh`, `FILTER=tests/abilities/test_blade_sweep tools/run_tests.sh`, `FILTER=tests/data tools/run_tests.sh`
Expected: 2 + 4 + all data tests PASS. Likely snags:
- Nobody takes damage: `area_entered` needs the strike's hitbox to be *monitoring* (Area2D default) and the hurtbox *monitorable* (default); two physics frames after `strike()` are required; the strike is added under the test host at the origin, so victims at ±30 px overlap a 50 px circle.
- Both strikes report the same radius: the `CircleShape2D` must be `resource_local_to_scene = true` in `melee_strike.tscn`.
- The strike is freed before overlaps are reported: `strike_seconds` below one physics step (1/60 s) — keep 0.2.

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/abilities assets data/abilities tests
git commit -q -m "feat(abilities): add Blade Sweep with a reusable MeleeStrike hit area

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Bounty — gold on every kill

**Files:**
- Create: `src/abilities/bounty/bounty_ability.gd`, `src/abilities/bounty/bounty_ability.tscn`, `data/abilities/bounty.tres`; icon `assets/art/icons/fortune_cookie.png`
- Modify: `assets/SOURCES.md`, `tests/data/test_ability_data.gd` (append)
- Test: `tests/abilities/test_bounty_ability.gd`

**Interfaces:**
- Consumes: `ActorAbility`, `AbilityContext.wallet` (Task 5), `GameplayTags.Event_Kill` (Task 7), GAS `trigger_event_tag` + `send_gameplay_event` (an event with a non-spec payload calls `try_activate(payload)`; the ability reads `current_event_payload` during activation), `Wallet.add`.
- Produces: `BountyAbility` (`ActorAbility`, scene `res://src/abilities/bounty/bounty_ability.tscn`, `ability_tag = &"Ability.Bounty"`, `trigger_event_tag = &"Event.Kill"`): `@export gold_per_kill: Curve` (1/2/3/5/8/12); `validate()`; on activation adds `roundi(gold_per_kill.sample(ability_level))` gold to `context.wallet` and returns true (false, nothing added, without a context/wallet/curve). `data/abilities/bounty.tres`: `id = &"bounty"`, icon `fortune_cookie.png` (16x16, tinted when revealed).

- [ ] **Step 1: Write the failing tests**

`tests/abilities/test_bounty_ability.gd`:
```gdscript
extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const BOUNTY_SCENE: PackedScene = preload("res://src/abilities/bounty/bounty_ability.tscn")


func _rig(level: float) -> Dictionary:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	var wallet := Wallet.new()
	player.setup(profile, targets, layer, wallet)
	var bounty := BOUNTY_SCENE.instantiate() as BountyAbility
	bounty.ability_level = level
	player.grant_ability(bounty)
	return {"player": player, "wallet": wallet, "bounty": bounty}


func test_pays_gold_for_every_kill_event_at_the_level_amount() -> void:
	var rig := _rig(4.0)  # 5 gold per kill
	var player: Player = rig["player"]
	var wallet: Wallet = rig["wallet"]
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq(wallet.gold, 5)
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, adopt(Node2D.new()))
	assert_eq(wallet.gold, 10, "payload type does not matter")
	assert_eq(wallet.stone, 0)


func test_level_one_and_six_pay_the_curve_ends() -> void:
	var low := _rig(1.0)
	(low["player"] as Player).asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((low["wallet"] as Wallet).gold, 1)
	var high := _rig(6.0)
	(high["player"] as Player).asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((high["wallet"] as Wallet).gold, 12)


func test_ignores_other_events() -> void:
	var rig := _rig(2.0)
	(rig["player"] as Player).asc.send_gameplay_event(GameplayTags.Ability_Dash, null)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_revoked_bounty_stops_paying() -> void:
	var rig := _rig(2.0)
	var player: Player = rig["player"]
	player.revoke_ability(rig["bounty"])
	await next_frame()
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_without_a_wallet_it_fails_quietly() -> void:
	var rig := _rig(2.0)
	var bounty: BountyAbility = rig["bounty"]
	bounty.bind_context(AbilityContext.new(rig["player"], null, null, null, null))
	var paid: bool = await bounty.try_activate()
	assert_false(paid)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_validate_requires_the_curve() -> void:
	var bounty := BOUNTY_SCENE.instantiate() as BountyAbility
	assert_no_errors(bounty.validate(), "scene as shipped")
	bounty.gold_per_kill = null
	assert_eq(bounty.validate().size(), 1)
	bounty.free()
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/abilities/test_bounty tools/run_tests.sh`
Expected: parse error — `BountyAbility`/scene missing.

- [ ] **Step 3: Implement**

`src/abilities/bounty/bounty_ability.gd`:
```gdscript
class_name BountyAbility
extends ActorAbility
## Pays gold into the run's wallet every time the owner is credited with a kill (Event.Kill).
## The amount is a curve over the grade level. The ASC activates it through `trigger_event_tag`.

@export var gold_per_kill: Curve


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if gold_per_kill == null:
		errors.append("Bounty: gold_per_kill curve is null")
	return errors


func _activate_ability() -> bool:
	if context == null or context.wallet == null or gold_per_kill == null:
		return false
	context.wallet.add(Wallet.Kind.GOLD, roundi(gold_per_kill.sample(ability_level)))
	return true
```

`src/abilities/bounty/bounty_ability.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/abilities/bounty/bounty_ability.gd" id="1_bounty"]

[sub_resource type="Curve" id="Curve_gold"]
_limits = [0.0, 15.0, 1.0, 6.0]
_data = [Vector2(1, 1), 0.0, 0.0, 0, 0, Vector2(2, 2), 0.0, 0.0, 0, 0, Vector2(3, 3), 0.0, 0.0, 0, 0, Vector2(4, 5), 0.0, 0.0, 0, 0, Vector2(5, 8), 0.0, 0.0, 0, 0, Vector2(6, 12), 0.0, 0.0, 0, 0]
point_count = 6

[node name="BountyAbility" type="Node"]
script = ExtResource("1_bounty")
ability_name = "Bounty"
ability_tag = &"Ability.Bounty"
trigger_event_tag = &"Event.Kill"
gold_per_kill = SubResource("Curve_gold")
```

Icon and data:
```bash
cp /d/CM/Godot/res/opengameart/2D/16x16-food/food_0/food/fortune_cookie.png assets/art/icons/
```
Append to `assets/SOURCES.md`:
```
| `art/icons/fortune_cookie.png` | OpenGameArt "16x16 Food" by ARoachIFoundOnMyPillow (`opengameart/2D/16x16-food`) | CC0 |
```

`data/abilities/bounty.tres`:
```
[gd_resource type="Resource" script_class="AbilityDef" load_steps=4 format=3]

[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="1_def"]
[ext_resource type="PackedScene" path="res://src/abilities/bounty/bounty_ability.tscn" id="2_scene"]
[ext_resource type="Texture2D" path="res://assets/art/icons/fortune_cookie.png" id="3_icon"]

[resource]
script = ExtResource("1_def")
id = &"bounty"
display_name = "Bounty"
description = "Every kill pays extra gold."
icon = ExtResource("3_icon")
min_grade = 1
ability_scene = ExtResource("2_scene")
```

Append to `tests/data/test_ability_data.gd`:
```gdscript
func test_bounty_def_validates_and_listens_for_kills() -> void:
	var def := _def("res://data/abilities/bounty.tres")
	assert_no_errors(def.validate(), "bounty")
	var root := def.ability_scene.instantiate() as GameplayAbility
	assert_true(root is BountyAbility)
	assert_eq(root.trigger_event_tag, GameplayTags.Event_Kill)
	root.free()
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/abilities/test_bounty tools/run_tests.sh`, `FILTER=tests/data tools/run_tests.sh`
Expected: 6 + all data tests PASS. Likely snag: `wallet.gold` stays 0 — the ability was not granted (the scene root must be under the ASC: `Player.grant_ability` does that) or `trigger_event_tag` in the scene is misspelled (`&"Event.Kill"`, matched exactly by `send_gameplay_event`).

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/abilities assets data/abilities tests
git commit -q -m "feat(abilities): add Bounty, an on-kill gold hybrid triggered by Event.Kill

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: AbilityBar and AbilitySlotView in the HUD

**Files:**
- Create: `src/ui/hud/ability_slot_view.gd`, `src/ui/hud/ability_slot_view.tscn`, `src/ui/hud/ability_bar.gd`, `src/ui/hud/ability_bar.tscn`
- Modify: `src/ui/hud/hud.gd`, `src/ui/hud/hud.tscn`
- Test: `tests/ui/test_ability_bar.gd`, `tests/ui/test_hud.gd` (append)

**Interfaces:**
- Consumes: `Loadout` (`slot_changed`, `slot`, `size`), `AbilitySlot`, `GradeService` (`promote_cost`, `reroll_cost`, `resolved`), `GradeResult.Outcome`, `Price.is_affordable`, `Wallet.changed`, `Grade.color/display_name`, `AbilityDef.icon_for/display_name/revealed_icon` (Tasks 1–4). `hud.tscn` root `Hud` (CanvasLayer) with child `Root` (Control, `mouse_filter = 2`).
- Produces:
  - `AbilitySlotView` (Control scene `res://src/ui/hud/ability_slot_view.tscn`, 56x56): `slot_index: int`; `show_slot(slot: AbilitySlot, price: Price, affordable: bool, reroll_price: Price = null)`; static `cost_text(price) -> String` (`"100g"`, `"10s"`, `"MAX"`); `is_dimmed()`, `is_flashing()`, `flash()`; signals `promote_requested(index)` (left click) and `reroll_requested(index)` (Ctrl + left click); other buttons, releases and key events do nothing. Draws a grade-coloured frame; a revealed slot without `revealed_icon` tints the icon.
  - `AbilityBar` (HBoxContainer scene `res://src/ui/hud/ability_bar.tscn`): `bind(loadout, wallet, service)` (rebinding replaces the views and listeners); `slot_view(index) -> AbilitySlotView`; `refresh_all()`; signals `promote_requested(index)`, `reroll_requested(index)` forwarded from the views; flashes a slot when `service.resolved` reports a non-OK outcome. Never mutates models.
  - `Hud.ability_bar: AbilityBar` (`%AbilityBar`, bottom-centre of the HUD; unbound until `Main` binds it).

- [ ] **Step 1: Write the failing tests**

`tests/ui/test_ability_bar.gd`:
```gdscript
extends TestCase

const BAR_SCENE: PackedScene = preload("res://src/ui/hud/ability_bar.tscn")


func _click(ctrl: bool, button: MouseButton = MOUSE_BUTTON_LEFT, pressed: bool = true) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = pressed
	event.ctrl_pressed = ctrl
	return event


func _rig(gold: int = 120, stone: int = 5) -> Dictionary:
	var def := DefFactory.vitality()
	var defs: Array[AbilityDef] = [def]
	var loadout := Loadout.new(3)
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, gold)
	wallet.add(Wallet.Kind.STONE, stone)
	var service := GradeService.new(loadout, AbilityPool.new(defs), DefFactory.grade_table(), wallet, RunRng.new(2))
	var bar := adopt(BAR_SCENE.instantiate()) as AbilityBar
	bar.bind(loadout, wallet, service)
	return {"bar": bar, "loadout": loadout, "wallet": wallet, "service": service, "def": def}


func _cost_text(view: AbilitySlotView) -> String:
	return (view.get_node("%Cost") as Label).text


func _icon(view: AbilitySlotView) -> TextureRect:
	return view.get_node("%Icon") as TextureRect


func test_creates_one_view_per_slot_showing_the_next_cost() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	var loadout: Loadout = rig["loadout"]
	assert_eq(bar.get_child_count(), 3)
	assert_null(bar.slot_view(3))
	for i in 3:
		assert_eq(bar.slot_view(i).slot_index, i)
		assert_eq(_cost_text(bar.slot_view(i)), "100g")
		assert_null(_icon(bar.slot_view(i)).texture)
	loadout.set_slot(1, rig["def"], Grade.Value.LEGENDARY)
	assert_eq(_cost_text(bar.slot_view(1)), "10s")
	assert_eq(_icon(bar.slot_view(1)).texture, (rig["def"] as AbilityDef).icon)
	loadout.set_slot(1, rig["def"], Grade.Value.REVEALED)
	assert_eq(_cost_text(bar.slot_view(1)), "MAX")
	assert_eq(_cost_text(bar.slot_view(0)), "100g", "other slots untouched")


func test_click_emits_promote_and_ctrl_click_emits_reroll() -> void:
	var bar: AbilityBar = _rig()["bar"]
	var promotes: Array = []
	var rerolls: Array = []
	bar.promote_requested.connect(func(index: int) -> void: promotes.append(index))
	bar.reroll_requested.connect(func(index: int) -> void: rerolls.append(index))
	bar.slot_view(1)._gui_input(_click(false))
	bar.slot_view(2)._gui_input(_click(true))
	bar.slot_view(0)._gui_input(_click(false, MOUSE_BUTTON_RIGHT))
	bar.slot_view(0)._gui_input(_click(true, MOUSE_BUTTON_LEFT, false))
	bar.slot_view(0)._gui_input(InputEventKey.new())
	assert_eq(promotes, [1])
	assert_eq(rerolls, [2])


func test_the_bar_only_emits_requests() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	bar.slot_view(0)._gui_input(_click(false))
	bar.slot_view(0)._gui_input(_click(true))
	assert_eq((rig["wallet"] as Wallet).gold, 120, "no spending from the view")
	assert_true((rig["loadout"] as Loadout).slot(0).is_empty(), "no slot change from the view")


func test_unaffordable_slots_are_dimmed_and_follow_the_wallet() -> void:
	var rig := _rig(120, 0)
	var bar: AbilityBar = rig["bar"]
	var wallet: Wallet = rig["wallet"]
	var loadout: Loadout = rig["loadout"]
	assert_false(bar.slot_view(0).is_dimmed(), "120 gold covers 100")
	wallet.try_spend(Wallet.Kind.GOLD, 100)
	assert_true(bar.slot_view(0).is_dimmed(), "20 gold left")
	assert_true(bar.slot_view(2).is_dimmed(), "every empty slot")
	wallet.add(Wallet.Kind.GOLD, 100)
	assert_false(bar.slot_view(0).is_dimmed())
	loadout.set_slot(0, rig["def"], Grade.Value.LEGENDARY)
	assert_true(bar.slot_view(0).is_dimmed(), "10 stone needed, 0 owned")
	loadout.set_slot(0, rig["def"], Grade.Value.REVEALED)
	assert_false(bar.slot_view(0).is_dimmed(), "nothing to buy is not unaffordable")


func test_failed_actions_flash_the_slot_and_successful_ones_do_not() -> void:
	var rig := _rig(100, 0)
	var bar: AbilityBar = rig["bar"]
	var service: GradeService = rig["service"]
	assert_eq(service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_false(bar.slot_view(0).is_flashing())
	assert_eq(service.promote(1).outcome, GradeResult.Outcome.NOT_ENOUGH)
	assert_true(bar.slot_view(1).is_flashing())
	assert_false(bar.slot_view(0).is_flashing(), "only the refused slot")
	await wait_seconds(0.4)
	assert_false(bar.slot_view(1).is_flashing(), "the flash fades")


func test_revealed_slot_uses_the_revealed_icon_or_a_shine_tint() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	var def: AbilityDef = rig["def"]
	var loadout: Loadout = rig["loadout"]
	loadout.set_slot(0, def, Grade.Value.LEGENDARY)
	assert_eq(_icon(bar.slot_view(0)).modulate, Color.WHITE, "not revealed: no tint")
	loadout.set_slot(0, def, Grade.Value.REVEALED)
	assert_eq(_icon(bar.slot_view(0)).texture, def.icon)
	assert_true(_icon(bar.slot_view(0)).modulate != Color.WHITE, "tinted: no revealed icon")
	def.revealed_icon = DefFactory.texture(16, 16)
	loadout.set_slot(0, def, Grade.Value.REVEALED)
	assert_eq(_icon(bar.slot_view(0)).texture, def.revealed_icon)
	assert_eq(_icon(bar.slot_view(0)).modulate, Color.WHITE)


func test_rebinding_replaces_the_views_without_duplicate_listeners() -> void:
	var rig := _rig()
	var bar: AbilityBar = rig["bar"]
	var second := Loadout.new(2)
	bar.bind(second, rig["wallet"], rig["service"])
	await next_frame()
	assert_eq(bar.get_child_count(), 2)
	var promotes: Array = []
	bar.promote_requested.connect(func(index: int) -> void: promotes.append(index))
	bar.slot_view(0)._gui_input(_click(false))
	assert_eq(promotes, [0])
	(rig["wallet"] as Wallet).add(Wallet.Kind.GOLD, 1)
	assert_eq(bar.get_child_count(), 2, "the old listeners are gone")
```

Append to `tests/ui/test_hud.gd`:
```gdscript
func test_hud_hosts_an_unbound_ability_bar() -> void:
	var hud: Hud = _rig()["hud"]
	assert_not_null(hud.ability_bar)
	assert_eq(hud.ability_bar.get_child_count(), 0, "empty until Main binds it")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/ui tools/run_tests.sh`
Expected: parse errors — `AbilityBar`, `AbilitySlotView`, `Hud.ability_bar` missing.

- [ ] **Step 3: Implement the slot view**

`src/ui/hud/ability_slot_view.gd`:
```gdscript
class_name AbilitySlotView
extends Control
## One HUD slot: the ability's icon inside a grade-coloured frame and the cost of its next promotion.
## A view only: clicks become request signals; the app decides what happens.

signal promote_requested(index: int)
signal reroll_requested(index: int)

const EMPTY_FRAME_COLOR: Color = Color(0.3, 0.3, 0.33)
const BACKGROUND_COLOR: Color = Color(0.08, 0.08, 0.1, 0.85)
const FRAME_WIDTH: float = 3.0
const DIM_ALPHA: float = 0.35
const REVEALED_TINT: Color = Color(1.0, 0.95, 0.55)
const FLASH_COLOR: Color = Color(1.0, 0.35, 0.35)
const FLASH_SECONDS: float = 0.25

var slot_index: int = 0

var _frame_color: Color = EMPTY_FRAME_COLOR
var _dimmed: bool = false

@onready var _icon: TextureRect = %Icon
@onready var _cost: Label = %Cost


## `price` is the next promotion's price (null = nothing left to promote); `affordable` whether the
## wallet covers it; `reroll_price` only feeds the tooltip.
func show_slot(slot: AbilitySlot, price: Price, affordable: bool, reroll_price: Price = null) -> void:
	var filled := slot != null and not slot.is_empty()
	_frame_color = Grade.color(slot.grade) if filled else EMPTY_FRAME_COLOR
	_icon.texture = slot.def.icon_for(slot.grade) if filled else null
	var tint_revealed := filled and slot.grade == Grade.Value.REVEALED and slot.def.revealed_icon == null
	_icon.modulate = REVEALED_TINT if tint_revealed else Color.WHITE
	_dimmed = price != null and not affordable
	_icon.self_modulate = Color(1.0, 1.0, 1.0, DIM_ALPHA if _dimmed else 1.0)
	_cost.text = cost_text(price)
	tooltip_text = _tooltip(slot, price, reroll_price)
	queue_redraw()


static func cost_text(price: Price) -> String:
	if price == null:
		return "MAX"
	return "%d%s" % [price.amount, "g" if price.kind == Wallet.Kind.GOLD else "s"]


func is_dimmed() -> bool:
	return _dimmed


func is_flashing() -> bool:
	return modulate != Color.WHITE


## Brief red tint for a refused action.
func flash() -> void:
	modulate = FLASH_COLOR
	create_tween().tween_property(self, "modulate", Color.WHITE, FLASH_SECONDS)


func _gui_input(event: InputEvent) -> void:
	var click := event as InputEventMouseButton
	if click == null or not click.pressed or click.button_index != MOUSE_BUTTON_LEFT:
		return
	if click.ctrl_pressed:
		reroll_requested.emit(slot_index)
	else:
		promote_requested.emit(slot_index)
	accept_event()


func _draw() -> void:
	var rect := Rect2(Vector2.ZERO, size)
	draw_rect(rect, BACKGROUND_COLOR)
	draw_rect(rect, _frame_color, false, FRAME_WIDTH)


func _tooltip(slot: AbilitySlot, price: Price, reroll_price: Price) -> String:
	if slot == null or slot.is_empty():
		return "Empty slot\nClick: buy a Common ability (%s)" % cost_text(price)
	var text := "%s (%s)\nClick: promote (%s)" % [slot.def.display_name, Grade.display_name(slot.grade), cost_text(price)]
	if reroll_price != null:
		text += "\nCtrl+click: reroll (%s)" % cost_text(reroll_price)
	return text
```

`src/ui/hud/ability_slot_view.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://src/ui/hud/ability_slot_view.gd" id="1_view"]

[node name="AbilitySlotView" type="Control"]
custom_minimum_size = Vector2(56, 56)
layout_mode = 3
anchors_preset = 0
offset_right = 56.0
offset_bottom = 56.0
mouse_filter = 0
script = ExtResource("1_view")

[node name="Icon" type="TextureRect" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = 8.0
offset_top = 6.0
offset_right = -8.0
offset_bottom = -14.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2
expand_mode = 1
stretch_mode = 5

[node name="Cost" type="Label" parent="."]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 12
anchor_top = 1.0
anchor_right = 1.0
anchor_bottom = 1.0
offset_top = -16.0
grow_horizontal = 2
grow_vertical = 0
mouse_filter = 2
theme_override_font_sizes/font_size = 10
horizontal_alignment = 1
```
(`expand_mode = 1` ignores the texture size, `stretch_mode = 5` keeps the aspect centred, so 16x16 and 128x128 icons both fill the slot; the project's nearest filter keeps pixel art crisp.)

- [ ] **Step 4: Implement the bar and host it in the HUD**

`src/ui/hud/ability_bar.gd`:
```gdscript
class_name AbilityBar
extends HBoxContainer
## The row of ability slots. A view: it reads Loadout, Wallet and GradeService through signals and
## only emits requests; it never mutates them.

signal promote_requested(index: int)
signal reroll_requested(index: int)

const SLOT_SCENE: PackedScene = preload("res://src/ui/hud/ability_slot_view.tscn")

var _loadout: Loadout
var _wallet: Wallet
var _service: GradeService
var _views: Array[AbilitySlotView] = []


func bind(loadout: Loadout, wallet: Wallet, service: GradeService) -> void:
	_unbind()
	_loadout = loadout
	_wallet = wallet
	_service = service
	for index in loadout.size():
		var view := SLOT_SCENE.instantiate() as AbilitySlotView
		view.slot_index = index
		add_child(view)
		view.promote_requested.connect(promote_requested.emit)
		view.reroll_requested.connect(reroll_requested.emit)
		_views.append(view)
	loadout.slot_changed.connect(_on_slot_changed)
	wallet.changed.connect(_on_wallet_changed)
	service.resolved.connect(_on_resolved)
	refresh_all()


func slot_view(index: int) -> AbilitySlotView:
	if index < 0 or index >= _views.size():
		return null
	return _views[index]


func refresh_all() -> void:
	for index in _views.size():
		_refresh(index)


func _refresh(index: int) -> void:
	var price := _service.promote_cost(index)
	var affordable := price != null and price.is_affordable(_wallet)
	_views[index].show_slot(_loadout.slot(index), price, affordable, _service.reroll_cost(index))


func _on_slot_changed(index: int, _slot: AbilitySlot) -> void:
	if index >= 0 and index < _views.size():
		_refresh(index)


func _on_wallet_changed(_gold: int, _stone: int) -> void:
	refresh_all()


func _on_resolved(index: int, result: GradeResult) -> void:
	var view := slot_view(index)
	if view != null and result.outcome != GradeResult.Outcome.OK:
		view.flash()


func _unbind() -> void:
	if _loadout != null and _loadout.slot_changed.is_connected(_on_slot_changed):
		_loadout.slot_changed.disconnect(_on_slot_changed)
	if _wallet != null and _wallet.changed.is_connected(_on_wallet_changed):
		_wallet.changed.disconnect(_on_wallet_changed)
	if _service != null and _service.resolved.is_connected(_on_resolved):
		_service.resolved.disconnect(_on_resolved)
	for view in _views:
		remove_child(view)
		view.queue_free()
	_views.clear()
```

`src/ui/hud/ability_bar.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://src/ui/hud/ability_bar.gd" id="1_bar"]

[node name="AbilityBar" type="HBoxContainer"]
mouse_filter = 2
theme_override_constants/separation = 6
alignment = 1
script = ExtResource("1_bar")
```

`src/ui/hud/hud.tscn` — change `load_steps=3` to `load_steps=4`, add the ext resource after the timeline script:
```
[ext_resource type="PackedScene" path="res://src/ui/hud/ability_bar.tscn" id="3_bar"]
```
and append this node at the end of the file:
```
[node name="AbilityBar" parent="Root" instance=ExtResource("3_bar")]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 7
anchor_left = 0.5
anchor_top = 1.0
anchor_right = 0.5
anchor_bottom = 1.0
offset_left = -186.0
offset_top = -76.0
offset_right = 186.0
offset_bottom = -16.0
grow_horizontal = 2
grow_vertical = 0
```

`src/ui/hud/hud.gd` — add the public bar above the private `@onready` vars:
```gdscript
## Bound by Main (the stage layer must not know the HUD); empty until then.
@onready var ability_bar: AbilityBar = %AbilityBar
```

- [ ] **Step 5: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/ui tools/run_tests.sh`
Expected: 7 bar tests + 4 hud tests + the Phase 1 ui tests PASS. Likely snags:
- `%Icon`/`%Cost` null: `unique_name_in_owner = true` must be on those nodes in `ability_slot_view.tscn`.
- `is_flashing()` false right after a refused action: `GradeService.resolved` must be connected in `bind`, and `flash()` sets `modulate` synchronously before the tween.
- `get_child_count()` still 3 after rebinding: `_unbind` must `remove_child` (queue_free alone leaves the node until the frame ends).
- Mouse clicks do not reach the slots in the editor: the slot's `mouse_filter` must be 0 (STOP); the bar's and `Root`'s are 2 (IGNORE) on purpose.

- [ ] **Step 6: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/ui tests/ui
git commit -q -m "feat(ui): add AbilityBar with six slot views (click promote, ctrl+click reroll)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Stage composition and Main routing, with an integration test

**Files:**
- Modify: `src/stage/stage.gd`, `src/stage/stage.tscn`, `src/app/main.gd`
- Test: `tests/stage/test_stage.gd` (append)

**Interfaces:**
- Consumes: `Loadout`, `AbilityPool`, `GradeService`, `AbilityInstaller` (Tasks 3, 4, 6), `PlayerProfile.slot_count/abilities`, `StageDef.grade_table` (Task 2), `Hud.ability_bar` + `AbilityBar.bind/promote_requested/reroll_requested` (Task 11), the ability data files (Tasks 7–10).
- Produces:
  - `Stage.loadout: Loadout` and `Stage.grade_service: GradeService`, built in `start()` from the profile's `slot_count`/`abilities`, the stage's `grade_table`, and the run's wallet and rng; a `Systems/AbilityInstaller` node bound to `(loadout, player)`.
  - `Main` binds `_hud.ability_bar` to `(stage.loadout, stage.run_state.wallet, stage.grade_service)` and routes `promote_requested`/`reroll_requested` to `grade_service.promote/reroll` while the run is in progress (`_result == null`); after the run ends clicks are ignored.

- [ ] **Step 1: Write the failing tests**

Append to `tests/stage/test_stage.gd` (add the three constants next to the existing ones):
```gdscript
const VITALITY: AbilityDef = preload("res://data/abilities/vitality.tres")
const VOLLEY: AbilityDef = preload("res://data/abilities/volley.tres")
const BOUNTY: AbilityDef = preload("res://data/abilities/bounty.tres")


## A stage whose profile offers `abilities`; `quiet` removes every enemy so nothing dies or drops.
func _shop_stage(abilities: Array[AbilityDef], gold: int, stone: int, quiet: bool = true) -> Stage:
	var def := _stage_def(60.0)
	if quiet:
		def.trickle_enemies.clear()
		def.waves.entries.clear()
	def.start_gold = gold
	def.start_stone = stone
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	profile.slot_count = 2
	profile.abilities = abilities
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_true(stage.start(def, profile, 11))
	return stage


func test_stage_builds_the_loadout_and_grade_service_from_the_profile() -> void:
	var defs: Array[AbilityDef] = [VITALITY]
	var stage := _shop_stage(defs, 200, 0)
	assert_eq(stage.loadout.size(), 2)
	assert_eq(stage.run_state.wallet.gold, 200)
	assert_eq(stage.grade_service.promote_cost(0).amount, 100)
	assert_null(stage.grade_service.reroll_cost(0))


func test_buying_promoting_and_revealing_through_the_service_changes_the_player() -> void:
	var defs: Array[AbilityDef] = [VITALITY]
	var stage := _shop_stage(defs, 1500, 20)
	var asc := stage.player.asc
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.gold, 1400)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 120.0)
	for i in 4:
		assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.loadout.slot(0).grade, Grade.Value.LEGENDARY)
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 260.0)
	assert_eq(stage.grade_service.reroll(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.stone, 18)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 260.0, 0.001, "reinstalled, not stacked")
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.loadout.slot(0).grade, Grade.Value.REVEALED)
	assert_eq(stage.loadout.slot(0).def, VITALITY)
	assert_eq(stage.run_state.wallet.stone, 8)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 340.0)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_eq(stage.run_state.wallet.stone, 8)


func test_an_attack_ability_is_granted_to_the_player_asc() -> void:
	var defs: Array[AbilityDef] = [VOLLEY]
	var stage := _shop_stage(defs, 100, 0)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	var volley: VolleyAbility = null
	for child in stage.player.asc.get_children():
		if child is VolleyAbility:
			volley = child
	assert_not_null(volley)
	assert_almost_eq(volley.ability_level, 1.0)
	assert_true(volley.context == stage.player.ability_context)
	assert_true(volley.is_physics_processing(), "auto-fires in the running stage")


func test_bounty_pays_gold_when_the_stage_reports_a_kill() -> void:
	var defs: Array[AbilityDef] = [BOUNTY]
	var stage := _shop_stage(defs, 100, 0, false)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.gold, 0)
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		_kill(stage, victim.get_node("AbilitySystemComponent"))
	assert_eq(stage.run_state.wallet.gold, victims.size(), "1 gold per kill at COMMON; the drops are never collected")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/stage/test_stage tools/run_tests.sh`
Expected: parse errors — `Stage.loadout`/`grade_service` missing.

- [ ] **Step 3: Implement**

`src/stage/stage.tscn` — change `load_steps=11` to `load_steps=12`, add after the loot director ext resource:
```
[ext_resource type="Script" path="res://src/abilities/ability_installer.gd" id="11_installer"]
```
and append at the end:
```
[node name="AbilityInstaller" type="Node" parent="Systems"]
script = ExtResource("11_installer")
```

`src/stage/stage.gd` (whole file; the kill event from Task 7 is kept):
```gdscript
class_name Stage
extends Node2D
## Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the
## signals between modules, and reports a RunResult. Modules never look each other up.
## The stage knows nothing about UI: whoever presents it binds to the public models below.

signal finished(result: RunResult)

const PLAYER_START: Vector2 = Vector2(0.0, 150.0)

var run_state: RunState
## The ability slots and the shop rules over them; the HUD binds to these through Main.
var loadout: Loadout
var grade_service: GradeService

var _flow := StageFlow.new()

@onready var player: Player = $World/Player
@onready var building: Building = $World/Building
@onready var clock: StageClock = $Systems/StageClock
@onready var waves: WaveDirector = $Systems/WaveDirector
@onready var _world: Node2D = $World
@onready var _arena: Arena = $Arena
@onready var _enemy_layer: Node2D = $World/Enemies
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
	_loot.bind(_factory, _pickup_layer, player, run_state.wallet, stage_def.drop_multiplier)
	# The shop: pure rules over the run's wallet and rng; the installer mirrors slots onto the player.
	loadout = Loadout.new(profile.slot_count)
	grade_service = GradeService.new(loadout, AbilityPool.new(profile.abilities), stage_def.grade_table, run_state.wallet, run_state.rng)
	_installer.bind(loadout, player)
	_wire()
	_flow.transition_to(StageFlow.State.RUNNING)
	clock.start(stage_def.duration)
	return true


func _wire() -> void:
	building.destroyed.connect(_finish.bind(RunResult.Outcome.FAILED))
	player.died.connect(_finish.bind(RunResult.Outcome.FAILED))
	clock.ended.connect(_finish.bind(RunResult.Outcome.CLEARED))
	_factory.spawned.connect(_on_enemy_spawned)


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
	clock.stop()
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	finished.emit(run_state.to_result(outcome, clock.elapsed))
```

`src/app/main.gd` (whole file):
```gdscript
extends Node
## Entry point: runs the default stage with its HUD, routes HUD requests to the stage, and restarts
## on demand. Phase 4 replaces this with the menu flow.

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const HUD_SCENE: PackedScene = preload("res://src/ui/hud/hud.tscn")
const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")
const PROFILE: PlayerProfile = preload("res://data/characters/default_profile.tres")

const BUILDING_BAR_OFFSET := Vector2(0, -60)

var _stage: Stage
var _hud: Hud
var _building_bar: WorldHealthBar
var _result: RunResult


func _ready() -> void:
	_start_run()


func _unhandled_input(event: InputEvent) -> void:
	if _result != null and event.is_action_pressed(InputActions.RESTART):
		_start_run()


func _start_run() -> void:
	_discard_run()
	_result = null
	_stage = STAGE_SCENE.instantiate() as Stage
	_hud = HUD_SCENE.instantiate() as Hud
	_building_bar = WorldHealthBar.new()
	add_child(_stage)
	add_child(_building_bar)
	add_child(_hud)
	if not _stage.start(STAGE_DEF, PROFILE):
		get_tree().quit(1)
		return
	_hud.bind(_stage.player.health, _stage.building.health, _stage.run_state.wallet, _stage.clock, _stage.waves.timeline)
	_hud.ability_bar.bind(_stage.loadout, _stage.run_state.wallet, _stage.grade_service)
	_hud.ability_bar.promote_requested.connect(_on_promote_requested)
	_hud.ability_bar.reroll_requested.connect(_on_reroll_requested)
	_building_bar.bind(_stage.building.health, _stage.building, BUILDING_BAR_OFFSET)
	_stage.finished.connect(_on_finished)


func _discard_run() -> void:
	for node: Node in [_stage, _building_bar, _hud]:
		if node != null:
			remove_child(node)
			node.queue_free()


## The HUD only asks; the app decides. The shop closes with the run.
func _on_promote_requested(index: int) -> void:
	if _result == null:
		_stage.grade_service.promote(index)


func _on_reroll_requested(index: int) -> void:
	if _result == null:
		_stage.grade_service.reroll(index)


func _on_finished(result: RunResult) -> void:
	_result = result
	_hud.show_result(result)
	print("[Main] %s after %.1fs - kills %d, gold %d, stone %d" % [
		RunResult.Outcome.keys()[result.outcome], result.elapsed, result.kills, result.gold, result.stone,
	])
```

- [ ] **Step 4: Scan, run to verify pass**

godot-ai `filesystem_manage` `{"op": "scan"}`, then `FILTER=tests/stage/test_stage tools/run_tests.sh`
Expected: 15 PASS (the Phase 1 ten, the Task 7 kill test, the four new). Likely snags:
- `$Systems/AbilityInstaller` null: the node must be declared with `parent="Systems"` in `stage.tscn`.
- Bounty test gold off by the drops: the player must not collect pickups — it stands at `(0, 150)` with a 70 px magnet while pickups land on the 520 px ring; if gold exceeds the kill count, an enemy walked into the magnet range — kill earlier (shorten the `wait_seconds` to 0.6).
- `max_health` 280 after the reroll: the installer stacked instead of replacing — `Loadout.set_slot` emitted but the installer missed the uninstall (Task 6 regression).

- [ ] **Step 5: Run the whole suite and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS.
```bash
git add src/stage src/app tests/stage
git commit -q -m "feat(stage): compose the ability shop in Stage and route HUD slot requests from Main

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Ship the data, in-editor acceptance, documentation sync

**Files:**
- Modify: `data/stages/stage_01.tres` (`drop_multiplier`), `data/characters/default_profile.tres` (slots + abilities), `tests/data/test_game_data.gd`, `CLAUDE.md`, `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`, `docs/superpowers/specs/2026-10-13-phase2-ability-system-design.md` (a short "as built" note)

**Interfaces:**
- Consumes: everything above; godot-ai `project_run`, `logs_read`, `editor_screenshot`/game screenshot, `project_manage` stop (the same acceptance tools as the Phase 1 Task 13).
- Produces: `stage_01.tres` with `drop_multiplier = 10000.0`; `default_profile.tres` with `slot_count = 6` and the seven abilities; docs that match the code.

- [ ] **Step 1: Write the failing data tests**

In `tests/data/test_game_data.gd` add to `DATA_FILES`:
```gdscript
	"res://data/abilities/prospector.tres",
	"res://data/abilities/swift_boots.tres",
	"res://data/abilities/vitality.tres",
	"res://data/abilities/might.tres",
	"res://data/abilities/volley.tres",
	"res://data/abilities/blade_sweep.tres",
	"res://data/abilities/bounty.tres",
```
and append:
```gdscript
func test_default_profile_offers_the_seven_phase_2_abilities() -> void:
	var profile := load("res://data/characters/default_profile.tres") as PlayerProfile
	assert_eq(profile.slot_count, 6)
	var ids: Array = []
	for ability in profile.abilities:
		ids.append(String(ability.id))
	ids.sort()
	assert_eq(ids, ["blade_sweep", "bounty", "might", "prospector", "swift_boots", "vitality", "volley"])
	assert_no_errors(profile.validate(), "default_profile")


func test_stage_01_uses_the_prototype_drop_multiplier() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_almost_eq(stage.drop_multiplier, 10000.0)
```
Run: `FILTER=tests/data/test_game_data tools/run_tests.sh` → the two new tests FAIL.

- [ ] **Step 2: Update the data**

`data/stages/stage_01.tres`: change `drop_multiplier = 1.0` to `drop_multiplier = 10000.0` (prototype tuning so every grade is reachable in a short run; set it back to 1.0 for balanced play).

`data/characters/default_profile.tres` (whole file):
```
[gd_resource type="Resource" script_class="PlayerProfile" load_steps=12 format=3]

[ext_resource type="Script" path="res://src/defs/player_profile.gd" id="1_profile"]
[ext_resource type="Script" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="Resource" path="res://data/characters/soldier_visual.tres" id="3_visual"]
[ext_resource type="Script" path="res://src/defs/ability_def.gd" id="4_ability"]
[ext_resource type="Resource" path="res://data/abilities/prospector.tres" id="5_prospector"]
[ext_resource type="Resource" path="res://data/abilities/swift_boots.tres" id="6_swift"]
[ext_resource type="Resource" path="res://data/abilities/vitality.tres" id="7_vitality"]
[ext_resource type="Resource" path="res://data/abilities/might.tres" id="8_might"]
[ext_resource type="Resource" path="res://data/abilities/volley.tres" id="9_volley"]
[ext_resource type="Resource" path="res://data/abilities/blade_sweep.tres" id="10_sweep"]
[ext_resource type="Resource" path="res://data/abilities/bounty.tres" id="11_bounty"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
max_health = 100.0
move_speed = 140.0
attack = 1.0

[resource]
script = ExtResource("1_profile")
stats = SubResource("Resource_stats")
visual = ExtResource("3_visual")
slot_count = 6
abilities = Array[ExtResource("4_ability")]([ExtResource("5_prospector"), ExtResource("6_swift"), ExtResource("7_vitality"), ExtResource("8_might"), ExtResource("9_volley"), ExtResource("10_sweep"), ExtResource("11_bounty")])
```

Run: `FILTER=tests/data tools/run_tests.sh` → all PASS, then `timeout 120 tools/run_tests.sh` → all PASS.

- [ ] **Step 3: Automated smoke run in the editor**

1. godot-ai `project_run` (main scene); wait ~5 s; `logs_read` → **no** errors or warnings from our scripts (`Stage.start` validation errors here mean a data file is wrong — fix the `.tres`, not the code).
2. Screenshot of the running game: six framed slots along the bottom centre, each labelled `100g`; wallet `Gold 200   Stone 0` top-left; the Phase 1 HUD unchanged.
3. Let it run ~20 s so the first kills happen: the wallet jumps by thousands per coin (drop multiplier 10000) and stone appears after a few kills.
4. Stop the run (`project_manage` stop).

- [ ] **Step 4: Manual acceptance checklist** (play ~3 minutes; every line is a pass/fail — record failures and fix them inside this task before committing)

- Clicking an empty slot with ≥ 100 gold fills it with a random Common ability: icon appears, frame turns grey-white, the label shows `150g`, gold drops by 100. With < 100 gold the slot is dimmed and a click only flashes it red.
- Clicking a filled slot promotes it: the frame colour walks grey → blue → purple → orange → gold (Legendary), costs 150/250/400/600 gold, and the ability may change (the icon changes).
- On a Legendary slot the label reads `10s`; clicking with ≥ 10 stone turns it Revealed (cyan frame, revealed icon or shiny tint, label `MAX`); further clicks do nothing.
- Ctrl+click on a filled slot costs 2 stone and usually swaps the ability at the same grade (same frame colour); Ctrl+click on an empty or Revealed slot only flashes.
- Vitality raises the player bar's maximum (the bar is no longer full); Swift Boots visibly speeds the soldier; Might makes arrows kill orcs in fewer hits.
- Volley shoots several arrows at once toward the nearest enemies; Blade Sweep draws a ring around the soldier and damages adjacent orcs; Bounty makes gold tick up on every kill without picking anything up.
- Two slots holding the same passive stack (e.g. two Vitality slots → a larger bar).
- Clicking on slots never moves the soldier, dashes, or shoots; the game keeps running while shopping (no pause).
- After GAME OVER / STAGE CLEARED the slots ignore clicks; **R** restarts with 200 gold, empty slots and base stats.
- `logs_read` shows no script errors after the session.
- Frame rate stays smooth during the biggest wave with Volley at Legendary (8 arrows every 1.2 s).

- [ ] **Step 5: Sync the documentation**

Edit `CLAUDE.md`:
1. In **Commands**, extend the tag line to: "Rebuild gameplay tags after editing `TAGS` in `tools/sync_tags.gd`: `Godot --headless --path . -s res://tools/sync_tags.gd`, then rescan the editor filesystem (godot-ai `filesystem_manage` scan) before running tests."
2. In **Architecture rules**, append:
```markdown
- Abilities: `src/abilities/` holds the shop (`Loadout`/`AbilitySlot`, `AbilityPool`, `GradeService`) and the
  `AbilityInstaller` that mirrors slots onto the player's ASC (passives = INFINITE effects held by handle,
  attacks = `ActorAbility` scenes granted through `Player.grant_ability`). Grade maps to GAS `ability_level`
  (COMMON 1 … REVEALED 6); every ability number is a `Curve` sampled at that level. `Stage` exposes `loadout`
  and `grade_service`; `Main` binds `Hud.ability_bar` and routes its `promote_requested`/`reroll_requested`
  signals — the HUD never mutates models. `Stage` sends `Event.Kill` (payload: the enemy) to the player's ASC
  on every enemy death; on-kill abilities use `trigger_event_tag`.
- Attack cooldowns live in `PeriodicAbility` (a curve + `step(delta)`), not in GAS cooldown effects; a
  promoted/rerolled attack starts with a fresh cooldown.
```
3. Add a section after **Adding an enemy**:
```markdown
## Adding an ability (data only)
1. Duplicate a file in `data/abilities/` (`vitality.tres` for a passive, `volley.tres` for an attack): set `id`,
   names, `icon` (and `revealed_icon` or rely on the shine tint), `min_grade`. A passive is an INFINITE
   `GameplayEffect` whose modifiers use `scaling_curve` (6 points, x = grade level 1..6) with `magnitude = 1.0`
   (GAS multiplies the curve by it — 0 zeroes every grade; `validate()` catches it). An attack points
   `ability_scene` at a scene whose root extends `ActorAbility` (usually `PeriodicAbility`) with its numbers as
   exported curves; the root may implement `validate()`.
2. Add it to `PlayerProfile.abilities` in `data/characters/default_profile.tres`; `Stage.start` validates it.
   Add the file to `DATA_FILES` in `tests/data/test_game_data.gd` and, for a passive, to `PASSIVES` in
   `tests/data/test_ability_data.gd`.
3. Shop prices live in `data/grade_table.tres`; the run's start gold/stone and `drop_multiplier` in the
   `StageDef` (`stage_01.tres` uses 10000 for prototyping — set it to 1 for balanced play).
```

Edit the parent spec `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`:
1. §1.3 default 1: replace "Shop (ability promote/reroll) is a pause overlay available at any time during a stage; only currency gates it." with "The shop is the six HUD slots (click = promote, Ctrl+click = reroll), usable at any time while the stage keeps running; only currency gates it (Phase 2)."
2. §3.1 folder tree: `abilities/` line → `ability_slot, loadout, ability_pool, grade_result, grade_service, ability_installer, periodic_ability, volley/, blade_sweep/, bounty/`; `defs/` `[P2]` list → `ability_def, grade_table` (there is no `ability_pool` def); `core/` gains `grade, price`; `ui/hud/` gains `ability_bar, ability_slot_view` (drop `[P2] shop/`); `data/` gains `abilities/` and `grade_table.tres`.
3. §3.2: add rows for `core/Grade`, `core/Price`, `defs/AbilityDef`, `defs/GradeTable`, `abilities/Loadout`, `abilities/AbilityPool`, `abilities/GradeService`, `abilities/AbilityInstaller`, `abilities/PeriodicAbility`, `abilities/MeleeStrike`, `ui/hud/AbilityBar` with the interfaces from this plan's File Structure, and extend the `actors/*` row with `Player.ability_context/grant_ability/revoke_ability`, the `components/ActorAbility / AbilityContext` row with `wallet`, `stage/LootDirector` with `drop_multiplier`, `stage/Stage` with `loadout`, `grade_service`, `Event.Kill`.
4. §4 Phase 2: replace the paragraph with "Phase 2 — Ability system (built): `Grade`, `Price`, `AbilityDef` (passive INFINITE effect and/or ability scene, curves over grade level), `GradeTable`; `Loadout`/`AbilitySlot`, `AbilityPool`, `GradeService` (atomic promote/reveal/reroll over Wallet + RunRng, `GradeResult` outcomes), `AbilityInstaller` (handles per slot); `Player.grant_ability/revoke_ability`; `Event.Kill` sent by `Stage`; HUD `AbilityBar` bound by `Main`. Abilities: Prospector, Swift Boots, Vitality, Might (passives), Volley, Blade Sweep (`PeriodicAbility` scenes), Bounty (on-kill hybrid). Prices 100/150/250/400/600 gold, reveal 10 stone, reroll 2 stone; 200 start gold; `drop_multiplier` 10000 in `stage_01` for prototyping."
5. Add "## 8. Phase 2 notes" after §7: acceptance evidence from Step 3/4 (one line), and known follow-ups: "`max_health` passives do not heal and nothing re-clamps health when `max_health` drops (removing a Vitality slot leaves health above the new maximum until the next hit)"; "attack cooldowns reset when a slot is promoted/rerolled (fresh instance)"; "rerolling a one-ability pool returns the same ability and still charges"; "the 128x128 rpg-icons have an opaque background"; "selling/removing abilities is out of scope".

Append to `docs/superpowers/specs/2026-10-13-phase2-ability-system-design.md` a short "## 11. As built (2026-10-13)" listing the deviations named in this plan's File Structure section (GradeResult class, Price objects, PeriodicAbility cooldown curves instead of GAS cooldown effects, public `Player.asc`, the bar inside `hud.tscn` bound by `Main`).

- [ ] **Step 6: Verify and commit**

Run: `timeout 120 tools/run_tests.sh` → all PASS, then:
```bash
git add data tests CLAUDE.md docs
git commit -q -m "feat(data): ship the seven abilities in the default profile; docs: sync CLAUDE.md and specs with Phase 2

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (Phase 2 spec §2–§9):** owner decisions — 6 slots, click/Ctrl+click (Tasks 11, 12, 13 data), 100 gold none→common and 200 start gold (Task 2 data, Task 4 rules), the grade ladder with gold promotions re-rolled from the target pool and the stone reveal of the same ability (Task 4), stone rerolls within the grade (Task 4), passives/attacks/hybrids (Tasks 7–10), `drop_multiplier` as data (Tasks 2, 13). §3 defaults — all in `data/grade_table.tres`, `stage_01.tres`, `default_profile.tres` (Tasks 2, 13). §4 data model — `Grade`, `AbilityDef`, `GradeTable`, StageDef/PlayerProfile fields, validation in `Stage.start` (Tasks 1, 2, 12). §5 modules — `AbilitySlot`, `Loadout`, `AbilityPool`, `GradeService`, `AbilityInstaller`, Player extension, `AbilityBar`/`AbilitySlotView`, Stage composition, Main binding, `RunState` seeding, `AbilityContext.wallet` (Tasks 3–6, 11, 12, 2, 5). §6 — the seven abilities with curves in data/scenes, `Event.Kill` from the Stage, icons copied with provenance (Tasks 7–10). §7 — empty-slot buy, refused actions change nothing and flash (Tasks 4, 11), same ability allowed / different preferred (Tasks 3, 4), uninstall-before-install with tracked handles (Task 6), stacking duplicates (Task 6), no pause (Task 12 Main; Task 13 checklist). §9 — unit tests with seeded `RunRng`, installer tests on a real `Player`, per-ability behaviour tests, bar tests, Stage integration test, editor acceptance (Tasks 3–13). §10 risks recorded in the Phase 2 notes (Task 13). Out of scope (§8) stays out: no selling, no tooltips beyond name/cost (the tooltip shows name, grade and costs only), no sound.

**Review Focus mapping:** 1 → Task 4 `test_promote_without_enough_gold_changes_nothing`, `test_reroll_is_refused_on_empty_and_revealed_slots_and_without_stone`, `test_out_of_range_slot_is_not_applicable`; 2 → Task 3 `test_roll_with_only_the_excluded_def_returns_it`, `test_empty_pool_rolls_null_and_nulls_are_skipped`, Task 4 `test_promote_into_an_empty_pool_spends_nothing`, `test_reroll_on_a_single_ability_pool_keeps_the_ability_but_still_charges`; 3 → Task 5 `test_revoke_mid_activation_strips_its_owned_tags`, Task 6 `test_many_changes_leave_no_leftovers`, `test_changing_a_slot_mid_activation_strips_its_tags`, `test_rebinding_does_not_double_install`; 4 → Task 4 `test_reveal_keeps_the_same_ability_and_costs_stone`, Task 12 `test_buying_promoting_and_revealing_through_the_service_changes_the_player`; 5 → Task 11 `test_click_emits_promote_and_ctrl_click_emits_reroll`, `test_the_bar_only_emits_requests`, Task 12 `_result == null` guard + Task 13 checklist; 6 → Task 1 `test_passive_must_be_infinite_with_curved_nonzero_modifiers`, `test_scene_root_must_be_a_gameplay_ability_and_may_reject_itself`, `test_validate_reports_wrong_size_and_negative_costs_without_crashing`, Task 2 `test_profile_rejects_zero_slots_null_and_duplicate_abilities`.

**Type consistency checked across tasks:** `Grade.Value`/`Grade.level/next/is_rollable/color/display_name` (1 → 3, 4, 6, 11); `Price(kind, amount).is_affordable(wallet)` (1 → 4, 11); `GradeTable.promote_price/reroll_price` (1 → 4); `AbilityDef.icon_for/passive_effect/ability_scene/min_grade` (1 → 3, 6, 11); `Loadout.slot/set_slot/size/slot_changed` (3 → 4, 6, 11, 12); `AbilityPool.roll(grade, rng, exclude)` (3 → 4); `GradeResult.Outcome`, `GradeService.promote/reroll/promote_cost/reroll_cost/resolved` (4 → 11, 12); `AbilityContext._init(actor, mover, targets, projectile_layer, wallet)` and `Player.setup(profile, targets, projectile_layer, wallet)`, `Player.asc/ability_context/grant_ability/revoke_ability` (5 → 6, 8, 9, 10, 12); `AbilityInstaller.bind/installed_effect/installed_ability` (6 → 12); `GameplayTags.Event_Kill` (7 → 10, 12); `PeriodicAbility.step/start_cooldown/validate/cooldown` (8 → 9); `MeleeStrike.strike(asc, effect, damage, radius, seconds)` (9); `AbilitySlotView.show_slot(slot, price, affordable, reroll_price)`, `AbilityBar.bind/slot_view/promote_requested/reroll_requested`, `Hud.ability_bar` (11 → 12); `LootDirector.bind(..., drop_multiplier)` (2 → 12); `DefFactory.curve/passive_effect/ability/vitality/grade_table` (1 → 2, 3, 4, 6, 11).

**GAS behaviours the code relies on (verified in `D:\CM\Godot\plugins\GodotGAS`, re-check if the plugin version changes):** `apply_gameplay_effect(effect, source_asc, level)` returns the persistent `ActiveGameplayEffect` for INFINITE effects and `remove_active_effect` ignores unknown handles (Task 6); `GameplayEffectModifier.calculate_magnitude(level) = scaling_curve.sample(level) * magnitude` (Tasks 1, 7); `GameplayAbility._ready` self-grants under an ASC parent, `remove_ability` only untracks + `queue_free`s, `abort_ability` strips `activation_owned_tags` (Task 5); `send_gameplay_event` calls `try_activate(payload)` on abilities whose `trigger_event_tag` matches (Tasks 7, 10); INFINITE effects stack under `FREE` (Task 6); `Curve` serialises as `_limits`/`_data`/`point_count` and samples point values exactly at point x (Task 7 — with the explicit fallback documented there).

**Known risks the executor should expect:** hand-written `.tscn/.tres` text may need small corrections on first load (the plan says what each count/id must be); `Curve` `_limits` ordering is covered by the Task 7 data test and its fallback; the opaque 128x128 attack icons may look heavy next to the 16x16 ones (a data-only swap); the seeded roll expectations in Task 4 hold because the exclusion logic is deterministic, while the promotion-changes-ability test only needs one of 16 seeded rolls to differ.
