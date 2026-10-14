# Phase 4b — Hub, stage select, sequential unlock: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Main menu → hub → stage → result → hub. The hub lists the stages in catalog order; a stage unlocks when the previous one has been cleared; clears persist in the save (v1 → v2 migration); a second stage ships as data only.

**Architecture:** An ordered `StageCatalog` resource (data) defines stages and unlock order. Only `cleared_stages` is saved; `StageProgress` derives LOCKED/AVAILABLE/CLEARED from catalog + save. A single `Hub` screen renders one `StageCard` per stage and only sends requests to `SceneRouter`. `Main` records a clear next to the gem credit. `Stage` is not modified.

**Tech Stack:** Godot 4.7.2, GDScript (typed, tabs), the repo's headless `TestCase` runner.

**Spec:** `docs/superpowers/specs/2026-10-14-phase4b-hub-stage-select-design.md` (builds on `2026-10-14-phase4a-scene-flow-save-design.md` and its implementation, already on this branch's base). Read both before starting.

## Global Constraints

- `Stage` and everything under `src/stage/**` is not modified.
- Stage order and content are data (`.tres`), never code. No balance numbers in code.
- Unlock is derived, never stored: only `cleared_stages` is saved. Stage `i` is unlocked when `i == 0` or stage `i-1` is in `cleared_stages`; a cleared stage is still playable.
- Save file stays JSON at `user://save.json`: `{"version": 2, "gem": 0, "cleared_stages": ["stage_01"], "settings": {"master_volume": 1.0}}`. The v1 → v2 step is the only migration code added and keeps every v1 field.
- Unreadable saves are moved to `save.json.bak`, never overwritten silently. Ids in `cleared_stages` that are not in the catalog are preserved and ignored.
- `meta/` and `app/` are the only layers that may touch autoloads. The autoload scripts (`SceneRouter`, `SaveService`) have no `class_name`.
- Re-clearing a stage keeps paying gem (4a behaviour); a failed run records nothing.
- Code style: typed GDScript, tabs, `##` doc comments, match surrounding files. Tests mirror `src/` under `tests/`, extend `TestCase`, files `test_*.gd`, methods `test_*`.
- New `class_name`s need a class-cache refresh: run the first test command of each task with `IMPORT=1` (editor closed) so `.godot/global_script_class_cache.cfg` and `.gd.uid` files are generated. Commit generated `.uid` files with their scripts (`git add <directory>`).
- Run tests with `tools/run_tests.sh` (`FILTER=<path substring>`, `IMPORT=1`). `godot-ai` MCP may be down: edit `project.godot` and `.tscn`/`.tres` by hand.
- The working tree has unrelated untracked plugin files under `addons/`. Never `git add -A`/`git add .`; add only the paths named in each task.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (shown below as a second `-m`).

## Review Focus

Failure modes the spec implies that the obvious tests would miss; each is pinned in the owning task.

1. A **v1 save** (real player data) must upgrade to v2 without losing gem or volume, and must not be quarantined (Task 1).
2. `cleared_stages` that is not an array, or contains a non-string, is corruption → `.bak`; but an **unknown stage id** in a valid list is preserved through load → save, not dropped (Task 1).
3. A **locked stage must not start**, even if its handler is called directly (Task 5).
4. An **empty or invalid catalog** must not crash the hub; Back still works (Task 5).
5. A **failed run records nothing**, and a run that reports `finished` twice records the clear once; Enter on the result screen goes to the hub (Task 6).

---

## File structure

| File | Create/Modify | Responsibility |
|---|---|---|
| `src/meta/save_data.gd` | Modify | `cleared_stages`, `mark_cleared`, dict round trip + validation |
| `src/meta/save_migrations.gd` | Modify | `CURRENT_VERSION = 2`, v1 → v2 step |
| `src/meta/save_service.gd` | Modify | `record_clear(stage_id)` |
| `src/defs/stage_catalog.gd` | Create | `StageCatalog`: ordered `StageDef` list, `index_of`, `validate` |
| `data/stages/stage_02.tres` | Create | Second stage (data only) |
| `data/stages/stage_catalog.tres` | Create | The shipped catalog: stage_01, stage_02 |
| `src/meta/stage_progress.gd` | Create | `StageProgress`: derived unlock status |
| `src/app/scene_router.gd` | Modify | `Route.HUB` |
| `src/meta/hub/hub.gd` / `hub.tscn` | Create | The hub screen |
| `src/meta/hub/stage_card.gd` / `stage_card.tscn` | Create | One stage row |
| `src/meta/menu/main_menu.gd` | Modify | Start → HUB |
| `src/app/main.gd` | Modify | Record clear; Enter → HUB |
| `CLAUDE.md` | Modify | Phase 4b notes + "Adding a stage" |
| `tests/meta/*`, `tests/defs/test_stage_catalog.gd`, `tests/data/test_game_data.gd`, `tests/app/*` | Create/Modify | Tests per task |

---

### Task 1: Save schema v2 (`cleared_stages`)

**Files:**
- Modify: `src/meta/save_data.gd`, `src/meta/save_migrations.gd`
- Test: `tests/meta/test_save_data.gd`, `tests/meta/test_save_migrations.gd`, `tests/meta/test_save_store.gd`

**Interfaces:**
- Produces:
  - `SaveData.cleared_stages: Array[StringName]`, `SaveData.mark_cleared(id: StringName) -> void` (no duplicates). `to_dict()` stores `"cleared_stages"` as an array of strings; `from_dict` returns null when it is not an array or holds a non-string.
  - `SaveMigrations.CURRENT_VERSION == 2`; `steps()` returns one step (v1 → v2).

- [ ] **Step 1: Write the failing tests**

Append to `tests/meta/test_save_data.gd`:

```gdscript
func test_cleared_stages_default_to_empty() -> void:
	assert_eq(SaveData.new().cleared_stages.size(), 0)


func test_mark_cleared_ignores_an_id_already_present() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	data.mark_cleared(&"stage_01")
	assert_eq(data.cleared_stages, [&"stage_01"])


func test_cleared_stages_round_trip() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	data.mark_cleared(&"stage_02")
	var restored := SaveData.from_dict(data.to_dict())
	assert_not_null(restored)
	assert_eq(restored.cleared_stages, [&"stage_01", &"stage_02"])


func test_cleared_stages_are_stored_as_plain_strings() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	var stored: Array = data.to_dict()["cleared_stages"]
	assert_eq(typeof(stored[0]), TYPE_STRING)


func test_an_unknown_stage_id_survives_load_and_save() -> void:
	var restored := SaveData.from_dict({"cleared_stages": ["ghost_stage"]})
	assert_not_null(restored)
	assert_eq(restored.to_dict()["cleared_stages"], ["ghost_stage"])


func test_invalid_cleared_stages_are_rejected() -> void:
	assert_null(SaveData.from_dict({"cleared_stages": "stage_01"}), "not an array")
	assert_null(SaveData.from_dict({"cleared_stages": [1]}), "number entry")
	assert_null(SaveData.from_dict({"cleared_stages": ["stage_01", null]}), "null entry")
```

Append to `tests/meta/test_save_migrations.gd`:

```gdscript
func test_current_version_is_two() -> void:
	assert_eq(SaveMigrations.CURRENT_VERSION, 2)


func test_a_v1_save_upgrades_to_v2_and_keeps_every_field() -> void:
	var migrated := SaveMigrations.migrate({"version": 1, "gem": 4, "settings": {"master_volume": 0.5}})
	assert_eq(migrated.get("version"), 2)
	assert_eq(migrated.get("gem"), 4)
	assert_eq(migrated.get("settings"), {"master_volume": 0.5})
	assert_eq(migrated.get("cleared_stages"), [])


func test_a_v2_save_keeps_its_cleared_stages() -> void:
	var migrated := SaveMigrations.migrate({"version": 2, "cleared_stages": ["stage_01"]})
	assert_eq(migrated.get("cleared_stages"), ["stage_01"])
```

Append to `tests/meta/test_save_store.gd`:

```gdscript
func test_a_v1_file_loads_migrated_without_a_backup() -> void:
	SaveFiles.write_text(PATH, '{"version": 1, "gem": 5, "settings": {"master_volume": 0.5}}')
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 5)
	assert_almost_eq(data.settings.master_volume, 0.5)
	assert_eq(data.cleared_stages.size(), 0)
	assert_false(FileAccess.file_exists(PATH + ".bak"))


func test_cleared_stages_survive_write_and_load() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	assert_true(SaveStore.write(PATH, data))
	assert_eq(SaveStore.load_or_default(PATH).cleared_stages, [&"stage_01"])


func test_malformed_cleared_stages_are_treated_as_corrupt() -> void:
	for text in ['{"version": 2, "cleared_stages": [1]}', '{"version": 2, "cleared_stages": "stage_01"}']:
		SaveFiles.reset(DIR)
		SaveFiles.write_text(PATH, text)
		assert_eq(SaveStore.load_or_default(PATH).cleared_stages.size(), 0, text)
		assert_true(FileAccess.file_exists(PATH + ".bak"), "backup for %s" % text)
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: FAIL (`cleared_stages` / `mark_cleared` not found; `CURRENT_VERSION` is 1).

- [ ] **Step 3: Implement**

`src/meta/save_migrations.gd`: change `const CURRENT_VERSION: int = 1` to `2`, and replace `steps()` with

```gdscript
## steps()[i] upgrades a version (i + 1) dictionary to version (i + 2).
static func steps() -> Array[Callable]:
	var result: Array[Callable] = []
	result.append(_v1_to_v2)
	return result
```

and add below `read_int`:

```gdscript
## v2 adds the list of cleared stage ids.
static func _v1_to_v2(source: Dictionary) -> Dictionary:
	source["cleared_stages"] = []
	return source
```

`src/meta/save_data.gd`: add after `var gem: int = 0`

```gdscript
## Ids of stages the player has cleared. Unlock state is derived from this (see StageProgress); ids that are no
## longer in the catalog are kept so progress survives content changes.
var cleared_stages: Array[StringName] = []
```

add after `to_dict`'s opening dictionary key `"gem": gem,` the line `"cleared_stages": _cleared_stage_strings(),`, add the method

```gdscript
func mark_cleared(id: StringName) -> void:
	if not cleared_stages.has(id):
		cleared_stages.append(id)


func _cleared_stage_strings() -> Array:
	var result: Array = []
	for id in cleared_stages:
		result.append(String(id))
	return result
```

and in `from_dict`, after the `gem` block and before the `settings` block, add

```gdscript
	if source.has("cleared_stages"):
		var raw_ids: Variant = source["cleared_stages"]
		if typeof(raw_ids) != TYPE_ARRAY:
			return null
		for raw_id in raw_ids:
			if typeof(raw_id) != TYPE_STRING and typeof(raw_id) != TYPE_STRING_NAME:
				return null
			result.mark_cleared(StringName(raw_id))
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed` (the 4a tests, including "future version rejected" and "json float version accepted", still pass against version 2).

- [ ] **Step 5: Commit**

```bash
git add src/meta tests/meta
git commit -m "feat(meta): save schema v2 with cleared_stages and the v1->v2 migration" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `SaveService.record_clear`

**Files:**
- Modify: `src/meta/save_service.gd`
- Test: `tests/meta/test_save_service.gd`

**Interfaces:**
- Consumes: `SaveData.mark_cleared`.
- Produces: `SaveService.record_clear(stage_id: StringName) -> void` (emits `changed`; does not save).

- [ ] **Step 1: Write the failing tests**

Append to `tests/meta/test_save_service.gd`:

```gdscript
func test_record_clear_marks_the_stage_and_emits_changed() -> void:
	var service := _service()
	var emitted: Array = []
	service.changed.connect(func() -> void: emitted.append(true))
	service.record_clear(&"stage_01")
	assert_eq(service.data.cleared_stages, [&"stage_01"])
	assert_eq(emitted.size(), 1)


func test_record_clear_twice_keeps_one_entry() -> void:
	var service := _service()
	service.record_clear(&"stage_01")
	service.record_clear(&"stage_01")
	assert_eq(service.data.cleared_stages.size(), 1)


func test_a_recorded_clear_survives_save_and_reload() -> void:
	var service := _service()
	service.record_clear(&"stage_01")
	assert_true(service.save())
	assert_eq(_service().data.cleared_stages, [&"stage_01"])
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/meta/test_save_service tools/run_tests.sh`
Expected: FAIL (`record_clear` not found).

- [ ] **Step 3: Implement**

In `src/meta/save_service.gd`, after `add_gem` add:

```gdscript
## Marks a stage as cleared. The caller saves (Main does so once, together with the gem credit).
func record_clear(stage_id: StringName) -> void:
	data.mark_cleared(stage_id)
	changed.emit()
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/meta/save_service.gd tests/meta/test_save_service.gd
git commit -m "feat(meta): SaveService.record_clear" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `StageCatalog`, `stage_02`, and the shipped catalog

**Files:**
- Create: `src/defs/stage_catalog.gd`, `data/stages/stage_02.tres`, `data/stages/stage_catalog.tres`
- Test: `tests/defs/test_stage_catalog.gd`; modify `tests/data/test_game_data.gd`

**Interfaces:**
- Produces: `StageCatalog` (`Resource`): `@export var stages: Array[StageDef]`, `index_of(id: StringName) -> int` (-1 when absent), `validate() -> PackedStringArray`. Data: `res://data/stages/stage_catalog.tres` (stage_01, stage_02 in that order).

- [ ] **Step 1: Write the failing tests**

`tests/defs/test_stage_catalog.gd`:

```gdscript
extends TestCase


func _stage(id: StringName) -> StageDef:
	var stage := DefFactory.stage()
	stage.id = id
	return stage


func _catalog(ids: Array[StringName]) -> StageCatalog:
	var catalog := StageCatalog.new()
	for id in ids:
		catalog.stages.append(_stage(id))
	return catalog


func test_a_valid_catalog_has_no_errors() -> void:
	assert_no_errors(_catalog([&"a", &"b"]).validate(), "two distinct valid stages")


func test_an_empty_catalog_is_an_error() -> void:
	assert_eq(StageCatalog.new().validate().size(), 1)


func test_a_null_entry_is_an_error() -> void:
	var catalog := _catalog([&"a"])
	catalog.stages.append(null)
	assert_eq(catalog.validate().size(), 1)


func test_duplicate_ids_are_an_error() -> void:
	assert_eq(_catalog([&"a", &"a"]).validate().size(), 1)


func test_a_broken_nested_stage_is_reported() -> void:
	var catalog := _catalog([&"a", &"b"])
	catalog.stages[1].duration = 0.0
	assert_true(catalog.validate().size() >= 1)


func test_index_of_finds_a_stage_or_returns_minus_one() -> void:
	var catalog := _catalog([&"a", &"b"])
	assert_eq(catalog.index_of(&"b"), 1)
	assert_eq(catalog.index_of(&"missing"), -1)
```

Append to `tests/data/test_game_data.gd` (and add `"res://data/stages/stage_02.tres",` and `"res://data/stages/stage_catalog.tres",` to `DATA_FILES`, after the `stage_01.tres` line):

```gdscript
func test_stage_02_validates_and_pays_gem() -> void:
	var stage := load("res://data/stages/stage_02.tres") as StageDef
	assert_not_null(stage)
	assert_no_errors(stage.validate(), "stage_02")
	assert_eq(stage.id, &"stage_02")
	assert_true(stage.gem_reward > 0)


func test_the_shipped_catalog_is_valid_and_ordered() -> void:
	var catalog := load("res://data/stages/stage_catalog.tres") as StageCatalog
	assert_not_null(catalog)
	assert_no_errors(catalog.validate(), "stage_catalog")
	assert_eq(catalog.stages.size(), 2)
	assert_eq(catalog.stages[0].id, &"stage_01")
	assert_eq(catalog.stages[1].id, &"stage_02")
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/defs/test_stage_catalog tools/run_tests.sh`
Expected: FAIL (`StageCatalog` not declared).

- [ ] **Step 3: Implement**

`src/defs/stage_catalog.gd`:

```gdscript
class_name StageCatalog
extends Resource
## The ordered list of stages. Order is unlock order: a stage opens once the one before it is cleared.

@export var stages: Array[StageDef] = []


## Index of the stage with this id, or -1.
func index_of(id: StringName) -> int:
	for index in stages.size():
		if stages[index] != null and stages[index].id == id:
			return index
	return -1


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if stages.is_empty():
		errors.append("StageCatalog: no stages")
	var seen: Dictionary[StringName, bool] = {}
	for stage in stages:
		if stage == null:
			errors.append("StageCatalog: null stage")
			continue
		if seen.has(stage.id):
			errors.append("StageCatalog: duplicate stage id '%s'" % stage.id)
		seen[stage.id] = true
		errors.append_array(stage.validate())
	return errors
```

`data/stages/stage_02.tres` (a copy of `stage_01.tres` with its own id, name, building health and reward; no `uid` in the header so it does not clash with stage_01's):

```
[gd_resource type="Resource" script_class="StageDef" format=3]

[ext_resource type="Script" uid="uid://edi68dpd4cif" path="res://src/defs/stage_def.gd" id="1_stage"]
[ext_resource type="Script" uid="uid://bpy31rqnwoa0w" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="Script" uid="uid://7lu42npm7k84" path="res://src/defs/enemy_def.gd" id="3_enemy_script"]
[ext_resource type="Texture2D" uid="uid://5ym870q28bhr" path="res://assets/art/world/grass.png" id="4_grass"]
[ext_resource type="Texture2D" uid="uid://cadl0aiogqx84" path="res://assets/art/farmrpg/house.png" id="5_house"]
[ext_resource type="Resource" uid="uid://xtn3qim6evc7" path="res://data/enemies/orc.tres" id="6_orc"]
[ext_resource type="Resource" uid="uid://cvw2bvotb2gvx" path="res://data/enemies/orc_swarmer.tres" id="7_swarmer"]
[ext_resource type="Resource" uid="uid://c07j5gs0twowk" path="res://data/waves/stage_01_waves.tres" id="8_waves"]
[ext_resource type="Resource" path="res://data/grade_table.tres" id="9_grades"]
[ext_resource type="Resource" path="res://data/events/stage_01_events.tres" id="10_events"]
[ext_resource type="Resource" path="res://data/bosses/orc_warlord.tres" id="11_boss"]

[sub_resource type="Resource" id="Resource_building"]
script = ExtResource("2_stats")
max_health = 400.0
move_speed = 0.0

[sub_resource type="AtlasTexture" id="AtlasTexture_house"]
atlas = ExtResource("5_house")
region = Rect2(2, 2, 76, 90)

[resource]
script = ExtResource("1_stage")
id = &"stage_02"
display_name = "Orc Frontier"
ground_texture = ExtResource("4_grass")
building_texture = SubResource("AtlasTexture_house")
building_stats = SubResource("Resource_building")
trickle_enemies = Array[ExtResource("3_enemy_script")]([ExtResource("6_orc"), ExtResource("7_swarmer")])
waves = ExtResource("8_waves")
start_gold = 200
start_stone = 0
drop_multiplier = 10000.0
grade_table = ExtResource("9_grades")
events = ExtResource("10_events")
boss = ExtResource("11_boss")
gem_reward = 20
```

`data/stages/stage_catalog.tres`:

```
[gd_resource type="Resource" script_class="StageCatalog" format=3]

[ext_resource type="Script" path="res://src/defs/stage_catalog.gd" id="1_catalog"]
[ext_resource type="Script" path="res://src/defs/stage_def.gd" id="2_stage_def"]
[ext_resource type="Resource" path="res://data/stages/stage_01.tres" id="3_stage_01"]
[ext_resource type="Resource" path="res://data/stages/stage_02.tres" id="4_stage_02"]

[resource]
script = ExtResource("1_catalog")
stages = Array[ExtResource("2_stage_def")]([ExtResource("3_stage_01"), ExtResource("4_stage_02")])
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/defs tools/run_tests.sh` then `FILTER=tests/data tools/run_tests.sh`
Expected: `0 failed`. If `stage_02` fails to validate, compare it against `stage_01.tres` line by line (the textures/enemies/waves references must match).

- [ ] **Step 5: Commit**

```bash
git add src/defs/stage_catalog.gd src/defs/stage_catalog.gd.uid data/stages tests/defs tests/data
git commit -m "feat(data): add StageCatalog, stage_02 and the shipped catalog" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `StageProgress` (derived unlock)

**Files:**
- Create: `src/meta/stage_progress.gd`
- Test: `tests/meta/test_stage_progress.gd`

**Interfaces:**
- Consumes: `StageCatalog.stages`, `SaveData.cleared_stages`.
- Produces: `StageProgress` (`RefCounted`): `enum Status { LOCKED, AVAILABLE, CLEARED }`, `_init(catalog: StageCatalog, save: SaveData)`, `status(index: int) -> Status`, `is_unlocked(index: int) -> bool` (`status != LOCKED`; false for an out-of-range index).

- [ ] **Step 1: Write the failing tests**

`tests/meta/test_stage_progress.gd`:

```gdscript
extends TestCase


func _progress(cleared: Array[StringName]) -> StageProgress:
	var catalog := StageCatalog.new()
	for id: StringName in [&"a", &"b", &"c"]:
		var stage := DefFactory.stage()
		stage.id = id
		catalog.stages.append(stage)
	var save := SaveData.new()
	for id in cleared:
		save.mark_cleared(id)
	return StageProgress.new(catalog, save)


func test_the_first_stage_is_always_available() -> void:
	var progress := _progress([])
	assert_eq(progress.status(0), StageProgress.Status.AVAILABLE)
	assert_true(progress.is_unlocked(0))


func test_later_stages_are_locked_until_the_previous_is_cleared() -> void:
	var progress := _progress([])
	assert_eq(progress.status(1), StageProgress.Status.LOCKED)
	assert_false(progress.is_unlocked(1))
	assert_eq(progress.status(2), StageProgress.Status.LOCKED)


func test_clearing_a_stage_unlocks_the_next_one_only() -> void:
	var progress := _progress([&"a"])
	assert_eq(progress.status(0), StageProgress.Status.CLEARED)
	assert_eq(progress.status(1), StageProgress.Status.AVAILABLE)
	assert_eq(progress.status(2), StageProgress.Status.LOCKED)


func test_a_cleared_stage_stays_playable() -> void:
	assert_true(_progress([&"a"]).is_unlocked(0))


func test_an_id_not_in_the_catalog_is_ignored() -> void:
	var progress := _progress([&"ghost"])
	assert_eq(progress.status(0), StageProgress.Status.AVAILABLE)
	assert_eq(progress.status(1), StageProgress.Status.LOCKED)


func test_an_out_of_range_index_is_locked() -> void:
	var progress := _progress([&"a", &"b", &"c"])
	assert_eq(progress.status(3), StageProgress.Status.LOCKED)
	assert_eq(progress.status(-1), StageProgress.Status.LOCKED)
	assert_false(progress.is_unlocked(3))
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/meta/test_stage_progress tools/run_tests.sh`
Expected: FAIL (`StageProgress` not declared).

- [ ] **Step 3: Implement**

`src/meta/stage_progress.gd`:

```gdscript
class_name StageProgress
extends RefCounted
## Which stages are locked, available or cleared. Derived from the catalog order and the save; stores nothing.

enum Status { LOCKED, AVAILABLE, CLEARED }

var _catalog: StageCatalog
var _save: SaveData


func _init(catalog: StageCatalog, save: SaveData) -> void:
	_catalog = catalog
	_save = save


## CLEARED if the stage id is saved as cleared; else AVAILABLE if it is first or the previous stage is cleared; else LOCKED.
func status(index: int) -> Status:
	if index < 0 or index >= _catalog.stages.size():
		return Status.LOCKED
	if _save.cleared_stages.has(_catalog.stages[index].id):
		return Status.CLEARED
	if index == 0 or _save.cleared_stages.has(_catalog.stages[index - 1].id):
		return Status.AVAILABLE
	return Status.LOCKED


## A cleared stage stays playable.
func is_unlocked(index: int) -> bool:
	return status(index) != Status.LOCKED
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/meta/stage_progress.gd src/meta/stage_progress.gd.uid tests/meta/test_stage_progress.gd tests/meta/test_stage_progress.gd.uid
git commit -m "feat(meta): add StageProgress (derived stage unlock)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `HUB` route, `Hub` screen and `StageCard`

**Files:**
- Modify: `src/app/scene_router.gd`
- Create: `src/meta/hub/hub.gd`, `src/meta/hub/hub.tscn`, `src/meta/hub/stage_card.gd`, `src/meta/hub/stage_card.tscn`
- Test: `tests/meta/test_hub.gd` (the existing `test_every_route_has_a_scene_that_loads` in `tests/app/test_scene_router.gd` covers the new route)

**Interfaces:**
- Consumes: `StageCatalog`, `StageProgress`, `SaveService` (`data`), `SceneRouter` (`go`, `Route`, `PARAM_STAGE_DEF`).
- Produces:
  - `SceneRouter.Route.HUB` (appended last in the enum so existing values do not shift) → `res://src/meta/hub/hub.tscn`.
  - `Hub` (scene root `Control`): optional injection `var catalog: StageCatalog`, `var save_service: Node`, `var router: Node`; `_on_play_pressed(index: int)`. Unique-name nodes: `%StageList` (VBox of cards), `%EmptyLabel`, `%SkillTreeButton` (disabled), `%BackButton`.
  - `StageCard` (`class_name StageCard`, root `HBoxContainer`): `signal play_pressed(index: int)`, `setup(index: int, stage_name: String, status: StageProgress.Status)`. Unique-name nodes: `%NameLabel`, `%StatusLabel`, `%PlayButton`.

- [ ] **Step 1: Write the failing tests**

`tests/meta/test_hub.gd`:

```gdscript
extends TestCase

const HUB_SCENE: PackedScene = preload("res://src/meta/hub/hub.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_hub"
const PATH := "user://test_hub/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _catalog(count: int) -> StageCatalog:
	var catalog := StageCatalog.new()
	for index in count:
		var stage := DefFactory.stage()
		stage.id = StringName("s%d" % (index + 1))
		stage.display_name = "Stage %d" % (index + 1)
		catalog.stages.append(stage)
	return catalog


func _hub(catalog: StageCatalog, service: Node, router: Node) -> Control:
	var hub := HUB_SCENE.instantiate() as Control
	hub.catalog = catalog
	hub.save_service = service
	hub.router = router
	return adopt(hub)


func _card(hub: Control, index: int) -> Node:
	return hub.get_node("%StageList").get_child(index)


func _play_button(hub: Control, index: int) -> Button:
	return _card(hub, index).get_node("%PlayButton") as Button


func test_one_card_per_stage_with_its_name() -> void:
	var hub := _hub(_catalog(3), _service(), adopt(Probe.new()))
	assert_eq(hub.get_node("%StageList").get_child_count(), 3)
	assert_eq(_card(hub, 1).get_node("%NameLabel").text, "Stage 2")


func test_only_the_first_stage_is_playable_on_a_fresh_save() -> void:
	var hub := _hub(_catalog(3), _service(), adopt(Probe.new()))
	assert_false(_play_button(hub, 0).disabled)
	assert_true(_play_button(hub, 1).disabled)
	assert_true(_play_button(hub, 2).disabled)
	assert_eq(_card(hub, 1).get_node("%StatusLabel").text, "Locked")


func test_a_cleared_stage_opens_the_next_and_shows_cleared() -> void:
	var service := _service()
	service.record_clear(&"s1")
	var hub := _hub(_catalog(3), service, adopt(Probe.new()))
	assert_eq(_card(hub, 0).get_node("%StatusLabel").text, "Cleared")
	assert_false(_play_button(hub, 0).disabled, "a cleared stage can be replayed")
	assert_false(_play_button(hub, 1).disabled)
	assert_true(_play_button(hub, 2).disabled)


func test_play_goes_to_the_stage_with_that_stage_def() -> void:
	var catalog := _catalog(2)
	var router: Node = adopt(Probe.new())
	var hub := _hub(catalog, _service(), router)
	_play_button(hub, 0).pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])
	assert_true(router.take_params().get(Probe.PARAM_STAGE_DEF) == catalog.stages[0])


func test_a_locked_stage_does_not_start_even_when_the_handler_is_called() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(_catalog(2), _service(), router)
	hub._on_play_pressed(1)
	assert_eq(router.changed_to.size(), 0)


func test_back_goes_to_the_main_menu() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(_catalog(1), _service(), router)
	hub.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_the_skill_tree_button_is_reserved_but_disabled() -> void:
	var hub := _hub(_catalog(1), _service(), adopt(Probe.new()))
	assert_true(hub.get_node("%SkillTreeButton").disabled)


func test_an_empty_catalog_shows_an_empty_state_and_back_still_works() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(StageCatalog.new(), _service(), router)
	assert_eq(hub.get_node("%StageList").get_child_count(), 0)
	assert_true(hub.get_node("%EmptyLabel").visible)
	hub.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_an_invalid_catalog_does_not_crash_the_hub() -> void:
	var catalog := _catalog(2)
	catalog.stages[1].duration = 0.0
	var hub := _hub(catalog, _service(), adopt(Probe.new()))
	assert_true(hub.get_node("%EmptyLabel").visible)
	assert_eq(hub.get_node("%StageList").get_child_count(), 0)


func test_the_shipped_catalog_is_the_default() -> void:
	var hub := HUB_SCENE.instantiate() as Control
	hub.save_service = _service()
	hub.router = adopt(Probe.new())
	adopt(hub)
	assert_eq(hub.get_node("%StageList").get_child_count(), 2)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/meta/test_hub tools/run_tests.sh`
Expected: FAIL (`hub.tscn` does not exist).

- [ ] **Step 3: Implement**

`src/app/scene_router.gd`: change the enum and table:

```gdscript
enum Route { LOGO, MAIN_MENU, SETTINGS, STAGE, HUB }
```

and add inside `ROUTES` after the `Route.STAGE` entry:

```gdscript
	Route.HUB: "res://src/meta/hub/hub.tscn",
```

`src/meta/hub/stage_card.gd`:

```gdscript
class_name StageCard
extends HBoxContainer
## One stage row in the hub: name, status and a Play button. It only reports the click.

signal play_pressed(index: int)

var _index: int = -1

@onready var _name_label: Label = %NameLabel
@onready var _status_label: Label = %StatusLabel
@onready var _play_button: Button = %PlayButton


func _ready() -> void:
	_play_button.pressed.connect(_on_play_pressed)


## Call after the card is in the tree.
func setup(index: int, stage_name: String, status: StageProgress.Status) -> void:
	_index = index
	_name_label.text = stage_name
	_status_label.text = _status_text(status)
	_play_button.disabled = status == StageProgress.Status.LOCKED


func _status_text(status: StageProgress.Status) -> String:
	match status:
		StageProgress.Status.LOCKED:
			return "Locked"
		StageProgress.Status.CLEARED:
			return "Cleared"
		_:
			return ""


func _on_play_pressed() -> void:
	play_pressed.emit(_index)
```

`src/meta/hub/stage_card.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/hub/stage_card.gd" id="1_card"]

[node name="StageCard" type="HBoxContainer"]
custom_minimum_size = Vector2(420, 0)
theme_override_constants/separation = 16
script = ExtResource("1_card")

[node name="NameLabel" type="Label" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(220, 0)
layout_mode = 2
size_flags_horizontal = 3
text = "Stage"

[node name="StatusLabel" type="Label" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(80, 0)
layout_mode = 2

[node name="PlayButton" type="Button" parent="."]
unique_name_in_owner = true
layout_mode = 2
text = "Play"
```

`src/meta/hub/hub.gd`:

```gdscript
extends Control
## The hub: one card per catalog stage, Back, and a reserved Skill Tree button (4c). It owns no state: it asks the
## router to move on. A stage opens once the previous one is cleared (see StageProgress).

const STAGE_CARD_SCENE: PackedScene = preload("res://src/meta/hub/stage_card.tscn")
const DEFAULT_CATALOG: StageCatalog = preload("res://data/stages/stage_catalog.tres")

## Default to the shipped catalog and the autoloads in _ready; tests set them first.
var catalog: StageCatalog
var save_service: Node
var router: Node

var _progress: StageProgress

@onready var _stage_list: VBoxContainer = %StageList
@onready var _empty_label: Label = %EmptyLabel
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if catalog == null:
		catalog = DEFAULT_CATALOG
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_back_button.pressed.connect(_on_back_pressed)
	_back_button.grab_focus()
	var errors := catalog.validate()
	if not errors.is_empty():
		push_error("Hub: unusable stage catalog: %s" % ", ".join(errors))
		_empty_label.visible = true
		return
	_progress = StageProgress.new(catalog, save_service.data)
	for index in catalog.stages.size():
		var card := STAGE_CARD_SCENE.instantiate() as StageCard
		_stage_list.add_child(card)
		card.setup(index, catalog.stages[index].display_name, _progress.status(index))
		card.play_pressed.connect(_on_play_pressed)


func _on_play_pressed(index: int) -> void:
	if _progress == null or not _progress.is_unlocked(index):
		return
	router.go(SceneRouter.Route.STAGE, {SceneRouter.PARAM_STAGE_DEF: catalog.stages[index]})


func _on_back_pressed() -> void:
	router.go(SceneRouter.Route.MAIN_MENU)
```

`src/meta/hub/hub.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/hub/hub.gd" id="1_hub"]

[node name="Hub" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_hub")

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
text = "Stages"
horizontal_alignment = 1

[node name="StageList" type="VBoxContainer" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
theme_override_constants/separation = 8

[node name="EmptyLabel" type="Label" parent="Center/Column"]
unique_name_in_owner = true
visible = false
layout_mode = 2
text = "No stages available"
horizontal_alignment = 1

[node name="SkillTreeButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
disabled = true
text = "Skill Tree (coming soon)"

[node name="BackButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Back"
```

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/meta tools/run_tests.sh` and `FILTER=tests/app tools/run_tests.sh`
Expected: `0 failed` (the 4a route-load test now also loads `hub.tscn`; the invalid-catalog test prints an expected `ERROR: Hub: unusable stage catalog` line).

- [ ] **Step 5: Commit**

```bash
git add src/app/scene_router.gd src/meta/hub tests/meta
git commit -m "feat(meta): add the HUB route, the hub screen and stage cards" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Wire the flow (menu → hub, `Main` records clears, result → hub)

**Files:**
- Modify: `src/meta/menu/main_menu.gd`, `src/app/main.gd`
- Test: modify `tests/meta/test_main_menu.gd`, `tests/app/test_main.gd`, `tests/app/test_boot_flow.gd`

**Interfaces:**
- Consumes: `SceneRouter.Route.HUB` (Task 5), `SaveService.record_clear` (Task 2), `Hub` (Task 5), `StageCatalog` data (Task 3).
- Produces: Start → `HUB`; `Main` records the clear for `CLEARED` runs; Enter on the result screen → `HUB`.

- [ ] **Step 1: Update and add the failing tests**

`tests/meta/test_main_menu.gd`: replace `test_start_goes_to_the_stage_with_the_shipped_stage_def` with

```gdscript
func test_start_goes_to_the_hub_without_params() -> void:
	var router: Node = adopt(Probe.new())
	var menu := _menu(router)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])
	assert_eq(router.take_params(), {})
```

`tests/app/test_main.gd`: run

```bash
sed -i 's/Probe.Route.MAIN_MENU/Probe.Route.HUB/; s/returns_to_the_main_menu/returns_to_the_hub/' tests/app/test_main.gd
```

and append:

```gdscript
func test_a_cleared_run_records_the_clear_and_saves_it() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.cleared_stages, [&"stage_01"])
	assert_eq(SaveStore.load_or_default(PATH).cleared_stages, [&"stage_01"], "persisted to disk")


func test_a_failed_run_records_no_clear() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	assert_eq(service.data.cleared_stages.size(), 0)


func test_finishing_twice_records_the_clear_once() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.cleared_stages.size(), 1)
```

Replace the whole of `tests/app/test_boot_flow.gd` with:

```gdscript
extends TestCase
## Walks the real scenes through the real router table and shipped catalog, with a temp save:
## menu -> hub -> stage_01 cleared -> hub shows stage_02 open.

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_boot_flow"
const PATH := "user://test_boot_flow/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _hub(service: Node, router: Node) -> Node:
	var hub: Node = load(router.path_for(Probe.Route.HUB)).instantiate()
	hub.save_service = service
	hub.router = router
	return adopt(hub)


func _play_button(hub: Node, index: int) -> Button:
	return hub.get_node("%StageList").get_child(index).get_node("%PlayButton") as Button


func test_clearing_stage_01_from_the_hub_pays_gem_unlocks_stage_02_and_survives_a_relaunch() -> void:
	var router: Node = adopt(Probe.new())
	var service := _service()

	var menu: Node = load(router.path_for(Probe.Route.MAIN_MENU)).instantiate()
	menu.router = router
	adopt(menu)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])

	var hub := _hub(service, router)
	assert_false(_play_button(hub, 0).disabled)
	assert_true(_play_button(hub, 1).disabled, "stage_02 starts locked")
	_play_button(hub, 0).pressed.emit()
	assert_eq(router.changed_to.back(), router.path_for(Probe.Route.STAGE))

	var main: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	main.save_service = service
	main.router = router
	# The probe never changes scenes, so hand Main the params the router kept for it.
	main.stage_def = router.take_params().get(Probe.PARAM_STAGE_DEF) as StageDef
	adopt(main)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	main._stage.finished.emit(result)
	assert_eq(service.data.gem, main.stage_def.gem_reward)

	var relaunched := _service()
	assert_eq(relaunched.data.cleared_stages, [&"stage_01"], "a fresh session reads the saved clear")
	var reopened_hub := _hub(relaunched, router)
	assert_false(_play_button(reopened_hub, 1).disabled, "stage_02 is open after clearing stage_01")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/app tools/run_tests.sh` and `FILTER=tests/meta/test_main_menu tools/run_tests.sh`
Expected: FAIL (Start still routes to `STAGE`; `Main` does not record clears; Enter still routes to the main menu).

- [ ] **Step 3: Implement**

`src/meta/menu/main_menu.gd`: delete the line `const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")`, update the class doc comment's last sentence (`## Start / Settings / Quit. Holds no state: it asks the router to move on.`), and replace `_on_start_pressed` with

```gdscript
func _on_start_pressed() -> void:
	router.go(SceneRouter.Route.HUB)
```

`src/app/main.gd`: in `_unhandled_input` change `router.go(SceneRouter.Route.MAIN_MENU)` to `router.go(SceneRouter.Route.HUB)`; in `_on_finished`, after `save_service.add_gem(result.gem)` and before `save_service.save()` add

```gdscript
	if result.outcome == RunResult.Outcome.CLEARED:
		save_service.record_clear(stage_def.id)
```

- [ ] **Step 4: Run to verify pass**

Run: `tools/run_tests.sh` (whole suite)
Expected: `0 failed`, no `SCRIPT ERROR`.

- [ ] **Step 5: Commit**

```bash
git add src/meta/menu/main_menu.gd src/app/main.gd tests/meta/test_main_menu.gd tests/app
git commit -m "feat(app): menu starts at the hub, Main records clears and returns to the hub" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Documentation and end-to-end check

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Update `CLAUDE.md`**

In "Architecture rules", after the Phase 4a bullets, append:

```markdown
- Phase 4b hub: `Start` goes to `HUB`; `Hub` (`src/meta/hub/`) lists `data/stages/stage_catalog.tres` (an ordered `StageCatalog`;
  order is unlock order) and sends `go(STAGE, {PARAM_STAGE_DEF: stage})`. Unlock is derived (`StageProgress`: first stage
  open, otherwise the previous stage must be in `SaveData.cleared_stages`; a cleared stage stays playable) - never store an
  unlocked list. `Main` calls `SaveService.record_clear(stage_def.id)` for a CLEARED run next to the gem credit and saves
  once; Enter on the result screen returns to the hub. Save schema is v2; ids in `cleared_stages` that are not in the
  catalog are kept, not dropped.
```

Add a section after "Adding an ability (data only)":

```markdown
## Adding a stage (data only)
1. Duplicate `data/stages/stage_02.tres` (drop the header `uid` so it does not clash); change `id`, `display_name`, stats,
   `gem_reward`, and point `waves`/`events`/`boss` at new or shared `.tres` files. `StageDef.validate()` must pass.
2. Append it to `data/stages/stage_catalog.tres` (`stages` order = unlock order) and add the file to `DATA_FILES` in
   `tests/data/test_game_data.gd`; update the catalog order assertion in the same file.
```

In "Running the game", change the sentence about the result screen to: `After a run ends Enter returns to the hub; R still restarts (dev shortcut - a restarted clear pays gem again).`

- [ ] **Step 2: Run the whole suite**

Run: `tools/run_tests.sh`
Expected: `---- N passed, 0 failed ----`.

- [ ] **Step 3: Drive the real scenes once (not a substitute for playing)**

Save the following as a scratch script outside the project (for example in the session scratchpad directory) and run it with `Godot --headless --path . -s <script>`. It uses a temp save path so the developer's real `save.json` is untouched. Report what it prints.

```gdscript
extends SceneTree

const SAVE := "user://flow_check_4b.json"
var _frames := 0
var _step := 0


func _initialize() -> void:
	var svc = root.get_node("SaveService")
	svc.path = SAVE
	svc.reload()
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
				var list := s.get_node("%StageList")
				print("[4b] hub cards=%d stage_02 disabled=%s" % [list.get_child_count(), list.get_child(1).get_node("%PlayButton").disabled])
				list.get_child(0).get_node("%PlayButton").pressed.emit()
				_step = 2
		2:
			if s.name == "Main" and s.get("_stage") != null:
				var result := RunResult.new()
				result.outcome = RunResult.Outcome.CLEARED
				s._stage.finished.emit(result)
				var enter := InputEventKey.new()
				enter.keycode = KEY_ENTER
				enter.pressed = true
				Input.parse_input_event(enter)
				_step = 3
		3:
			if s.name == "Hub":
				var list2 := s.get_node("%StageList")
				print("[4b] back at hub; stage_01=%s stage_02 disabled=%s" % [list2.get_child(0).get_node("%StatusLabel").text, list2.get_child(1).get_node("%PlayButton").disabled])
				DirAccess.remove_absolute(SAVE)
				quit()
	if _frames > 3000:
		print("[4b] TIMEOUT step=%d scene=%s" % [_step, s.name])
		quit(1)
	return false
```

Expected output: `hub cards=2 stage_02 disabled=true`, then `back at hub; stage_01=Cleared stage_02 disabled=false`.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: document the Phase 4b hub, unlock rule and how to add a stage" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review (spec coverage)

- Modules table: `StageCatalog` + data (T3), `stage_02` (T3), `SaveData` (T1), `SaveMigrations` (T1), `SaveService.record_clear` (T2), `StageProgress` (T4), `Hub` + `StageCard` (T5).
- Routes (`HUB`): T5. MainMenu change: T6. `Main` records clears, Enter → hub: T6. `Stage` untouched: no task edits `src/stage/`.
- Data flow 1-5: menu → hub (T6), hub build/status rendering (T5), Play with re-check (T5), result handling (T6), return to hub (T6).
- Save format v2 and migration: T1. Error handling: v1 save (T1), non-array/non-string (T1), unknown id preserved (T1 + T4), empty/invalid catalog (T5), duplicate clears (T1/T2).
- Testing list: catalog (T3), SaveData (T1), SaveMigrations (T1), SaveStore v1 file (T1), SaveService (T2), StageProgress (T4), Hub (T5), MainMenu (T6), Main (T6), smoke (T6 `test_boot_flow`). The "future version rejected" 4a test keeps running unchanged.
- Type consistency: `cleared_stages: Array[StringName]`, `mark_cleared(id)`, `record_clear(stage_id)`, `StageProgress.new(catalog, save)`, `status(index)`, `is_unlocked(index)`, `Status { LOCKED, AVAILABLE, CLEARED }`, `StageCard.setup(index, stage_name, status)`/`play_pressed(index)`, `Hub._on_play_pressed(index)`, `Route.HUB` are used with identical names across tasks.
