# Phase 4a — Scene flow, save foundation, main menu, settings: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A player can launch the game, pass the logo, start `stage_01` from a main menu, clear it, earn gem that is saved to disk, relaunch and still have it, and adjust a persisted master volume.

**Architecture:** Two autoloads (`SceneRouter`, `SaveService`) own scene changes and the live `SaveData`. `SaveData`/`SettingsData`/`SaveMigrations`/`SaveStore` are plain classes (unit-testable with temp paths); the autoloads are thin wiring. Screens (`Main`, `MainMenu`, `SettingsScreen`, `Logo`) take their collaborators through injectable properties that default to the autoloads, so tests inject fakes. `Stage` is not modified.

**Tech Stack:** Godot 4.7.2, GDScript (typed, tabs), the repo's headless `TestCase` runner.

**Spec:** `docs/superpowers/specs/2026-10-14-phase4a-scene-flow-save-design.md` (parent: `2026-10-13-vampire-survivor-base-design.md`). Read both before starting.

## Global Constraints

- Only two new autoloads: `SceneRouter` and `SaveService`. Their scripts have **no `class_name`** (a global class named like an autoload is an error); other scripts reach them by the autoload name.
- `Stage` and everything below `app/`/`meta/` must not reference the autoloads. Do not edit `src/stage/**`.
- `app/` and `meta/` are the only layers that may touch autoloads.
- Save file: `user://save.json`, JSON (never `.tres`), `{"version": 1, "gem": 0, "settings": {"master_volume": 1.0}}`. Writes go to `save.json.tmp` then rename. Unreadable files are moved to `save.json.bak`, never overwritten.
- Gem is the only persistent currency. Reward comes from data (`StageDef.gem_reward`), never a literal in code. CLEARED pays `gem_reward`, FAILED pays 0.
- Master volume is clamped to 0..1.
- No balance numbers in code. Never edit `addons/`.
- Code style: typed GDScript, tabs, `##` doc comments, match surrounding files. Tests mirror `src/` under `tests/`, extend `TestCase`, files named `test_*.gd`, methods `test_*`.
- New `class_name`s need a class-cache refresh: run the first test of each task with `IMPORT=1` (editor closed) so `.godot/global_script_class_cache.cfg` and the `.gd.uid` files are generated. Commit the generated `.uid` files with their scripts (`git add <directory>`).
- Run tests with `tools/run_tests.sh` (`FILTER=<path substring>`, `IMPORT=1`). The `godot-ai` MCP server was down when this plan was written: edit `project.godot` by hand and do not rely on editor tools.
- The working tree has unrelated untracked plugin files under `addons/`. Never `git add -A`/`git add .`; add only the paths named in each task.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (shown below as a second `-m`).

## Review Focus

Failure modes the spec implies that a reader would not think to test; each is pinned by a test in the owning task.

1. `gem` written by the game comes back from JSON as a float (`5.0`): it must load as `5`, while `2.5`, a string, a negative, or a huge float is treated as corruption (Task 1).
2. A save file that is valid JSON but not an object (`[]`, `42`, `null`) or is empty (0 bytes) must be quarantined, not crash (Task 2).
3. A second corrupt file when a `save.json.bak` already exists must still be quarantined and still start from defaults (Task 2).
4. Volume `0` must not produce `-inf` dB; out-of-range volume is clamped (Task 1 for the model, Task 3 for the audio bus).
5. A run that reports `finished` twice credits gem once; FAILED credits 0; a negative `add_gem` is ignored (Tasks 3 and 6).

---

## File structure

| File | Create/Modify | Responsibility |
|---|---|---|
| `src/meta/settings_data.gd` | Create | `SettingsData`: persisted options (`master_volume`), clamped, dict round trip |
| `src/meta/save_migrations.gd` | Create | `SaveMigrations`: version check + N→N+1 steps; `read_int` JSON-number helper |
| `src/meta/save_data.gd` | Create | `SaveData`: `gem`, `settings`; `to_dict`/`from_dict` |
| `src/meta/save_store.gd` | Create | `SaveStore`: atomic write, load, quarantine |
| `src/meta/save_service.gd` | Create | Autoload `SaveService`: live `data`, `add_gem`, `save`, `reload` |
| `src/app/audio_settings.gd` | Create | `AudioSettings.apply(settings)` to the Master bus |
| `src/app/scene_router.gd` | Create | Autoload `SceneRouter`: routes, `go`, `take_params` |
| `src/defs/stage_def.gd` | Modify | `gem_reward`, `gem_for(outcome)`, validation |
| `src/defs/run_result.gd` | Modify | `gem` field |
| `data/stages/stage_01.tres` | Modify | `gem_reward = 10` |
| `src/core/input_actions.gd` | Modify | `CONFIRM` action name |
| `src/app/main.gd` | Modify | Stage screen: params, gem credit, Continue |
| `src/ui/hud/hud.gd` | Modify | Result text mentions Enter/R |
| `src/app/logo.gd` / `logo.tscn` | Create | Boot screen → main menu |
| `src/meta/menu/main_menu.gd` / `.tscn` | Create | Start / Settings / Quit |
| `src/meta/menu/settings_screen.gd` / `.tscn` | Create | Volume slider + Back |
| `project.godot` | Modify | Two autoloads; `main_scene` → logo |
| `tests/support/save_files.gd` | Create | Temp-directory helper for save tests |
| `tests/support/scene_router_probe.gd` | Create | Router subclass that records instead of changing scenes |
| `tests/meta/*`, `tests/app/*`, `tests/defs/test_stage_def.gd`, `tests/data/test_game_data.gd` | Create/Modify | Tests per task |
| `CLAUDE.md` | Modify | Phase 4a section |

---

### Task 1: Save model (`SettingsData`, `SaveMigrations`, `SaveData`)

**Files:**
- Create: `src/meta/settings_data.gd`, `src/meta/save_migrations.gd`, `src/meta/save_data.gd`
- Test: `tests/meta/test_settings_data.gd`, `tests/meta/test_save_migrations.gd`, `tests/meta/test_save_data.gd`

**Interfaces:**
- Produces:
  - `SettingsData` (`RefCounted`): `var master_volume: float` (setter clamps 0..1), `to_dict() -> Dictionary`, `static from_dict(source: Dictionary) -> SettingsData` (null on a wrong-typed field).
  - `SaveMigrations`: `const CURRENT_VERSION := 1`, `static steps() -> Array[Callable]`, `static migrate(source: Dictionary) -> Dictionary`, `static migrate_with(source: Dictionary, steps: Array[Callable], current: int) -> Dictionary` (empty dict = rejected), `static read_int(raw: Variant) -> Variant` (`int` or `null`).
  - `SaveData` (`RefCounted`): `var gem: int`, `var settings: SettingsData`, `to_dict()`, `static from_dict(source: Dictionary) -> SaveData` (null when invalid; `source` must already be migrated).

- [ ] **Step 1: Write the failing tests**

`tests/meta/test_settings_data.gd`:

```gdscript
extends TestCase


func test_default_volume_is_full() -> void:
	assert_almost_eq(SettingsData.new().master_volume, 1.0)


func test_volume_is_clamped_to_zero_one() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 3.0
	assert_almost_eq(settings.master_volume, 1.0)
	settings.master_volume = -2.0
	assert_almost_eq(settings.master_volume, 0.0)


func test_dict_round_trip() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.25
	var restored := SettingsData.from_dict(settings.to_dict())
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 0.25)


func test_missing_key_keeps_the_default() -> void:
	var restored := SettingsData.from_dict({})
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 1.0)


func test_wrong_type_is_rejected() -> void:
	assert_null(SettingsData.from_dict({"master_volume": "loud"}))


func test_out_of_range_volume_from_a_file_is_clamped_not_rejected() -> void:
	var restored := SettingsData.from_dict({"master_volume": 7})
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 1.0)
```

`tests/meta/test_save_migrations.gd`:

```gdscript
extends TestCase


func test_shipped_steps_cover_every_version_below_current() -> void:
	assert_eq(SaveMigrations.steps().size(), SaveMigrations.CURRENT_VERSION - 1, "one step per past version")


func test_current_version_is_returned_unchanged() -> void:
	var source := {"version": SaveMigrations.CURRENT_VERSION, "gem": 4}
	var migrated := SaveMigrations.migrate(source)
	assert_eq(migrated.get("gem"), 4)
	assert_eq(migrated.get("version"), SaveMigrations.CURRENT_VERSION)


func test_old_save_is_upgraded_through_every_step() -> void:
	var steps: Array[Callable] = [
		func(d: Dictionary) -> Dictionary:
			d["gem"] = d.get("coins", 0)
			d.erase("coins")
			return d,
		func(d: Dictionary) -> Dictionary:
			d["extra"] = true
			return d,
	]
	var migrated := SaveMigrations.migrate_with({"version": 1, "coins": 7}, steps, 3)
	assert_eq(migrated.get("gem"), 7)
	assert_true(migrated.get("extra", false))
	assert_false(migrated.has("coins"))
	assert_eq(migrated.get("version"), 3)


func test_future_version_is_rejected() -> void:
	assert_true(SaveMigrations.migrate({"version": SaveMigrations.CURRENT_VERSION + 1}).is_empty())


func test_missing_or_invalid_version_is_rejected() -> void:
	assert_true(SaveMigrations.migrate({}).is_empty())
	assert_true(SaveMigrations.migrate({"version": "1"}).is_empty())
	assert_true(SaveMigrations.migrate({"version": 0}).is_empty())
	assert_true(SaveMigrations.migrate({"version": 1.5}).is_empty())


func test_json_style_float_version_is_accepted() -> void:
	assert_false(SaveMigrations.migrate({"version": 1.0}).is_empty())


func test_migration_does_not_mutate_its_input() -> void:
	var source := {"version": 1, "gem": 1}
	SaveMigrations.migrate(source)
	assert_eq(source, {"version": 1, "gem": 1})


func test_read_int_accepts_whole_numbers_only() -> void:
	assert_eq(SaveMigrations.read_int(5), 5)
	assert_eq(SaveMigrations.read_int(5.0), 5)
	assert_null(SaveMigrations.read_int(2.5))
	assert_null(SaveMigrations.read_int("5"))
	assert_null(SaveMigrations.read_int(null))
	assert_null(SaveMigrations.read_int(1.0e30))
```

`tests/meta/test_save_data.gd`:

```gdscript
extends TestCase


func test_defaults() -> void:
	var data := SaveData.new()
	assert_eq(data.gem, 0)
	assert_almost_eq(data.settings.master_volume, 1.0)


func test_to_dict_carries_the_current_version() -> void:
	assert_eq(SaveData.new().to_dict().get("version"), SaveMigrations.CURRENT_VERSION)


func test_round_trip() -> void:
	var data := SaveData.new()
	data.gem = 42
	data.settings.master_volume = 0.5
	var restored := SaveData.from_dict(data.to_dict())
	assert_not_null(restored)
	assert_eq(restored.gem, 42)
	assert_almost_eq(restored.settings.master_volume, 0.5)


func test_missing_fields_get_defaults() -> void:
	var restored := SaveData.from_dict({"version": 1})
	assert_not_null(restored)
	assert_eq(restored.gem, 0)
	assert_almost_eq(restored.settings.master_volume, 1.0)


func test_gem_written_as_a_json_float_loads_as_an_int() -> void:
	var restored := SaveData.from_dict({"gem": 5.0})
	assert_not_null(restored)
	assert_eq(restored.gem, 5)
	assert_eq(typeof(restored.gem), TYPE_INT)


func test_invalid_gem_is_rejected() -> void:
	assert_null(SaveData.from_dict({"gem": 2.5}), "fraction")
	assert_null(SaveData.from_dict({"gem": "5"}), "string")
	assert_null(SaveData.from_dict({"gem": -1}), "negative")
	assert_null(SaveData.from_dict({"gem": 1.0e30}), "huge")


func test_invalid_settings_are_rejected() -> void:
	assert_null(SaveData.from_dict({"settings": 3}), "not a dictionary")
	assert_null(SaveData.from_dict({"settings": {"master_volume": "x"}}), "bad field")
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `IMPORT=1 FILTER=tests/meta tools/run_tests.sh`
Expected: FAIL / `!! script errors detected` (`SettingsData`, `SaveMigrations`, `SaveData` not declared).

- [ ] **Step 3: Implement**

`src/meta/settings_data.gd`:

```gdscript
class_name SettingsData
extends RefCounted
## Player-adjustable options that persist across runs.

const DEFAULT_MASTER_VOLUME: float = 1.0

## Linear 0..1; the setter clamps.
var master_volume: float = DEFAULT_MASTER_VOLUME:
	set(value):
		master_volume = clampf(value, 0.0, 1.0)


func to_dict() -> Dictionary:
	return {"master_volume": master_volume}


## Missing keys keep their defaults; returns null when a present key has the wrong type.
static func from_dict(source: Dictionary) -> SettingsData:
	var result := SettingsData.new()
	if source.has("master_volume"):
		var volume: Variant = source["master_volume"]
		if typeof(volume) != TYPE_FLOAT and typeof(volume) != TYPE_INT:
			return null
		result.master_volume = float(volume)
	return result
```

`src/meta/save_migrations.gd`:

```gdscript
class_name SaveMigrations
extends RefCounted
## Brings a loaded save dictionary up to the current schema one version at a time.
## To change the schema: bump CURRENT_VERSION and append one step to `steps()`.

const CURRENT_VERSION: int = 1
## JSON numbers are floats; anything beyond this is not a plausible whole count.
const MAX_SAFE_INT: float = 9.0e15


## steps()[i] upgrades a version (i + 1) dictionary to version (i + 2).
static func steps() -> Array[Callable]:
	return []


## Returns the migrated copy, or an empty dictionary when the version is missing, invalid or from the future.
static func migrate(source: Dictionary) -> Dictionary:
	return migrate_with(source, steps(), CURRENT_VERSION)


static func migrate_with(source: Dictionary, migration_steps: Array[Callable], current: int) -> Dictionary:
	var raw_version: Variant = read_int(source.get("version"))
	if raw_version == null:
		return {}
	var version: int = raw_version
	if version < 1 or version > current:
		return {}
	var result := source.duplicate(true)
	while version < current:
		result = migration_steps[version - 1].call(result)
		version += 1
	result["version"] = current
	return result


## A whole JSON number as an int, else null. JSON has no int type, so 5 is stored and read back as 5.0.
static func read_int(raw: Variant) -> Variant:
	if typeof(raw) == TYPE_INT:
		return raw
	if typeof(raw) == TYPE_FLOAT and absf(raw) < MAX_SAFE_INT and is_equal_approx(raw, roundf(raw)):
		return int(raw)
	return null
```

`src/meta/save_data.gd`:

```gdscript
class_name SaveData
extends RefCounted
## Everything that persists between sessions. Serialised by SaveStore; migrated by SaveMigrations.

## The only persistent currency.
var gem: int = 0
var settings: SettingsData = SettingsData.new()


func to_dict() -> Dictionary:
	return {
		"version": SaveMigrations.CURRENT_VERSION,
		"gem": gem,
		"settings": settings.to_dict(),
	}


## `source` must already be migrated. Missing fields keep defaults; returns null when a present field is invalid.
static func from_dict(source: Dictionary) -> SaveData:
	var result := SaveData.new()
	if source.has("gem"):
		var gem_value: Variant = SaveMigrations.read_int(source["gem"])
		if gem_value == null or gem_value < 0:
			return null
		result.gem = gem_value
	if source.has("settings"):
		var raw_settings: Variant = source["settings"]
		if typeof(raw_settings) != TYPE_DICTIONARY:
			return null
		var parsed := SettingsData.from_dict(raw_settings)
		if parsed == null:
			return null
		result.settings = parsed
	return result
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: all `tests/meta/*` PASS, `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/meta tests/meta
git commit -m "feat(meta): add the versioned save model (SettingsData, SaveMigrations, SaveData)" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `SaveStore` (file IO)

**Files:**
- Create: `src/meta/save_store.gd`, `tests/support/save_files.gd`
- Test: `tests/meta/test_save_store.gd`

**Interfaces:**
- Consumes: `SaveData.to_dict/from_dict`, `SaveMigrations.migrate`.
- Produces:
  - `SaveStore.load_or_default(path: String) -> SaveData` (never null).
  - `SaveStore.write(path: String, data: SaveData) -> bool`.
  - `SaveFiles.reset(directory: String) -> void` (test helper: creates the directory and deletes every file in it).

- [ ] **Step 1: Write the test helper and the failing tests**

`tests/support/save_files.gd`:

```gdscript
class_name SaveFiles
extends RefCounted
## Test helper: a clean scratch directory under user:// for save-file tests.


static func reset(directory: String) -> void:
	DirAccess.make_dir_recursive_absolute(directory)
	var dir := DirAccess.open(directory)
	if dir == null:
		return
	for file_name in dir.get_files():
		dir.remove(file_name)


static func write_text(path: String, text: String) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text)
	file.close()
```

`tests/meta/test_save_store.gd`:

```gdscript
extends TestCase

const DIR := "user://test_save_store"
const PATH := "user://test_save_store/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func test_missing_file_gives_defaults_and_no_backup() -> void:
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 0)
	assert_false(FileAccess.file_exists(PATH + ".bak"))


func test_write_then_load_round_trips() -> void:
	var data := SaveData.new()
	data.gem = 12
	data.settings.master_volume = 0.4
	assert_true(SaveStore.write(PATH, data))
	var loaded := SaveStore.load_or_default(PATH)
	assert_eq(loaded.gem, 12)
	assert_almost_eq(loaded.settings.master_volume, 0.4)


func test_write_leaves_no_temp_file_and_overwrites_an_existing_save() -> void:
	var first := SaveData.new()
	first.gem = 1
	var second := SaveData.new()
	second.gem = 2
	assert_true(SaveStore.write(PATH, first))
	assert_true(SaveStore.write(PATH, second))
	assert_false(FileAccess.file_exists(PATH + ".tmp"))
	assert_eq(SaveStore.load_or_default(PATH).gem, 2)


func test_write_to_a_missing_directory_fails_without_crashing() -> void:
	assert_false(SaveStore.write("user://test_save_store/missing_dir/save.json", SaveData.new()))


func test_corrupt_json_is_moved_to_backup_and_defaults_are_used() -> void:
	SaveFiles.write_text(PATH, "{ not json")
	var data := SaveStore.load_or_default(PATH)
	assert_eq(data.gem, 0)
	assert_false(FileAccess.file_exists(PATH), "the unreadable file is moved away")
	assert_eq(FileAccess.get_file_as_string(PATH + ".bak"), "{ not json", "the player's bytes are kept")


func test_valid_json_that_is_not_an_object_is_treated_as_corrupt() -> void:
	for text in ["[]", "42", "null", "\"hi\""]:
		SaveFiles.reset(DIR)
		SaveFiles.write_text(PATH, text)
		assert_eq(SaveStore.load_or_default(PATH).gem, 0, text)
		assert_true(FileAccess.file_exists(PATH + ".bak"), "backup for %s" % text)


func test_empty_file_is_treated_as_corrupt() -> void:
	SaveFiles.write_text(PATH, "")
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.file_exists(PATH + ".bak"))


func test_wrong_field_type_is_treated_as_corrupt() -> void:
	SaveFiles.write_text(PATH, '{"version": 1, "gem": "lots"}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.file_exists(PATH + ".bak"))


func test_future_version_is_treated_as_corrupt_and_preserved() -> void:
	SaveFiles.write_text(PATH, '{"version": 999, "gem": 5}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_true(FileAccess.get_file_as_string(PATH + ".bak").contains("999"))


func test_a_second_corrupt_file_replaces_an_existing_backup() -> void:
	SaveFiles.write_text(PATH + ".bak", "old backup")
	SaveFiles.write_text(PATH, "garbage")
	assert_eq(SaveStore.load_or_default(PATH).gem, 0)
	assert_eq(FileAccess.get_file_as_string(PATH + ".bak"), "garbage")


func test_gem_stored_as_a_float_in_the_file_loads() -> void:
	SaveFiles.write_text(PATH, '{"version": 1, "gem": 5.0}')
	assert_eq(SaveStore.load_or_default(PATH).gem, 5)
	assert_false(FileAccess.file_exists(PATH + ".bak"))
```

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/meta/test_save_store tools/run_tests.sh`
Expected: FAIL (`SaveStore` not declared).

- [ ] **Step 3: Implement**

`src/meta/save_store.gd`:

```gdscript
class_name SaveStore
extends RefCounted
## Reads and writes the save file. Never loses the player's data: writes are atomic and unreadable files are
## moved aside rather than overwritten.


## Always returns a usable SaveData: defaults when the file is absent or unreadable.
static func load_or_default(path: String) -> SaveData:
	if not FileAccess.file_exists(path):
		return SaveData.new()
	var json := JSON.new()
	if json.parse(FileAccess.get_file_as_string(path)) == OK and typeof(json.data) == TYPE_DICTIONARY:
		var migrated := SaveMigrations.migrate(json.data)
		if not migrated.is_empty():
			var data := SaveData.from_dict(migrated)
			if data != null:
				return data
	_quarantine(path)
	return SaveData.new()


## Writes `<path>.tmp` then renames it over `path`, so a crash cannot leave a half-written save.
static func write(path: String, data: SaveData) -> bool:
	var temp_path := path + ".tmp"
	var file := FileAccess.open(temp_path, FileAccess.WRITE)
	if file == null:
		push_error("SaveStore: cannot open '%s' for writing (error %d)" % [temp_path, FileAccess.get_open_error()])
		return false
	file.store_string(JSON.stringify(data.to_dict(), "\t"))
	file.close()
	var error := DirAccess.rename_absolute(temp_path, path)
	if error != OK:
		push_error("SaveStore: cannot move '%s' over '%s' (error %d)" % [temp_path, path, error])
		return false
	return true


static func _quarantine(path: String) -> void:
	var backup := path + ".bak"
	if FileAccess.file_exists(backup):
		DirAccess.remove_absolute(backup)
	var error := DirAccess.rename_absolute(path, backup)
	push_warning("SaveStore: '%s' is unreadable; kept as '%s' and starting from defaults (rename error %d)" % [path, backup, error])
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`. If `test_write_then_load_round_trips` or the overwrite test fail on `rename_absolute` over an existing file on this platform, make `write` call `DirAccess.remove_absolute(path)` before the rename when the target exists, and re-run (keep the temp-then-rename order).

- [ ] **Step 5: Commit**

```bash
git add src/meta tests/meta tests/support/save_files.gd tests/support/save_files.gd.uid
git commit -m "feat(meta): add SaveStore with atomic writes and corrupt-file quarantine" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `AudioSettings` and the `SaveService` autoload

**Files:**
- Create: `src/app/audio_settings.gd`, `src/meta/save_service.gd`
- Modify: `project.godot` (`[autoload]` section)
- Test: `tests/app/test_audio_settings.gd`, `tests/meta/test_save_service.gd`

**Interfaces:**
- Consumes: `SaveStore`, `SettingsData`.
- Produces:
  - `AudioSettings.apply(settings: SettingsData) -> void` (static).
  - Autoload `SaveService` (no `class_name`): `var data: SaveData`, `var path: String` (default `"user://save.json"`), `signal changed`, `reload() -> void`, `add_gem(amount: int) -> void` (ignores negatives), `save() -> bool`.

- [ ] **Step 1: Write the failing tests**

`tests/app/test_audio_settings.gd`:

```gdscript
extends TestCase

var _saved_db: float = 0.0


func before_each() -> void:
	_saved_db = AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func after_each() -> void:
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Master"), _saved_db)


func _master_db() -> float:
	return AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func test_half_volume_sets_the_matching_decibels() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.5
	AudioSettings.apply(settings)
	assert_almost_eq(_master_db(), linear_to_db(0.5), 0.01)


func test_full_volume_is_zero_db() -> void:
	AudioSettings.apply(SettingsData.new())
	assert_almost_eq(_master_db(), 0.0, 0.01)


func test_zero_volume_is_finite_and_effectively_silent() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.0
	AudioSettings.apply(settings)
	assert_true(is_finite(_master_db()), "no -inf")
	assert_true(_master_db() <= -60.0)
```

`tests/meta/test_save_service.gd`:

```gdscript
extends TestCase

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const DIR := "user://test_save_service"
const PATH := "user://test_save_service/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func test_starts_from_defaults_without_a_file() -> void:
	assert_eq(_service().data.gem, 0)


func test_add_gem_accumulates_and_emits_changed() -> void:
	var service := _service()
	var emitted: Array = []
	service.changed.connect(func() -> void: emitted.append(true))
	service.add_gem(3)
	service.add_gem(4)
	assert_eq(service.data.gem, 7)
	assert_eq(emitted.size(), 2)


func test_negative_gem_is_ignored() -> void:
	var service := _service()
	service.add_gem(5)
	service.add_gem(-3)
	assert_eq(service.data.gem, 5)


func test_save_then_reload_keeps_gem_and_volume() -> void:
	var service := _service()
	service.add_gem(9)
	service.data.settings.master_volume = 0.3
	assert_true(service.save())
	var fresh := _service()
	assert_eq(fresh.data.gem, 9)
	assert_almost_eq(fresh.data.settings.master_volume, 0.3)
```

(`adopt` adds the node to the tree, so `_ready` runs `reload()` with the temp path set beforehand.)

- [ ] **Step 2: Run to verify failure**

Run: `IMPORT=1 FILTER=tests/app/test_audio tools/run_tests.sh` then `FILTER=tests/meta/test_save_service tools/run_tests.sh`
Expected: FAIL (`AudioSettings` not declared; `save_service.gd` missing).

- [ ] **Step 3: Implement**

`src/app/audio_settings.gd`:

```gdscript
class_name AudioSettings
extends RefCounted
## Applies persisted settings to the audio server.

## Floor for the linear volume so 0 maps to a finite, inaudible level instead of -inf dB.
const MIN_LINEAR: float = 0.0001


static func apply(settings: SettingsData) -> void:
	var bus := AudioServer.get_bus_index(&"Master")
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(settings.master_volume, MIN_LINEAR)))
```

`src/meta/save_service.gd`:

```gdscript
extends Node
## Autoload "SaveService". Holds the session's SaveData and persists it. Screens request changes here and
## never keep save state of their own. No class_name: a global class must not share an autoload's name.

signal changed

const SAVE_PATH: String = "user://save.json"

var data: SaveData = SaveData.new()
## Overridable so tests can point at a scratch file; set before the node enters the tree.
var path: String = SAVE_PATH


func _ready() -> void:
	reload()


func reload() -> void:
	data = SaveStore.load_or_default(path)
	AudioSettings.apply(data.settings)
	changed.emit()


func add_gem(amount: int) -> void:
	if amount < 0:
		push_error("SaveService: add_gem(%d) ignored, amount must be >= 0" % amount)
		return
	data.gem += amount
	changed.emit()


func save() -> bool:
	return SaveStore.write(path, data)
```

`project.godot`: in `[autoload]`, directly after the `GameplayCueManager` line add only

```
SaveService="*res://src/meta/save_service.gd"
```

(`SceneRouter` is registered in Task 4, once its script exists.)

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/app/test_audio_settings tools/run_tests.sh` and `FILTER=tests/meta tools/run_tests.sh`
Expected: `0 failed`, no `SCRIPT ERROR` (the autoload loads at startup, so a broken `save_service.gd` fails every run).

- [ ] **Step 5: Commit**

```bash
git add src/app/audio_settings.gd src/app/audio_settings.gd.uid src/meta tests/app tests/meta project.godot
git commit -m "feat(meta): add AudioSettings and the SaveService autoload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `SceneRouter` autoload

**Files:**
- Create: `src/app/scene_router.gd`, `tests/support/scene_router_probe.gd`
- Modify: `project.godot` (autoload line)
- Test: `tests/app/test_scene_router.gd`

**Interfaces:**
- Produces: autoload `SceneRouter` (no `class_name`):
  - `enum Route { LOGO, MAIN_MENU, SETTINGS, STAGE }`
  - `const ROUTES: Dictionary[Route, String]`
  - `const PARAM_STAGE_DEF: StringName = &"stage_def"`
  - `path_for(route: Route) -> String` (`""` when unknown)
  - `go(route: Route, params: Dictionary = {}) -> bool`
  - `take_params() -> Dictionary` (returns once, then `{}`)
  - overridable `_change_scene(path: String) -> Error`
- Test probe `tests/support/scene_router_probe.gd` (extends the router script): records `changed_to: Array[String]` and returns `next_error`.

Note: `logo.tscn`, `main_menu.tscn` and `settings_screen.tscn` do not exist until Task 7; the "every route loads" test is added there. This task tests the mechanics only.

- [ ] **Step 1: Write the probe and the failing tests**

`tests/support/scene_router_probe.gd`:

```gdscript
extends "res://src/app/scene_router.gd"
## Test double: records scene changes instead of performing them.

var changed_to: Array[String] = []
var next_error: Error = OK


func _change_scene(path: String) -> Error:
	changed_to.append(path)
	return next_error
```

`tests/app/test_scene_router.gd`:

```gdscript
extends TestCase

const Router := preload("res://src/app/scene_router.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _probe() -> Node:
	return adopt(Probe.new())


func test_go_changes_to_the_route_scene() -> void:
	var router := _probe()
	assert_true(router.go(Router.Route.MAIN_MENU))
	assert_eq(router.changed_to, [Router.ROUTES[Router.Route.MAIN_MENU]])


func test_params_are_handed_over_exactly_once() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"a": 1})
	assert_eq(router.take_params(), {"a": 1})
	assert_eq(router.take_params(), {}, "second read is empty")


func test_a_new_go_replaces_unread_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"a": 1})
	router.go(Router.Route.MAIN_MENU)
	assert_eq(router.take_params(), {})


func test_unknown_route_changes_nothing_and_keeps_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"keep": true})
	assert_false(router.call("go", 999, {"x": 1}))
	assert_eq(router.changed_to.size(), 1, "no second scene change")
	assert_eq(router.take_params(), {"keep": true})


func test_failed_scene_change_reports_false_and_keeps_previous_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"keep": true})
	router.next_error = ERR_CANT_OPEN
	assert_false(router.go(Router.Route.MAIN_MENU, {"lost": true}))
	assert_eq(router.take_params(), {"keep": true})


func test_path_for_an_unknown_route_is_empty() -> void:
	assert_eq(_probe().call("path_for", 999), "")


func test_the_stage_param_key_is_a_stable_name() -> void:
	assert_eq(Router.PARAM_STAGE_DEF, &"stage_def")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/app/test_scene_router tools/run_tests.sh`
Expected: FAIL (`scene_router.gd` not found).

- [ ] **Step 3: Implement**

`src/app/scene_router.gd`:

```gdscript
extends Node
## Autoload "SceneRouter". The only place that knows scene paths. Screens ask for a route and hand the next
## screen one-shot params. No class_name: a global class must not share an autoload's name.

enum Route { LOGO, MAIN_MENU, SETTINGS, STAGE }

const ROUTES: Dictionary[Route, String] = {
	Route.LOGO: "res://src/app/logo.tscn",
	Route.MAIN_MENU: "res://src/meta/menu/main_menu.tscn",
	Route.SETTINGS: "res://src/meta/menu/settings_screen.tscn",
	Route.STAGE: "res://src/app/main.tscn",
}

## Param keys screens agree on, so no screen hard-codes another's strings.
const PARAM_STAGE_DEF: StringName = &"stage_def"

var _params: Dictionary = {}


func path_for(route: Route) -> String:
	return ROUTES.get(route, "")


## Changes to the route's scene; `params` become readable once through `take_params()` by the next screen.
## Returns false (and leaves the current screen and params alone) for an unknown route or a failed change.
func go(route: Route, params: Dictionary = {}) -> bool:
	var path := path_for(route)
	if path == "":
		push_error("SceneRouter: unknown route %s" % str(route))
		return false
	var previous := _params
	_params = params
	var error := _change_scene(path)
	if error != OK:
		_params = previous
		push_error("SceneRouter: cannot change to '%s' (error %d)" % [path, error])
		return false
	return true


func take_params() -> Dictionary:
	var taken := _params
	_params = {}
	return taken


func _change_scene(path: String) -> Error:
	return get_tree().change_scene_to_file(path)
```

`project.godot`: in `[autoload]`, add `SceneRouter="*res://src/app/scene_router.gd"` before the `SaveService` line.

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 FILTER=tests/app tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/app/scene_router.gd src/app/scene_router.gd.uid tests/app tests/support/scene_router_probe.gd tests/support/scene_router_probe.gd.uid project.godot
git commit -m "feat(app): add the SceneRouter autoload" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Gem data (`StageDef.gem_reward`, `RunResult.gem`)

**Files:**
- Modify: `src/defs/stage_def.gd`, `src/defs/run_result.gd`, `data/stages/stage_01.tres`
- Test: `tests/defs/test_stage_def.gd`, `tests/data/test_game_data.gd`

**Interfaces:**
- Produces: `StageDef.gem_reward: int`, `StageDef.gem_for(outcome: RunResult.Outcome) -> int`, `RunResult.gem: int`.

- [ ] **Step 1: Write the failing tests**

Append to `tests/defs/test_stage_def.gd`:

```gdscript
func test_gem_reward_is_paid_on_clear_only() -> void:
	var def := DefFactory.stage()
	def.gem_reward = 7
	assert_eq(def.gem_for(RunResult.Outcome.CLEARED), 7)
	assert_eq(def.gem_for(RunResult.Outcome.FAILED), 0)


func test_gem_reward_defaults_to_zero() -> void:
	assert_eq(DefFactory.stage().gem_reward, 0)


func test_negative_gem_reward_is_a_validation_error() -> void:
	var def := DefFactory.stage()
	def.gem_reward = -1
	assert_eq(def.validate().size(), 1)
```

Append to `tests/data/test_game_data.gd`:

```gdscript
func test_stage_01_pays_gem() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_true(stage.gem_reward > 0, "a cleared stage_01 must pay gem")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/defs/test_stage_def tools/run_tests.sh`
Expected: FAIL (`gem_reward` / `gem_for` not found).

- [ ] **Step 3: Implement**

`src/defs/run_result.gd`: after `var stone: int = 0` add

```gdscript
## Persistent currency earned by this run; filled in by the caller from StageDef.gem_for (Stage leaves it 0).
var gem: int = 0
```

`src/defs/stage_def.gd`: after the `@export var boss: BossDef` line add

```gdscript
## Gem (the persistent currency) paid when the stage is cleared; a failed run pays nothing.
@export var gem_reward: int = 0
```

in `validate()`, after the `drop_multiplier` check add

```gdscript
	if gem_reward < 0:
		errors.append("StageDef '%s': gem_reward must be >= 0" % id)
```

and after `validate()` add

```gdscript
func gem_for(outcome: RunResult.Outcome) -> int:
	return gem_reward if outcome == RunResult.Outcome.CLEARED else 0
```

`data/stages/stage_01.tres`: after the `boss = ExtResource("11_boss")` line add `gem_reward = 10`.

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/defs tools/run_tests.sh` and `FILTER=tests/data tools/run_tests.sh`
Expected: `0 failed`.

- [ ] **Step 5: Commit**

```bash
git add src/defs/stage_def.gd src/defs/run_result.gd data/stages/stage_01.tres tests/defs/test_stage_def.gd tests/data/test_game_data.gd
git commit -m "feat(defs): add StageDef.gem_reward and RunResult.gem" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `Main` becomes the stage screen (params, gem credit, Continue)

**Files:**
- Modify: `src/app/main.gd`, `src/core/input_actions.gd`, `src/ui/hud/hud.gd:58-61`
- Test: `tests/app/test_main.gd`; check `tests/ui/test_hud.gd` for assertions on the result text.

**Interfaces:**
- Consumes: `SceneRouter.take_params()/go`, `SceneRouter.PARAM_STAGE_DEF`, `SaveService.add_gem/save`, `StageDef.gem_for`.
- Produces on `Main`: `var save_service: Node`, `var router: Node`, `var stage_def: StageDef` (all optional injection points; defaults resolved in `_ready`). `InputActions.CONFIRM: StringName = &"ui_accept"`.

- [ ] **Step 1: Write the failing test**

`tests/app/test_main.gd`:

```gdscript
extends TestCase

const MAIN_SCENE: PackedScene = preload("res://src/app/main.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_main"
const PATH := "user://test_main/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _main(service: Node, router: Node = null) -> Node:
	var main := MAIN_SCENE.instantiate()
	main.save_service = service
	if router != null:
		main.router = router
	return adopt(main)


func _result(outcome: RunResult.Outcome) -> RunResult:
	var result := RunResult.new()
	result.outcome = outcome
	return result


func test_the_default_stage_is_the_shipped_stage_when_no_params_are_given() -> void:
	var main := _main(_service())
	assert_eq(main.stage_def.id, &"stage_01")


func test_a_cleared_run_credits_the_stage_reward_and_saves_it() -> void:
	var service := _service()
	var main := _main(service)
	var reward: int = main.stage_def.gem_reward
	assert_true(reward > 0)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.gem, reward)
	assert_eq(SaveStore.load_or_default(PATH).gem, reward, "persisted to disk")


func test_a_failed_run_credits_nothing() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	assert_eq(service.data.gem, 0)


func test_finishing_twice_credits_only_once() -> void:
	var service := _service()
	var main := _main(service)
	var reward: int = main.stage_def.gem_reward
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.gem, reward)


func test_confirm_after_a_result_returns_to_the_main_menu() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	var press := InputEventAction.new()
	press.action = InputActions.CONFIRM
	press.pressed = true
	main._unhandled_input(press)
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_confirm_during_a_run_does_nothing() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	var press := InputEventAction.new()
	press.action = InputActions.CONFIRM
	press.pressed = true
	main._unhandled_input(press)
	assert_eq(router.changed_to.size(), 0)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/app/test_main tools/run_tests.sh`
Expected: FAIL (`save_service`, `stage_def`, `InputActions.CONFIRM` not found).

- [ ] **Step 3: Implement**

`src/core/input_actions.gd`: after the `RESTART` line add

```gdscript
const CONFIRM: StringName = &"ui_accept"
```

`src/app/main.gd` — replace the header/constants and the first functions up to `_start_run`, and `_on_finished`:

```gdscript
extends Node
## The stage screen: runs a stage with its HUD, routes HUD requests to the stage, pays the run's gem into the
## save, and leaves for the main menu on request. Reached through SceneRouter (params: the StageDef).

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const HUD_SCENE: PackedScene = preload("res://src/ui/hud/hud.tscn")
## Used when the scene runs standalone (F6) with no params.
const DEFAULT_STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")
const PROFILE: PlayerProfile = preload("res://data/characters/default_profile.tres")

const BUILDING_BAR_OFFSET := Vector2(0, -60)

## Collaborators default to the autoloads in _ready; tests set them first.
var save_service: Node
var router: Node
var stage_def: StageDef

var _stage: Stage
var _hud: Hud
var _building_bar: WorldHealthBar
var _result: RunResult


func _ready() -> void:
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	if stage_def == null:
		stage_def = router.take_params().get(SceneRouter.PARAM_STAGE_DEF, DEFAULT_STAGE_DEF) as StageDef
	_start_run()


func _unhandled_input(event: InputEvent) -> void:
	if _result == null:
		return
	if event.is_action_pressed(InputActions.RESTART):
		_start_run()
	elif event.is_action_pressed(InputActions.CONFIRM):
		router.go(SceneRouter.Route.MAIN_MENU)
```

In `_start_run`, change `_stage.start(STAGE_DEF, PROFILE)` to `_stage.start(stage_def, PROFILE)`. Replace `_on_finished` with:

```gdscript
func _on_finished(result: RunResult) -> void:
	if _result != null:
		return
	result.gem = stage_def.gem_for(result.outcome)
	_result = result
	save_service.add_gem(result.gem)
	save_service.save()
	_hud.show_result(result)
	print("[Main] %s after %.1fs - kills %d, gold %d, stone %d, gem %d" % [
		RunResult.Outcome.keys()[result.outcome], result.elapsed, result.kills, result.gold, result.stone, result.gem,
	])
```

`src/ui/hud/hud.gd` `show_result`: change the text to include gem and the new prompt:

```gdscript
	_result_label.text = "%s\nKills %d   Gold %d   Stone %d   Gem %d\nEnter: continue   R: restart" % [title, result.kills, result.gold, result.stone, result.gem]
```

If `tests/ui/test_hud.gd` asserts the old "Press R to restart" text, update that assertion to the new text.

Note for the implementer: R (restart) is a dev shortcut kept from earlier phases; a restarted run is a new run and pays gem again when cleared. This is intended for now (spec: "R-restart stays as a dev shortcut").

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/app tools/run_tests.sh`, then the whole suite `tools/run_tests.sh`
Expected: `0 failed` (the Stage tests are untouched).

- [ ] **Step 5: Commit**

```bash
git add src/app/main.gd src/core/input_actions.gd src/ui/hud/hud.gd tests/app/test_main.gd tests/app/test_main.gd.uid tests/ui
git commit -m "feat(app): Main takes its stage from router params, pays gem once and continues to the menu" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Logo, main menu, settings screen, boot wiring

**Files:**
- Create: `src/app/logo.gd`, `src/app/logo.tscn`, `src/meta/menu/main_menu.gd`, `src/meta/menu/main_menu.tscn`, `src/meta/menu/settings_screen.gd`, `src/meta/menu/settings_screen.tscn`
- Modify: `project.godot` (`application/run/main_scene`)
- Test: `tests/app/test_logo.gd`, `tests/meta/test_main_menu.gd`, `tests/meta/test_settings_screen.gd`, extend `tests/app/test_scene_router.gd`

**Interfaces:**
- Consumes: `SceneRouter` (injected as `router`), `SaveService` (injected as `save_service`), `AudioSettings.apply`.
- Produces: scenes at the paths in `SceneRouter.ROUTES`; `Logo`/`MainMenu`/`SettingsScreen` each expose optional `router` (and `SettingsScreen` also `save_service`).

- [ ] **Step 1: Write the failing tests**

Append to `tests/app/test_scene_router.gd`:

```gdscript
func test_every_route_has_a_scene_that_loads() -> void:
	assert_eq(Router.ROUTES.size(), Router.Route.size(), "every Route enum value has a path")
	for route in Router.ROUTES:
		var path: String = Router.ROUTES[route]
		assert_true(load(path) is PackedScene, path)
```

`tests/app/test_logo.gd`:

```gdscript
extends TestCase

const LOGO_SCENE: PackedScene = preload("res://src/app/logo.tscn")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _key_press() -> InputEventKey:
	var event := InputEventKey.new()
	event.pressed = true
	event.keycode = KEY_SPACE
	return event


func _logo(router: Node) -> Control:
	var logo := LOGO_SCENE.instantiate() as Control
	logo.router = router
	return adopt(logo)


func test_a_key_press_skips_to_the_main_menu() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	logo._unhandled_input(_key_press())
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_proceeding_twice_changes_scene_once() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	logo._unhandled_input(_key_press())
	logo._unhandled_input(_key_press())
	assert_eq(router.changed_to.size(), 1)


func test_a_key_release_does_not_skip() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	var release := _key_press()
	release.pressed = false
	logo._unhandled_input(release)
	assert_eq(router.changed_to.size(), 0)
```

`tests/meta/test_main_menu.gd`:

```gdscript
extends TestCase

const MENU_SCENE: PackedScene = preload("res://src/meta/menu/main_menu.tscn")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _menu(router: Node) -> Control:
	var menu := MENU_SCENE.instantiate() as Control
	menu.router = router
	return adopt(menu)


func test_start_goes_to_the_stage_with_the_shipped_stage_def() -> void:
	var router: Node = adopt(Probe.new())
	var menu := _menu(router)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])
	var params: Dictionary = router.take_params()
	var stage := params.get(Probe.PARAM_STAGE_DEF) as StageDef
	assert_not_null(stage)
	assert_eq(stage.id, &"stage_01")


func test_settings_goes_to_the_settings_screen() -> void:
	var router: Node = adopt(Probe.new())
	var menu := _menu(router)
	menu.get_node("%SettingsButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.SETTINGS)])


func test_the_menu_shows_a_quit_button() -> void:
	var menu := _menu(adopt(Probe.new()))
	assert_not_null(menu.get_node_or_null("%QuitButton"))
```

`tests/meta/test_settings_screen.gd`:

```gdscript
extends TestCase

const SCREEN_SCENE: PackedScene = preload("res://src/meta/menu/settings_screen.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_settings_screen"
const PATH := "user://test_settings_screen/save.json"

var _saved_db: float = 0.0


func before_each() -> void:
	SaveFiles.reset(DIR)
	_saved_db = AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func after_each() -> void:
	SaveFiles.reset(DIR)
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Master"), _saved_db)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _screen(service: Node, router: Node) -> Control:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.save_service = service
	screen.router = router
	return adopt(screen)


func test_the_slider_starts_at_the_saved_volume() -> void:
	var service := _service()
	service.data.settings.master_volume = 0.3
	var screen := _screen(service, adopt(Probe.new()))
	assert_almost_eq(screen.get_node("%VolumeSlider").value, 0.3, 0.001)


func test_moving_the_slider_updates_the_model_and_the_audio_bus() -> void:
	var service := _service()
	var screen := _screen(service, adopt(Probe.new()))
	screen.get_node("%VolumeSlider").value = 0.5
	assert_almost_eq(service.data.settings.master_volume, 0.5, 0.001)
	var bus_db := AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))
	assert_almost_eq(bus_db, linear_to_db(0.5), 0.01)


func test_back_saves_and_returns_to_the_main_menu() -> void:
	var service := _service()
	var router: Node = adopt(Probe.new())
	var screen := _screen(service, router)
	screen.get_node("%VolumeSlider").value = 0.25
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])
	assert_almost_eq(SaveStore.load_or_default(PATH).settings.master_volume, 0.25, 0.001, "persisted")
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/app tools/run_tests.sh` and `FILTER=tests/meta tools/run_tests.sh`
Expected: FAIL (scenes missing).

- [ ] **Step 3: Implement the scenes**

`src/app/logo.gd`:

```gdscript
extends Control
## Boot screen: shows the title briefly, then goes to the main menu. Any key press skips ahead.

const HOLD_SECONDS: float = 1.5

## Defaults to the SceneRouter autoload in _ready; tests set it first.
var router: Node

var _done: bool = false


func _ready() -> void:
	if router == null:
		router = SceneRouter
	get_tree().create_timer(HOLD_SECONDS).timeout.connect(_proceed)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_pressed():
		_proceed()


func _proceed() -> void:
	if _done:
		return
	_done = true
	router.go(SceneRouter.Route.MAIN_MENU)
```

`src/app/logo.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/app/logo.gd" id="1_logo"]

[node name="Logo" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_logo")

[node name="Title" type="Label" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -250.0
offset_top = -24.0
offset_right = 250.0
offset_bottom = 24.0
grow_horizontal = 2
grow_vertical = 2
theme_override_font_sizes/font_size = 36
text = "VAMPIRE SURVIVOR AI"
horizontal_alignment = 1
vertical_alignment = 1
```

`src/meta/menu/main_menu.gd`:

```gdscript
extends Control
## Start / Settings / Quit. Holds no state: it asks the router to move on. 4b replaces Start with the hub.

const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")

## Defaults to the SceneRouter autoload in _ready; tests set it first.
var router: Node

@onready var _start_button: Button = %StartButton
@onready var _settings_button: Button = %SettingsButton
@onready var _quit_button: Button = %QuitButton


func _ready() -> void:
	if router == null:
		router = SceneRouter
	_start_button.pressed.connect(_on_start_pressed)
	_settings_button.pressed.connect(_on_settings_pressed)
	_quit_button.pressed.connect(_on_quit_pressed)
	_start_button.grab_focus()


func _on_start_pressed() -> void:
	router.go(SceneRouter.Route.STAGE, {SceneRouter.PARAM_STAGE_DEF: STAGE_DEF})


func _on_settings_pressed() -> void:
	router.go(SceneRouter.Route.SETTINGS)


func _on_quit_pressed() -> void:
	get_tree().quit()
```

`src/meta/menu/main_menu.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/menu/main_menu.gd" id="1_menu"]

[node name="MainMenu" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_menu")

[node name="Center" type="CenterContainer" parent="."]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2

[node name="Column" type="VBoxContainer" parent="Center"]
custom_minimum_size = Vector2(260, 0)
layout_mode = 2
theme_override_constants/separation = 12

[node name="Title" type="Label" parent="Center/Column"]
layout_mode = 2
theme_override_font_sizes/font_size = 32
text = "VAMPIRE SURVIVOR AI"
horizontal_alignment = 1

[node name="StartButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Start"

[node name="SettingsButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Settings"

[node name="QuitButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Quit"
```

`src/meta/menu/settings_screen.gd`:

```gdscript
extends Control
## Master-volume slider. Changes apply immediately; leaving the screen saves once.

## Default to the autoloads in _ready; tests set them first.
var save_service: Node
var router: Node

@onready var _volume_slider: HSlider = %VolumeSlider
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_volume_slider.value = save_service.data.settings.master_volume
	_volume_slider.value_changed.connect(_on_volume_changed)
	_back_button.pressed.connect(_on_back_pressed)
	_back_button.grab_focus()


func _on_volume_changed(value: float) -> void:
	save_service.data.settings.master_volume = value
	AudioSettings.apply(save_service.data.settings)


func _on_back_pressed() -> void:
	save_service.save()
	router.go(SceneRouter.Route.MAIN_MENU)
```

`src/meta/menu/settings_screen.tscn`:

```
[gd_scene format=3]

[ext_resource type="Script" path="res://src/meta/menu/settings_screen.gd" id="1_settings"]

[node name="SettingsScreen" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_settings")

[node name="Center" type="CenterContainer" parent="."]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2

[node name="Column" type="VBoxContainer" parent="Center"]
custom_minimum_size = Vector2(320, 0)
layout_mode = 2
theme_override_constants/separation = 12

[node name="Title" type="Label" parent="Center/Column"]
layout_mode = 2
theme_override_font_sizes/font_size = 28
text = "Settings"
horizontal_alignment = 1

[node name="VolumeLabel" type="Label" parent="Center/Column"]
layout_mode = 2
text = "Master volume"

[node name="VolumeSlider" type="HSlider" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
max_value = 1.0
step = 0.01
value = 1.0

[node name="BackButton" type="Button" parent="Center/Column"]
unique_name_in_owner = true
layout_mode = 2
text = "Back"
```

`project.godot`: set `run/main_scene="res://src/app/logo.tscn"` (hand edit; it was previously `res://src/app/main.tscn`).

- [ ] **Step 4: Run to verify pass**

Run: `IMPORT=1 tools/run_tests.sh` (whole suite)
Expected: `0 failed`, no `SCRIPT ERROR`/`Parse Error`. If a `@onready` unique-name lookup fails, confirm `unique_name_in_owner = true` is on the node and the name is spelled exactly.

- [ ] **Step 5: Commit**

```bash
git add src/app/logo.gd src/app/logo.gd.uid src/app/logo.tscn src/meta/menu tests/app tests/meta project.godot
git commit -m "feat(meta): add the logo, main menu and settings screens and boot into the logo" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: End-to-end check and documentation

**Files:**
- Modify: `CLAUDE.md`
- Test: `tests/app/test_boot_flow.gd`

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Write the smoke test**

`tests/app/test_boot_flow.gd`:

```gdscript
extends TestCase
## Walks the real scenes through the real router table: logo -> menu -> stage -> menu, with a temp save.

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_boot_flow"
const PATH := "user://test_boot_flow/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func test_start_from_the_menu_runs_the_shipped_stage_and_pays_gem_that_survives_a_relaunch() -> void:
	var router: Node = adopt(Probe.new())
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	adopt(service)

	var menu := load(router.path_for(Probe.Route.MAIN_MENU)).instantiate()
	menu.router = router
	adopt(menu)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])

	var main := load(router.path_for(Probe.Route.STAGE)).instantiate()
	main.save_service = service
	main.router = router
	# The probe never changes scenes, so hand Main the params the router kept for it.
	main.stage_def = router.take_params().get(Probe.PARAM_STAGE_DEF) as StageDef
	adopt(main)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	main._stage.finished.emit(result)

	var reward: int = main.stage_def.gem_reward
	assert_true(reward > 0)
	assert_eq(service.data.gem, reward)
	var relaunched: Node = SERVICE_SCRIPT.new()
	relaunched.path = PATH
	adopt(relaunched)
	assert_eq(relaunched.data.gem, reward, "a fresh session reads the saved gem")
```

- [ ] **Step 2: Run to verify it passes**

Run: `FILTER=tests/app/test_boot_flow tools/run_tests.sh`, then `tools/run_tests.sh`
Expected: `0 failed`. Then run the game once by hand: `"$GODOT_BIN" --path .` (or open the editor) and check logo → menu → Start → (die or clear) → Enter returns to the menu → Settings slider changes volume → relaunch keeps gem and volume. Report what you observed; do not claim it works without running it.

- [ ] **Step 3: Update `CLAUDE.md`**

In "Architecture rules" append:

```markdown
- Phase 4a meta flow: autoloads `SceneRouter` (`src/app/`) and `SaveService` (`src/meta/`), neither with a `class_name`.
  Screens (`Main`, `MainMenu`, `SettingsScreen`, `Logo`) take `router`/`save_service` as optional properties that
  default to the autoloads in `_ready`; tests inject `tests/support/scene_router_probe.gd` and a `SaveService` with a
  temp `path`. `Stage` never sees the autoloads. Routes live only in `SceneRouter.ROUTES`; one-shot data goes through
  `go(route, params)` / `take_params()` with keys defined on the router (`PARAM_STAGE_DEF`).
- Save: JSON at `user://save.json` (`SaveData` -> `SaveStore`). To change the schema bump `SaveMigrations.CURRENT_VERSION`
  and append one step to `SaveMigrations.steps()`; add a migration test. Unreadable files are moved to `save.json.bak`.
  `Main` sets `RunResult.gem` from `StageDef.gem_for(outcome)` and credits it once per run; gem is the only persistent currency.
```

In "Running the game" change the main scene to `src/app/logo.tscn` (logo -> main menu -> `src/app/main.tscn` as the stage screen) and mention Enter returns to the menu after a run while R still restarts.

- [ ] **Step 4: Run the whole suite once more**

Run: `tools/run_tests.sh`
Expected: `---- N passed, 0 failed ----`.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md tests/app/test_boot_flow.gd tests/app/test_boot_flow.gd.uid
git commit -m "docs: document the Phase 4a meta flow and add the boot-flow smoke test" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-review (spec coverage)

- Modules table: `SceneRouter` (T4), `SaveData` (T1), `SaveMigrations` (T1), `SaveStore` (T2), `SaveService` (T3), `AudioSettings` (T3), `MainMenu`/`SettingsScreen` (T7). Logo (T7).
- Changes to existing code: `Main` (T6), `main_scene` (T7), `RunResult.gem`/`StageDef.gem_reward` (T5). `Stage` untouched (no task edits `src/stage/`).
- Data flow 1-4: boot/load (T3, T7), menu (T7), settings with save on leave (T7), stage + single gem credit + Continue (T6, T8).
- Save format, error handling (missing, corrupt, wrong type, future version, write failure, unknown route, failed scene load, empty params): T1, T2, T4, T6.
- Testing list: migrations/data/store/router/data rule/smoke all present (T1, T2, T4, T5, T8); "every route's scene path loads" is in T7 because the scenes arrive there.
- Type consistency: `read_int`, `migrate_with(source, steps, current)`, `SaveStore.load_or_default/write`, `SaveService.path/data/add_gem/save/reload`, `SceneRouter.go/take_params/path_for/PARAM_STAGE_DEF`, `StageDef.gem_for`, `InputActions.CONFIRM` are used with identical names in later tasks.
