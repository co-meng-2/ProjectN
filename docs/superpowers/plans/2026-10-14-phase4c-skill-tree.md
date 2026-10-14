# Phase 4c — Gem skill tree: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** In the hub, spend persistent gem in a Skill Tree (nodes with prerequisites) on permanent upgrades; the upgrades apply from the next run.

**Architecture:** A `SkillTreeDef` resource (ordered `SkillNodeDef`s) defines content as data. Stat bonuses are INFINITE GAS effects scaled by the purchased level (exactly like passive abilities, same validation); slot bonuses change the derived profile. Only `skill_levels` is saved; availability is derived. A pure `ProfileBuilder` derives the run's `PlayerProfile` (carrying `SkillBonus`es), `Player.setup` applies them, and `Stage` is not modified.

**Tech Stack:** Godot 4.7.2, GDScript (typed, tabs), GodotGAS addon (not edited), the repo's headless `TestCase` runner.

**Spec:** `docs/superpowers/specs/2026-10-14-phase4c-skill-tree-design.md` (builds on the 4a and 4b specs and implementations already on this branch's base). Read it before starting.

## Global Constraints

- `Stage`, `StatBlock`, `AbilityInstaller` and everything under `src/stage/**` are not modified. Never edit `addons/`.
- Skill content (nodes, costs, order, requirements) is data (`.tres`), never code. No balance numbers in code.
- Stat bonuses are INFINITE `GameplayEffect`s whose modifiers use `scaling_curve` over the purchased level with `magnitude = 1.0`; the purchased level is applied as the effect level. Slot count is the only exception (`slot_bonus` per level changes the derived profile).
- Availability is derived, never stored: a node is available when every id in `requires` has level >= 1. A node may only require nodes listed **before** it.
- Save: JSON `user://save.json`, `{"version": 3, "gem": 30, "cleared_stages": ["stage_01"], "skill_levels": {"vitality": 2}, "settings": {"master_volume": 1.0}}`. v2 → v3 only adds `skill_levels = {}`.
- Unreadable saves go to `save.json.bak`, never overwritten silently. Unknown skill ids and levels above `max_level` stay in the save; the profile build clamps and ignores them.
- Purchases are all-or-nothing: availability, level cap and gem are checked, then gem is deducted and the level raised together.
- `meta/` and `app/` are the only layers that touch autoloads; autoload scripts have no `class_name`.
- Code style: typed GDScript, tabs, `##` doc comments, match surrounding files. Tests mirror `src/` under `tests/`, extend `TestCase`, files `test_*.gd`, methods `test_*`.
- New `class_name`s need a class-cache refresh: run the first test command of each task with `IMPORT=1` (editor closed) so `.godot/global_script_class_cache.cfg` and `.gd.uid` files are generated; commit the `.uid` files with their scripts (`git add <directory>`).
- Run tests with `tools/run_tests.sh` (`FILTER=<path substring>`, `IMPORT=1`). A test that calls a missing method shows `SCRIPT ERROR` lines, which is the RED signal. `godot-ai` MCP may be down: edit `project.godot`, `.tscn`, `.tres` by hand.
- The working tree has unrelated untracked plugin files under `addons/`. Never `git add -A`/`git add .`; add only the paths named in each task.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (shown below as a second `-m`).

## Review Focus

Failure modes the obvious tests would miss; each is pinned in the owning task.

1. A **max-health bonus must start the run at full modified health** (100/120, not 100/100 with a bigger bar) (Task 4).
2. A saved level **above `max_level`** or an **id no longer in the tree** must be kept in the save but clamped/ignored when the profile is built; the base profile resource must never be mutated (Tasks 1, 7).
3. A **v2 save upgrades to v3** keeping gem, clears and volume; a malformed `skill_levels` is corruption → `.bak` (Task 1).
4. **Locked, maxed or unaffordable** purchases change nothing, including when the handler is called directly (Tasks 6, 8).
5. An **invalid skill tree** must not crash: the screen shows an empty state with Back working, and `Main` starts with the unmodified base profile (Tasks 8, 9).

---

## File structure

| File | Create/Modify | Responsibility |
|---|---|---|
| `src/meta/save_data.gd` | Modify | `skill_levels`, `level_of`, `set_level`, `spend_gem`, dict round trip |
| `src/meta/save_migrations.gd` | Modify | `CURRENT_VERSION = 3`, v2 → v3 step |
| `src/defs/passive_effect_check.gd` | Create | Shared validation of "permanent passive" effects |
| `src/defs/ability_def.gd` | Modify | Use `PassiveEffectCheck` |
| `src/defs/skill_node_def.gd` | Create | One node |
| `src/defs/skill_tree_def.gd` | Create | Ordered nodes + validation |
| `src/defs/skill_bonus.gd` | Create | Effect + level pair carried on a profile |
| `src/defs/player_profile.gd` | Modify | `skill_bonuses` + validation |
| `src/actors/player/player.gd` | Modify | Apply bonuses, refill health |
| `data/skill_tree/*.tres` | Create | 4 nodes + the tree |
| `src/meta/skill_tree/skill_tree_progress.gd` | Create | Status / cost / buy rules |
| `src/meta/skill_tree/profile_builder.gd` | Create | Pure profile derivation |
| `src/app/scene_router.gd` | Modify | `Route.SKILL_TREE` |
| `src/meta/skill_tree/skill_tree_screen.gd` / `.tscn`, `skill_node_row.gd` / `.tscn` | Create | The screen |
| `src/meta/hub/hub.gd` / `hub.tscn` | Modify | Enable the Skill Tree button |
| `src/app/main.gd` | Modify | Build the run profile from the save |
| `tests/...` , `CLAUDE.md` | Create/Modify | Tests per task; docs |

---

### Task 1: Save schema v3 (`skill_levels`)

**Files:**
- Modify: `src/meta/save_data.gd`, `src/meta/save_migrations.gd`
- Test: `tests/meta/test_save_data.gd`, `tests/meta/test_save_migrations.gd`, `tests/meta/test_save_store.gd`

**Interfaces:**
- Produces:
  - `SaveData.skill_levels: Dictionary[StringName, int]`; `level_of(id: StringName) -> int` (0 when absent); `set_level(id: StringName, level: int) -> void` (a level <= 0 removes the entry); `spend_gem(amount: int) -> bool` (false and no change when `amount < 0` or gem is short).
  - `to_dict()` stores `"skill_levels"` as `{String: int}`; `from_dict` returns null when it is not an object, a value is not a whole number, or a level is negative.
  - `SaveMigrations.CURRENT_VERSION == 3`; `steps()` returns two steps.

- [ ] **Step 1: Update the two 4b migration tests and write the failing tests**

In `tests/meta/test_save_migrations.gd` replace

```gdscript
func test_current_version_is_two() -> void:
	assert_eq(SaveMigrations.CURRENT_VERSION, 2)
```

with

```gdscript
func test_current_version_is_three() -> void:
	assert_eq(SaveMigrations.CURRENT_VERSION, 3)
```

and in `test_a_v1_save_upgrades_to_v2_and_keeps_every_field` replace `assert_eq(migrated.get("version"), 2)` with `assert_eq(migrated.get("version"), 3)` and add the line `assert_eq(migrated.get("skill_levels"), {})` after the `cleared_stages` assertion. Then append:

```gdscript
func test_a_v2_save_upgrades_to_v3_and_keeps_every_field() -> void:
	var migrated := SaveMigrations.migrate({"version": 2, "gem": 7, "cleared_stages": ["stage_01"], "settings": {"master_volume": 0.5}})
	assert_eq(migrated.get("version"), 3)
	assert_eq(migrated.get("gem"), 7)
	assert_eq(migrated.get("cleared_stages"), ["stage_01"])
	assert_eq(migrated.get("settings"), {"master_volume": 0.5})
	assert_eq(migrated.get("skill_levels"), {})


func test_a_v3_save_keeps_its_skill_levels() -> void:
	var migrated := SaveMigrations.migrate({"version": 3, "skill_levels": {"vitality": 2}})
	assert_eq(migrated.get("skill_levels"), {"vitality": 2})
```

Append to `tests/meta/test_save_data.gd`:

```gdscript
func test_skill_levels_default_to_empty_and_level_of_is_zero() -> void:
	var data := SaveData.new()
	assert_eq(data.skill_levels.size(), 0)
	assert_eq(data.level_of(&"vitality"), 0)


func test_set_level_stores_and_zero_removes() -> void:
	var data := SaveData.new()
	data.set_level(&"vitality", 2)
	assert_eq(data.level_of(&"vitality"), 2)
	data.set_level(&"vitality", 0)
	assert_false(data.skill_levels.has(&"vitality"))


func test_spend_gem_deducts_or_changes_nothing() -> void:
	var data := SaveData.new()
	data.gem = 10
	assert_true(data.spend_gem(4))
	assert_eq(data.gem, 6)
	assert_false(data.spend_gem(7), "short")
	assert_eq(data.gem, 6)
	assert_false(data.spend_gem(-1), "negative")
	assert_eq(data.gem, 6)


func test_skill_levels_round_trip() -> void:
	var data := SaveData.new()
	data.set_level(&"vitality", 2)
	data.set_level(&"might", 1)
	var restored := SaveData.from_dict(data.to_dict())
	assert_not_null(restored)
	assert_eq(restored.level_of(&"vitality"), 2)
	assert_eq(restored.level_of(&"might"), 1)


func test_skill_level_written_as_a_json_float_loads_as_an_int() -> void:
	var restored := SaveData.from_dict({"skill_levels": {"vitality": 2.0}})
	assert_not_null(restored)
	assert_eq(restored.level_of(&"vitality"), 2)


func test_an_unknown_skill_id_survives_load_and_save() -> void:
	var restored := SaveData.from_dict({"skill_levels": {"ghost_skill": 3}})
	assert_not_null(restored)
	assert_eq(restored.to_dict()["skill_levels"], {"ghost_skill": 3})


func test_invalid_skill_levels_are_rejected() -> void:
	assert_null(SaveData.from_dict({"skill_levels": [1]}), "not an object")
	assert_null(SaveData.from_dict({"skill_levels": {"vitality": "2"}}), "string level")
	assert_null(SaveData.from_dict({"skill_levels": {"vitality": 1.5}}), "fractional level")
	assert_null(SaveData.from_dict({"skill_levels": {"vitality": -1}}), "negative level")
	assert_null(SaveData.from_dict({"skill_levels": {"vitality": null}}), "null level")
```

Append to `tests/meta/test_save_store.gd`:

```gdscript
func test_a_v2_file_loads_migrated_without_a_backup() -> void:
	SaveFiles.write_text(PATH, '{"version": 2, "gem": 5, "cleared_stages": ["stage_01"]}')
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 5)
	assert_eq(data.cleared_stages, [&"stage_01"])
	assert_eq(data.skill_levels.size(), 0)
	assert_false(FileAccess.file_exists(PATH + ".bak"))


func test_skill_levels_survive_write_and_load() -> void:
	var data := SaveData.new()
	data.set_level(&"vitality", 2)
	assert_true(SaveStore.write(PATH, data))
	assert_eq(SaveStore.load_or_default(PATH).level_of(&"vitality"), 2)


func test_malformed_skill_levels_are_treated_as_corrupt() -> void:
	for text in ['{"version": 3, "skill_levels": [1]}', '{"version": 3, "skill_levels": {"vitality": -1}}', '{"version": 3, "skill_levels": {"vitality": "x"}}']:
		SaveFiles.reset(DIR)
		SaveFiles.write_text(PATH, text)
		assert_eq(SaveStore.load_or_default(PATH).skill_levels.size(), 0, text)
		assert_true(FileAccess.file_exists(PATH + ".bak"), "backup for %s" % text)
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: FAIL (`skill_levels` / `level_of` / `spend_gem` missing; `CURRENT_VERSION` is 2).

- [ ] **Step 3: Implement**

`src/meta/save_migrations.gd`: change `const CURRENT_VERSION: int = 2` to `3`; in `steps()` add `result.append(_v2_to_v3)` after `result.append(_v1_to_v2)`; append at the end of the file:

```gdscript


## v3 adds the purchased skill levels.
static func _v2_to_v3(source: Dictionary) -> Dictionary:
	source["skill_levels"] = {}
	return source
```

`src/meta/save_data.gd`: add after the `cleared_stages` declaration

```gdscript
## Purchased skill levels by node id. Ids no longer in the skill tree are kept so investments survive content changes.
var skill_levels: Dictionary[StringName, int] = {}
```

in `to_dict()` add `"skill_levels": _skill_level_strings(),` after the `"cleared_stages"` line; add methods after `mark_cleared`:

```gdscript
func level_of(id: StringName) -> int:
	return skill_levels.get(id, 0)


## A level <= 0 removes the entry.
func set_level(id: StringName, level: int) -> void:
	if level <= 0:
		skill_levels.erase(id)
	else:
		skill_levels[id] = level


## Deducts gem all-or-nothing; false (and no change) when the amount is negative or exceeds the balance.
func spend_gem(amount: int) -> bool:
	if amount < 0 or gem < amount:
		return false
	gem -= amount
	return true


func _skill_level_strings() -> Dictionary:
	var result: Dictionary = {}
	for id in skill_levels:
		result[String(id)] = skill_levels[id]
	return result
```

and in `from_dict`, after the `cleared_stages` block and before the `settings` block:

```gdscript
	if source.has("skill_levels"):
		var raw_levels: Variant = source["skill_levels"]
		if typeof(raw_levels) != TYPE_DICTIONARY:
			return null
		for raw_id in raw_levels:
			if typeof(raw_id) != TYPE_STRING and typeof(raw_id) != TYPE_STRING_NAME:
				return null
			var level: Variant = SaveMigrations.read_int(raw_levels[raw_id])
			if level == null or level < 0:
				return null
			result.set_level(StringName(raw_id), level)
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed` (the 4a/4b tests, including "future version rejected", still pass against version 3).

- [ ] **Step 5: Commit**

```bash
git add src/meta tests/meta
git commit -m "feat(meta): save schema v3 with skill_levels, spend_gem and the v2->v3 migration" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Extract `PassiveEffectCheck`

**Files:**
- Create: `src/defs/passive_effect_check.gd`
- Modify: `src/defs/ability_def.gd`
- Test: `tests/defs/test_passive_effect_check.gd` (existing `tests/defs/test_ability_def.gd` must stay green unchanged)

**Interfaces:**
- Produces: `PassiveEffectCheck.validate(effect: GameplayEffect, subject: String) -> PackedStringArray` (static). One error per problem: not INFINITE; stacking not FREE; no modifiers; per modifier: null / empty attribute / no curve / magnitude 0. `subject` prefixes each message (for example `"AbilityDef 'vitality': passive_effect"`).

- [ ] **Step 1: Write the failing tests**

`tests/defs/test_passive_effect_check.gd`:

```gdscript
extends TestCase


func _effect() -> GameplayEffect:
	return DefFactory.passive_effect("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0, 20.0]))


func test_a_valid_passive_has_no_errors() -> void:
	assert_no_errors(PassiveEffectCheck.validate(_effect(), "x"), "valid passive")


func test_a_non_infinite_effect_is_an_error() -> void:
	var effect := _effect()
	effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_non_free_stacking_is_an_error() -> void:
	var effect := _effect()
	effect.stacking_policy = GameplayEffect.StackingPolicy.REFRESH_DURATION
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_an_effect_without_modifiers_is_an_error() -> void:
	var effect := _effect()
	effect.modifiers.clear()
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_a_null_modifier_is_an_error() -> void:
	var effect := _effect()
	effect.modifiers.append(null)
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_a_modifier_without_attribute_or_curve_or_with_zero_magnitude_is_an_error() -> void:
	var no_attribute := _effect()
	no_attribute.modifiers[0].attribute_name = ""
	assert_eq(PassiveEffectCheck.validate(no_attribute, "x").size(), 1, "attribute")
	var no_curve := _effect()
	no_curve.modifiers[0].scaling_curve = null
	assert_eq(PassiveEffectCheck.validate(no_curve, "x").size(), 1, "curve")
	var zero := _effect()
	zero.modifiers[0].magnitude = 0.0
	assert_eq(PassiveEffectCheck.validate(zero, "x").size(), 1, "magnitude")


func test_the_subject_prefixes_the_message() -> void:
	var effect := _effect()
	effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_true(PassiveEffectCheck.validate(effect, "Thing 'a'")[0].begins_with("Thing 'a'"))
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/defs/test_passive_effect_check tools/run_tests.sh`
Expected: FAIL (`PassiveEffectCheck` not declared).

- [ ] **Step 3: Implement**

`src/defs/passive_effect_check.gd`:

```gdscript
class_name PassiveEffectCheck
extends RefCounted
## Shared validation for permanent "passive" GameplayEffects: ability passives and skill-tree effects are authored and
## applied the same way (INFINITE, modifiers scaled by a curve over the level, magnitude 1.0).


## `subject` names the effect in messages, for example "AbilityDef 'vitality': passive_effect".
static func validate(effect: GameplayEffect, subject: String) -> PackedStringArray:
	var errors := PackedStringArray()
	if effect.policy != GameplayEffect.DurationPolicy.INFINITE:
		errors.append("%s must be INFINITE" % subject)
	if effect.stacking_policy != GameplayEffect.StackingPolicy.FREE:
		# Non-FREE stacking can hand two holders the same handle; clearing one would remove both.
		errors.append("%s stacking_policy must be FREE" % subject)
	if effect.modifiers.is_empty():
		errors.append("%s has no modifiers" % subject)
	for modifier in effect.modifiers:
		if modifier == null:
			errors.append("%s: null modifier" % subject)
		elif modifier.attribute_name == "":
			errors.append("%s: modifier without attribute_name" % subject)
		elif modifier.scaling_curve == null:
			errors.append("%s: modifier '%s' needs a scaling_curve over the level" % [subject, modifier.attribute_name])
		elif is_zero_approx(modifier.magnitude):
			# GAS multiplies the curve sample by `magnitude`; 0 would silently zero every level.
			errors.append("%s: modifier '%s' has magnitude 0 (use 1.0 with a curve)" % [subject, modifier.attribute_name])
	return errors
```

In `src/defs/ability_def.gd` replace the whole body of `_validate_passive()` (the function from `func _validate_passive()` through its `return errors`) with:

```gdscript
func _validate_passive() -> PackedStringArray:
	return PassiveEffectCheck.validate(passive_effect, "AbilityDef '%s': passive_effect" % id)
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/defs tools/run_tests.sh` then `FILTER=tests/data tools/run_tests.sh`
Expected: `0 failed` (`test_ability_def` and `test_ability_data` unchanged and green).

- [ ] **Step 5: Commit**

```bash
git add src/defs/passive_effect_check.gd src/defs/passive_effect_check.gd.uid src/defs/ability_def.gd tests/defs
git commit -m "refactor(defs): extract PassiveEffectCheck from AbilityDef" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `SkillNodeDef`, `SkillTreeDef`, `SkillBonus`

**Files:**
- Create: `src/defs/skill_node_def.gd`, `src/defs/skill_tree_def.gd`, `src/defs/skill_bonus.gd`
- Modify: `tests/support/def_factory.gd`
- Test: `tests/defs/test_skill_node_def.gd`, `tests/defs/test_skill_tree_def.gd`

**Interfaces:**
- Consumes: `PassiveEffectCheck.validate`.
- Produces:
  - `SkillNodeDef` (`Resource`): `id`, `display_name`, `description`, `requires: Array[StringName]`, `max_level: int`, `costs: PackedInt32Array`, `effect: GameplayEffect`, `slot_bonus: int`; `validate()`.
  - `SkillTreeDef` (`Resource`): `nodes: Array[SkillNodeDef]`; `index_of(id) -> int` (-1 when absent); `node_by_id(id) -> SkillNodeDef` (null when absent); `validate()`.
  - `SkillBonus` (`RefCounted`): `effect: GameplayEffect`, `level: int`, `_init(bonus_effect = null, bonus_level = 1)`.
  - `DefFactory.skill_node(id, requires = [], max_level = 2, costs = [10, 20]) -> SkillNodeDef` (valid; a max_health ADD effect `+10/+20`).

- [ ] **Step 1: Write the factory helper and the failing tests**

Append to `tests/support/def_factory.gd` (after `vitality`):

```gdscript
## A valid skill node: max_health ADD +10/+20 over its levels, costs [10, 20] by default.
static func skill_node(id: StringName, requires: Array[StringName] = [], max_level: int = 2, costs: PackedInt32Array = PackedInt32Array([10, 20])) -> SkillNodeDef:
	var node := SkillNodeDef.new()
	node.id = id
	node.display_name = String(id).capitalize()
	node.requires = requires
	node.max_level = max_level
	node.costs = costs
	node.effect = passive_effect("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0, 20.0]))
	return node
```

`tests/defs/test_skill_node_def.gd`:

```gdscript
extends TestCase


func test_a_valid_node_has_no_errors() -> void:
	assert_no_errors(DefFactory.skill_node(&"a").validate(), "effect node")


func test_a_slot_only_node_is_valid() -> void:
	var node := DefFactory.skill_node(&"slot", [], 1, PackedInt32Array([50]))
	node.effect = null
	node.slot_bonus = 1
	assert_no_errors(node.validate(), "slot node")


func test_empty_id_or_name_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a")
	node.id = &""
	assert_eq(node.validate().size(), 1, "id")
	var unnamed := DefFactory.skill_node(&"a")
	unnamed.display_name = ""
	assert_eq(unnamed.validate().size(), 1, "name")


func test_max_level_below_one_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a", [], 0, PackedInt32Array())
	assert_eq(node.validate().size(), 1)


func test_costs_must_match_max_level() -> void:
	var node := DefFactory.skill_node(&"a", [], 3, PackedInt32Array([10, 20]))
	assert_eq(node.validate().size(), 1)


func test_a_non_positive_cost_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a", [], 2, PackedInt32Array([10, 0]))
	assert_eq(node.validate().size(), 1)


func test_a_node_must_do_something() -> void:
	var node := DefFactory.skill_node(&"a")
	node.effect = null
	assert_eq(node.validate().size(), 1)


func test_a_negative_slot_bonus_is_an_error() -> void:
	var node := DefFactory.skill_node(&"a")
	node.slot_bonus = -1
	assert_eq(node.validate().size(), 1)


func test_a_node_cannot_require_itself() -> void:
	assert_eq(DefFactory.skill_node(&"a", [&"a"]).validate().size(), 1)


func test_effect_problems_are_reported_through_the_shared_check() -> void:
	var node := DefFactory.skill_node(&"a")
	node.effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(node.validate().size(), 1, "not INFINITE")
	var zero := DefFactory.skill_node(&"a")
	zero.effect.modifiers[0].magnitude = 0.0
	assert_eq(zero.validate().size(), 1, "magnitude 0")
```

`tests/defs/test_skill_tree_def.gd`:

```gdscript
extends TestCase


func _tree(nodes: Array[SkillNodeDef]) -> SkillTreeDef:
	var tree := SkillTreeDef.new()
	tree.nodes = nodes
	return tree


func test_a_valid_tree_has_no_errors() -> void:
	var tree := _tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"])])
	assert_no_errors(tree.validate(), "a then b requiring a")


func test_an_empty_tree_is_an_error() -> void:
	assert_eq(SkillTreeDef.new().validate().size(), 1)


func test_a_null_node_is_an_error() -> void:
	var tree := _tree([DefFactory.skill_node(&"a")])
	tree.nodes.append(null)
	assert_eq(tree.validate().size(), 1)


func test_duplicate_ids_are_an_error() -> void:
	assert_eq(_tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"a")]).validate().size(), 1)


func test_requiring_an_unknown_node_is_an_error() -> void:
	assert_eq(_tree([DefFactory.skill_node(&"a", [&"ghost"])]).validate().size(), 1)


func test_requiring_a_node_listed_later_is_an_error() -> void:
	var tree := _tree([DefFactory.skill_node(&"a", [&"b"]), DefFactory.skill_node(&"b")])
	assert_eq(tree.validate().size(), 1)


func test_a_broken_nested_node_is_reported() -> void:
	var broken := DefFactory.skill_node(&"b", [&"a"])
	broken.max_level = 0
	assert_true(_tree([DefFactory.skill_node(&"a"), broken]).validate().size() >= 1)


func test_index_of_and_node_by_id() -> void:
	var tree := _tree([DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b")])
	assert_eq(tree.index_of(&"b"), 1)
	assert_eq(tree.index_of(&"missing"), -1)
	assert_eq(tree.node_by_id(&"a").id, &"a")
	assert_null(tree.node_by_id(&"missing"))
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/defs/test_skill tools/run_tests.sh`
Expected: FAIL (`SkillNodeDef` / `SkillTreeDef` not declared).

- [ ] **Step 3: Implement**

`src/defs/skill_node_def.gd`:

```gdscript
class_name SkillNodeDef
extends Resource
## One skill-tree node. Buying a level costs gem (`costs[level]` for the next level). A node's `effect` is an INFINITE
## GameplayEffect authored like an ability passive: modifiers scale with the purchased level through `scaling_curve`
## with magnitude 1.0. `slot_bonus` (per level) raises the player's ability slot count, which is not a GAS attribute.

@export var id: StringName
@export var display_name: String = ""
@export_multiline var description: String = ""
## Ids of nodes that must have level >= 1 before this one can be bought. Only nodes listed earlier in the tree.
@export var requires: Array[StringName] = []
@export var max_level: int = 1
## Gem cost of each level: costs[0] buys level 1.
@export var costs: PackedInt32Array = PackedInt32Array()
@export var effect: GameplayEffect
@export var slot_bonus: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("SkillNodeDef: id is empty")
	if display_name.is_empty():
		errors.append("SkillNodeDef '%s': display_name is empty" % id)
	if max_level < 1:
		errors.append("SkillNodeDef '%s': max_level must be >= 1" % id)
	elif costs.size() != max_level:
		errors.append("SkillNodeDef '%s': costs needs one entry per level (%d)" % [id, max_level])
	for cost in costs:
		if cost <= 0:
			errors.append("SkillNodeDef '%s': every cost must be > 0" % id)
			break
	if slot_bonus < 0:
		errors.append("SkillNodeDef '%s': slot_bonus must be >= 0" % id)
	if effect == null and slot_bonus <= 0:
		errors.append("SkillNodeDef '%s': needs an effect and/or a slot_bonus" % id)
	if requires.has(id):
		errors.append("SkillNodeDef '%s': cannot require itself" % id)
	if effect != null:
		errors.append_array(PassiveEffectCheck.validate(effect, "SkillNodeDef '%s': effect" % id))
	return errors
```

`src/defs/skill_tree_def.gd`:

```gdscript
class_name SkillTreeDef
extends Resource
## The ordered skill-tree nodes. Order is display order and dependency order: a node may only require nodes listed
## before it, so the tree can never contain a cycle.

@export var nodes: Array[SkillNodeDef] = []


## Index of the node with this id, or -1.
func index_of(id: StringName) -> int:
	for index in nodes.size():
		if nodes[index] != null and nodes[index].id == id:
			return index
	return -1


func node_by_id(id: StringName) -> SkillNodeDef:
	var index := index_of(id)
	return nodes[index] if index >= 0 else null


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if nodes.is_empty():
		errors.append("SkillTreeDef: no nodes")
	var earlier: Dictionary[StringName, bool] = {}
	for node in nodes:
		if node == null:
			errors.append("SkillTreeDef: null node")
			continue
		if earlier.has(node.id):
			errors.append("SkillTreeDef: duplicate node id '%s'" % node.id)
		for required in node.requires:
			if not earlier.has(required):
				errors.append("SkillTreeDef: node '%s' requires '%s', which is not listed before it" % [node.id, required])
		errors.append_array(node.validate())
		earlier[node.id] = true
	return errors
```

`src/defs/skill_bonus.gd`:

```gdscript
class_name SkillBonus
extends RefCounted
## One permanent bonus carried on a PlayerProfile: apply `effect` to the player's ASC at `level`.

var effect: GameplayEffect
var level: int = 1


func _init(bonus_effect: GameplayEffect = null, bonus_level: int = 1) -> void:
	effect = bonus_effect
	level = bonus_level
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/defs tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/defs/skill_node_def.gd src/defs/skill_node_def.gd.uid src/defs/skill_tree_def.gd src/defs/skill_tree_def.gd.uid src/defs/skill_bonus.gd src/defs/skill_bonus.gd.uid tests/defs tests/support/def_factory.gd
git commit -m "feat(defs): add SkillNodeDef, SkillTreeDef and SkillBonus" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `PlayerProfile.skill_bonuses` and `Player.setup`

**Files:**
- Modify: `src/defs/player_profile.gd`, `src/actors/player/player.gd`
- Test: `tests/defs/test_player_profile.gd`, `tests/actors/test_player.gd`

**Interfaces:**
- Consumes: `SkillBonus` (Task 3), `ASC.apply_gameplay_effect(effect, source_asc, level)`, `ASC.initialize_attribute_overrides(Dictionary[String, float])`.
- Produces: `PlayerProfile.skill_bonuses: Array[SkillBonus]` (not exported); `validate()` rejects a null bonus, a null effect, or `level < 1`. `Player.setup` applies each bonus at its level, then fills health to the modified max, then binds health.

- [ ] **Step 1: Write the failing tests**

Append to `tests/defs/test_player_profile.gd`:

```gdscript
func test_valid_skill_bonuses_have_no_errors() -> void:
	var profile := DefFactory.profile()
	profile.skill_bonuses.append(SkillBonus.new(DefFactory.passive_effect("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0])), 1))
	assert_no_errors(profile.validate(), "one bonus")


func test_a_null_or_effectless_skill_bonus_is_an_error() -> void:
	var profile := DefFactory.profile()
	profile.skill_bonuses.append(null)
	profile.skill_bonuses.append(SkillBonus.new(null, 1))
	assert_eq(profile.validate().size(), 2)


func test_a_skill_bonus_level_below_one_is_an_error() -> void:
	var profile := DefFactory.profile()
	profile.skill_bonuses.append(SkillBonus.new(DefFactory.passive_effect("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0])), 0))
	assert_eq(profile.validate().size(), 1)
```

Append to `tests/actors/test_player.gd`:

```gdscript
func _bonus(attribute: String, operation: GameplayEffectModifier.Operation, per_level: PackedFloat32Array, level: int) -> SkillBonus:
	return SkillBonus.new(DefFactory.passive_effect(attribute, operation, per_level), level)


func test_a_max_health_bonus_starts_the_player_at_full_modified_health() -> void:
	var profile := DefFactory.profile()
	profile.skill_bonuses.append(_bonus("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([20.0, 40.0]), 2))
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(profile, adopt(TargetProvider.new()) as TargetProvider, adopt(Node2D.new()), Wallet.new())
	assert_almost_eq(player.health.maximum(), 140.0, 0.001, "base 100 + 40")
	assert_almost_eq(player.health.current(), 140.0, 0.001, "starts full, not 100/140")


func test_move_speed_and_attack_bonuses_reach_the_attributes() -> void:
	var profile := DefFactory.profile()
	profile.skill_bonuses.append(_bonus("move_speed", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0, 20.0]), 1))
	profile.skill_bonuses.append(_bonus("attack", GameplayEffectModifier.Operation.PERCENT_ADD, PackedFloat32Array([0.1, 0.2]), 2))
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(profile, adopt(TargetProvider.new()) as TargetProvider, adopt(Node2D.new()), Wallet.new())
	assert_almost_eq(_asc(player).get_attribute("move_speed").current_value, 110.0, 0.001)
	assert_almost_eq(_asc(player).get_attribute("attack").current_value, 1.2, 0.001)


func test_a_profile_without_bonuses_keeps_its_base_health() -> void:
	var profile := DefFactory.profile()
	profile.stats.max_health = 77.0
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(profile, adopt(TargetProvider.new()) as TargetProvider, adopt(Node2D.new()), Wallet.new())
	assert_almost_eq(player.health.maximum(), 77.0)
	assert_almost_eq(player.health.current(), 77.0)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/defs/test_player_profile tools/run_tests.sh` and `FILTER=tests/actors/test_player tools/run_tests.sh`
Expected: FAIL (`skill_bonuses` missing; the health test would fail even after the field exists until `Player.setup` applies the bonuses and refills health).

- [ ] **Step 3: Implement**

`src/defs/player_profile.gd`: add after the `abilities` declaration

```gdscript
## Permanent skill-tree bonuses, set by ProfileBuilder (not authored in data). Player.setup applies them.
var skill_bonuses: Array[SkillBonus] = []
```

and in `validate()`, before `return errors`:

```gdscript
	for bonus in skill_bonuses:
		if bonus == null or bonus.effect == null:
			errors.append("PlayerProfile: skill bonus without an effect")
		elif bonus.level < 1:
			errors.append("PlayerProfile: skill bonus level must be >= 1")
```

`src/actors/player/player.gd`: in `setup`, replace the first two lines

```gdscript
	asc.initialize_attribute_overrides(profile.stats.to_overrides())
	health.bind(asc)
```

with

```gdscript
	asc.initialize_attribute_overrides(profile.stats.to_overrides())
	_apply_skill_bonuses(profile.skill_bonuses)
	health.bind(asc)
```

and add this method after `setup`:

```gdscript
## Permanent skill-tree effects, applied at their purchased level. Health starts full AFTER them: the stats override
## above filled health to the base max, and an INFINITE max_health bonus does not heal.
func _apply_skill_bonuses(bonuses: Array[SkillBonus]) -> void:
	if bonuses.is_empty():
		return
	for bonus in bonuses:
		asc.apply_gameplay_effect(bonus.effect, asc, float(bonus.level))
	var full_health: Dictionary[String, float] = {"health": asc.get_attribute("max_health").current_value}
	asc.initialize_attribute_overrides(full_health)
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/defs tools/run_tests.sh`, `FILTER=tests/actors tools/run_tests.sh`, then the whole suite `tools/run_tests.sh`
Expected: `0 failed`, no `SCRIPT ERROR`. If the health assertion reads 100, the OVERRIDE refill did not take: re-check that `initialize_attribute_overrides` is called after the effects and before `health.bind`.

- [ ] **Step 5: Commit**

```bash
git add src/defs/player_profile.gd src/actors/player/player.gd tests/defs/test_player_profile.gd tests/actors/test_player.gd
git commit -m "feat(actors): Player applies skill bonuses and starts at full modified health" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Shipped skill-tree data

**Files:**
- Create: `data/skill_tree/vitality.tres`, `data/skill_tree/swiftness.tres`, `data/skill_tree/might.tres`, `data/skill_tree/extra_slot.tres`, `data/skill_tree/skill_tree.tres`
- Modify: `tests/data/test_game_data.gd` (`DATA_FILES`)
- Test: `tests/data/test_skill_tree_data.gd`

**Interfaces:**
- Produces: `res://data/skill_tree/skill_tree.tres` (`SkillTreeDef`): `vitality` (max_health ADD +10/+20/+35, 3 levels, costs 10/20/40), `swiftness` (move_speed ADD +10/+20/+30, 3 levels, costs 10/20/40), `might` (attack PERCENT_ADD +0.1/+0.2/+0.3, 3 levels, costs 20/40/80, requires vitality), `extra_slot` (`slot_bonus = 1`, 1 level, cost 100, requires might), in that order.

- [ ] **Step 1: Write the failing tests**

Add these lines to `DATA_FILES` in `tests/data/test_game_data.gd` (after the `stage_catalog.tres` entry):

```
	"res://data/skill_tree/vitality.tres",
	"res://data/skill_tree/swiftness.tres",
	"res://data/skill_tree/might.tres",
	"res://data/skill_tree/extra_slot.tres",
	"res://data/skill_tree/skill_tree.tres",
```

`tests/data/test_skill_tree_data.gd`:

```gdscript
extends TestCase

const TREE_PATH := "res://data/skill_tree/skill_tree.tres"

## node id -> [attribute, value at level 1, value at max level] on a StatBlock.new() actor
## (max_health 100, move_speed 100, attack 1.0).
const EFFECT_NODES: Dictionary = {
	"vitality": ["max_health", 110.0, 135.0],
	"swiftness": ["move_speed", 110.0, 130.0],
	"might": ["attack", 1.1, 1.3],
}


func _tree() -> SkillTreeDef:
	var tree := load(TREE_PATH) as SkillTreeDef
	assert_not_null(tree)
	return tree


func test_the_shipped_tree_validates_and_has_the_expected_order() -> void:
	var tree := _tree()
	assert_no_errors(tree.validate(), "skill_tree")
	var ids: Array[StringName] = []
	for node in tree.nodes:
		ids.append(node.id)
	assert_eq(ids, [&"vitality", &"swiftness", &"might", &"extra_slot"])


func test_the_shipped_requirements() -> void:
	var tree := _tree()
	assert_eq(tree.node_by_id(&"vitality").requires.size(), 0)
	assert_eq(tree.node_by_id(&"swiftness").requires.size(), 0)
	assert_eq(tree.node_by_id(&"might").requires, [&"vitality"])
	assert_eq(tree.node_by_id(&"extra_slot").requires, [&"might"])


func test_effect_nodes_scale_with_the_purchased_level() -> void:
	var tree := _tree()
	for id: String in EFFECT_NODES:
		var expected: Array = EFFECT_NODES[id]
		var node := tree.node_by_id(StringName(id))
		var actor := ActorFixture.bare_actor(self, StatBlock.new())
		var asc := ActorFixture.asc_of(actor)
		var handle := asc.apply_gameplay_effect(node.effect, asc, 1.0)
		assert_not_null(handle, id)
		assert_almost_eq(asc.get_attribute(expected[0]).current_value, expected[1], 0.001, "%s at level 1" % id)
		asc.remove_active_effect(handle)
		asc.apply_gameplay_effect(node.effect, asc, float(node.max_level))
		assert_almost_eq(asc.get_attribute(expected[0]).current_value, expected[2], 0.001, "%s at max level" % id)


func test_extra_slot_is_a_pure_slot_bonus() -> void:
	var node := _tree().node_by_id(&"extra_slot")
	assert_null(node.effect)
	assert_eq(node.slot_bonus, 1)
	assert_eq(node.max_level, 1)


func test_costs_grow_with_each_level() -> void:
	for node in _tree().nodes:
		for index in range(1, node.costs.size()):
			assert_true(node.costs[index] > node.costs[index - 1], "%s level %d" % [node.id, index + 1])
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/data tools/run_tests.sh`
Expected: FAIL (the data files do not exist; `load` returns null).

- [ ] **Step 3: Create the data files**

`data/skill_tree/vitality.tres`:

```
[gd_resource type="Resource" script_class="SkillNodeDef" format=3]

[ext_resource type="Script" path="res://src/defs/skill_node_def.gd" id="1_node"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 40.0, 1.0, 3.0]
_data = [Vector2(1, 10), 0.0, 0.0, 0, 0, Vector2(2, 20), 0.0, 0.0, 0, 0, Vector2(3, 35), 0.0, 0.0, 0, 0]
point_count = 3

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
script = ExtResource("1_node")
id = &"vitality"
display_name = "Vitality"
description = "More maximum health from the start of every run."
max_level = 3
costs = PackedInt32Array(10, 20, 40)
effect = SubResource("Effect_vitality")
```

`data/skill_tree/swiftness.tres`:

```
[gd_resource type="Resource" script_class="SkillNodeDef" format=3]

[ext_resource type="Script" path="res://src/defs/skill_node_def.gd" id="1_node"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 40.0, 1.0, 3.0]
_data = [Vector2(1, 10), 0.0, 0.0, 0, 0, Vector2(2, 20), 0.0, 0.0, 0, 0, Vector2(3, 30), 0.0, 0.0, 0, 0]
point_count = 3

[sub_resource type="Resource" id="Modifier_move_speed"]
script = ExtResource("3_modifier")
attribute_name = "move_speed"
operation = 0
magnitude = 1.0
scaling_curve = SubResource("Curve_levels")

[sub_resource type="Resource" id="Effect_swiftness"]
script = ExtResource("2_effect")
policy = 2
modifiers = Array[ExtResource("3_modifier")]([SubResource("Modifier_move_speed")])

[resource]
script = ExtResource("1_node")
id = &"swiftness"
display_name = "Swiftness"
description = "Move faster from the start of every run."
max_level = 3
costs = PackedInt32Array(10, 20, 40)
effect = SubResource("Effect_swiftness")
```

`data/skill_tree/might.tres`:

```
[gd_resource type="Resource" script_class="SkillNodeDef" format=3]

[ext_resource type="Script" path="res://src/defs/skill_node_def.gd" id="1_node"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="2_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect_modifier.gd" id="3_modifier"]

[sub_resource type="Curve" id="Curve_levels"]
_limits = [0.0, 0.4, 1.0, 3.0]
_data = [Vector2(1, 0.1), 0.0, 0.0, 0, 0, Vector2(2, 0.2), 0.0, 0.0, 0, 0, Vector2(3, 0.3), 0.0, 0.0, 0, 0]
point_count = 3

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
script = ExtResource("1_node")
id = &"might"
display_name = "Might"
description = "All your hits deal more damage."
requires = Array[StringName]([&"vitality"])
max_level = 3
costs = PackedInt32Array(20, 40, 80)
effect = SubResource("Effect_might")
```

`data/skill_tree/extra_slot.tres`:

```
[gd_resource type="Resource" script_class="SkillNodeDef" format=3]

[ext_resource type="Script" path="res://src/defs/skill_node_def.gd" id="1_node"]

[resource]
script = ExtResource("1_node")
id = &"extra_slot"
display_name = "Extra Slot"
description = "One more ability slot."
requires = Array[StringName]([&"might"])
max_level = 1
costs = PackedInt32Array(100)
slot_bonus = 1
```

`data/skill_tree/skill_tree.tres`:

```
[gd_resource type="Resource" script_class="SkillTreeDef" format=3]

[ext_resource type="Script" path="res://src/defs/skill_tree_def.gd" id="1_tree"]
[ext_resource type="Script" path="res://src/defs/skill_node_def.gd" id="2_node_def"]
[ext_resource type="Resource" path="res://data/skill_tree/vitality.tres" id="3_vitality"]
[ext_resource type="Resource" path="res://data/skill_tree/swiftness.tres" id="4_swiftness"]
[ext_resource type="Resource" path="res://data/skill_tree/might.tres" id="5_might"]
[ext_resource type="Resource" path="res://data/skill_tree/extra_slot.tres" id="6_extra_slot"]

[resource]
script = ExtResource("1_tree")
nodes = Array[ExtResource("2_node_def")]([ExtResource("3_vitality"), ExtResource("4_swiftness"), ExtResource("5_might"), ExtResource("6_extra_slot")])
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/data tools/run_tests.sh`
Expected: `0 failed`. If a node fails to load, compare its header and `Array[...]` syntax with `data/abilities/might.tres` and `data/stages/stage_catalog.tres`.

- [ ] **Step 5: Commit**

```bash
git add data/skill_tree tests/data
git commit -m "feat(data): ship the skill tree (vitality, swiftness, might, extra slot)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `SkillTreeProgress` (availability and purchase)

**Files:**
- Create: `src/meta/skill_tree/skill_tree_progress.gd`
- Test: `tests/meta/test_skill_tree_progress.gd`

**Interfaces:**
- Consumes: `SkillTreeDef.nodes`, `SaveData.level_of/set_level/spend_gem/gem`.
- Produces: `SkillTreeProgress` (`RefCounted`): `enum Status { LOCKED, AVAILABLE, MAXED }`, `_init(tree: SkillTreeDef, save: SaveData)`, `level(index) -> int`, `status(index) -> Status` (out of range is `LOCKED`), `next_cost(index) -> int` (-1 when maxed or out of range), `can_buy(index) -> bool`, `buy(index) -> bool`.

- [ ] **Step 1: Write the failing tests**

`tests/meta/test_skill_tree_progress.gd`:

```gdscript
extends TestCase

## a: 2 levels, costs 10/20. b: 1 level, cost 5, requires a.


func _setup(gem: int, levels: Dictionary = {}) -> Array:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"], 1, PackedInt32Array([5]))]
	var save := SaveData.new()
	save.gem = gem
	for id: String in levels:
		save.set_level(StringName(id), levels[id])
	return [SkillTreeProgress.new(tree, save), save]


func test_a_node_without_requirements_is_available() -> void:
	var progress: SkillTreeProgress = _setup(0)[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.AVAILABLE)


func test_a_node_is_locked_until_every_requirement_has_a_level() -> void:
	var progress: SkillTreeProgress = _setup(0)[0]
	assert_eq(progress.status(1), SkillTreeProgress.Status.LOCKED)
	var unlocked: SkillTreeProgress = _setup(0, {"a": 1})[0]
	assert_eq(unlocked.status(1), SkillTreeProgress.Status.AVAILABLE)


func test_a_node_at_max_level_is_maxed() -> void:
	var progress: SkillTreeProgress = _setup(0, {"a": 2})[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.MAXED)
	assert_eq(progress.next_cost(0), -1)


func test_next_cost_follows_the_current_level() -> void:
	assert_eq((_setup(0)[0] as SkillTreeProgress).next_cost(0), 10)
	assert_eq((_setup(0, {"a": 1})[0] as SkillTreeProgress).next_cost(0), 20)


func test_buy_deducts_gem_and_raises_the_level() -> void:
	var setup := _setup(25)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_true(progress.buy(0))
	assert_eq(save.gem, 15)
	assert_eq(save.level_of(&"a"), 1)
	assert_false(progress.buy(0), "level 2 costs 20 but only 15 is left")
	assert_eq(save.gem, 15)
	assert_eq(save.level_of(&"a"), 1)


func test_buy_fails_without_enough_gem_and_changes_nothing() -> void:
	var setup := _setup(9)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.can_buy(0))
	assert_false(progress.buy(0))
	assert_eq(save.gem, 9)
	assert_eq(save.level_of(&"a"), 0)


func test_buy_fails_on_a_locked_node_even_with_plenty_of_gem() -> void:
	var setup := _setup(1000)
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.buy(1))
	assert_eq(save.gem, 1000)
	assert_eq(save.level_of(&"b"), 0)


func test_buy_fails_on_a_maxed_node() -> void:
	var setup := _setup(1000, {"a": 2})
	var progress: SkillTreeProgress = setup[0]
	var save: SaveData = setup[1]
	assert_false(progress.buy(0))
	assert_eq(save.gem, 1000)
	assert_eq(save.level_of(&"a"), 2)


func test_buying_the_requirement_opens_the_child() -> void:
	var setup := _setup(30)
	var progress: SkillTreeProgress = setup[0]
	progress.buy(0)
	assert_eq(progress.status(1), SkillTreeProgress.Status.AVAILABLE)
	assert_true(progress.buy(1))
	assert_eq((setup[1] as SaveData).gem, 15)


func test_an_out_of_range_index_is_locked_and_cannot_be_bought() -> void:
	var progress: SkillTreeProgress = _setup(1000)[0]
	assert_eq(progress.status(5), SkillTreeProgress.Status.LOCKED)
	assert_eq(progress.status(-1), SkillTreeProgress.Status.LOCKED)
	assert_eq(progress.next_cost(5), -1)
	assert_false(progress.buy(5))


func test_a_saved_level_above_max_reads_as_maxed() -> void:
	var progress: SkillTreeProgress = _setup(0, {"a": 9})[0]
	assert_eq(progress.status(0), SkillTreeProgress.Status.MAXED)
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/meta/test_skill_tree_progress tools/run_tests.sh`
Expected: FAIL (`SkillTreeProgress` not declared).

- [ ] **Step 3: Implement**

`src/meta/skill_tree/skill_tree_progress.gd`:

```gdscript
class_name SkillTreeProgress
extends RefCounted
## Which skill nodes are locked, available or maxed, and the purchase rule. Derived from the tree and the save; it
## stores nothing itself (levels and gem live in SaveData).

enum Status { LOCKED, AVAILABLE, MAXED }

var _tree: SkillTreeDef
var _save: SaveData


func _init(tree: SkillTreeDef, save: SaveData) -> void:
	_tree = tree
	_save = save


func level(index: int) -> int:
	if not _in_range(index):
		return 0
	return _save.level_of(_tree.nodes[index].id)


## MAXED at max level (a saved level above the max counts); else AVAILABLE when every required node has level >= 1; else LOCKED.
func status(index: int) -> Status:
	if not _in_range(index):
		return Status.LOCKED
	var node := _tree.nodes[index]
	if _save.level_of(node.id) >= node.max_level:
		return Status.MAXED
	for required in node.requires:
		if _save.level_of(required) < 1:
			return Status.LOCKED
	return Status.AVAILABLE


## Gem cost of the next level, or -1 when maxed, out of range, or the costs list is shorter than the level.
func next_cost(index: int) -> int:
	if not _in_range(index):
		return -1
	var node := _tree.nodes[index]
	var current := _save.level_of(node.id)
	if current >= node.max_level or current >= node.costs.size():
		return -1
	return node.costs[current]


func can_buy(index: int) -> bool:
	return status(index) == Status.AVAILABLE and next_cost(index) >= 0 and _save.gem >= next_cost(index)


## All-or-nothing: deducts the gem and raises the level together, or changes nothing.
func buy(index: int) -> bool:
	if not can_buy(index):
		return false
	var node := _tree.nodes[index]
	var current := _save.level_of(node.id)
	if not _save.spend_gem(next_cost(index)):
		return false
	_save.set_level(node.id, current + 1)
	return true


func _in_range(index: int) -> bool:
	return index >= 0 and index < _tree.nodes.size() and _tree.nodes[index] != null
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/meta/skill_tree tests/meta/test_skill_tree_progress.gd tests/meta/test_skill_tree_progress.gd.uid
git commit -m "feat(meta): add SkillTreeProgress (availability and all-or-nothing purchase)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `ProfileBuilder`

**Files:**
- Create: `src/meta/skill_tree/profile_builder.gd`
- Test: `tests/meta/test_profile_builder.gd`

**Interfaces:**
- Consumes: `SkillTreeDef`, `SkillBonus`, `PlayerProfile`.
- Produces: `ProfileBuilder.build(base: PlayerProfile, tree: SkillTreeDef, levels: Dictionary[StringName, int]) -> PlayerProfile` (static, pure).

- [ ] **Step 1: Write the failing tests**

`tests/meta/test_profile_builder.gd`:

```gdscript
extends TestCase


func _tree() -> SkillTreeDef:
	var slot := DefFactory.skill_node(&"slot", [&"a"], 2, PackedInt32Array([10, 20]))
	slot.effect = null
	slot.slot_bonus = 1
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), slot]
	return tree


func _levels(values: Dictionary) -> Dictionary[StringName, int]:
	var result: Dictionary[StringName, int] = {}
	for id: String in values:
		result[StringName(id)] = values[id]
	return result


func test_without_purchases_the_profile_equals_the_base() -> void:
	var base := DefFactory.profile()
	var built := ProfileBuilder.build(base, _tree(), _levels({}))
	assert_true(built.stats == base.stats)
	assert_true(built.visual == base.visual)
	assert_eq(built.slot_count, base.slot_count)
	assert_eq(built.abilities.size(), base.abilities.size())
	assert_eq(built.skill_bonuses.size(), 0)


func test_the_base_profile_is_never_mutated() -> void:
	var base := DefFactory.profile()
	var slots_before := base.slot_count
	ProfileBuilder.build(base, _tree(), _levels({"a": 2, "slot": 2}))
	assert_eq(base.slot_count, slots_before)
	assert_eq(base.skill_bonuses.size(), 0)


func test_each_bought_effect_node_becomes_a_bonus_at_its_level() -> void:
	var tree := _tree()
	var built := ProfileBuilder.build(DefFactory.profile(), tree, _levels({"a": 2}))
	assert_eq(built.skill_bonuses.size(), 1)
	assert_true(built.skill_bonuses[0].effect == tree.nodes[0].effect)
	assert_eq(built.skill_bonuses[0].level, 2)


func test_slot_bonuses_add_up_per_level() -> void:
	var base := DefFactory.profile()
	var built := ProfileBuilder.build(base, _tree(), _levels({"slot": 2}))
	assert_eq(built.slot_count, base.slot_count + 2)
	assert_eq(built.skill_bonuses.size(), 0, "a slot-only node carries no effect")


func test_a_saved_level_above_max_is_clamped() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"a": 99}))
	assert_eq(built.skill_bonuses[0].level, 2)


func test_an_id_not_in_the_tree_is_ignored() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"ghost": 3}))
	assert_eq(built.skill_bonuses.size(), 0)


func test_a_built_profile_validates() -> void:
	var built := ProfileBuilder.build(DefFactory.profile(), _tree(), _levels({"a": 1, "slot": 1}))
	assert_no_errors(built.validate(), "built profile")
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/meta/test_profile_builder tools/run_tests.sh`
Expected: FAIL (`ProfileBuilder` not declared).

- [ ] **Step 3: Implement**

`src/meta/skill_tree/profile_builder.gd`:

```gdscript
class_name ProfileBuilder
extends RefCounted
## Derives the PlayerProfile for a run from the base profile and the purchased skill levels. Pure: the base profile is
## never modified, and Stage only ever sees the result.


## Levels above a node's max_level are clamped; ids that are not in the tree are ignored (they stay in the save).
## Fields are copied explicitly: when PlayerProfile gains a field, copy it here too.
static func build(base: PlayerProfile, tree: SkillTreeDef, levels: Dictionary[StringName, int]) -> PlayerProfile:
	var result := PlayerProfile.new()
	result.stats = base.stats
	result.visual = base.visual
	result.slot_count = base.slot_count
	result.abilities = base.abilities.duplicate()
	result.skill_bonuses = base.skill_bonuses.duplicate()
	for node in tree.nodes:
		if node == null:
			continue
		var level := mini(levels.get(node.id, 0), node.max_level)
		if level < 1:
			continue
		if node.effect != null:
			result.skill_bonuses.append(SkillBonus.new(node.effect, level))
		result.slot_count += node.slot_bonus * level
	return result
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/meta/skill_tree tests/meta/test_profile_builder.gd tests/meta/test_profile_builder.gd.uid
git commit -m "feat(meta): add ProfileBuilder (run profile from skill levels)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `SKILL_TREE` route, the screen, and the hub button

**Files:**
- Modify: `src/app/scene_router.gd`, `src/meta/hub/hub.gd`, `src/meta/hub/hub.tscn`, `tests/meta/test_hub.gd`
- Create: `src/meta/skill_tree/skill_node_row.gd` / `.tscn`, `src/meta/skill_tree/skill_tree_screen.gd` / `.tscn`
- Test: `tests/meta/test_skill_tree_screen.gd`

**Interfaces:**
- Consumes: `SkillTreeProgress` (Task 6), `SkillTreeDef`/`SkillNodeDef` (Task 3), `SceneRouter`, `SaveService` (`data`, `save()`).
- Produces:
  - `SceneRouter.Route.SKILL_TREE` (appended last) → `res://src/meta/skill_tree/skill_tree_screen.tscn`.
  - `SkillNodeRow` (`class_name SkillNodeRow`, root `HBoxContainer`): `signal buy_pressed(index: int)`, `setup(index: int, title: String, tooltip: String, detail: String, buy_text: String, buy_enabled: bool)`, `focus_buy()`. Unique-name nodes `%NameLabel`, `%DetailLabel`, `%BuyButton`.
  - `SkillTreeScreen` (root `Control`): optional injection `tree: SkillTreeDef`, `save_service: Node`, `router: Node`; `_on_buy_pressed(index: int)`. Unique-name nodes `%GemLabel`, `%NodeList`, `%EmptyLabel`, `%BackButton`.

- [ ] **Step 1: Write the failing tests**

In `tests/meta/test_hub.gd` replace `test_the_skill_tree_button_is_reserved_but_disabled` with

```gdscript
func test_the_skill_tree_button_opens_the_skill_tree() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(_catalog(1), _service(), router)
	assert_false(hub.get_node("%SkillTreeButton").disabled)
	hub.get_node("%SkillTreeButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.SKILL_TREE)])
```

`tests/meta/test_skill_tree_screen.gd`:

```gdscript
extends TestCase

const SCREEN_SCENE: PackedScene = preload("res://src/meta/skill_tree/skill_tree_screen.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_skill_tree_screen"
const PATH := "user://test_skill_tree_screen/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service(gem: int = 0) -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	adopt(service)
	service.data.gem = gem
	return service


## a: 2 levels costs 10/20. b: 1 level cost 5, requires a.
func _tree() -> SkillTreeDef:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"], 1, PackedInt32Array([5]))]
	return tree


func _screen(tree: SkillTreeDef, service: Node, router: Node) -> Control:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.tree = tree
	screen.save_service = service
	screen.router = router
	return adopt(screen)


func _row(screen: Control, index: int) -> Node:
	return screen.get_node("%NodeList").get_child(index)


func _buy_button(screen: Control, index: int) -> Button:
	return _row(screen, index).get_node("%BuyButton") as Button


func test_one_row_per_node_and_the_gem_balance() -> void:
	var screen := _screen(_tree(), _service(25), adopt(Probe.new()))
	assert_eq(screen.get_node("%NodeList").get_child_count(), 2)
	assert_true(screen.get_node("%GemLabel").text.contains("25"))


func test_row_states_for_available_locked_and_unaffordable() -> void:
	var screen := _screen(_tree(), _service(10), adopt(Probe.new()))
	assert_false(_buy_button(screen, 0).disabled, "a is affordable at 10")
	assert_true(_buy_button(screen, 1).disabled, "b is locked")
	assert_true(_row(screen, 1).get_node("%DetailLabel").text.contains("Requires"))
	var poor := _screen(_tree(), _service(9), adopt(Probe.new()))
	assert_true(_buy_button(poor, 0).disabled, "a is not affordable at 9")


func test_buying_updates_gem_level_children_and_saves() -> void:
	var service := _service(25)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	_buy_button(screen, 0).pressed.emit()
	assert_eq(service.data.gem, 15)
	assert_eq(service.data.level_of(&"a"), 1)
	assert_true(screen.get_node("%GemLabel").text.contains("15"))
	assert_false(_buy_button(screen, 1).disabled, "b is now open and affordable")
	assert_eq(SaveStore.load_or_default(PATH).level_of(&"a"), 1, "persisted to disk")


func test_a_maxed_node_cannot_be_bought_again() -> void:
	var service := _service(100)
	service.data.set_level(&"a", 2)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	assert_true(_buy_button(screen, 0).disabled)
	assert_true(_row(screen, 0).get_node("%DetailLabel").text.contains("Max"))


func test_the_handler_refuses_locked_unaffordable_and_maxed_nodes() -> void:
	var service := _service(1000)
	service.data.set_level(&"a", 0)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	screen._on_buy_pressed(1)
	assert_eq(service.data.gem, 1000, "locked b")
	assert_eq(service.data.level_of(&"b"), 0)
	var poor := _service(3)
	var poor_screen := _screen(_tree(), poor, adopt(Probe.new()))
	poor_screen._on_buy_pressed(0)
	assert_eq(poor.data.gem, 3, "short on gem")
	assert_eq(poor.data.level_of(&"a"), 0)


func test_back_returns_to_the_hub() -> void:
	var router: Node = adopt(Probe.new())
	var screen := _screen(_tree(), _service(), router)
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func test_an_invalid_tree_shows_an_empty_state_and_back_still_works() -> void:
	var router: Node = adopt(Probe.new())
	var screen := _screen(SkillTreeDef.new(), _service(), router)
	assert_eq(screen.get_node("%NodeList").get_child_count(), 0)
	assert_true(screen.get_node("%EmptyLabel").visible)
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func test_focus_starts_on_the_first_buyable_row_else_back() -> void:
	var screen := _screen(_tree(), _service(10), adopt(Probe.new()))
	assert_true(screen.get_viewport().gui_get_focus_owner() == _buy_button(screen, 0))
	var poor := _screen(_tree(), _service(0), adopt(Probe.new()))
	assert_true(poor.get_viewport().gui_get_focus_owner() == poor.get_node("%BackButton"))


func test_the_shipped_tree_is_the_default() -> void:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.save_service = _service()
	screen.router = adopt(Probe.new())
	adopt(screen)
	assert_eq(screen.get_node("%NodeList").get_child_count(), 4)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/meta/test_skill_tree_screen tools/run_tests.sh` and `FILTER=tests/meta/test_hub tools/run_tests.sh`
Expected: FAIL (scene missing; `Route.SKILL_TREE` missing; the hub button is still disabled).

- [ ] **Step 3: Implement**

`src/app/scene_router.gd`: change the enum to `enum Route { LOGO, MAIN_MENU, SETTINGS, STAGE, HUB, SKILL_TREE }` and add to `ROUTES` after the `Route.HUB` entry:

```gdscript
	Route.SKILL_TREE: "res://src/meta/skill_tree/skill_tree_screen.tscn",
```

`src/meta/hub/hub.tscn`: in the `SkillTreeButton` node remove the line `disabled = true` and change `text = "Skill Tree (coming soon)"` to `text = "Skill Tree"`.

`src/meta/hub/hub.gd`: change the doc comment's first line to `## The hub: one card per catalog stage, a Skill Tree button and Back. It owns no state: it asks the`, add after the `_back_button` onready line

```gdscript
@onready var _skill_tree_button: Button = %SkillTreeButton
```

add in `_ready` right after `_back_button.pressed.connect(_on_back_pressed)`

```gdscript
	_skill_tree_button.pressed.connect(_on_skill_tree_pressed)
```

and add the method after `_on_back_pressed`:

```gdscript


func _on_skill_tree_pressed() -> void:
	router.go(SceneRouter.Route.SKILL_TREE)
```

`src/meta/skill_tree/skill_node_row.gd`:

```gdscript
class_name SkillNodeRow
extends HBoxContainer
## One skill-tree row: name, detail line and a Buy button. It only reports the click.

signal buy_pressed(index: int)

var _index: int = -1

@onready var _name_label: Label = %NameLabel
@onready var _detail_label: Label = %DetailLabel
@onready var _buy_button: Button = %BuyButton


func _ready() -> void:
	_buy_button.pressed.connect(_on_buy_pressed)


## Call after the row is in the tree.
func setup(index: int, title: String, tooltip: String, detail: String, buy_text: String, buy_enabled: bool) -> void:
	_index = index
	_name_label.text = title
	_name_label.tooltip_text = tooltip
	_detail_label.text = detail
	_buy_button.text = buy_text
	_buy_button.disabled = not buy_enabled


func focus_buy() -> void:
	_buy_button.grab_focus()


func _on_buy_pressed() -> void:
	buy_pressed.emit(_index)
```

`src/meta/skill_tree/skill_node_row.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/skill_tree/skill_node_row.gd" id="1_row"]

[node name="SkillNodeRow" type="HBoxContainer"]
custom_minimum_size = Vector2(520, 0)
theme_override_constants/separation = 16
script = ExtResource("1_row")

[node name="NameLabel" type="Label" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(140, 0)
layout_mode = 2
mouse_filter = 0
text = "Skill"

[node name="DetailLabel" type="Label" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(260, 0)
layout_mode = 2
size_flags_horizontal = 3

[node name="BuyButton" type="Button" parent="."]
unique_name_in_owner = true
layout_mode = 2
text = "Buy"
```

`src/meta/skill_tree/skill_tree_screen.gd`:

```gdscript
extends Control
## The skill-tree screen: gem balance, one row per node, Back. It owns no state: purchases go through
## SkillTreeProgress, then the save is written once and the rows are rebuilt.

const ROW_SCENE: PackedScene = preload("res://src/meta/skill_tree/skill_node_row.tscn")
const DEFAULT_TREE: SkillTreeDef = preload("res://data/skill_tree/skill_tree.tres")

## Default to the shipped tree and the autoloads in _ready; tests set them first.
var tree: SkillTreeDef
var save_service: Node
var router: Node

var _progress: SkillTreeProgress

@onready var _gem_label: Label = %GemLabel
@onready var _node_list: VBoxContainer = %NodeList
@onready var _empty_label: Label = %EmptyLabel
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if tree == null:
		tree = DEFAULT_TREE
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_back_button.pressed.connect(_on_back_pressed)
	var errors := tree.validate()
	if not errors.is_empty():
		push_error("SkillTreeScreen: unusable skill tree: %s" % ", ".join(errors))
		_empty_label.visible = true
		_gem_label.text = "Gem %d" % save_service.data.gem
		_back_button.grab_focus()
		return
	_progress = SkillTreeProgress.new(tree, save_service.data)
	_rebuild()


func _rebuild() -> void:
	for child in _node_list.get_children():
		_node_list.remove_child(child)
		child.queue_free()
	_gem_label.text = "Gem %d" % save_service.data.gem
	var focus_row: SkillNodeRow = null
	for index in tree.nodes.size():
		var node := tree.nodes[index]
		var row := ROW_SCENE.instantiate() as SkillNodeRow
		_node_list.add_child(row)
		var can_buy := _progress.can_buy(index)
		row.setup(index, node.display_name, node.description, _detail_text(index), _buy_text(index), can_buy)
		row.buy_pressed.connect(_on_buy_pressed)
		if focus_row == null and can_buy:
			focus_row = row
	if focus_row != null:
		focus_row.focus_buy()
	else:
		_back_button.grab_focus()


func _detail_text(index: int) -> String:
	var node := tree.nodes[index]
	match _progress.status(index):
		SkillTreeProgress.Status.LOCKED:
			var names: PackedStringArray = []
			for required in node.requires:
				var required_node := tree.node_by_id(required)
				names.append(required_node.display_name if required_node != null else String(required))
			return "Requires: %s" % ", ".join(names)
		SkillTreeProgress.Status.MAXED:
			return "Lv %d/%d   Max" % [node.max_level, node.max_level]
		_:
			return "Lv %d/%d   Cost %d" % [_progress.level(index), node.max_level, _progress.next_cost(index)]


func _buy_text(index: int) -> String:
	return "Max" if _progress.status(index) == SkillTreeProgress.Status.MAXED else "Buy"


func _on_buy_pressed(index: int) -> void:
	if _progress == null or not _progress.buy(index):
		return
	save_service.save()
	_rebuild()


func _on_back_pressed() -> void:
	router.go(SceneRouter.Route.HUB)
```

`src/meta/skill_tree/skill_tree_screen.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/skill_tree/skill_tree_screen.gd" id="1_screen"]

[node name="SkillTreeScreen" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_screen")

[node name="Center" type="CenterContainer" parent="."]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2

[node name="Column" type="VBoxContainer" parent="Center"]
layout_mode = 2
theme_override_constants/separation = 14

[node name="Title" type="Label" parent="Center/Column"]
layout_mode = 2
theme_override_font_sizes/font_size = 28
text = "Skill Tree"
horizontal_alignment = 1

[node name="GemLabel" type="Label" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Gem 0"
horizontal_alignment = 1

[node name="NodeList" type="VBoxContainer" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
theme_override_constants/separation = 8

[node name="EmptyLabel" type="Label" parent="Center/Column"]
unique_name_in_owner = true
visible = false
layout_mode = 2
text = "No skills available"
horizontal_alignment = 1

[node name="BackButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Back"
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/meta tools/run_tests.sh` and `FILTER=tests/app tools/run_tests.sh`
Expected: `0 failed` (the existing route-load test now also loads the skill-tree screen; the invalid-tree test prints an expected `ERROR: SkillTreeScreen: unusable skill tree` line).

- [ ] **Step 5: Commit**

```bash
git add src/app/scene_router.gd src/meta/hub src/meta/skill_tree tests/meta
git commit -m "feat(meta): add the SKILL_TREE route and screen, and enable the hub button" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `Main` builds the run profile; smoke test; docs

**Files:**
- Modify: `src/app/main.gd`, `CLAUDE.md`
- Test: `tests/app/test_main.gd`, `tests/app/test_boot_flow.gd`

**Interfaces:**
- Consumes: `ProfileBuilder.build` (Task 7), `SkillTreeDef.validate`, shipped data (Task 5), `SkillTreeScreen` (Task 8).
- Produces: `Main` has an optional injectable `skill_tree: SkillTreeDef` (default: the shipped tree) and starts every run with `ProfileBuilder.build(BASE_PROFILE, skill_tree, save_service.data.skill_levels)`; an invalid tree falls back to the base profile.

- [ ] **Step 1: Write the failing tests**

Append to `tests/app/test_main.gd`:

```gdscript
func test_bought_skill_levels_shape_the_profile_the_stage_starts_with() -> void:
	var service := _service()
	service.data.set_level(&"vitality", 2)
	service.data.set_level(&"extra_slot", 1)
	var main := _main(service)
	assert_almost_eq(main._stage.player.health.maximum(), 120.0, 0.001, "base 100 + vitality level 2 (+20)")
	assert_almost_eq(main._stage.player.health.current(), 120.0, 0.001, "starts at full modified health")
	assert_eq(main._stage.loadout.size(), 7, "6 base slots + extra slot")


func test_without_purchases_the_run_uses_the_base_profile() -> void:
	var main := _main(_service())
	assert_almost_eq(main._stage.player.health.maximum(), 100.0, 0.001)
	assert_eq(main._stage.loadout.size(), 6)


func test_an_invalid_skill_tree_falls_back_to_the_base_profile() -> void:
	var service := _service()
	service.data.set_level(&"vitality", 3)
	var main := MAIN_SCENE.instantiate()
	main.save_service = service
	main.skill_tree = SkillTreeDef.new()
	adopt(main)
	assert_almost_eq(main._stage.player.health.maximum(), 100.0, 0.001, "no bonuses from a broken tree")
	assert_eq(main._stage.loadout.size(), 6)
```

Append to `tests/app/test_boot_flow.gd`:

```gdscript
func test_gem_from_a_clear_buys_a_skill_that_strengthens_the_next_run() -> void:
	var router: Node = adopt(Probe.new())
	var service := _service()

	var first: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	first.save_service = service
	first.router = router
	adopt(first)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	first._stage.finished.emit(result)
	assert_eq(service.data.gem, first.stage_def.gem_reward)

	var screen: Node = load(router.path_for(Probe.Route.SKILL_TREE)).instantiate()
	screen.save_service = service
	screen.router = router
	adopt(screen)
	screen.get_node("%NodeList").get_child(0).get_node("%BuyButton").pressed.emit()
	assert_eq(service.data.level_of(&"vitality"), 1)
	assert_eq(service.data.gem, first.stage_def.gem_reward - 10, "vitality level 1 costs 10")

	var second: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	second.save_service = service
	second.router = router
	adopt(second)
	assert_almost_eq(second._stage.player.health.maximum(), 110.0, 0.001, "base 100 + vitality level 1 (+10)")
	assert_almost_eq(second._stage.player.health.current(), 110.0, 0.001)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/app tools/run_tests.sh`
Expected: FAIL (`Main` ignores skill levels; `skill_tree` property missing).

- [ ] **Step 3: Implement**

`src/app/main.gd`: rename the constant `PROFILE` to `BASE_PROFILE` (its definition line and its single use), add after it

```gdscript
const SKILL_TREE: SkillTreeDef = preload("res://data/skill_tree/skill_tree.tres")
```

add `var skill_tree: SkillTreeDef` below `var stage_def: StageDef`, add in `_ready` after the `stage_def` block

```gdscript
	if skill_tree == null:
		skill_tree = SKILL_TREE
```

change `_stage.start(stage_def, PROFILE)` to `_stage.start(stage_def, _run_profile())`, and add this method after `_start_run`:

```gdscript
## Base profile plus the purchased skill levels. A broken skill tree must not stop a run: fall back to the base profile.
func _run_profile() -> PlayerProfile:
	var errors := skill_tree.validate()
	if not errors.is_empty():
		push_error("Main: unusable skill tree, starting with the base profile: %s" % ", ".join(errors))
		return BASE_PROFILE
	return ProfileBuilder.build(BASE_PROFILE, skill_tree, save_service.data.skill_levels)
```

Update the class doc comment's first line to mention the profile: `## The stage screen: runs a stage with its HUD (and the profile built from the purchased skills), routes HUD requests to the stage, pays the run's gem into the` and the next line `## save, and leaves for the hub on request. Reached through SceneRouter (params: the StageDef).` (this also fixes the stale "main menu" wording).

`CLAUDE.md`: in "Architecture rules", after the Phase 4b bullet append:

```markdown
- Phase 4c skill tree: `SkillTreeDef` (`data/skill_tree/skill_tree.tres`, ordered `SkillNodeDef`s; a node may only require nodes
  listed before it) is spent from the hub's Skill Tree screen (`SkillTreeProgress.buy` is all-or-nothing). Only
  `SaveData.skill_levels` is saved; availability is derived. `ProfileBuilder.build(base, tree, levels)` (pure) derives the run's
  `PlayerProfile`: each bought effect node becomes a `SkillBonus` (INFINITE GAS effect applied at the purchased level, authored
  like an ability passive and validated by `PassiveEffectCheck`), and `slot_bonus` raises `slot_count` (not a GAS attribute).
  `Player.setup` applies the bonuses and then refills health to the modified max. `Main` falls back to the base profile if the
  tree fails `validate()`. Saved levels above `max_level` / unknown ids are kept in the save and clamped/ignored at build.
```

and add after the "Adding a stage (data only)" section:

```markdown
## Adding a skill node (data only)
1. Duplicate `data/skill_tree/vitality.tres` (stat effect) or `extra_slot.tres` (slot bonus). Set `id`, names, `max_level`, `costs`
   (one gem cost per level), `requires` (ids of nodes listed EARLIER in the tree). An effect is an INFINITE `GameplayEffect` whose
   modifier uses `scaling_curve` over the purchased level 1..`max_level` with `magnitude = 1.0` (0 zeroes every level; `validate()` catches it).
2. Append it to `data/skill_tree/skill_tree.tres` in dependency order and add the file to `DATA_FILES` in `tests/data/test_game_data.gd`;
   extend `tests/data/test_skill_tree_data.gd` with its expected effect values.
```

- [ ] **Step 4: Run to verify pass**

Run: `tools/run_tests.sh` (whole suite)
Expected: `---- N passed, 0 failed ----`, no `SCRIPT ERROR` (the invalid-tree test prints expected `ERROR:` lines).

- [ ] **Step 5: Drive the real scenes once (not a substitute for playing)**

Save the following as a scratch script outside the project and run it with `Godot --headless --path . -s <script>`. It uses a temp save so the developer's real `save.json` is untouched. Report what it prints.

```gdscript
extends SceneTree

const SAVE := "user://flow_check_4c.json"
var _frames := 0
var _step := 0


func _initialize() -> void:
	var svc = root.get_node("SaveService")
	svc.path = SAVE
	svc.reload()
	svc.data.gem = 10
	change_scene_to_file("res://src/app/logo.tscn")


func _process(_delta: float) -> bool:
	_frames += 1
	var s := current_scene
	if s == null:
		return false
	match _step:
		0:
			if s.name == "MainMenu":
				s.get_node("%StartButton").pressed.emit()
				_step = 1
		1:
			if s.name == "Hub":
				s.get_node("%SkillTreeButton").pressed.emit()
				_step = 2
		2:
			if s.name == "SkillTreeScreen":
				var list := s.get_node("%NodeList")
				print("[4c] rows=%d might_buy_disabled=%s" % [list.get_child_count(), list.get_child(2).get_node("%BuyButton").disabled])
				list.get_child(0).get_node("%BuyButton").pressed.emit()
				print("[4c] after buying vitality: gem=%d level=%d" % [root.get_node("SaveService").data.gem, root.get_node("SaveService").data.level_of(&"vitality")])
				s.get_node("%BackButton").pressed.emit()
				_step = 3
		3:
			if s.name == "Hub":
				s.get_node("%StageList").get_child(0).get_node("%PlayButton").pressed.emit()
				_step = 4
		4:
			if s.name == "Main" and s.get("_stage") != null:
				print("[4c] run starts with max_health=%.0f health=%.0f" % [s._stage.player.health.maximum(), s._stage.player.health.current()])
				DirAccess.remove_absolute(SAVE)
				quit()
	if _frames > 3000:
		print("[4c] TIMEOUT step=%d scene=%s" % [_step, s.name])
		quit(1)
	return false
```

Expected output: `rows=4 might_buy_disabled=true`, `after buying vitality: gem=0 level=1`, `run starts with max_health=110 health=110`.

- [ ] **Step 6: Commit**

```bash
git add src/app/main.gd CLAUDE.md tests/app
git commit -m "feat(app): Main starts runs with the profile built from purchased skills" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review (spec coverage)

- Modules: `SkillNodeDef`/`SkillTreeDef`/`SkillBonus` (T3), `PlayerProfile.skill_bonuses` + `Player.setup` (T4), `SaveData`/`SaveMigrations` (T1), `SkillTreeProgress` (T6), `ProfileBuilder` (T7), `SkillTreeScreen`/`SkillNodeRow` (T8), shared passive validation (T2), shipped data (T5), `Main` wiring (T9).
- Routes and existing code: `Route.SKILL_TREE` and hub button (T8); `Main._start_run` profile + invalid-tree fallback (T9); `Stage`, `StatBlock`, `AbilityInstaller` untouched (no task edits them).
- Data flow 1-5: hub → screen (T8), rows by status (T8), buy + single save + rebuild (T8), run profile (T9), player application order (T4).
- Save format and migration: T1. Error handling: v2 save (T1), malformed `skill_levels` (T1), level above max / unknown id (T1 preserved, T7 clamped/ignored), invalid tree (T8 screen, T9 Main), locked/short/maxed buys (T6, T8).
- Testing list: node/tree defs (T3), SaveData/migrations/store (T1), progress (T6), builder (T7), Player (T4), PlayerProfile.validate (T4), screen (T8), hub (T8), Main (T9), smoke (T9); shipped data (T5).
- Type consistency: `skill_levels`, `level_of`, `set_level`, `spend_gem`, `SkillTreeProgress.Status {LOCKED, AVAILABLE, MAXED}`, `status/next_cost/can_buy/buy/level`, `ProfileBuilder.build(base, tree, levels)`, `SkillBonus(effect, level)`, `PassiveEffectCheck.validate(effect, subject)`, `Route.SKILL_TREE`, `SkillNodeRow.setup(index, title, tooltip, detail, buy_text, buy_enabled)` are used with identical names across tasks.
