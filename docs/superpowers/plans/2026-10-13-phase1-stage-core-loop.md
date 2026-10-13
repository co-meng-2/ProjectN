# Phase 1 — Stage Core Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A playable single stage: the player auto-fires arrows and dashes, enemies trickle in and arrive in waves shown on a timeline bar, kills drop gold/stone, and the run ends in CLEARED (time up) or FAILED (building or player destroyed).

**Architecture:** `Stage` is the composition root; modules are layered `core → defs → gas → components → actors → stage → ui → app` and talk upward only through typed signals. GodotGAS owns stats, the damage pipeline, invulnerability and the player's dash/arrow abilities; waves, spawning, loot, projectiles and UI are our own thin modules. All content is `.tres` data.

**Tech Stack:** Godot 4.7.2 (GDScript, typed), GodotGAS v1.1.2 (read-only plugin symlinked into `addons/`), headless custom test runner.

**Spec:** `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md` (read it first; its Section 3 is the module contract and Section 3.4 the dependency rules).

## Global Constraints

- Godot **4.7.2**; typed GDScript everywhere (`var x: T`, `-> T`); tabs for indentation (`.editorconfig`).
- **Never edit `addons/GodotGAS` or `addons/godot_ai`** (symlinks to `D:\CM\Godot\plugins\`). Generated files in `godot_gas/` are only changed by `tools/sync_tags.gd`.
- Layer rule: a file may reference only layers below it: `0 addons/godot_gas` → `1 core` → `2 defs` → `3 gas` → `4 components` → `5 actors` → `6 stage` → `7 ui` → `8 app`. Upward communication = signals. No `get_node("/root/...")`. No autoloads of ours in Phase 1.
- Components never look up their neighbours: actors inject dependencies through `bind(...)`.
- The ASC node must be a **direct child named exactly `AbilitySystemComponent`** of its actor root, and must be inside the tree **before** `initialize_attribute_overrides` is called (attribute sets are deep-copied in `_ready`).
- Call `asc.cleanup()` before freeing an actor that owns active effects/abilities.
- `class_name` on every script referenced by another script/def; files `snake_case.gd` ↔ `PascalCase` classes; signals are past-tense with typed args; members ordered signals, consts, `@export`, vars, `@onready`, `_ready`, public, private.
- Gameplay tags only through `GameplayTags.X` constants. Tags: `Ability.Arrow`, `Ability.Dash`, `Cooldown.Arrow`, `Cooldown.Dash`, `Damage.Amount` (set-by-caller key), `State.Invulnerable`.
- Physics layers (bit values): WORLD 1, ALLY_BODY 2, ENEMY_BODY 4, ALLY_HURT 8, ENEMY_HURT 16, PICKUP 32.
- No sound. Assets are copied from `D:\CM\Godot\res` into `res://assets/`; never reference the external path from the project.
- Commit after every task. Commit messages end with: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## How tests run

```bash
tools/run_tests.sh                    # whole suite
FILTER=test_wallet tools/run_tests.sh # one file (substring of its path)
IMPORT=1 tools/run_tests.sh           # run `godot --import` first (use when the editor is NOT open and new files/classes/assets were added)
```
With the Godot editor open (the normal workflow here) new `class_name` scripts and assets are picked up automatically; give the editor ~3 s after files are written. Output lines are `PASS  file::test` / `FAIL  file::test` + reasons, then `---- N passed, M failed ----`. Any `SCRIPT ERROR`/`Parse Error` in the output also fails the run.

## Review Focus

Failure modes the spec implies but no happy-path test would catch; each has a pinning test in the owning task:

1. **Double finish** — building destroyed and time-up in the same frame must emit `finished` exactly once with the first outcome (Task 12 `test_finish_is_idempotent`).
2. **Bad data** — empty trickle list, wave `time` beyond the stage duration, `count <= 0`, null texture/effect refs must produce `validate()` errors at `Stage.start`, never a crash mid-wave (Task 3 and Task 12).
3. **Dash invulnerability window** — damage is ignored while dashing, applied again right after; the tag must be removed even though the ability is a coroutine (Task 9 `test_dash_grants_invulnerability_only_while_active`).
4. **Dead/freed actors** — a projectile whose shooter was freed, or that hits an enemy that is already dying, must not error or double-hit (Task 7 `test_projectile_survives_freed_shooter`, Task 8 `test_dead_enemy_is_not_targetable`).
5. **Nothing to shoot** — with no enemy in range the arrow ability must fail quietly without starting a cooldown, every frame (Task 9 `test_arrow_without_target_does_not_start_cooldown`).
6. **Restart hygiene** — a second run starts with an empty wallet/clock and no leftover enemies or pickups (Task 12 `test_restart_starts_clean`).

---

## File Structure

```
tools/run_tests.sh                       run suite headless
tools/sync_tags.gd                       rebuild gameplay tag registry + generated constants
tests/run_tests.gd/.tscn                 runner (discovers tests/**/test_*.gd, skips tests/support)
tests/support/test_case.gd               TestCase base class (assertions, adopt(), waits)
tests/support/actor_fixture.gd           builds a bare actor with an ASC for GAS tests
assets/SOURCES.md                        provenance + license notes of copied art
assets/art/...                           copied sprites (tinyrpg/, farmrpg/, world/, items/)
data/{effects,characters,projectiles,drops,enemies,waves,stages}/*.tres
src/core/        run_rng timeline wallet physics_layers groups input_actions anim_names spawn_geometry
src/defs/        animation_strip visual_def stat_block projectile_def drop_entry drop_roll drop_table
                 enemy_def wave_entry wave_schedule stage_def player_profile run_result
src/gas/         attributes/{vitals,combat,economy}_attribute_set  executions/damage_execution
                 effect_builder  actor_asc.tscn
src/components/  health hurtbox hitbox effect_applier mover lifetime actor_visual contact_damage
                 loot_dropper target_provider
src/actors/      projectile/ pickup/(pickup, loot_collector) building/ enemy/ player/(player, arrow_ability, dash_ability)
src/stage/       stage_clock stage_flow run_state enemy_factory spawn_director wave_director arena stage(.tscn)
src/ui/hud/      hud(.tscn) timeline_bar
src/app/         main(.tscn)
```

Deviations from the spec's Phase 1 list (all justified by the spec's own "earns its existence" rule; Task 14 updates the spec): `FxSpawner` is deferred to Phase 3 (no Phase 1 consumer; hit feedback is `ActorVisual.flash_hit`); `LootCollector` lives in `actors/pickup/` (it must know `Pickup`, an actor, so it cannot sit in layer 4); `TargetProvider` lives in `components/` (actors need it for auto-aim and may not import `stage/`); sprites are described by `AnimationStrip` resources and turned into `SpriteFrames` at runtime (no hand-authored SpriteFrames); tests use a small in-repo headless runner instead of GUT/the editor runner (the editor runner cannot call non-`@tool` scripts); player death also ends the run as FAILED.

---

### Task 1: Headless test harness

**Files:**
- Create: `tests/support/test_case.gd`, `tests/run_tests.gd`, `tests/run_tests.tscn`, `tests/test_harness.gd`, `tools/run_tests.sh`

**Interfaces:**
- Produces: `class TestCase` — `host: Node`, `failures: Array[String]`, `before_each()`, `after_each()`, `adopt(node: Node) -> Node`, `release_owned_nodes()`, `next_frame()`, `wait_seconds(s: float)`, `make_texture(w: int, h: int) -> Texture2D`, `assert_true/assert_false(cond, msg)`, `assert_eq(actual, expected, msg)`, `assert_almost_eq(actual, expected, eps, msg)`, `assert_not_null(v, msg)`, `assert_null(v, msg)`, `assert_no_errors(errors: PackedStringArray, msg)`. Test files `extends TestCase` and define `test_*` methods (may `await`).

- [ ] **Step 1: Write the base class**

`tests/support/test_case.gd`:
```gdscript
class_name TestCase
extends RefCounted
## Base class for headless tests. Subclasses add `test_*` methods (they may await).
## The runner sets `host` to a Node inside the SceneTree before each test.

var host: Node
var failures: Array[String] = []
var assertion_count: int = 0
var _owned_nodes: Array[Node] = []


func before_each() -> void:
	pass


func after_each() -> void:
	pass


## Adds `node` under `host` and frees it automatically after the test.
func adopt(node: Node) -> Node:
	host.add_child(node)
	_owned_nodes.append(node)
	return node


func release_owned_nodes() -> void:
	for node in _owned_nodes:
		if is_instance_valid(node):
			node.free()
	_owned_nodes.clear()


func next_frame() -> void:
	await host.get_tree().process_frame


func wait_seconds(seconds: float) -> void:
	await host.get_tree().create_timer(seconds).timeout


func make_texture(width: int, height: int) -> Texture2D:
	return ImageTexture.create_from_image(Image.create(width, height, false, Image.FORMAT_RGBA8))


func assert_true(condition: bool, message: String = "") -> void:
	assertion_count += 1
	if not condition:
		failures.append(message if message != "" else "expected true")


func assert_false(condition: bool, message: String = "") -> void:
	assert_true(not condition, message if message != "" else "expected false")


func assert_eq(actual: Variant, expected: Variant, message: String = "") -> void:
	assertion_count += 1
	if actual != expected:
		failures.append("%s (expected %s, got %s)" % [message, str(expected), str(actual)])


func assert_almost_eq(actual: float, expected: float, epsilon: float = 0.001, message: String = "") -> void:
	assertion_count += 1
	if absf(actual - expected) > epsilon:
		failures.append("%s (expected %s ± %s, got %s)" % [message, str(expected), str(epsilon), str(actual)])


func assert_not_null(value: Variant, message: String = "") -> void:
	assertion_count += 1
	if value == null:
		failures.append(message if message != "" else "expected non-null")


func assert_null(value: Variant, message: String = "") -> void:
	assertion_count += 1
	if value != null:
		failures.append(message if message != "" else "expected null, got %s" % str(value))


func assert_no_errors(errors: PackedStringArray, message: String = "") -> void:
	assertion_count += 1
	if not errors.is_empty():
		failures.append("%s validation errors: %s" % [message, ", ".join(errors)])
```

- [ ] **Step 2: Write the runner**

`tests/run_tests.gd`:
```gdscript
extends Node
## Headless test runner.
## Usage: Godot --headless --path . res://tests/run_tests.tscn -- --filter=substring

const TEST_ROOT: String = "res://tests"
const TEST_FILE_PREFIX: String = "test_"
const TEST_METHOD_PREFIX: String = "test_"
const SUPPORT_DIR: String = "support"


func _ready() -> void:
	var filter := _read_filter()
	var paths := _discover(TEST_ROOT)
	paths.sort()
	var passed := 0
	var failed := 0
	for path in paths:
		if filter != "" and path.find(filter) == -1:
			continue
		var tally := await _run_file(path)
		passed += tally.x
		failed += tally.y
	print("---- %d passed, %d failed ----" % [passed, failed])
	get_tree().quit(1 if failed > 0 else 0)


func _read_filter() -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--filter="):
			return argument.trim_prefix("--filter=")
	return ""


func _discover(directory_path: String) -> PackedStringArray:
	var found := PackedStringArray()
	var directory := DirAccess.open(directory_path)
	if directory == null:
		return found
	for sub_directory in directory.get_directories():
		if sub_directory != SUPPORT_DIR:
			found.append_array(_discover(directory_path.path_join(sub_directory)))
	for file_name in directory.get_files():
		if file_name.begins_with(TEST_FILE_PREFIX) and file_name.ends_with(".gd"):
			found.append(directory_path.path_join(file_name))
	return found


## Returns Vector2i(passed, failed) for one test file.
func _run_file(path: String) -> Vector2i:
	var script := load(path) as GDScript
	if script == null:
		print("FAIL  %s  (could not load script)" % path)
		return Vector2i(0, 1)
	var passed := 0
	var failed := 0
	for method in script.get_script_method_list():
		var method_name: String = method["name"]
		if not method_name.begins_with(TEST_METHOD_PREFIX):
			continue
		var test := script.new() as TestCase
		if test == null:
			print("FAIL  %s  (does not extend TestCase)" % path)
			return Vector2i(passed, failed + 1)
		test.host = self
		test.before_each()
		await test.call(method_name)
		test.after_each()
		test.release_owned_nodes()
		var label := "%s::%s" % [path.trim_prefix(TEST_ROOT + "/"), method_name]
		if test.failures.is_empty():
			passed += 1
			print("PASS  %s" % label)
		else:
			failed += 1
			print("FAIL  %s" % label)
			for failure in test.failures:
				print("        - %s" % failure)
	return Vector2i(passed, failed)
```

`tests/run_tests.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://tests/run_tests.gd" id="1_runner"]

[node name="RunTests" type="Node"]
script = ExtResource("1_runner")
```

`tools/run_tests.sh`:
```bash
#!/usr/bin/env bash
# Runs the headless test suite. Env: GODOT_BIN, FILTER (path substring), IMPORT=1 (run --import first).
set -u
cd "$(dirname "$0")/.."
GODOT_BIN="${GODOT_BIN:-/d/CM/Godot/Godot_v4.7.2-stable_win64.exe}"
if [ "${IMPORT:-0}" = "1" ]; then
	"$GODOT_BIN" --headless --path . --import >/dev/null 2>&1
fi
output="$("$GODOT_BIN" --headless --path . res://tests/run_tests.tscn -- --filter="${FILTER:-}" 2>&1)"
status=$?
echo "$output" | grep -v "ObjectDB instances were leaked\|resources still in use\|at: cleanup\|at: clear\|BUG: Unreferenced static string"
if echo "$output" | grep -q "SCRIPT ERROR\|Parse Error\|Compile Error"; then
	echo "!! script errors detected"
	exit 1
fi
exit $status
```

- [ ] **Step 3: Write the harness self-test**

`tests/test_harness.gd`:
```gdscript
extends TestCase


func test_assert_eq_records_a_failure_on_mismatch() -> void:
	var probe := TestCase.new()
	probe.assert_eq(1, 2, "mismatch")
	assert_eq(probe.failures.size(), 1)


func test_assert_eq_passes_on_match() -> void:
	var probe := TestCase.new()
	probe.assert_eq("a", "a")
	assert_true(probe.failures.is_empty())


func test_assert_no_errors_reports_messages() -> void:
	var probe := TestCase.new()
	probe.assert_no_errors(PackedStringArray(["boom"]), "defs")
	assert_eq(probe.failures.size(), 1)
	assert_true(probe.failures[0].contains("boom"))


func test_adopt_frees_node_on_release() -> void:
	var node := adopt(Node.new())
	release_owned_nodes()
	assert_false(is_instance_valid(node))


func test_async_tests_can_await_frames() -> void:
	await next_frame()
	assert_true(true)
```

- [ ] **Step 4: Run and verify**

Run: `chmod +x tools/run_tests.sh && tools/run_tests.sh`
Expected: 5 `PASS test_harness.gd::...` lines and `---- 5 passed, 0 failed ----`, exit code 0.
If `class_name TestCase` is not found, wait 3 s for the editor scan (or run with `IMPORT=1` when the editor is closed) and re-run.

- [ ] **Step 5: Commit**

```bash
git add tests tools
git commit -q -m "test: add headless test runner and TestCase base

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Core modules and project settings

**Files:**
- Create: `src/core/run_rng.gd`, `timeline.gd`, `wallet.gd`, `physics_layers.gd`, `groups.gd`, `input_actions.gd`, `anim_names.gd`, `spawn_geometry.gd`
- Test: `tests/core/test_run_rng.gd`, `test_timeline.gd`, `test_wallet.gd`, `test_spawn_geometry.gd`, `test_physics_layers.gd`
- Modify (via MCP editor tools, never by hand while the editor is open): project settings and input map

**Interfaces:**
- Produces:
  - `RunRng(p_seed := 0)`: `seed_value: int`, `randf() -> float`, `randf_range(from: float, to: float) -> float`, `randi_range(from: int, to: int) -> int`, `chance(p: float) -> bool`, `pick(items: Array) -> Variant` (null if empty), `weighted_index(weights: Array[float]) -> int` (-1 if no positive weight), `weighted(items: Array, weights: Array[float]) -> Variant`, `offset_in_circle(radius: float) -> Vector2`.
  - `Timeline(p_duration: float, p_marks := PackedFloat32Array())`: `duration`, `marks` (sorted), `progress(time) -> float`, `next_after(time) -> float` (-1.0 if none), `crossed(from_time, to_time) -> PackedInt32Array` (indices with `from < mark <= to`).
  - `Wallet`: `enum Kind { GOLD, STONE }`, `gold`, `stone`, `signal changed(gold: int, stone: int)`, `balance(kind) -> int`, `add(kind, amount) -> void` (ignores `amount <= 0`), `try_spend(kind, amount) -> bool`.
  - `PhysicsLayers` consts `WORLD=1, ALLY_BODY=2, ENEMY_BODY=4, ALLY_HURT=8, ENEMY_HURT=16, PICKUP=32`; `Groups` consts `ENEMIES, PLAYER, BUILDING` (StringName); `InputActions` consts `MOVE_LEFT, MOVE_RIGHT, MOVE_UP, MOVE_DOWN, DASH, RESTART` + `static move_vector() -> Vector2`; `AnimNames` consts `IDLE, WALK, ATTACK, HURT, DEATH` (StringName `&"idle"` …).
  - `SpawnGeometry.ring_point(center: Vector2, radius: float, angle: float) -> Vector2`, `SpawnGeometry.clamp_to_rect(point: Vector2, bounds: Rect2, margin := 0.0) -> Vector2`.

- [ ] **Step 1: Write the failing tests**

`tests/core/test_run_rng.gd`:
```gdscript
extends TestCase


func test_same_seed_gives_same_sequence() -> void:
	var a := RunRng.new(42)
	var b := RunRng.new(42)
	for i in 5:
		assert_eq(a.randf(), b.randf(), "draw %d" % i)


func test_pick_returns_null_for_empty_array() -> void:
	assert_null(RunRng.new(1).pick([]))


func test_pick_returns_an_element() -> void:
	var items := ["a", "b", "c"]
	assert_true(items.has(RunRng.new(1).pick(items)))


func test_weighted_index_never_picks_zero_weight() -> void:
	var rng := RunRng.new(7)
	var weights: Array[float] = [0.0, 0.0, 5.0, 0.0]
	for i in 30:
		assert_eq(rng.weighted_index(weights), 2)


func test_weighted_index_is_minus_one_without_positive_weight() -> void:
	var weights: Array[float] = [0.0, -1.0]
	assert_eq(RunRng.new(1).weighted_index(weights), -1)
	var empty: Array[float] = []
	assert_eq(RunRng.new(1).weighted_index(empty), -1)


func test_weighted_returns_matching_item() -> void:
	var weights: Array[float] = [0.0, 1.0]
	assert_eq(RunRng.new(3).weighted(["x", "y"], weights), "y")


func test_chance_extremes() -> void:
	var rng := RunRng.new(9)
	for i in 20:
		assert_false(rng.chance(0.0))
		assert_true(rng.chance(1.0))


func test_offset_in_circle_stays_within_radius() -> void:
	var rng := RunRng.new(5)
	for i in 50:
		assert_true(rng.offset_in_circle(10.0).length() <= 10.0001)
```

`tests/core/test_timeline.gd`:
```gdscript
extends TestCase


func test_marks_are_sorted() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([60.0, 20.0, 40.0]))
	assert_eq(timeline.marks, PackedFloat32Array([20.0, 40.0, 60.0]))


func test_progress_is_clamped() -> void:
	var timeline := Timeline.new(100.0)
	assert_almost_eq(timeline.progress(25.0), 0.25)
	assert_almost_eq(timeline.progress(-5.0), 0.0)
	assert_almost_eq(timeline.progress(500.0), 1.0)


func test_progress_with_zero_duration_is_complete() -> void:
	assert_almost_eq(Timeline.new(0.0).progress(3.0), 1.0)


func test_next_after_skips_current_and_past_marks() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([20.0, 40.0]))
	assert_almost_eq(timeline.next_after(0.0), 20.0)
	assert_almost_eq(timeline.next_after(20.0), 40.0)
	assert_almost_eq(timeline.next_after(40.0), -1.0)


func test_crossed_excludes_start_and_includes_end() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([20.0, 40.0, 60.0]))
	assert_eq(timeline.crossed(20.0, 40.0), PackedInt32Array([1]))
	assert_eq(timeline.crossed(0.0, 100.0), PackedInt32Array([0, 1, 2]))
	assert_eq(timeline.crossed(41.0, 59.0), PackedInt32Array())
```

`tests/core/test_wallet.gd`:
```gdscript
extends TestCase


func test_add_increases_balance_and_emits_changed() -> void:
	var wallet := Wallet.new()
	var emitted: Array = []
	wallet.changed.connect(func(gold: int, stone: int) -> void: emitted.append([gold, stone]))
	wallet.add(Wallet.Kind.GOLD, 5)
	wallet.add(Wallet.Kind.STONE, 2)
	assert_eq(wallet.gold, 5)
	assert_eq(wallet.stone, 2)
	assert_eq(emitted, [[5, 0], [5, 2]])


func test_add_ignores_non_positive_amounts() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 0)
	wallet.add(Wallet.Kind.GOLD, -3)
	assert_eq(wallet.gold, 0)


func test_try_spend_succeeds_when_affordable() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.GOLD, 10)
	assert_true(wallet.try_spend(Wallet.Kind.GOLD, 4))
	assert_eq(wallet.balance(Wallet.Kind.GOLD), 6)


func test_try_spend_fails_without_changing_balance() -> void:
	var wallet := Wallet.new()
	wallet.add(Wallet.Kind.STONE, 1)
	assert_false(wallet.try_spend(Wallet.Kind.STONE, 2))
	assert_false(wallet.try_spend(Wallet.Kind.STONE, -1))
	assert_eq(wallet.stone, 1)
```

`tests/core/test_spawn_geometry.gd`:
```gdscript
extends TestCase


func test_ring_point_is_radius_away() -> void:
	var point := SpawnGeometry.ring_point(Vector2(10, 10), 50.0, 0.0)
	assert_almost_eq(point.x, 60.0)
	assert_almost_eq(point.y, 10.0)


func test_clamp_to_rect_respects_margin() -> void:
	var bounds := Rect2(-100, -100, 200, 200)
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(500, 0), bounds, 10.0), Vector2(90, 0))
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(0, -500), bounds), Vector2(0, -100))
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(5, 5), bounds), Vector2(5, 5))
```

`tests/core/test_physics_layers.gd`:
```gdscript
extends TestCase


func test_layers_are_distinct_single_bits() -> void:
	var layers := [
		PhysicsLayers.WORLD, PhysicsLayers.ALLY_BODY, PhysicsLayers.ENEMY_BODY,
		PhysicsLayers.ALLY_HURT, PhysicsLayers.ENEMY_HURT, PhysicsLayers.PICKUP,
	]
	var combined := 0
	for layer: int in layers:
		assert_eq(layer & (layer - 1), 0, "single bit")
		assert_eq(combined & layer, 0, "overlap")
		combined |= layer
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/core tools/run_tests.sh`
Expected: FAIL / errors because `RunRng`, `Timeline`, `Wallet`, `SpawnGeometry`, `PhysicsLayers` are not defined.

- [ ] **Step 3: Implement**

`src/core/run_rng.gd`:
```gdscript
class_name RunRng
extends RefCounted
## Seeded random source for one stage run so spawns, drops and rerolls are reproducible.

var seed_value: int
var _rng := RandomNumberGenerator.new()


func _init(p_seed: int = 0) -> void:
	seed_value = p_seed if p_seed != 0 else int(Time.get_ticks_usec())
	_rng.seed = seed_value


func randf() -> float:
	return _rng.randf()


func randf_range(from: float, to: float) -> float:
	return _rng.randf_range(from, to)


func randi_range(from: int, to: int) -> int:
	return _rng.randi_range(from, to)


func chance(probability: float) -> bool:
	return _rng.randf() < probability


func pick(items: Array) -> Variant:
	if items.is_empty():
		return null
	return items[_rng.randi_range(0, items.size() - 1)]


## Index chosen proportionally to `weights`; -1 when no weight is positive.
func weighted_index(weights: Array[float]) -> int:
	var total := 0.0
	for weight in weights:
		total += maxf(weight, 0.0)
	if total <= 0.0:
		return -1
	var roll := _rng.randf() * total
	var last_positive := -1
	for i in weights.size():
		if weights[i] <= 0.0:
			continue
		last_positive = i
		roll -= weights[i]
		if roll < 0.0:
			return i
	return last_positive


func weighted(items: Array, weights: Array[float]) -> Variant:
	var index := weighted_index(weights)
	return items[index] if index >= 0 and index < items.size() else null


## Uniformly distributed point inside a circle around the origin.
func offset_in_circle(radius: float) -> Vector2:
	return Vector2.from_angle(_rng.randf() * TAU) * radius * sqrt(_rng.randf())
```

`src/core/timeline.gd`:
```gdscript
class_name Timeline
extends RefCounted
## Pure schedule math: a duration plus sorted mark times (for example wave starts).

var duration: float
var marks: PackedFloat32Array


func _init(p_duration: float, p_marks: PackedFloat32Array = PackedFloat32Array()) -> void:
	duration = p_duration
	marks = p_marks.duplicate()
	marks.sort()


func progress(time: float) -> float:
	if duration <= 0.0:
		return 1.0
	return clampf(time / duration, 0.0, 1.0)


## Time of the first mark strictly after `time`, or -1.0 when none remain.
func next_after(time: float) -> float:
	for mark in marks:
		if mark > time:
			return mark
	return -1.0


## Indices of marks with from_time < mark <= to_time, in ascending order.
func crossed(from_time: float, to_time: float) -> PackedInt32Array:
	var indices := PackedInt32Array()
	for i in marks.size():
		if marks[i] > from_time and marks[i] <= to_time:
			indices.append(i)
	return indices
```

`src/core/wallet.gd`:
```gdscript
class_name Wallet
extends RefCounted
## Per-run currency balances. Gem is persistent meta currency and lives elsewhere (Phase 4).

signal changed(gold: int, stone: int)

enum Kind { GOLD, STONE }

var gold: int = 0
var stone: int = 0


func balance(kind: Kind) -> int:
	return gold if kind == Kind.GOLD else stone


func add(kind: Kind, amount: int) -> void:
	if amount <= 0:
		return
	_set(kind, balance(kind) + amount)


func try_spend(kind: Kind, amount: int) -> bool:
	if amount < 0 or balance(kind) < amount:
		return false
	_set(kind, balance(kind) - amount)
	return true


func _set(kind: Kind, value: int) -> void:
	if kind == Kind.GOLD:
		gold = value
	else:
		stone = value
	changed.emit(gold, stone)
```

`src/core/physics_layers.gd`:
```gdscript
class_name PhysicsLayers
extends RefCounted
## 2D physics layer bitmasks. Names must match [layer_names] in project settings.

const WORLD: int = 1 << 0
const ALLY_BODY: int = 1 << 1
const ENEMY_BODY: int = 1 << 2
const ALLY_HURT: int = 1 << 3
const ENEMY_HURT: int = 1 << 4
const PICKUP: int = 1 << 5
```

`src/core/groups.gd`:
```gdscript
class_name Groups
extends RefCounted
## Scene-tree group names. Used only for targeting queries.

const ENEMIES: StringName = &"enemies"
const PLAYER: StringName = &"player"
const BUILDING: StringName = &"building"
```

`src/core/input_actions.gd`:
```gdscript
class_name InputActions
extends RefCounted
## Input action names. Bindings live in the project's InputMap.

const MOVE_LEFT: StringName = &"move_left"
const MOVE_RIGHT: StringName = &"move_right"
const MOVE_UP: StringName = &"move_up"
const MOVE_DOWN: StringName = &"move_down"
const DASH: StringName = &"dash"
const RESTART: StringName = &"restart"


static func move_vector() -> Vector2:
	return Input.get_vector(MOVE_LEFT, MOVE_RIGHT, MOVE_UP, MOVE_DOWN)
```

`src/core/anim_names.gd`:
```gdscript
class_name AnimNames
extends RefCounted
## Animation names shared by VisualDef data and ActorVisual.

const IDLE: StringName = &"idle"
const WALK: StringName = &"walk"
const ATTACK: StringName = &"attack"
const HURT: StringName = &"hurt"
const DEATH: StringName = &"death"
```

`src/core/spawn_geometry.gd`:
```gdscript
class_name SpawnGeometry
extends RefCounted
## Pure helpers for choosing spawn positions.


static func ring_point(center: Vector2, radius: float, angle: float) -> Vector2:
	return center + Vector2.from_angle(angle) * radius


static func clamp_to_rect(point: Vector2, bounds: Rect2, margin: float = 0.0) -> Vector2:
	var inner := bounds.grow(-margin)
	return Vector2(
		clampf(point.x, inner.position.x, inner.end.x),
		clampf(point.y, inner.position.y, inner.end.y)
	)
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/core tools/run_tests.sh`
Expected: all `tests/core` tests PASS, 0 failed.

- [ ] **Step 5: Configure project settings and input map through the editor tools**

Use the godot-ai MCP tools (they persist through the running editor; do not hand-edit `project.godot` while the editor is open):

1. `project_manage` `settings_set`, one call per row:

| key | value |
|---|---|
| `application/run/main_scene` | `res://src/app/main.tscn` |
| `display/window/size/viewport_width` | `1280` |
| `display/window/size/viewport_height` | `720` |
| `layer_names/2d_physics/layer_1` | `World` |
| `layer_names/2d_physics/layer_2` | `AllyBody` |
| `layer_names/2d_physics/layer_3` | `EnemyBody` |
| `layer_names/2d_physics/layer_4` | `AllyHurt` |
| `layer_names/2d_physics/layer_5` | `EnemyHurt` |
| `layer_names/2d_physics/layer_6` | `Pickup` |

2. `input_map_manage` `ensure_binding` (event_type `key`) for each action/key: `move_left` ← `A`, `Left`; `move_right` ← `D`, `Right`; `move_up` ← `W`, `Up`; `move_down` ← `S`, `Down`; `dash` ← `Space`; `restart` ← `R`.

3. Verify: `input_map_manage` `list` shows the six actions; `project_manage` `settings_get` `layer_names/2d_physics/layer_4` returns `AllyHurt`. If `application/run/main_scene` is rejected because the scene does not exist yet, set it in Task 13 instead.

- [ ] **Step 6: Commit**

```bash
git add src/core tests/core project.godot
git commit -q -m "feat(core): add rng, timeline, wallet, layers, groups, input and spawn geometry

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Data definitions (`defs/`)

**Files:**
- Create: `src/defs/stat_block.gd`, `animation_strip.gd`, `visual_def.gd`, `projectile_def.gd`, `drop_entry.gd`, `drop_roll.gd`, `drop_table.gd`, `enemy_def.gd`, `wave_entry.gd`, `wave_schedule.gd`, `stage_def.gd`, `player_profile.gd`, `run_result.gd`
- Create: `tests/support/def_factory.gd`
- Test: `tests/defs/test_stat_block.gd`, `test_visual_def.gd`, `test_drop_table.gd`, `test_wave_schedule.gd`, `test_stage_def.gd`

**Interfaces:**
- Consumes: `Wallet.Kind`, `RunRng`, `AnimNames` (Task 2); GAS classes `GameplayEffect` (plugin).
- Produces (all `Resource` unless noted; every one has `validate() -> PackedStringArray` — empty means valid):
  - `StatBlock`: `max_health := 100.0`, `move_speed := 100.0`, `attack := 1.0`; `to_overrides() -> Dictionary[String, float]` (key order **max_health, health, move_speed, attack**).
  - `AnimationStrip`: `animation: StringName`, `texture: Texture2D`, `frame_count: int`, `fps: float`, `loop: bool`.
  - `VisualDef`: `strips: Array[AnimationStrip]`, `offset: Vector2`; `find_strip(animation) -> AnimationStrip`.
  - `ProjectileDef`: `texture`, `hit_effect: GameplayEffect`, `speed`, `lifetime`, `pierce: int`, `hit_radius`.
  - `DropEntry`: `kind: Wallet.Kind`, `icon: Texture2D`, `chance`, `min_amount`, `max_amount`. `DropRoll` (RefCounted): `kind`, `amount`, `icon`. `DropTable`: `entries: Array[DropEntry]`; `roll(rng: RunRng) -> Array[DropRoll]`.
  - `EnemyDef`: `enum TargetPolicy { BUILDING, PLAYER }`; `id: StringName`, `scene: PackedScene`, `visual: VisualDef`, `visual_scale: float = 1.0`, `tint: Color`, `stats: StatBlock`, `contact_effect: GameplayEffect`, `contact_damage: float`, `contact_interval: float`, `target_policy`, `body_radius: float`, `drops: DropTable`.
  - `WaveEntry`: `time: float`, `enemy: EnemyDef`, `count: int`, `label: String`. `WaveSchedule`: `entries: Array[WaveEntry]`; `sorted_entries() -> Array[WaveEntry]`; `times() -> PackedFloat32Array` (sorted); `validate_against(duration: float) -> PackedStringArray`.
  - `StageDef`: `id`, `display_name`, `duration`, `arena_size: Vector2`, `ground_texture`, `building_texture`, `building_stats: StatBlock`, `trickle_enemies: Array[EnemyDef]`, `trickle_interval_start`, `trickle_interval_end`, `trickle_cap: int`, `spawn_radius`, `waves: WaveSchedule`.
  - `PlayerProfile`: `stats: StatBlock`, `visual: VisualDef`. `RunResult` (RefCounted): `enum Outcome { CLEARED, FAILED }`, `outcome`, `elapsed: float`, `kills: int`, `gold: int`, `stone: int`.

- [ ] **Step 1: Write the test factory and failing tests**

`tests/support/def_factory.gd`:
```gdscript
class_name DefFactory
extends RefCounted
## Builds minimal valid defs for tests.


static func texture(width: int = 60, height: int = 10) -> Texture2D:
	return ImageTexture.create_from_image(Image.create(width, height, false, Image.FORMAT_RGBA8))


static func visual() -> VisualDef:
	var strip := AnimationStrip.new()
	strip.animation = AnimNames.IDLE
	strip.texture = texture(60, 10)
	strip.frame_count = 6
	var def := VisualDef.new()
	def.strips.append(strip)
	return def


static func enemy(scene: PackedScene = null) -> EnemyDef:
	var def := EnemyDef.new()
	def.id = &"test_enemy"
	def.scene = scene if scene != null else PackedScene.new()
	def.visual = visual()
	def.stats = StatBlock.new()
	def.contact_effect = GameplayEffect.new()
	return def


static func stage(duration: float = 10.0) -> StageDef:
	var def := StageDef.new()
	def.id = &"test_stage"
	def.duration = duration
	def.ground_texture = texture(32, 32)
	def.building_texture = texture(64, 64)
	def.building_stats = StatBlock.new()
	def.waves = WaveSchedule.new()
	return def
```

`tests/defs/test_stat_block.gd`:
```gdscript
extends TestCase


func test_overrides_list_max_health_before_health() -> void:
	var stats := StatBlock.new()
	stats.max_health = 250.0
	var keys := stats.to_overrides().keys()
	assert_eq(keys.find("max_health") < keys.find("health"), true, "max_health must be applied first")
	assert_eq(stats.to_overrides()["health"], 250.0)


func test_validate_rejects_non_positive_health() -> void:
	var stats := StatBlock.new()
	stats.max_health = 0.0
	assert_eq(stats.validate().size(), 1)
	assert_no_errors(StatBlock.new().validate(), "defaults")
```

`tests/defs/test_visual_def.gd`:
```gdscript
extends TestCase


func test_valid_visual_has_no_errors() -> void:
	assert_no_errors(DefFactory.visual().validate(), "factory visual")


func test_missing_idle_strip_is_an_error() -> void:
	var def := DefFactory.visual()
	def.strips[0].animation = AnimNames.WALK
	assert_eq(def.validate().size(), 1)


func test_strip_width_must_divide_by_frame_count() -> void:
	var def := DefFactory.visual()
	def.strips[0].frame_count = 7
	assert_eq(def.validate().size(), 1)


func test_null_texture_is_an_error() -> void:
	var def := DefFactory.visual()
	def.strips[0].texture = null
	assert_eq(def.validate().size(), 1)


func test_find_strip_returns_null_when_absent() -> void:
	assert_null(DefFactory.visual().find_strip(AnimNames.DEATH))
```

`tests/defs/test_drop_table.gd`:
```gdscript
extends TestCase


func _entry(kind: Wallet.Kind, chance: float, low: int, high: int) -> DropEntry:
	var entry := DropEntry.new()
	entry.kind = kind
	entry.chance = chance
	entry.min_amount = low
	entry.max_amount = high
	entry.icon = DefFactory.texture(8, 8)
	return entry


func test_certain_entry_always_drops_within_range() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.GOLD, 1.0, 2, 4))
	var rng := RunRng.new(11)
	for i in 20:
		var rolls := table.roll(rng)
		assert_eq(rolls.size(), 1)
		assert_eq(rolls[0].kind, Wallet.Kind.GOLD)
		assert_true(rolls[0].amount >= 2 and rolls[0].amount <= 4)


func test_impossible_entry_never_drops() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.STONE, 0.0, 1, 1))
	assert_true(table.roll(RunRng.new(1)).is_empty())


func test_validate_flags_inverted_range_and_bad_chance() -> void:
	var table := DropTable.new()
	table.entries.append(_entry(Wallet.Kind.GOLD, 1.5, 5, 2))
	assert_eq(table.validate().size(), 2)
```

`tests/defs/test_wave_schedule.gd`:
```gdscript
extends TestCase


func _entry(time: float, count: int = 5) -> WaveEntry:
	var entry := WaveEntry.new()
	entry.time = time
	entry.count = count
	entry.enemy = DefFactory.enemy()
	return entry


func test_sorted_entries_orders_by_time() -> void:
	var schedule := WaveSchedule.new()
	schedule.entries.append(_entry(40.0))
	schedule.entries.append(_entry(10.0))
	assert_eq(schedule.sorted_entries()[0].time, 10.0)
	assert_eq(schedule.times(), PackedFloat32Array([10.0, 40.0]))


func test_wave_after_stage_end_is_an_error() -> void:
	var schedule := WaveSchedule.new()
	schedule.entries.append(_entry(500.0))
	assert_eq(schedule.validate_against(100.0).size(), 1)


func test_non_positive_count_and_null_enemy_are_errors() -> void:
	var schedule := WaveSchedule.new()
	var bad_count := _entry(5.0, 0)
	var no_enemy := _entry(6.0)
	no_enemy.enemy = null
	schedule.entries.append(bad_count)
	schedule.entries.append(no_enemy)
	assert_eq(schedule.validate_against(100.0).size(), 2)
```

`tests/defs/test_stage_def.gd`:
```gdscript
extends TestCase


func test_factory_stage_is_valid_even_without_trickle_enemies() -> void:
	assert_no_errors(DefFactory.stage().validate(), "empty trickle list is allowed")


func test_stage_collects_errors_from_nested_defs() -> void:
	var def := DefFactory.stage()
	def.building_stats.max_health = 0.0
	var broken_enemy := DefFactory.enemy()
	broken_enemy.stats = null
	def.trickle_enemies.append(broken_enemy)
	assert_true(def.validate().size() >= 2)


func test_stage_requires_positive_duration_and_textures() -> void:
	var def := DefFactory.stage(0.0)
	def.ground_texture = null
	assert_eq(def.validate().size(), 2)


func test_stage_validates_waves_against_its_own_duration() -> void:
	var def := DefFactory.stage(60.0)
	var entry := WaveEntry.new()
	entry.time = 90.0
	entry.count = 3
	entry.enemy = DefFactory.enemy()
	def.waves.entries.append(entry)
	assert_eq(def.validate().size(), 1)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/defs tools/run_tests.sh`
Expected: FAIL/script errors — `StatBlock`, `VisualDef`, etc. are not defined.

- [ ] **Step 3: Implement the defs**

`src/defs/stat_block.gd`:
```gdscript
class_name StatBlock
extends Resource
## Base stats of an actor, applied to its ASC as attribute overrides.

@export var max_health: float = 100.0
@export var move_speed: float = 100.0
## Damage multiplier applied to hits this actor lands (1.0 = base damage).
@export var attack: float = 1.0


## max_health must precede health: health's clamp reads max_health.current_value.
func to_overrides() -> Dictionary[String, float]:
	var overrides: Dictionary[String, float] = {}
	overrides["max_health"] = max_health
	overrides["health"] = max_health
	overrides["move_speed"] = move_speed
	overrides["attack"] = attack
	return overrides


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if max_health <= 0.0:
		errors.append("StatBlock: max_health must be > 0")
	if move_speed < 0.0:
		errors.append("StatBlock: move_speed must be >= 0")
	return errors
```

`src/defs/animation_strip.gd`:
```gdscript
class_name AnimationStrip
extends Resource
## One animation stored as a horizontal strip of equally sized frames.

@export var animation: StringName = AnimNames.IDLE
@export var texture: Texture2D
@export_range(1, 64) var frame_count: int = 1
@export var fps: float = 10.0
@export var loop: bool = true


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if texture == null:
		errors.append("AnimationStrip '%s': texture is null" % animation)
	elif texture.get_width() % frame_count != 0:
		errors.append("AnimationStrip '%s': width %d is not divisible by frame_count %d" % [animation, texture.get_width(), frame_count])
	return errors
```

`src/defs/visual_def.gd`:
```gdscript
class_name VisualDef
extends Resource
## The set of animations an actor can play. Turned into SpriteFrames by ActorVisual.

@export var strips: Array[AnimationStrip] = []
@export var offset: Vector2 = Vector2.ZERO


func find_strip(animation: StringName) -> AnimationStrip:
	for strip in strips:
		if strip != null and strip.animation == animation:
			return strip
	return null


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if find_strip(AnimNames.IDLE) == null:
		errors.append("VisualDef: missing required '%s' strip" % AnimNames.IDLE)
	for strip in strips:
		if strip == null:
			errors.append("VisualDef: null strip")
		else:
			errors.append_array(strip.validate())
	return errors
```

`src/defs/projectile_def.gd`:
```gdscript
class_name ProjectileDef
extends Resource
## Data for a straight-flying projectile.

@export var texture: Texture2D
@export var hit_effect: GameplayEffect
@export var speed: float = 400.0
@export var lifetime: float = 0.7
## Extra enemies the projectile passes through (0 = stops at the first hit).
@export var pierce: int = 0
@export var hit_radius: float = 6.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if texture == null:
		errors.append("ProjectileDef: texture is null")
	if hit_effect == null:
		errors.append("ProjectileDef: hit_effect is null")
	if speed <= 0.0 or lifetime <= 0.0 or hit_radius <= 0.0:
		errors.append("ProjectileDef: speed, lifetime and hit_radius must be > 0")
	return errors
```

`src/defs/drop_entry.gd`:
```gdscript
class_name DropEntry
extends Resource
## One possible drop of a DropTable.

@export var kind: Wallet.Kind = Wallet.Kind.GOLD
@export var icon: Texture2D
@export_range(0.0, 1.0) var chance: float = 1.0
@export var min_amount: int = 1
@export var max_amount: int = 1


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if chance < 0.0 or chance > 1.0:
		errors.append("DropEntry: chance must be within [0, 1]")
	if min_amount < 1 or max_amount < min_amount:
		errors.append("DropEntry: need 1 <= min_amount <= max_amount")
	return errors
```

`src/defs/drop_roll.gd`:
```gdscript
class_name DropRoll
extends RefCounted
## The concrete result of rolling one DropEntry.

var kind: Wallet.Kind = Wallet.Kind.GOLD
var amount: int = 0
var icon: Texture2D
```

`src/defs/drop_table.gd`:
```gdscript
class_name DropTable
extends Resource
## What an enemy may drop on death.

@export var entries: Array[DropEntry] = []


func roll(rng: RunRng) -> Array[DropRoll]:
	var rolls: Array[DropRoll] = []
	for entry in entries:
		if not rng.chance(entry.chance):
			continue
		var drop := DropRoll.new()
		drop.kind = entry.kind
		drop.icon = entry.icon
		drop.amount = rng.randi_range(entry.min_amount, entry.max_amount)
		rolls.append(drop)
	return rolls


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	for entry in entries:
		if entry == null:
			errors.append("DropTable: null entry")
		else:
			errors.append_array(entry.validate())
	return errors
```

`src/defs/enemy_def.gd`:
```gdscript
class_name EnemyDef
extends Resource
## Everything that distinguishes one enemy type; the actor scene itself is generic.

enum TargetPolicy { BUILDING, PLAYER }

@export var id: StringName
@export var scene: PackedScene
@export var visual: VisualDef
@export var visual_scale: float = 1.0
@export var tint: Color = Color.WHITE
@export var stats: StatBlock
@export var contact_effect: GameplayEffect
@export var contact_damage: float = 8.0
@export var contact_interval: float = 0.9
@export var target_policy: TargetPolicy = TargetPolicy.BUILDING
@export var body_radius: float = 10.0
@export var drops: DropTable


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("EnemyDef: id is empty")
	if scene == null:
		errors.append("EnemyDef '%s': scene is null" % id)
	if contact_effect == null:
		errors.append("EnemyDef '%s': contact_effect is null" % id)
	if body_radius <= 0.0 or contact_damage < 0.0 or contact_interval <= 0.0 or visual_scale <= 0.0:
		errors.append("EnemyDef '%s': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0" % id)
	if visual == null:
		errors.append("EnemyDef '%s': visual is null" % id)
	else:
		errors.append_array(visual.validate())
	if stats == null:
		errors.append("EnemyDef '%s': stats is null" % id)
	else:
		errors.append_array(stats.validate())
	if drops != null:
		errors.append_array(drops.validate())
	return errors
```

`src/defs/wave_entry.gd`:
```gdscript
class_name WaveEntry
extends Resource
## One surge: `count` enemies of one type arrive at stage time `time`.

@export var time: float = 30.0
@export var enemy: EnemyDef
@export var count: int = 10
@export var label: String = "Wave"
```

`src/defs/wave_schedule.gd`:
```gdscript
class_name WaveSchedule
extends Resource
## Ordered surges of a stage. Entries may be authored in any order.

@export var entries: Array[WaveEntry] = []


func sorted_entries() -> Array[WaveEntry]:
	var sorted: Array[WaveEntry] = entries.duplicate()
	sorted.sort_custom(func(a: WaveEntry, b: WaveEntry) -> bool: return a.time < b.time)
	return sorted


func times() -> PackedFloat32Array:
	var result := PackedFloat32Array()
	for entry in sorted_entries():
		result.append(entry.time)
	return result


func validate_against(duration: float) -> PackedStringArray:
	var errors := PackedStringArray()
	for entry in entries:
		if entry == null:
			errors.append("WaveSchedule: null entry")
			continue
		if entry.enemy == null:
			errors.append("WaveEntry '%s': enemy is null" % entry.label)
		else:
			errors.append_array(entry.enemy.validate())
		if entry.count <= 0:
			errors.append("WaveEntry '%s': count must be > 0" % entry.label)
		if entry.time < 0.0 or entry.time > duration:
			errors.append("WaveEntry '%s': time %s is outside the stage duration %s" % [entry.label, entry.time, duration])
	return errors
```

`src/defs/stage_def.gd`:
```gdscript
class_name StageDef
extends Resource
## Static description of one stage.

@export var id: StringName
@export var display_name: String = ""
@export var duration: float = 180.0
@export var arena_size: Vector2 = Vector2(2400, 1600)
@export var ground_texture: Texture2D
@export var building_texture: Texture2D
@export var building_stats: StatBlock
@export var trickle_enemies: Array[EnemyDef] = []
@export var trickle_interval_start: float = 2.0
@export var trickle_interval_end: float = 0.8
@export var trickle_cap: int = 60
@export var spawn_radius: float = 520.0
@export var waves: WaveSchedule


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if duration <= 0.0:
		errors.append("StageDef '%s': duration must be > 0" % id)
	if arena_size.x <= 0.0 or arena_size.y <= 0.0 or spawn_radius <= 0.0:
		errors.append("StageDef '%s': arena_size and spawn_radius must be > 0" % id)
	if trickle_interval_start <= 0.0 or trickle_interval_end <= 0.0 or trickle_cap < 0:
		errors.append("StageDef '%s': trickle intervals must be > 0 and trickle_cap >= 0" % id)
	if ground_texture == null or building_texture == null:
		errors.append("StageDef '%s': ground_texture and building_texture are required" % id)
	if building_stats == null:
		errors.append("StageDef '%s': building_stats is null" % id)
	else:
		errors.append_array(building_stats.validate())
	for enemy in trickle_enemies:
		if enemy == null:
			errors.append("StageDef '%s': null trickle enemy" % id)
		else:
			errors.append_array(enemy.validate())
	if waves != null:
		errors.append_array(waves.validate_against(duration))
	return errors
```
Note: the "requires positive duration and textures" test expects exactly 2 errors for `stage(0.0)` with null ground_texture: duration error + textures error = 2. But `waves.validate_against(0.0)` with no entries → none. Good. `test_stage_collects_errors_from_nested_defs`: building_stats invalid (1) + broken enemy stats null (1) = ≥2 ✓. `test_stage_validates_waves...`: wave time 90 > 60 → 1 error; enemy valid ✓.

`src/defs/player_profile.gd`:
```gdscript
class_name PlayerProfile
extends Resource
## The player's starting stats and look for a run. Phase 4 derives this from the skill tree.

@export var stats: StatBlock
@export var visual: VisualDef


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
	return errors
```

`src/defs/run_result.gd`:
```gdscript
class_name RunResult
extends RefCounted
## What a finished stage run reports to its caller.

enum Outcome { CLEARED, FAILED }

var outcome: Outcome = Outcome.FAILED
var elapsed: float = 0.0
var kills: int = 0
var gold: int = 0
var stone: int = 0
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/defs tools/run_tests.sh`
Expected: all `tests/defs` PASS.

- [ ] **Step 5: Commit**

```bash
git add src/defs tests/defs tests/support
git commit -q -m "feat(defs): add typed data definitions with validation

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: GAS content — tags, attribute sets, damage pipeline, cooldown effects

**Files:**
- Create: `tools/sync_tags.gd`, `src/gas/attributes/vitals_attribute_set.gd`, `combat_attribute_set.gd`, `economy_attribute_set.gd`, `src/gas/executions/damage_execution.gd`, `src/gas/effect_builder.gd`, `src/gas/actor_asc.tscn`
- Create: `data/effects/damage_basic.tres`, `cooldown_dash.tres`, `cooldown_arrow.tres`
- Create: `tests/support/actor_fixture.gd`
- Modify (generated): `godot_gas/tag_registry.tres`, `godot_gas/gameplay_tags.gd`
- Test: `tests/gas/test_damage.gd`, `tests/gas/test_cooldown_effects.gd`

**Interfaces:**
- Consumes: `StatBlock.to_overrides()` (Task 3); GodotGAS: `AttributeSet.pre_attribute_change(name, proposed) -> float`, `AttributeData(initial)`, `GameplayExecutionCalculation.execute(spec, target_asc) -> Dictionary`, `GameplayEffectSpec.get_set_by_caller_magnitude(tag)`, `AbilitySystemComponent.apply_effect_spec_to_target / initialize_attribute_overrides / get_attribute / has_attribute / add_tag / remove_tag / has_tag`.
- Produces:
  - `GameplayTags` constants: `Ability_Arrow, Ability_Dash, Cooldown_Arrow, Cooldown_Dash, Damage_Amount, State_Invulnerable`.
  - Attributes by name — Vitals: `health`, `max_health`; Combat: `attack`, `move_speed`; Economy: `gold_gain`, `stone_gain`.
  - `EffectBuilder.damage(instigator: Node, causer: Node, effect: GameplayEffect, amount: float, level := 1.0) -> GameplayEffectSpec` — `instigator` is the **actor root** that has a child named `AbilitySystemComponent`.
  - `res://src/gas/actor_asc.tscn`: scene whose root is an `AbilitySystemComponent` carrying the three attribute sets. Instance it in every actor and name the node `AbilitySystemComponent`.
  - Effects: `damage_basic.tres` (INSTANT, `DamageExecution`, refused while `State.Invulnerable`), `cooldown_dash.tres` (DURATION 1.2 s, grants `Cooldown.Dash`), `cooldown_arrow.tres` (DURATION 0.7 s, grants `Cooldown.Arrow`).
  - Test helper `ActorFixture.make(test: TestCase, stats: StatBlock = null) -> Node2D`, `ActorFixture.asc_of(actor: Node) -> AbilitySystemComponent`.

- [ ] **Step 1: Register the gameplay tags**

`tools/sync_tags.gd`:
```gdscript
extends SceneTree
## Rebuilds godot_gas/tag_registry.tres and godot_gas/gameplay_tags.gd from TAGS.
## Run: Godot --headless --path . -s res://tools/sync_tags.gd

const REGISTRY_PATH: String = "res://godot_gas/tag_registry.tres"
const TAGS: PackedStringArray = [
	"Ability.Arrow",
	"Ability.Dash",
	"Cooldown.Arrow",
	"Cooldown.Dash",
	"Damage.Amount",
	"State.Invulnerable",
]


func _init() -> void:
	var registry := load(REGISTRY_PATH) as GameplayTagRegistry
	if registry == null:
		push_error("sync_tags: cannot load %s" % REGISTRY_PATH)
		quit(1)
		return
	for existing in registry.tags.duplicate():
		registry.remove_tag(existing)
	for tag in TAGS:
		var result := registry.add_tag(tag)
		if result.begins_with("Error"):
			push_error("sync_tags: %s" % result)
			quit(1)
			return
	quit(0)
```
Run: `"/d/CM/Godot/Godot_v4.7.2-stable_win64.exe" --headless --path . -s res://tools/sync_tags.gd`
Expected: prints `GodotGAS: Successfully generated gameplay_tags.gd with 6 constants.` Then `grep -c "const " godot_gas/gameplay_tags.gd` prints `6` and the file contains `const Damage_Amount: StringName = &"Damage.Amount"`. Tag parts must be single capitalised words (the registry lower-cases the rest of each part).

- [ ] **Step 2: Write the test helper and failing tests**

`tests/support/actor_fixture.gd`:
```gdscript
class_name ActorFixture
extends RefCounted
## Builds a bare actor (Node2D + AbilitySystemComponent child) for GAS-level tests.

const ACTOR_ASC: PackedScene = preload("res://src/gas/actor_asc.tscn")


## The ASC must be inside the tree before overrides are applied (attribute sets are copied in _ready).
static func make(test: TestCase, stats: StatBlock = null) -> Node2D:
	var actor := test.adopt(Node2D.new()) as Node2D
	var asc := ACTOR_ASC.instantiate() as AbilitySystemComponent
	asc.name = "AbilitySystemComponent"
	actor.add_child(asc)
	if stats != null:
		asc.initialize_attribute_overrides(stats.to_overrides())
	return actor


static func asc_of(actor: Node) -> AbilitySystemComponent:
	return actor.get_node("AbilitySystemComponent") as AbilitySystemComponent
```

`tests/gas/test_damage.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float, attack: float = 1.0) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	stats.attack = attack
	return stats


func _hit(attacker: Node2D, victim: Node2D, amount: float) -> ActiveGameplayEffect:
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	return ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func test_overrides_set_health_and_max_health() -> void:
	var actor := ActorFixture.make(self, _stats(50.0))
	assert_almost_eq(_health(actor), 50.0)
	assert_almost_eq(ActorFixture.asc_of(actor).get_attribute("max_health").current_value, 50.0)


func test_health_is_clamped_to_max_health() -> void:
	var actor := ActorFixture.make(self, _stats(50.0))
	var overrides: Dictionary[String, float] = {"health": 80.0}
	ActorFixture.asc_of(actor).initialize_attribute_overrides(overrides)
	assert_almost_eq(_health(actor), 50.0)


func test_damage_scales_with_source_attack() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0, 2.0))
	var victim := ActorFixture.make(self, _stats(100.0))
	assert_not_null(_hit(attacker, victim, 10.0))
	assert_almost_eq(_health(victim), 80.0)


func test_damage_cannot_push_health_below_zero() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(30.0))
	_hit(attacker, victim, 500.0)
	assert_almost_eq(_health(victim), 0.0)


func test_invulnerable_target_takes_no_damage_until_the_tag_is_removed() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(100.0))
	var victim_asc := ActorFixture.asc_of(victim)
	victim_asc.add_tag(GameplayTags.State_Invulnerable)
	assert_null(_hit(attacker, victim, 10.0), "blocked hits return null")
	assert_almost_eq(_health(victim), 100.0)
	victim_asc.remove_tag(GameplayTags.State_Invulnerable)
	_hit(attacker, victim, 10.0)
	assert_almost_eq(_health(victim), 90.0)


func test_attribute_sets_are_not_shared_between_actors() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var first := ActorFixture.make(self, _stats(100.0))
	var second := ActorFixture.make(self, _stats(100.0))
	_hit(attacker, first, 40.0)
	assert_almost_eq(_health(first), 60.0)
	assert_almost_eq(_health(second), 100.0)
```

`tests/gas/test_cooldown_effects.gd`:
```gdscript
extends TestCase


func test_cooldown_effects_grant_their_tags_for_a_duration() -> void:
	var dash := load("res://data/effects/cooldown_dash.tres") as GameplayEffect
	var arrow := load("res://data/effects/cooldown_arrow.tres") as GameplayEffect
	assert_eq(dash.policy, GameplayEffect.DurationPolicy.DURATION)
	assert_true(dash.granted_tags.has(GameplayTags.Cooldown_Dash))
	assert_eq(arrow.policy, GameplayEffect.DurationPolicy.DURATION)
	assert_true(arrow.granted_tags.has(GameplayTags.Cooldown_Arrow))


func test_cooldown_tag_is_present_then_expires() -> void:
	var effect := (load("res://data/effects/cooldown_dash.tres") as GameplayEffect).duplicate() as GameplayEffect
	effect.duration = 0.1
	var actor := ActorFixture.make(self)
	var asc := ActorFixture.asc_of(actor)
	asc.apply_gameplay_effect(effect)
	assert_true(asc.has_tag(GameplayTags.Cooldown_Dash))
	await wait_seconds(0.3)
	assert_false(asc.has_tag(GameplayTags.Cooldown_Dash))
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/gas tools/run_tests.sh`
Expected: errors — `actor_asc.tscn`, `EffectBuilder`, `damage_basic.tres` do not exist.

- [ ] **Step 4: Implement attribute sets, execution, builder**

`src/gas/attributes/vitals_attribute_set.gd`:
```gdscript
class_name VitalsAttributeSet
extends AttributeSet
## Hit points. `health` is clamped to [0, max_health].

var health: AttributeData = AttributeData.new(100.0)
var max_health: AttributeData = AttributeData.new(100.0)


func pre_attribute_change(attribute_name: String, proposed_value: float) -> float:
	match attribute_name:
		"health":
			return clampf(proposed_value, 0.0, max_health.current_value)
		"max_health":
			return maxf(proposed_value, 1.0)
	return proposed_value
```

`src/gas/attributes/combat_attribute_set.gd`:
```gdscript
class_name CombatAttributeSet
extends AttributeSet
## Offensive and mobility stats.

## Damage multiplier for hits this actor lands.
var attack: AttributeData = AttributeData.new(1.0)
## Pixels per second.
var move_speed: AttributeData = AttributeData.new(100.0)


func pre_attribute_change(_attribute_name: String, proposed_value: float) -> float:
	return maxf(proposed_value, 0.0)
```

`src/gas/attributes/economy_attribute_set.gd`:
```gdscript
class_name EconomyAttributeSet
extends AttributeSet
## Multipliers applied to currency the actor collects.

var gold_gain: AttributeData = AttributeData.new(1.0)
var stone_gain: AttributeData = AttributeData.new(1.0)


func pre_attribute_change(_attribute_name: String, proposed_value: float) -> float:
	return maxf(proposed_value, 0.0)
```

`src/gas/executions/damage_execution.gd`:
```gdscript
class_name DamageExecution
extends GameplayExecutionCalculation
## Damage = the amount injected by the caller (Damage.Amount) x the source's `attack` multiplier.


func execute(spec: GameplayEffectSpec, _target_asc: AbilitySystemComponent) -> Dictionary:
	var amount := spec.get_set_by_caller_magnitude(GameplayTags.Damage_Amount)
	var damage := maxf(amount * _source_attack(spec), 0.0)
	return {"health": -damage}


func _source_attack(spec: GameplayEffectSpec) -> float:
	var instigator: Node = spec.context.instigator if spec.context != null else null
	if instigator == null:
		return 1.0
	var source_asc := instigator.get_node_or_null("AbilitySystemComponent") as AbilitySystemComponent
	if source_asc == null or not source_asc.has_attribute("attack"):
		return 1.0
	return source_asc.get_attribute("attack").current_value
```

`src/gas/effect_builder.gd`:
```gdscript
class_name EffectBuilder
extends RefCounted
## The single place that assembles damage specs, so every hit carries the same context.


## `instigator` is the actor root owning an AbilitySystemComponent child; `causer` is whatever
## physically dealt the hit (projectile, the actor itself for melee).
static func damage(instigator: Node, causer: Node, effect: GameplayEffect, amount: float, level: float = 1.0) -> GameplayEffectSpec:
	var context := GameplayEffectContext.new(instigator, causer)
	var spec := GameplayEffectSpec.new(effect, context, level)
	spec.set_set_by_caller_magnitude(GameplayTags.Damage_Amount, amount)
	return spec
```

- [ ] **Step 5: Implement the shared ASC scene and the effect data**

`src/gas/actor_asc.tscn`:
```
[gd_scene load_steps=8 format=3]

[ext_resource type="Script" path="res://addons/GodotGAS/components/ability_system_component.gd" id="1_asc"]
[ext_resource type="Script" path="res://addons/GodotGAS/attributes/attribute_set.gd" id="2_set"]
[ext_resource type="Script" path="res://src/gas/attributes/vitals_attribute_set.gd" id="3_vitals"]
[ext_resource type="Script" path="res://src/gas/attributes/combat_attribute_set.gd" id="4_combat"]
[ext_resource type="Script" path="res://src/gas/attributes/economy_attribute_set.gd" id="5_economy"]

[sub_resource type="Resource" id="Resource_vitals"]
script = ExtResource("3_vitals")

[sub_resource type="Resource" id="Resource_combat"]
script = ExtResource("4_combat")

[sub_resource type="Resource" id="Resource_economy"]
script = ExtResource("5_economy")

[node name="AbilitySystemComponent" type="Node"]
script = ExtResource("1_asc")
attribute_sets = Array[ExtResource("2_set")]([SubResource("Resource_vitals"), SubResource("Resource_combat"), SubResource("Resource_economy")])
```

`data/effects/damage_basic.tres`:
```
[gd_resource type="Resource" script_class="GameplayEffect" load_steps=7 format=3]

[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="1_effect"]
[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_execution_calculation.gd" id="2_calc"]
[ext_resource type="Script" path="res://src/gas/executions/damage_execution.gd" id="3_damage"]
[ext_resource type="Script" path="res://addons/GodotGAS/gameplay_tag/gameplay_tag_query.gd" id="4_query"]

[sub_resource type="Resource" id="Resource_damage"]
script = ExtResource("3_damage")

[sub_resource type="Resource" id="Resource_query"]
script = ExtResource("4_query")
ignore_tags = Array[StringName]([&"State.Invulnerable"])

[resource]
script = ExtResource("1_effect")
application_query = SubResource("Resource_query")
executions = Array[ExtResource("2_calc")]([SubResource("Resource_damage")])
```

`data/effects/cooldown_dash.tres`:
```
[gd_resource type="Resource" script_class="GameplayEffect" load_steps=2 format=3]

[ext_resource type="Script" path="res://addons/GodotGAS/effects/gameplay_effect.gd" id="1_effect"]

[resource]
script = ExtResource("1_effect")
policy = 1
duration = 1.2
granted_tags = Array[StringName]([&"Cooldown.Dash"])
```

`data/effects/cooldown_arrow.tres`: identical to `cooldown_dash.tres` but `duration = 0.7` and `&"Cooldown.Arrow"`.

If Godot reports a parse error for any hand-written `.tscn/.tres` in this plan, open it once in the editor (or recreate the resource there) and compare with the text here; the property names and values are what matter, ids/uids are not.

- [ ] **Step 6: Run to verify pass**

Run: `IMPORT=0 FILTER=tests/gas tools/run_tests.sh`
Expected: 8 tests PASS. If `test_health_is_clamped_to_max_health` fails, the ASC did not clamp via `pre_attribute_change` — inspect `ability_system_component.gd:1029-1048` (`_commit_spec_math`) before changing anything else.

- [ ] **Step 7: Commit**

```bash
git add tools src/gas data/effects godot_gas tests/gas tests/support
git commit -q -m "feat(gas): add tags, attribute sets, damage execution and cooldown effects

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Combat components — Health, Hurtbox, Hitbox, EffectApplier

**Files:**
- Create: `src/components/health.gd`, `hurtbox.gd`, `hitbox.gd`, `effect_applier.gd`
- Modify: `tests/support/test_case.gd` (add `next_physics_frame`)
- Test: `tests/components/test_health.gd`, `test_hit_detection.gd`, `test_effect_applier.gd`

**Interfaces:**
- Consumes: `ActorFixture`, `EffectBuilder.damage(...)`, `StatBlock` (Tasks 3–4); ASC signals `attribute_changed(name, old, new, spec)`.
- Produces:
  - `Health` (Node): `signal changed(current: float, maximum: float)`, `signal damaged(amount: float, instigator: Node)`, `signal died`; `is_dead: bool`; `bind(asc)` (emits `changed` once); `current() -> float`; `maximum() -> float`. Never stores hit points itself.
  - `Hurtbox` (Area2D): `asc: AbilitySystemComponent`, `actor: Node2D`, `bind(asc, actor)`.
  - `Hitbox` (Area2D): `signal hurtbox_entered(hurtbox: Hurtbox)`, `once_per_target := true`, `reset()`, `overlapping_hurtboxes() -> Array[Hurtbox]`.
  - `EffectApplier` (Node): `bind(source_asc)`, `hit(hurtbox: Hurtbox, effect: GameplayEffect, amount: float, causer: Node = null) -> bool` (true only if the target actually received the effect).
  - `TestCase.next_physics_frame()`.

- [ ] **Step 1: Extend the test base class**

Add to `tests/support/test_case.gd` (after `next_frame`):
```gdscript
func next_physics_frame() -> void:
	await host.get_tree().physics_frame
```

- [ ] **Step 2: Write the failing tests**

`tests/components/test_health.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return stats


func _hit(attacker: Node2D, victim: Node2D, amount: float) -> void:
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))


func _health_for(actor: Node2D) -> Health:
	var health := Health.new()
	actor.add_child(health)
	return health


func test_bind_emits_initial_values() -> void:
	var victim := ActorFixture.make(self, _stats(50.0))
	var health := _health_for(victim)
	var seen: Array = []
	health.changed.connect(func(current: float, maximum: float) -> void: seen.append([current, maximum]))
	health.bind(ActorFixture.asc_of(victim))
	assert_eq(seen, [[50.0, 50.0]])


func test_damage_emits_damaged_with_instigator_and_changed() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(50.0))
	var health := _health_for(victim)
	health.bind(ActorFixture.asc_of(victim))
	var damages: Array = []
	var changes: Array = []
	health.damaged.connect(func(amount: float, instigator: Node) -> void: damages.append([amount, instigator]))
	health.changed.connect(func(current: float, _maximum: float) -> void: changes.append(current))
	_hit(attacker, victim, 12.0)
	assert_eq(damages, [[12.0, attacker]])
	assert_eq(changes, [38.0])
	assert_almost_eq(health.current(), 38.0)


func test_lethal_damage_emits_died_exactly_once() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(20.0))
	var health := _health_for(victim)
	health.bind(ActorFixture.asc_of(victim))
	var deaths: Array = []
	health.died.connect(func() -> void: deaths.append(true))
	_hit(attacker, victim, 500.0)
	_hit(attacker, victim, 500.0)
	assert_eq(deaths.size(), 1)
	assert_true(health.is_dead)
```

`tests/components/test_hit_detection.gd`:
```gdscript
extends TestCase


func _area(area: Area2D, layer: int, mask: int) -> Area2D:
	area.collision_layer = layer
	area.collision_mask = mask
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	area.add_child(shape)
	return adopt(area) as Area2D


func _pair() -> Array:
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ENEMY_HURT) as Hitbox
	var hurtbox := _area(Hurtbox.new(), PhysicsLayers.ENEMY_HURT, 0) as Hurtbox
	return [hitbox, hurtbox]


func test_hitbox_reports_overlapping_hurtbox() -> void:
	var pair := _pair()
	var hitbox: Hitbox = pair[0]
	var hurtbox: Hurtbox = pair[1]
	var entered: Array = []
	hitbox.hurtbox_entered.connect(func(found: Hurtbox) -> void: entered.append(found))
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered, [hurtbox])
	assert_eq(hitbox.overlapping_hurtboxes(), [hurtbox])


func test_once_per_target_ignores_reentry_until_reset() -> void:
	var pair := _pair()
	var hitbox: Hitbox = pair[0]
	var hurtbox: Hurtbox = pair[1]
	var entered: Array = []
	hitbox.hurtbox_entered.connect(func(found: Hurtbox) -> void: entered.append(found))
	await next_physics_frame()
	await next_physics_frame()
	hurtbox.position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	hurtbox.position = Vector2.ZERO
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered.size(), 1, "re-entry ignored")
	hurtbox.position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	hitbox.reset()
	hurtbox.position = Vector2.ZERO
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(entered.size(), 2, "re-entry counted after reset")


func test_non_hurtbox_areas_are_ignored() -> void:
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ENEMY_HURT) as Hitbox
	_area(Area2D.new(), PhysicsLayers.ENEMY_HURT, 0)
	await next_physics_frame()
	await next_physics_frame()
	assert_true(hitbox.overlapping_hurtboxes().is_empty())
```

`tests/components/test_effect_applier.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return stats


func _hurtbox_for(actor: Node2D) -> Hurtbox:
	var hurtbox := Hurtbox.new()
	actor.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(actor), actor)
	return hurtbox


func _applier_for(actor: Node2D) -> EffectApplier:
	var applier := EffectApplier.new()
	actor.add_child(applier)
	applier.bind(ActorFixture.asc_of(actor))
	return applier


func test_hit_damages_the_target() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(100.0))
	assert_true(_applier_for(attacker).hit(_hurtbox_for(victim), DAMAGE, 15.0))
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 85.0)


func test_hit_reports_false_when_target_is_invulnerable() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(100.0))
	ActorFixture.asc_of(victim).add_tag(GameplayTags.State_Invulnerable)
	assert_false(_applier_for(attacker).hit(_hurtbox_for(victim), DAMAGE, 15.0))


func test_hit_with_a_freed_source_is_a_safe_no_op() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var victim := ActorFixture.make(self, _stats(100.0))
	var applier := _applier_for(attacker)
	var hurtbox := _hurtbox_for(victim)
	ActorFixture.asc_of(attacker).free()
	assert_false(applier.hit(hurtbox, DAMAGE, 15.0))


func test_hit_with_an_unbound_hurtbox_is_a_safe_no_op() -> void:
	var attacker := ActorFixture.make(self, _stats(100.0))
	var hurtbox := adopt(Hurtbox.new()) as Hurtbox
	assert_false(_applier_for(attacker).hit(hurtbox, DAMAGE, 15.0))
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/components tools/run_tests.sh`
Expected: errors — `Health`, `Hurtbox`, `Hitbox`, `EffectApplier` undefined.

- [ ] **Step 4: Implement**

`src/components/health.gd`:
```gdscript
class_name Health
extends Node
## Domain view of an actor's vitals. Observes the ASC and never stores hit points itself.
## Bind it AFTER the actor's stats were applied so the initial overrides do not count as damage.

signal changed(current: float, maximum: float)
signal damaged(amount: float, instigator: Node)
signal died

var is_dead: bool = false
var _asc: AbilitySystemComponent


func bind(asc: AbilitySystemComponent) -> void:
	_asc = asc
	_asc.attribute_changed.connect(_on_attribute_changed)
	changed.emit(current(), maximum())


func current() -> float:
	return _asc.get_attribute("health").current_value if _asc != null else 0.0


func maximum() -> float:
	return _asc.get_attribute("max_health").current_value if _asc != null else 0.0


func _on_attribute_changed(attribute_name: String, old_value: float, new_value: float, spec: GameplayEffectSpec) -> void:
	if attribute_name != "health" and attribute_name != "max_health":
		return
	if attribute_name == "health" and new_value < old_value:
		var instigator: Node = null
		if spec != null and spec.context != null:
			instigator = spec.context.instigator
		damaged.emit(old_value - new_value, instigator)
	changed.emit(current(), maximum())
	if attribute_name == "health" and new_value <= 0.0 and not is_dead:
		is_dead = true
		died.emit()
```

`src/components/hurtbox.gd`:
```gdscript
class_name Hurtbox
extends Area2D
## Hit-receiving area. Knows which ASC and actor it protects; holds no behaviour.
## Configure in the scene: collision_layer = the owner's hurt layer, collision_mask = 0, monitoring = false.

var asc: AbilitySystemComponent
var actor: Node2D


func bind(p_asc: AbilitySystemComponent, p_actor: Node2D) -> void:
	asc = p_asc
	actor = p_actor
```

`src/components/hitbox.gd`:
```gdscript
class_name Hitbox
extends Area2D
## Hit-detecting area. Configure in the scene: collision_layer = 0, collision_mask = the target's hurt layer,
## monitorable = false.

signal hurtbox_entered(hurtbox: Hurtbox)

## When true, each hurtbox is reported once until reset() (projectiles). Contact damage polls instead.
@export var once_per_target: bool = true

var _already_hit: Dictionary = {}


func _ready() -> void:
	area_entered.connect(_on_area_entered)


func reset() -> void:
	_already_hit.clear()


func overlapping_hurtboxes() -> Array[Hurtbox]:
	var result: Array[Hurtbox] = []
	for area in get_overlapping_areas():
		var hurtbox := area as Hurtbox
		if hurtbox != null:
			result.append(hurtbox)
	return result


func _on_area_entered(area: Area2D) -> void:
	var hurtbox := area as Hurtbox
	if hurtbox == null:
		return
	if once_per_target:
		var id := hurtbox.get_instance_id()
		if _already_hit.has(id):
			return
		_already_hit[id] = true
	hurtbox_entered.emit(hurtbox)
```

`src/components/effect_applier.gd`:
```gdscript
class_name EffectApplier
extends Node
## Applies damage effects on behalf of one actor (the instigator).

var _source_asc: AbilitySystemComponent


func bind(source_asc: AbilitySystemComponent) -> void:
	_source_asc = source_asc


## Returns true only when the target actually received the effect.
func hit(hurtbox: Hurtbox, effect: GameplayEffect, amount: float, causer: Node = null) -> bool:
	if not is_instance_valid(_source_asc) or not is_instance_valid(hurtbox) or not is_instance_valid(hurtbox.asc):
		return false
	var instigator := _source_asc.get_parent()
	var spec := EffectBuilder.damage(instigator, causer if causer != null else instigator, effect, amount)
	return _source_asc.apply_effect_spec_to_target(spec, hurtbox.asc) != null
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/components tools/run_tests.sh`
Expected: all PASS. If `test_damage_emits_damaged...` shows `damaged` never fired, the ASC does not emit `attribute_changed` for instant effects — read `ability_system_component.gd:550-569` (`_recalculate_attribute`) and fix `Health` to read `effect_received` instead; do not edit the plugin.

- [ ] **Step 6: Commit**

```bash
git add src/components tests/components tests/support
git commit -q -m "feat(components): add Health, Hurtbox, Hitbox and EffectApplier

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Behaviour components — Mover, Lifetime, ActorVisual, ContactDamage, LootDropper, TargetProvider

**Files:**
- Create: `src/components/mover.gd`, `lifetime.gd`, `actor_visual.gd`, `contact_damage.gd`, `loot_dropper.gd`, `target_provider.gd`
- Test: `tests/components/test_mover.gd`, `test_lifetime.gd`, `test_actor_visual.gd`, `test_contact_damage.gd`, `test_loot_dropper.gd`, `test_target_provider.gd`

**Interfaces:**
- Consumes: Task 3–5 types (`VisualDef`, `DropTable`, `DropRoll`, `RunRng`, `EnemyDef.TargetPolicy`, `Health`, `Hitbox`, `Hurtbox`, `EffectApplier`).
- Produces:
  - `Mover` (Node): `signal dash_started`, `signal dash_finished`; `last_direction: Vector2`; `bind(body: CharacterBody2D, asc)`; `set_direction(Vector2)`; `direction() -> Vector2`; `is_dashing() -> bool`; `is_moving() -> bool`; `dash(direction, speed, duration)` (falls back to `last_direction` when direction is zero); `step(delta)` (called by `_physics_process`; sets `body.velocity` from the ASC `move_speed` attribute and calls `move_and_slide()`).
  - `Lifetime` (Node): `signal expired`; `@export seconds`; `restart(new_seconds)`; `step(delta)` (called by `_physics_process`).
  - `ActorVisual` (AnimatedSprite2D): `signal one_shot_finished(animation_name)`; `apply(def: VisualDef, scale_factor := 1.0, tint := Color.WHITE)`; `set_moving(bool)`; `face(direction_x: float)`; `play_once(animation_name)`; `flash_hit()`.
  - `ContactDamage` (Node): `signal attacked(targets: Array[Hurtbox])`; `bind(hitbox, applier, effect, damage, interval)`; `step(delta)`.
  - `LootDropper` (Node): `signal drop_requested(roll: DropRoll, position: Vector2)`; `bind(health, table: DropTable, rng: RunRng, anchor: Node2D)`.
  - `TargetProvider` (Node): `building: Node2D`, `player: Node2D`; `target_for(policy: EnemyDef.TargetPolicy) -> Node2D` (falls back to the other target, then null); `nearest_enemy(origin: Vector2, max_range: float) -> Node2D` (group `Groups.ENEMIES`, skips nodes queued for deletion).

- [ ] **Step 1: Write the failing tests**

`tests/components/test_mover.gd`:
```gdscript
extends TestCase


func _rig(speed: float) -> Array:
	var stats := StatBlock.new()
	stats.move_speed = speed
	var actor := ActorFixture.make(self, stats)
	var body := CharacterBody2D.new()
	actor.add_child(body)
	var mover := Mover.new()
	actor.add_child(mover)
	mover.bind(body, ActorFixture.asc_of(actor))
	return [body, mover]


func test_velocity_follows_direction_and_asc_speed() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	mover.set_direction(Vector2.RIGHT)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(100, 0))
	mover.set_direction(Vector2.ZERO)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2.ZERO)


func test_diagonal_input_is_not_faster() -> void:
	var rig := _rig(100.0)
	var mover: Mover = rig[1]
	mover.set_direction(Vector2(1, 1))
	mover.step(0.1)
	assert_almost_eq((rig[0] as CharacterBody2D).velocity.length(), 100.0, 0.01)


func test_dash_overrides_velocity_then_finishes() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	var finished: Array = []
	mover.dash_finished.connect(func() -> void: finished.append(true))
	mover.set_direction(Vector2.UP)
	mover.dash(Vector2.RIGHT, 400.0, 0.2)
	assert_true(mover.is_dashing())
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(400, 0))
	mover.step(0.15)
	assert_false(mover.is_dashing())
	assert_eq(finished.size(), 1)
	mover.step(0.1)
	assert_eq(body.velocity, Vector2(0, -100), "back to walking")


func test_dash_without_direction_uses_last_direction() -> void:
	var rig := _rig(100.0)
	var body: CharacterBody2D = rig[0]
	var mover: Mover = rig[1]
	mover.set_direction(Vector2.LEFT)
	mover.set_direction(Vector2.ZERO)
	mover.dash(Vector2.ZERO, 300.0, 0.2)
	mover.step(0.05)
	assert_eq(body.velocity, Vector2(-300, 0))
```

`tests/components/test_lifetime.gd`:
```gdscript
extends TestCase


func test_expires_once_when_time_runs_out() -> void:
	var lifetime := adopt(Lifetime.new()) as Lifetime
	var expired: Array = []
	lifetime.expired.connect(func() -> void: expired.append(true))
	lifetime.restart(1.0)
	lifetime.step(0.5)
	assert_eq(expired.size(), 0)
	lifetime.step(0.6)
	lifetime.step(5.0)
	assert_eq(expired.size(), 1)


func test_restart_rearms_the_countdown() -> void:
	var lifetime := adopt(Lifetime.new()) as Lifetime
	var expired: Array = []
	lifetime.expired.connect(func() -> void: expired.append(true))
	lifetime.restart(0.1)
	lifetime.step(0.2)
	lifetime.restart(0.1)
	lifetime.step(0.2)
	assert_eq(expired.size(), 2)
```

`tests/components/test_actor_visual.gd`:
```gdscript
extends TestCase


func _strip(animation: StringName, frames: int, loop: bool) -> AnimationStrip:
	var strip := AnimationStrip.new()
	strip.animation = animation
	strip.texture = DefFactory.texture(frames * 10, 10)
	strip.frame_count = frames
	strip.loop = loop
	return strip


func _visual() -> ActorVisual:
	var def := VisualDef.new()
	def.strips.append(_strip(AnimNames.IDLE, 6, true))
	def.strips.append(_strip(AnimNames.WALK, 8, true))
	def.strips.append(_strip(AnimNames.HURT, 4, false))
	var visual := adopt(ActorVisual.new()) as ActorVisual
	visual.apply(def, 2.0, Color.RED)
	return visual


func test_apply_builds_frames_from_strips() -> void:
	var visual := _visual()
	assert_eq(visual.sprite_frames.get_frame_count(AnimNames.IDLE), 6)
	assert_eq(visual.sprite_frames.get_frame_count(AnimNames.WALK), 8)
	assert_false(visual.sprite_frames.get_animation_loop(AnimNames.HURT))
	assert_eq(visual.animation, AnimNames.IDLE)
	assert_eq(visual.scale, Vector2(2, 2))
	assert_eq(visual.modulate, Color.RED)


func test_set_moving_switches_between_walk_and_idle() -> void:
	var visual := _visual()
	visual.set_moving(true)
	assert_eq(visual.animation, AnimNames.WALK)
	visual.set_moving(false)
	assert_eq(visual.animation, AnimNames.IDLE)


func test_one_shot_is_not_interrupted_by_locomotion() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.HURT)
	visual.set_moving(true)
	assert_eq(visual.animation, AnimNames.HURT)


func test_missing_one_shot_animation_is_ignored() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.DEATH)
	assert_eq(visual.animation, AnimNames.IDLE)


func test_face_flips_horizontally_only_with_real_input() -> void:
	var visual := _visual()
	visual.face(-1.0)
	assert_true(visual.flip_h)
	visual.face(0.0)
	assert_true(visual.flip_h, "zero input keeps the facing")
	visual.face(1.0)
	assert_false(visual.flip_h)
```

`tests/components/test_contact_damage.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats() -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = 100.0
	return stats


func _area(area: Area2D, layer: int, mask: int, parent: Node) -> Area2D:
	area.collision_layer = layer
	area.collision_mask = mask
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	area.add_child(shape)
	parent.add_child(area)
	return area


func _rig() -> Array:
	var attacker := ActorFixture.make(self, _stats())
	var victim := ActorFixture.make(self, _stats())
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ALLY_HURT, attacker) as Hitbox
	hitbox.once_per_target = false
	var hurtbox := _area(Hurtbox.new(), PhysicsLayers.ALLY_HURT, 0, victim) as Hurtbox
	hurtbox.bind(ActorFixture.asc_of(victim), victim)
	var applier := EffectApplier.new()
	attacker.add_child(applier)
	applier.bind(ActorFixture.asc_of(attacker))
	var contact := ContactDamage.new()
	attacker.add_child(contact)
	contact.bind(hitbox, applier, DAMAGE, 10.0, 1.0)
	return [contact, victim]


func _health(victim: Node2D) -> float:
	return ActorFixture.asc_of(victim).get_attribute("health").current_value


func test_damages_overlapping_hurtbox_then_waits_for_interval() -> void:
	var rig := _rig()
	var contact: ContactDamage = rig[0]
	var victim: Node2D = rig[1]
	await next_physics_frame()
	await next_physics_frame()
	var attacks: Array = []
	contact.attacked.connect(func(targets: Array[Hurtbox]) -> void: attacks.append(targets.size()))
	contact.step(0.0)
	assert_almost_eq(_health(victim), 90.0)
	contact.step(0.5)
	assert_almost_eq(_health(victim), 90.0, 0.001, "still cooling down")
	contact.step(0.6)
	assert_almost_eq(_health(victim), 80.0)
	assert_eq(attacks, [1, 1])


func test_nothing_happens_without_overlap() -> void:
	var rig := _rig()
	var contact: ContactDamage = rig[0]
	var victim: Node2D = rig[1]
	(victim.get_child(-1) as Node2D).position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	contact.step(0.0)
	assert_almost_eq(_health(victim), 100.0)
```
Note: `(victim.get_child(-1) as Node2D)` is the Hurtbox, last child added to the victim (ASC added first by fixture, then hurtbox).

`tests/components/test_loot_dropper.gd`:
```gdscript
extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _table(chance: float) -> DropTable:
	var entry := DropEntry.new()
	entry.kind = Wallet.Kind.GOLD
	entry.chance = chance
	entry.min_amount = 3
	entry.max_amount = 3
	var table := DropTable.new()
	table.entries.append(entry)
	return table


func _kill(table: DropTable) -> Array:
	var stats := StatBlock.new()
	stats.max_health = 10.0
	var killer := ActorFixture.make(self, stats)
	var victim := ActorFixture.make(self, stats)
	victim.position = Vector2(40, 60)
	var health := Health.new()
	victim.add_child(health)
	health.bind(ActorFixture.asc_of(victim))
	var dropper := LootDropper.new()
	victim.add_child(dropper)
	dropper.bind(health, table, RunRng.new(3), victim)
	var drops: Array = []
	dropper.drop_requested.connect(func(roll: DropRoll, position: Vector2) -> void: drops.append([roll, position]))
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))
	return drops


func test_death_requests_the_rolled_drops_at_the_anchor() -> void:
	var drops := _kill(_table(1.0))
	assert_eq(drops.size(), 1)
	assert_eq(drops[0][0].amount, 3)
	assert_true((drops[0][1] as Vector2).distance_to(Vector2(40, 60)) <= 12.0, "scattered near the anchor")


func test_no_table_means_no_drops() -> void:
	assert_true(_kill(null).is_empty())


func test_failed_chance_means_no_drops() -> void:
	assert_true(_kill(_table(0.0)).is_empty())
```

`tests/components/test_target_provider.gd`:
```gdscript
extends TestCase


func _provider() -> TargetProvider:
	return adopt(TargetProvider.new()) as TargetProvider


func _enemy(at: Vector2) -> Node2D:
	var node := adopt(Node2D.new()) as Node2D
	node.add_to_group(Groups.ENEMIES)
	node.global_position = at
	return node


func test_nearest_enemy_picks_closest_within_range() -> void:
	var provider := _provider()
	var far := _enemy(Vector2(200, 0))
	var near := _enemy(Vector2(50, 0))
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 300.0), near)
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 100.0), near)
	far.queue_free()


func test_nearest_enemy_is_null_when_nothing_is_in_range() -> void:
	var provider := _provider()
	_enemy(Vector2(500, 0))
	assert_null(provider.nearest_enemy(Vector2.ZERO, 100.0))


func test_nearest_enemy_skips_queued_for_deletion() -> void:
	var provider := _provider()
	var doomed := _enemy(Vector2(10, 0))
	var other := _enemy(Vector2(80, 0))
	doomed.queue_free()
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 300.0), other)


func test_nearest_enemy_skips_nodes_that_left_the_group() -> void:
	var provider := _provider()
	var dying := _enemy(Vector2(10, 0))
	dying.remove_from_group(Groups.ENEMIES)
	assert_null(provider.nearest_enemy(Vector2.ZERO, 300.0))


func test_target_for_prefers_policy_target_then_falls_back() -> void:
	var provider := _provider()
	var building := adopt(Node2D.new()) as Node2D
	var player := adopt(Node2D.new()) as Node2D
	provider.building = building
	provider.player = player
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.BUILDING), building)
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.PLAYER), player)
	player.free()
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.PLAYER), building)
	building.free()
	assert_null(provider.target_for(EnemyDef.TargetPolicy.BUILDING))
```
Note: tests that `free()` an adopted node are safe — `release_owned_nodes` checks `is_instance_valid`.

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/components tools/run_tests.sh`
Expected: errors — `Mover`, `Lifetime`, `ActorVisual`, `ContactDamage`, `LootDropper`, `TargetProvider` undefined.

- [ ] **Step 3: Implement**

`src/components/mover.gd`:
```gdscript
class_name Mover
extends Node
## Drives a CharacterBody2D at the speed of the ASC's `move_speed` attribute, with a dash override.

signal dash_started
signal dash_finished

var last_direction: Vector2 = Vector2.RIGHT
var _body: CharacterBody2D
var _asc: AbilitySystemComponent
var _direction: Vector2 = Vector2.ZERO
var _dash_velocity: Vector2 = Vector2.ZERO
var _dash_time_left: float = 0.0


func bind(body: CharacterBody2D, asc: AbilitySystemComponent) -> void:
	_body = body
	_asc = asc


func set_direction(heading: Vector2) -> void:
	_direction = heading.limit_length(1.0)
	if _direction != Vector2.ZERO:
		last_direction = _direction.normalized()


func direction() -> Vector2:
	return _direction


func is_dashing() -> bool:
	return _dash_time_left > 0.0


func is_moving() -> bool:
	return _direction != Vector2.ZERO or is_dashing()


func dash(dash_direction: Vector2, speed: float, duration: float) -> void:
	var heading := dash_direction.normalized() if dash_direction != Vector2.ZERO else last_direction
	_dash_velocity = heading * speed
	_dash_time_left = duration
	dash_started.emit()


func step(delta: float) -> void:
	if _body == null:
		return
	if _dash_time_left > 0.0:
		_dash_time_left -= delta
		_body.velocity = _dash_velocity
		if _dash_time_left <= 0.0:
			dash_finished.emit()
	else:
		_body.velocity = _direction * _asc.get_attribute("move_speed").current_value
	_body.move_and_slide()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/components/lifetime.gd`:
```gdscript
class_name Lifetime
extends Node
## Counts down and announces expiry; the owner decides what to do about it.

signal expired

@export var seconds: float = 1.0

var _remaining: float = 0.0
var _has_expired: bool = false


func _ready() -> void:
	_remaining = seconds


func restart(new_seconds: float) -> void:
	seconds = new_seconds
	_remaining = new_seconds
	_has_expired = false


func step(delta: float) -> void:
	if _has_expired:
		return
	_remaining -= delta
	if _remaining <= 0.0:
		_has_expired = true
		expired.emit()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/components/actor_visual.gd`:
```gdscript
class_name ActorVisual
extends AnimatedSprite2D
## Plays an actor's animations. Builds SpriteFrames at runtime from a VisualDef's strips.

signal one_shot_finished(animation_name: StringName)

const HIT_FLASH_COLOR: Color = Color(1.0, 0.45, 0.45)
const HIT_FLASH_SECONDS: float = 0.15

var _base_tint: Color = Color.WHITE
var _moving: bool = false
var _one_shot_active: bool = false


func _ready() -> void:
	animation_finished.connect(_on_animation_finished)


func apply(def: VisualDef, scale_factor: float = 1.0, tint: Color = Color.WHITE) -> void:
	sprite_frames = _build_frames(def)
	position = def.offset
	scale = Vector2.ONE * scale_factor
	_base_tint = tint
	modulate = tint
	_play_locomotion()


func set_moving(moving: bool) -> void:
	_moving = moving
	if not _one_shot_active:
		_play_locomotion()


func face(direction_x: float) -> void:
	if absf(direction_x) > 0.01:
		flip_h = direction_x < 0.0


## Plays a non-looping animation, then returns to idle/walk (DEATH stays on its last frame).
func play_once(animation_name: StringName) -> void:
	if sprite_frames == null or not sprite_frames.has_animation(animation_name):
		return
	_one_shot_active = true
	play(animation_name)


func flash_hit() -> void:
	modulate = HIT_FLASH_COLOR
	create_tween().tween_property(self, "modulate", _base_tint, HIT_FLASH_SECONDS)


func _build_frames(def: VisualDef) -> SpriteFrames:
	var frames := SpriteFrames.new()
	frames.remove_animation(&"default")
	for strip in def.strips:
		frames.add_animation(strip.animation)
		frames.set_animation_loop(strip.animation, strip.loop)
		frames.set_animation_speed(strip.animation, strip.fps)
		var frame_width := strip.texture.get_width() / float(strip.frame_count)
		var frame_height := float(strip.texture.get_height())
		for i in strip.frame_count:
			var frame := AtlasTexture.new()
			frame.atlas = strip.texture
			frame.region = Rect2(i * frame_width, 0.0, frame_width, frame_height)
			frames.add_frame(strip.animation, frame)
	return frames


func _play_locomotion() -> void:
	if sprite_frames == null:
		return
	var wanted := AnimNames.WALK if _moving else AnimNames.IDLE
	if not sprite_frames.has_animation(wanted):
		wanted = AnimNames.IDLE
	if animation != wanted or not is_playing():
		play(wanted)


func _on_animation_finished() -> void:
	var finished := animation
	one_shot_finished.emit(finished)
	if finished == AnimNames.DEATH:
		return
	_one_shot_active = false
	_play_locomotion()
```

`src/components/contact_damage.gd`:
```gdscript
class_name ContactDamage
extends Node
## Periodically damages every hurtbox overlapping a hitbox (enemy melee, hazards).

signal attacked(targets: Array[Hurtbox])

var _hitbox: Hitbox
var _applier: EffectApplier
var _effect: GameplayEffect
var _damage: float = 0.0
var _interval: float = 1.0
var _cooldown: float = 0.0


func bind(hitbox: Hitbox, applier: EffectApplier, effect: GameplayEffect, damage: float, interval: float) -> void:
	_hitbox = hitbox
	_applier = applier
	_effect = effect
	_damage = damage
	_interval = interval


func step(delta: float) -> void:
	_cooldown = maxf(_cooldown - delta, 0.0)
	if _cooldown > 0.0 or _hitbox == null:
		return
	var targets := _hitbox.overlapping_hurtboxes()
	if targets.is_empty():
		return
	for target in targets:
		_applier.hit(target, _effect, _damage)
	_cooldown = _interval
	attacked.emit(targets)


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/components/loot_dropper.gd`:
```gdscript
class_name LootDropper
extends Node
## Rolls an actor's DropTable when it dies and asks the world to spawn the pickups.

signal drop_requested(roll: DropRoll, position: Vector2)

const SCATTER_RADIUS: float = 10.0

var _table: DropTable
var _rng: RunRng
var _anchor: Node2D


func bind(health: Health, table: DropTable, rng: RunRng, anchor: Node2D) -> void:
	_table = table
	_rng = rng
	_anchor = anchor
	health.died.connect(_on_died)


func _on_died() -> void:
	if _table == null:
		return
	for roll in _table.roll(_rng):
		drop_requested.emit(roll, _anchor.global_position + _rng.offset_in_circle(SCATTER_RADIUS))
```

`src/components/target_provider.gd`:
```gdscript
class_name TargetProvider
extends Node
## Answers targeting questions: who an enemy should chase, who an ability should shoot.

var building: Node2D
var player: Node2D


func target_for(policy: EnemyDef.TargetPolicy) -> Node2D:
	var preferred := player if policy == EnemyDef.TargetPolicy.PLAYER else building
	var fallback := building if policy == EnemyDef.TargetPolicy.PLAYER else player
	if is_instance_valid(preferred):
		return preferred
	if is_instance_valid(fallback):
		return fallback
	return null


func nearest_enemy(origin: Vector2, max_range: float) -> Node2D:
	var nearest: Node2D = null
	var nearest_distance := max_range * max_range
	for node in get_tree().get_nodes_in_group(Groups.ENEMIES):
		var enemy := node as Node2D
		if enemy == null or enemy.is_queued_for_deletion():
			continue
		var distance := origin.distance_squared_to(enemy.global_position)
		if distance <= nearest_distance:
			nearest = enemy
			nearest_distance = distance
	return nearest
```

- [ ] **Step 4: Run to verify pass**

Run: `FILTER=tests/components tools/run_tests.sh`
Expected: all `tests/components` PASS. Typical fixes: if `test_dash_overrides_velocity_then_finishes` reports velocity `(100, 0)`-style mismatches, `step` ordering is wrong (dash must win while `_dash_time_left > 0`); if `ActorVisual` tests report a null `sprite_frames`, `apply` ran before the node entered the tree — `adopt` first.

- [ ] **Step 5: Commit**

```bash
git add src/components tests/components
git commit -q -m "feat(components): add Mover, Lifetime, ActorVisual, ContactDamage, LootDropper, TargetProvider

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Art assets, Projectile and Pickup

**Files:**
- Create: `assets/SOURCES.md`, `assets/art/**` (copied PNGs)
- Create: `src/actors/projectile/projectile.gd`, `projectile.tscn`; `src/actors/pickup/pickup.gd`, `pickup.tscn`, `loot_collector.gd`
- Test: `tests/actors/test_projectile.gd`, `test_pickup.gd`

**Interfaces:**
- Consumes: `Hitbox`, `Hurtbox`, `EffectApplier`, `Lifetime` (Tasks 5–6); `ProjectileDef`, `DropRoll` (Task 3).
- Produces:
  - Art files (all under `res://assets/art/`): `tinyrpg/soldier/Soldier_{Idle,Walk,Attack01,Hurt,Death}.png` (frames: 6, 8, 6, 4, 4; 100x100 each), `tinyrpg/orc/Orc_{Idle,Walk,Attack01,Hurt,Death}.png` (same frame counts), `tinyrpg/arrow.png` (32x32), `farmrpg/house.png` (224x112), `world/grass.png` (32x32 tile), `items/coin.png`, `items/stone.png`.
  - `Projectile` (Node2D scene `res://src/actors/projectile/projectile.tscn`): `launch(def: ProjectileDef, shooter_asc: AbilitySystemComponent, damage: float, direction: Vector2)`; call it after adding the node to the tree.
  - `Pickup` (Area2D scene `res://src/actors/pickup/pickup.tscn`): `signal reached(pickup: Pickup)`; `kind: Wallet.Kind`, `amount: int`; `setup(roll: DropRoll)`; `attract_to(target: Node2D)`; `step(delta)`.
  - `LootCollector` (Area2D): `signal collected(kind: Wallet.Kind, amount: int)`; `bind(anchor: Node2D)`.

- [ ] **Step 1: Copy the art into the project and record provenance**

```bash
RES=/d/CM/Godot/res
DEST=assets/art
mkdir -p $DEST/tinyrpg/soldier $DEST/tinyrpg/orc $DEST/farmrpg $DEST/world $DEST/items
SOLDIER="$RES/TinyRPG/Characters(100x100 split)/Soldier/Soldier"
ORC="$RES/TinyRPG/Characters(100x100 split)/Orc/Orc"
for n in Idle Walk Attack01 Hurt Death; do
	cp "$SOLDIER/Soldier_$n.png" $DEST/tinyrpg/soldier/
	cp "$ORC/Orc_$n.png" $DEST/tinyrpg/orc/
done
cp "$RES/TinyRPG/Arrow(Projectile)/Arrow01(32x32).png" $DEST/tinyrpg/arrow.png
cp "$RES/FarmRPG/Objects/House.png" $DEST/farmrpg/house.png
cp "$RES/opengameart/2D/32x32-grass-tile/grass_tile_0.png" $DEST/world/grass.png
cp "$RES/opengameart/2D/rotating-coin/Coins_0/coin_01.png" $DEST/items/coin.png
cp "$RES/kenney/2D/abstract-platformer/PNG/Items/blueCrystal.png" $DEST/items/stone.png
find $DEST -name "*.png" | wc -l
```
Expected: `18`. Then wait for the editor to import them (or run `IMPORT=1 tools/run_tests.sh` with the editor closed) and confirm each PNG has a sibling `.import` file: `find assets -name "*.png.import" | wc -l` → `18`.

`assets/SOURCES.md`:
```markdown
# Asset sources

Copied from the local library `D:\CM\Godot\res`. Replace freely; only `data/` references these files.

| Path | Origin | License note |
|---|---|---|
| `art/tinyrpg/**` | Tiny RPG Character Asset Pack (Soldier, Orc, Arrow) | no license file shipped with the pack — verify before any release |
| `art/farmrpg/house.png` | Farm RPG FREE 16x16 Tiny Asset Pack | no license file shipped with the pack — verify before any release |
| `art/world/grass.png` | OpenGameArt "32x32 grass tile" | CC0 (per `_source.txt`) |
| `art/items/coin.png` | OpenGameArt "Rotating coin" | see its `_source.txt` |
| `art/items/stone.png` | Kenney Abstract Platformer (`blueCrystal`) | CC0 (Kenney standard license) |
```

- [ ] **Step 2: Write the failing tests**

`tests/actors/test_projectile.gd`:
```gdscript
extends TestCase

const PROJECTILE_SCENE: PackedScene = preload("res://src/actors/projectile/projectile.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _def(pierce: int = 0, lifetime: float = 1.0) -> ProjectileDef:
	var def := ProjectileDef.new()
	def.texture = DefFactory.texture(32, 32)
	def.hit_effect = DAMAGE
	def.speed = 50.0
	def.lifetime = lifetime
	def.pierce = pierce
	def.hit_radius = 8.0
	return def


func _actor(max_health: float = 100.0) -> Node2D:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return ActorFixture.make(self, stats)


func _victim() -> Node2D:
	var victim := _actor()
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = PhysicsLayers.ENEMY_HURT
	hurtbox.collision_mask = 0
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 12.0
	shape.shape = circle
	hurtbox.add_child(shape)
	victim.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(victim), victim)
	return victim


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func _fire(shooter: Node2D, def: ProjectileDef) -> Projectile:
	var projectile := adopt(PROJECTILE_SCENE.instantiate()) as Projectile
	projectile.launch(def, ActorFixture.asc_of(shooter), 10.0, Vector2.RIGHT)
	return projectile


func test_projectile_damages_a_victim_and_is_consumed() -> void:
	var shooter := _actor()
	var victim := _victim()
	var projectile := _fire(shooter, _def())
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(victim), 90.0)
	assert_true(projectile.is_queued_for_deletion())


func test_projectile_without_pierce_hits_only_one_of_two_overlapping_victims() -> void:
	var shooter := _actor()
	var first := _victim()
	var second := _victim()
	_fire(shooter, _def(0))
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	var damaged := 0
	for victim in [first, second]:
		if _health(victim) < 100.0:
			damaged += 1
	assert_eq(damaged, 1)


func test_projectile_with_pierce_hits_both() -> void:
	var shooter := _actor()
	var first := _victim()
	var second := _victim()
	_fire(shooter, _def(1))
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(first), 90.0)
	assert_almost_eq(_health(second), 90.0)


func test_projectile_survives_freed_shooter() -> void:
	var shooter := _actor()
	var victim := _victim()
	var projectile := _fire(shooter, _def())
	ActorFixture.asc_of(shooter).free()
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(victim), 100.0, 0.001, "no damage without a living source")
	assert_true(projectile.is_queued_for_deletion(), "still consumed")


func test_projectile_expires_after_its_lifetime() -> void:
	var shooter := _actor()
	var projectile := _fire(shooter, _def(0, 0.1))
	await wait_seconds(0.4)
	assert_true(projectile.is_queued_for_deletion())


func test_projectile_flies_along_its_direction() -> void:
	var shooter := _actor()
	var projectile := _fire(shooter, _def())
	var start := projectile.global_position
	await wait_seconds(0.2)
	assert_true(projectile.global_position.x > start.x + 3.0)
	assert_almost_eq(projectile.global_position.y, start.y, 0.01)
```

`tests/actors/test_pickup.gd`:
```gdscript
extends TestCase

const PICKUP_SCENE: PackedScene = preload("res://src/actors/pickup/pickup.tscn")


func _roll(kind: Wallet.Kind, amount: int) -> DropRoll:
	var roll := DropRoll.new()
	roll.kind = kind
	roll.amount = amount
	roll.icon = DefFactory.texture(32, 32)
	return roll


func _pickup(position: Vector2) -> Pickup:
	var pickup := adopt(PICKUP_SCENE.instantiate()) as Pickup
	pickup.global_position = position
	pickup.setup(_roll(Wallet.Kind.STONE, 4))
	return pickup


func test_setup_copies_kind_amount_and_fits_the_icon() -> void:
	var pickup := _pickup(Vector2.ZERO)
	assert_eq(pickup.kind, Wallet.Kind.STONE)
	assert_eq(pickup.amount, 4)
	assert_almost_eq((pickup.get_node("Sprite2D") as Sprite2D).scale.x, 0.5)


func test_pickup_without_target_stays_put() -> void:
	var pickup := _pickup(Vector2(30, 0))
	pickup.step(1.0)
	assert_eq(pickup.global_position, Vector2(30, 0))


func test_attracted_pickup_flies_to_the_target_and_reports_once() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var pickup := _pickup(Vector2(100, 0))
	var reached: Array = []
	pickup.reached.connect(func(found: Pickup) -> void: reached.append(found))
	pickup.attract_to(target)
	for i in 40:
		pickup.step(0.05)
	assert_eq(reached, [pickup])
	assert_true(pickup.is_queued_for_deletion())


func test_collector_attracts_and_reports_collected_amounts() -> void:
	var anchor := adopt(Node2D.new()) as Node2D
	var collector := LootCollector.new()
	collector.collision_layer = 0
	collector.collision_mask = PhysicsLayers.PICKUP
	collector.monitorable = false
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 60.0
	shape.shape = circle
	collector.add_child(shape)
	anchor.add_child(collector)
	collector.bind(anchor)
	var collected: Array = []
	collector.collected.connect(func(kind: Wallet.Kind, amount: int) -> void: collected.append([kind, amount]))
	_pickup(Vector2(30, 0))
	await wait_seconds(0.6)
	assert_eq(collected, [[Wallet.Kind.STONE, 4]])
```

- [ ] **Step 3: Run to verify failure**

Run: `FILTER=tests/actors tools/run_tests.sh`
Expected: errors — `Projectile`, `Pickup`, `LootCollector` and their scenes do not exist.

- [ ] **Step 4: Implement Projectile**

`src/actors/projectile/projectile.gd`:
```gdscript
class_name Projectile
extends Node2D
## A straight-flying projectile that applies its def's effect to hurtboxes it touches.

var _def: ProjectileDef
var _damage: float = 0.0
var _velocity: Vector2 = Vector2.ZERO
var _pierce_left: int = 0
var _spent: bool = false

@onready var _sprite: Sprite2D = $Sprite2D
@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the projectile has entered the tree.
func launch(def: ProjectileDef, shooter_asc: AbilitySystemComponent, damage: float, direction: Vector2) -> void:
	_def = def
	_damage = damage
	_pierce_left = def.pierce
	_velocity = direction.normalized() * def.speed
	rotation = direction.angle()
	_sprite.texture = def.texture
	(_hit_shape.shape as CircleShape2D).radius = def.hit_radius
	_applier.bind(shooter_asc)
	_lifetime.restart(def.lifetime)


func _physics_process(delta: float) -> void:
	global_position += _velocity * delta


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	if _spent:
		return
	_applier.hit(hurtbox, _def.hit_effect, _damage, self)
	if _pierce_left > 0:
		_pierce_left -= 1
		return
	_spent = true
	queue_free()
```

`src/actors/projectile/projectile.tscn`:
```
[gd_scene load_steps=6 format=3]

[ext_resource type="Script" path="res://src/actors/projectile/projectile.gd" id="1_projectile"]
[ext_resource type="Script" path="res://src/components/hitbox.gd" id="2_hitbox"]
[ext_resource type="Script" path="res://src/components/effect_applier.gd" id="3_applier"]
[ext_resource type="Script" path="res://src/components/lifetime.gd" id="4_lifetime"]

[sub_resource type="CircleShape2D" id="CircleShape2D_hit"]
resource_local_to_scene = true
radius = 6.0

[node name="Projectile" type="Node2D"]
script = ExtResource("1_projectile")

[node name="Sprite2D" type="Sprite2D" parent="."]

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

- [ ] **Step 5: Implement Pickup and LootCollector**

`src/actors/pickup/pickup.gd`:
```gdscript
class_name Pickup
extends Area2D
## A currency drop. Sits still until a collector attracts it, then flies to the target and is consumed.

signal reached(pickup: Pickup)

const ICON_SIZE: float = 16.0

@export var attract_speed: float = 260.0
@export var reach_distance: float = 8.0

var kind: Wallet.Kind = Wallet.Kind.GOLD
var amount: int = 0
var _target: Node2D

@onready var _sprite: Sprite2D = $Sprite2D


## Call after the pickup has entered the tree.
func setup(roll: DropRoll) -> void:
	kind = roll.kind
	amount = roll.amount
	_sprite.texture = roll.icon
	if roll.icon != null:
		var largest := maxf(roll.icon.get_width(), roll.icon.get_height())
		_sprite.scale = Vector2.ONE * (ICON_SIZE / largest)


func attract_to(target: Node2D) -> void:
	_target = target


func step(delta: float) -> void:
	if not is_instance_valid(_target):
		return
	var offset := _target.global_position - global_position
	var travel := attract_speed * delta
	if offset.length() <= reach_distance + travel:
		_target = null
		reached.emit(self)
		queue_free()
		return
	global_position += offset.normalized() * travel


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/actors/pickup/pickup.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/actors/pickup/pickup.gd" id="1_pickup"]

[sub_resource type="CircleShape2D" id="CircleShape2D_pickup"]
radius = 6.0

[node name="Pickup" type="Area2D"]
collision_layer = 32
collision_mask = 0
monitoring = false
script = ExtResource("1_pickup")

[node name="Sprite2D" type="Sprite2D" parent="."]

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_pickup")
```

`src/actors/pickup/loot_collector.gd`:
```gdscript
class_name LootCollector
extends Area2D
## Magnet area. Pickups entering it are attracted to `anchor`; `collected` fires when they arrive.
## Configure in the scene: collision_layer = 0, collision_mask = PICKUP, monitorable = false.

signal collected(kind: Wallet.Kind, amount: int)

var _anchor: Node2D


func bind(anchor: Node2D) -> void:
	_anchor = anchor
	area_entered.connect(_on_area_entered)


func _on_area_entered(area: Area2D) -> void:
	var pickup := area as Pickup
	if pickup == null:
		return
	pickup.reached.connect(_on_pickup_reached, CONNECT_ONE_SHOT)
	pickup.attract_to(_anchor)


func _on_pickup_reached(pickup: Pickup) -> void:
	collected.emit(pickup.kind, pickup.amount)
```

- [ ] **Step 6: Run to verify pass**

Run: `FILTER=tests/actors tools/run_tests.sh`
Expected: all PASS. If the projectile tests never register a hit, the projectile spawned before the first physics step finished registering the victim's area — keep the three `next_physics_frame()` awaits (the engine needs one step to register new areas and one to report overlaps).

- [ ] **Step 7: Commit**

```bash
git add assets src/actors tests/actors
git commit -q -m "feat(actors): add art assets, Projectile, Pickup and LootCollector

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Building and Enemy actors

**Files:**
- Create: `src/actors/building/building.gd`, `building.tscn`; `src/actors/enemy/enemy.gd`, `enemy.tscn`
- Test: `tests/actors/test_building.gd`, `tests/actors/test_enemy.gd`

**Interfaces:**
- Consumes: all components (Tasks 5–6), `actor_asc.tscn`, `EnemyDef`, `StatBlock`, `RunRng`, `Groups`, `AnimNames`.
- Produces:
  - `Building` (StaticBody2D scene `res://src/actors/building/building.tscn`): `signal destroyed`; public `asc: AbilitySystemComponent`, `health: Health`; `setup(texture: Texture2D, stats: StatBlock)` (call after the node is in the tree). Joins group `Groups.BUILDING`.
  - `Enemy` (CharacterBody2D scene `res://src/actors/enemy/enemy.tscn`): `signal died(enemy: Enemy)`, `signal loot_dropped(roll: DropRoll, position: Vector2)`; `def: EnemyDef`; `setup(p_def: EnemyDef, target: Node2D, rng: RunRng)` (call after the node is in the tree). Joins `Groups.ENEMIES` in `setup`, leaves it on death, frees itself 0.6 s after death.

- [ ] **Step 1: Write the failing tests**

`tests/actors/test_building.gd`:
```gdscript
extends TestCase

const BUILDING_SCENE: PackedScene = preload("res://src/actors/building/building.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _building(max_health: float) -> Building:
	var stats := StatBlock.new()
	stats.max_health = max_health
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	return building


func _hit(building: Building, amount: float) -> void:
	var stats := StatBlock.new()
	var attacker := ActorFixture.make(self, stats)
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, building.asc)


func test_setup_applies_stats_and_joins_the_building_group() -> void:
	var building := _building(300.0)
	assert_almost_eq(building.health.current(), 300.0)
	assert_true(building.is_in_group(Groups.BUILDING))


func test_hurtbox_sits_on_the_ally_hurt_layer() -> void:
	var building := _building(300.0)
	var hurtbox := building.get_node("Hurtbox") as Hurtbox
	assert_eq(hurtbox.collision_layer, PhysicsLayers.ALLY_HURT)
	assert_eq(hurtbox.asc, building.asc)


func test_lethal_damage_emits_destroyed_once() -> void:
	var building := _building(100.0)
	var destroyed: Array = []
	building.destroyed.connect(func() -> void: destroyed.append(true))
	_hit(building, 60.0)
	assert_eq(destroyed.size(), 0)
	_hit(building, 60.0)
	_hit(building, 60.0)
	assert_eq(destroyed.size(), 1)
```

`tests/actors/test_enemy.gd`:
```gdscript
extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BUILDING_SCENE: PackedScene = preload("res://src/actors/building/building.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _def(max_health: float = 30.0, with_drop: bool = false) -> EnemyDef:
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.stats.max_health = max_health
	def.stats.move_speed = 120.0
	def.contact_effect = DAMAGE
	def.contact_damage = 10.0
	if with_drop:
		var entry := DropEntry.new()
		entry.chance = 1.0
		entry.icon = DefFactory.texture(8, 8)
		def.drops = DropTable.new()
		def.drops.entries.append(entry)
	return def


func _enemy(def: EnemyDef, target: Node2D, at: Vector2 = Vector2.ZERO) -> Enemy:
	var enemy := adopt(ENEMY_SCENE.instantiate()) as Enemy
	enemy.global_position = at
	enemy.setup(def, target, RunRng.new(5))
	return enemy


func _kill(enemy: Enemy) -> void:
	var stats := StatBlock.new()
	var killer := ActorFixture.make(self, stats)
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 9999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, enemy.get_node("AbilitySystemComponent"))


func test_setup_applies_stats_and_joins_the_enemy_group() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(45.0), target)
	assert_almost_eq((enemy.get_node("Health") as Health).current(), 45.0)
	assert_true(enemy.is_in_group(Groups.ENEMIES))


func test_enemy_walks_toward_its_target() -> void:
	var target := adopt(Node2D.new()) as Node2D
	target.global_position = Vector2(300, 0)
	var enemy := _enemy(_def(), target)
	await wait_seconds(0.3)
	assert_true(enemy.global_position.x > 5.0, "moved toward +x")
	assert_almost_eq(enemy.global_position.y, 0.0, 0.5)


func test_enemy_without_a_valid_target_stands_still() -> void:
	var target := Node2D.new()
	var enemy := _enemy(_def(), target)
	target.free()
	await wait_seconds(0.2)
	assert_almost_eq(enemy.global_position.length(), 0.0, 0.5)


func test_enemy_damages_an_overlapping_ally_hurtbox() -> void:
	var stats := StatBlock.new()
	stats.max_health = 200.0
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	_enemy(_def(), building)
	await wait_seconds(0.4)
	assert_true(building.health.current() < 200.0)


func test_death_emits_signals_leaves_the_group_and_frees_itself() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(30.0, true), target, Vector2(10, 20))
	var deaths: Array = []
	var drops: Array = []
	enemy.died.connect(func(found: Enemy) -> void: deaths.append(found))
	enemy.loot_dropped.connect(func(roll: DropRoll, position: Vector2) -> void: drops.append([roll, position]))
	_kill(enemy)
	assert_eq(deaths, [enemy])
	assert_eq(drops.size(), 1)
	assert_false(enemy.is_in_group(Groups.ENEMIES))
	await wait_seconds(0.9)
	assert_false(is_instance_valid(enemy), "freed after the death delay")


func test_dead_enemy_is_not_targetable() -> void:
	var provider := adopt(TargetProvider.new()) as TargetProvider
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(), target, Vector2(40, 0))
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 200.0), enemy)
	_kill(enemy)
	assert_null(provider.nearest_enemy(Vector2.ZERO, 200.0))


func test_dead_enemy_stops_dealing_damage() -> void:
	var stats := StatBlock.new()
	stats.max_health = 200.0
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	var enemy := _enemy(_def(), building)
	_kill(enemy)
	await wait_seconds(0.5)
	assert_almost_eq(building.health.current(), 200.0)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/actors/test_b tools/run_tests.sh` and `FILTER=tests/actors/test_e tools/run_tests.sh`
Expected: errors — `Building`/`Enemy` scenes missing.

- [ ] **Step 3: Implement Building**

`src/actors/building/building.gd`:
```gdscript
class_name Building
extends StaticBody2D
## The structure the player defends. Emits `destroyed` when its health reaches zero.

signal destroyed

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _sprite: Sprite2D = $Sprite2D


func _ready() -> void:
	add_to_group(Groups.BUILDING)


## Call after the building has entered the tree.
func setup(texture: Texture2D, stats: StatBlock) -> void:
	_sprite.texture = texture
	asc.initialize_attribute_overrides(stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	health.died.connect(destroyed.emit)
```

`src/actors/building/building.tscn`:
```
[gd_scene load_steps=8 format=3]

[ext_resource type="Script" path="res://src/actors/building/building.gd" id="1_building"]
[ext_resource type="PackedScene" path="res://src/gas/actor_asc.tscn" id="2_asc"]
[ext_resource type="Script" path="res://src/components/health.gd" id="3_health"]
[ext_resource type="Script" path="res://src/components/hurtbox.gd" id="4_hurtbox"]

[sub_resource type="RectangleShape2D" id="RectangleShape2D_body"]
size = Vector2(190, 60)

[sub_resource type="RectangleShape2D" id="RectangleShape2D_hurt"]
size = Vector2(214, 90)

[node name="Building" type="StaticBody2D"]
collision_layer = 1
collision_mask = 0
script = ExtResource("1_building")

[node name="Sprite2D" type="Sprite2D" parent="."]

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
position = Vector2(0, 20)
shape = SubResource("RectangleShape2D_body")

[node name="AbilitySystemComponent" parent="." instance=ExtResource("2_asc")]

[node name="Health" type="Node" parent="."]
script = ExtResource("3_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 8
collision_mask = 0
monitoring = false
script = ExtResource("4_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
position = Vector2(0, 14)
shape = SubResource("RectangleShape2D_hurt")
```

- [ ] **Step 4: Implement Enemy**

`src/actors/enemy/enemy.gd`:
```gdscript
class_name Enemy
extends CharacterBody2D
## Generic enemy configured by an EnemyDef: chases a target and damages allies it touches.

signal died(enemy: Enemy)
signal loot_dropped(roll: DropRoll, position: Vector2)

const FREE_DELAY_AFTER_DEATH: float = 0.6
const HURTBOX_PADDING: float = 2.0
const CONTACT_PADDING: float = 4.0

var def: EnemyDef
var _target: Node2D

@onready var _asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var _health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _contact_hitbox: Hitbox = $ContactHitbox
@onready var _applier: EffectApplier = $EffectApplier
@onready var _contact: ContactDamage = $ContactDamage
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _dropper: LootDropper = $LootDropper
@onready var _body_shape: CollisionShape2D = $CollisionShape2D
@onready var _hurt_shape: CollisionShape2D = $Hurtbox/CollisionShape2D
@onready var _contact_shape: CollisionShape2D = $ContactHitbox/CollisionShape2D


## Call after the enemy has entered the tree.
func setup(p_def: EnemyDef, target: Node2D, rng: RunRng) -> void:
	def = p_def
	_target = target
	_asc.initialize_attribute_overrides(def.stats.to_overrides())
	_health.bind(_asc)
	_hurtbox.bind(_asc, self)
	_applier.bind(_asc)
	_mover.bind(self, _asc)
	_contact.bind(_contact_hitbox, _applier, def.contact_effect, def.contact_damage, def.contact_interval)
	_dropper.bind(_health, def.drops, rng, self)
	_visual.apply(def.visual, def.visual_scale, def.tint)
	(_body_shape.shape as CircleShape2D).radius = def.body_radius
	(_hurt_shape.shape as CircleShape2D).radius = def.body_radius + HURTBOX_PADDING
	(_contact_shape.shape as CircleShape2D).radius = def.body_radius + CONTACT_PADDING
	_health.damaged.connect(_on_damaged)
	_health.died.connect(_on_died)
	_dropper.drop_requested.connect(loot_dropped.emit)
	_contact.attacked.connect(_on_attacked)
	add_to_group(Groups.ENEMIES)


func _physics_process(_delta: float) -> void:
	if not is_instance_valid(_target):
		_mover.set_direction(Vector2.ZERO)
		_visual.set_moving(false)
		return
	var to_target := _target.global_position - global_position
	_mover.set_direction(to_target.normalized())
	_visual.face(to_target.x)
	_visual.set_moving(true)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_attacked(_targets: Array[Hurtbox]) -> void:
	_visual.play_once(AnimNames.ATTACK)


func _on_died() -> void:
	remove_from_group(Groups.ENEMIES)
	set_physics_process(false)
	_mover.set_physics_process(false)
	_contact.set_physics_process(false)
	_hurtbox.set_deferred("monitorable", false)
	_contact_hitbox.set_deferred("monitoring", false)
	_body_shape.set_deferred("disabled", true)
	_asc.cleanup.call_deferred()
	_visual.play_once(AnimNames.DEATH)
	died.emit(self)
	get_tree().create_timer(FREE_DELAY_AFTER_DEATH).timeout.connect(queue_free)
```

`src/actors/enemy/enemy.tscn`:
```
[gd_scene load_steps=14 format=3]

[ext_resource type="Script" path="res://src/actors/enemy/enemy.gd" id="1_enemy"]
[ext_resource type="PackedScene" path="res://src/gas/actor_asc.tscn" id="2_asc"]
[ext_resource type="Script" path="res://src/components/health.gd" id="3_health"]
[ext_resource type="Script" path="res://src/components/hurtbox.gd" id="4_hurtbox"]
[ext_resource type="Script" path="res://src/components/hitbox.gd" id="5_hitbox"]
[ext_resource type="Script" path="res://src/components/effect_applier.gd" id="6_applier"]
[ext_resource type="Script" path="res://src/components/contact_damage.gd" id="7_contact"]
[ext_resource type="Script" path="res://src/components/mover.gd" id="8_mover"]
[ext_resource type="Script" path="res://src/components/actor_visual.gd" id="9_visual"]
[ext_resource type="Script" path="res://src/components/loot_dropper.gd" id="10_dropper"]

[sub_resource type="CircleShape2D" id="CircleShape2D_body"]
resource_local_to_scene = true
radius = 10.0

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
resource_local_to_scene = true
radius = 12.0

[sub_resource type="CircleShape2D" id="CircleShape2D_contact"]
resource_local_to_scene = true
radius = 14.0

[node name="Enemy" type="CharacterBody2D"]
collision_layer = 4
collision_mask = 1
motion_mode = 1
script = ExtResource("1_enemy")

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

[node name="LootDropper" type="Node" parent="."]
script = ExtResource("10_dropper")
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/actors tools/run_tests.sh`
Expected: all `tests/actors` PASS. If `test_enemy_damages_an_overlapping_ally_hurtbox` fails, check the enemy's `ContactHitbox.collision_mask = 8` (ALLY_HURT) and the building's `Hurtbox.collision_layer = 8`.

- [ ] **Step 6: Commit**

```bash
git add src/actors tests/actors
git commit -q -m "feat(actors): add Building and Enemy

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Player with arrow and dash abilities

**Files:**
- Create: `src/actors/player/arrow_ability.gd`, `dash_ability.gd`, `player.gd`, `player.tscn`; `data/projectiles/arrow.tres`
- Test: `tests/actors/test_player.gd`

**Interfaces:**
- Consumes: Tasks 3–8 (`PlayerProfile`, `Projectile`, `LootCollector`, `TargetProvider`, `Mover`, `ActorVisual`, `Health`, `InputActions`, effects `cooldown_arrow.tres`/`cooldown_dash.tres`, tags `Ability_*`/`Cooldown_*`/`State_Invulnerable`).
- Produces:
  - `ArrowAbility` (GameplayAbility): `@export projectile_scene, projectile_def, base_damage := 12.0, attack_range := 260.0`; `signal fired(direction: Vector2)`; `bind(actor: Node2D, targets: TargetProvider, projectile_layer: Node)`. Fails without starting a cooldown when no enemy is in range.
  - `DashAbility` (GameplayAbility): `@export dash_speed := 480.0, dash_duration := 0.18`; `bind(mover: Mover)`. Grants `State.Invulnerable` while active (set in the scene via `activation_owned_tags`).
  - `Player` (CharacterBody2D scene `res://src/actors/player/player.tscn`): `signal died`, `signal loot_collected(kind: Wallet.Kind, amount: int)`; public `health: Health`; `setup(profile: PlayerProfile, targets: TargetProvider, projectile_layer: Node)` (after entering the tree); `limit_camera(bounds: Rect2)`; `gain_multiplier(kind: Wallet.Kind) -> float`. Joins `Groups.PLAYER` in `setup`.
  - `data/projectiles/arrow.tres`: ProjectileDef (arrow.png, `damage_basic` hit effect, speed 420, lifetime 0.7, pierce 0).

- [ ] **Step 1: Write the failing tests**

`tests/actors/test_player.gd`:
```gdscript
extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _profile(max_health: float = 100.0, move_speed: float = 140.0) -> PlayerProfile:
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.stats.max_health = max_health
	profile.stats.move_speed = move_speed
	profile.visual = DefFactory.visual()
	return profile


func _player() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(_profile(), targets, layer)
	targets.player = player
	return [player, layer, targets]


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _asc(player: Player) -> AbilitySystemComponent:
	return player.get_node("AbilitySystemComponent") as AbilitySystemComponent


func _hit_player(player: Player, amount: float) -> ActiveGameplayEffect:
	var attacker := ActorFixture.make(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	return ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, _asc(player))


func test_setup_applies_profile_stats_and_joins_group() -> void:
	var player: Player = _player()[0]
	assert_almost_eq(player.health.current(), 100.0)
	assert_almost_eq(_asc(player).get_attribute("move_speed").current_value, 140.0)
	assert_true(player.is_in_group(Groups.PLAYER))
	assert_almost_eq(player.gain_multiplier(Wallet.Kind.GOLD), 1.0)


func test_arrow_without_target_does_not_start_cooldown() -> void:
	var rig := _player()
	var player: Player = rig[0]
	var arrow := _asc(player).get_node("ArrowAbility") as ArrowAbility
	for i in 3:
		var fired: bool = await arrow.try_activate()
		assert_false(fired)
	assert_false(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	assert_eq((rig[1] as Node2D).get_child_count(), 0)


func test_arrow_fires_at_the_nearest_enemy_then_cools_down() -> void:
	var rig := _player()
	var player: Player = rig[0]
	_enemy_at(Vector2(400, 0))
	_enemy_at(Vector2(120, 0))
	var arrow := _asc(player).get_node("ArrowAbility") as ArrowAbility
	var directions: Array = []
	arrow.fired.connect(func(direction: Vector2) -> void: directions.append(direction))
	var first: bool = await arrow.try_activate()
	assert_true(first)
	assert_eq((rig[1] as Node2D).get_child_count(), 1, "one projectile spawned")
	assert_almost_eq((directions[0] as Vector2).x, 1.0, 0.001)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	var second: bool = await arrow.try_activate()
	assert_false(second, "blocked by cooldown")


func test_arrow_ignores_enemies_beyond_its_range() -> void:
	var rig := _player()
	_enemy_at(Vector2(900, 0))
	var arrow := _asc(rig[0] as Player).get_node("ArrowAbility") as ArrowAbility
	var fired: bool = await arrow.try_activate()
	assert_false(fired)


func test_dash_grants_invulnerability_only_while_active() -> void:
	var player: Player = _player()[0]
	var dash := _asc(player).get_node("DashAbility") as DashAbility
	dash.try_activate()
	await next_frame()
	assert_true(_asc(player).has_tag(GameplayTags.State_Invulnerable))
	assert_null(_hit_player(player, 10.0), "damage refused while dashing")
	assert_almost_eq(player.health.current(), 100.0)
	await wait_seconds(0.5)
	assert_false(_asc(player).has_tag(GameplayTags.State_Invulnerable))
	assert_not_null(_hit_player(player, 10.0))
	assert_almost_eq(player.health.current(), 90.0)


func test_dash_starts_a_cooldown_that_blocks_a_second_dash() -> void:
	var player: Player = _player()[0]
	var dash := _asc(player).get_node("DashAbility") as DashAbility
	var first: bool = await dash.try_activate()
	assert_true(first)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Dash))
	var second: bool = await dash.try_activate()
	assert_false(second)


func test_lethal_damage_emits_died() -> void:
	var player: Player = _player()[0]
	var deaths: Array = []
	player.died.connect(func() -> void: deaths.append(true))
	_hit_player(player, 9999.0)
	assert_eq(deaths.size(), 1)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/actors/test_player tools/run_tests.sh`
Expected: errors — `Player`, `ArrowAbility`, `DashAbility` undefined.

- [ ] **Step 3: Implement abilities**

`src/actors/player/arrow_ability.gd`:
```gdscript
class_name ArrowAbility
extends GameplayAbility
## Auto-fires a short-range arrow at the nearest enemy. Fails without a cooldown when nothing is in range.

signal fired(direction: Vector2)

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var base_damage: float = 12.0
@export var attack_range: float = 260.0

var _actor: Node2D
var _targets: TargetProvider
var _projectile_layer: Node


func bind(actor: Node2D, targets: TargetProvider, projectile_layer: Node) -> void:
	_actor = actor
	_targets = targets
	_projectile_layer = projectile_layer


func _activate_ability() -> bool:
	var origin := _actor.global_position
	var target := _targets.nearest_enemy(origin, attack_range)
	if target == null:
		return false
	commit_ability()
	var direction := (target.global_position - origin).normalized()
	var projectile := projectile_scene.instantiate() as Projectile
	_projectile_layer.add_child(projectile)
	projectile.global_position = origin
	projectile.launch(projectile_def, owner_asc, base_damage * ability_level, direction)
	fired.emit(direction)
	return true
```

`src/actors/player/dash_ability.gd`:
```gdscript
class_name DashAbility
extends GameplayAbility
## A short burst of speed. Invulnerability comes from `activation_owned_tags` set in the scene.

@export var dash_speed: float = 480.0
@export var dash_duration: float = 0.18

var _mover: Mover


func bind(mover: Mover) -> void:
	_mover = mover


func _activate_ability() -> bool:
	commit_ability()
	_mover.dash(_mover.direction(), dash_speed, dash_duration)
	await task_wait_delay(dash_duration)
	return true
```

`data/projectiles/arrow.tres`:
```
[gd_resource type="Resource" script_class="ProjectileDef" load_steps=4 format=3]

[ext_resource type="Script" path="res://src/defs/projectile_def.gd" id="1_def"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/arrow.png" id="2_tex"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="3_damage"]

[resource]
script = ExtResource("1_def")
texture = ExtResource("2_tex")
hit_effect = ExtResource("3_damage")
speed = 420.0
lifetime = 0.7
pierce = 0
hit_radius = 6.0
```

- [ ] **Step 4: Implement Player**

`src/actors/player/player.gd`:
```gdscript
class_name Player
extends CharacterBody2D
## The player's actor: input -> Mover, auto-fire arrow, dash, loot magnet, camera.

signal died
signal loot_collected(kind: Wallet.Kind, amount: int)

@onready var health: Health = $Health
@onready var _asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var _arrow: ArrowAbility = $AbilitySystemComponent/ArrowAbility
@onready var _dash: DashAbility = $AbilitySystemComponent/DashAbility
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _collector: LootCollector = $Collector
@onready var _camera: Camera2D = $Camera2D


## Call after the player has entered the tree.
func setup(profile: PlayerProfile, targets: TargetProvider, projectile_layer: Node) -> void:
	_asc.initialize_attribute_overrides(profile.stats.to_overrides())
	health.bind(_asc)
	_hurtbox.bind(_asc, self)
	_mover.bind(self, _asc)
	_visual.apply(profile.visual)
	_arrow.bind(self, targets, projectile_layer)
	_dash.bind(_mover)
	_collector.bind(self)
	_arrow.fired.connect(_on_arrow_fired)
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
	return _asc.get_attribute(attribute).current_value


func _physics_process(_delta: float) -> void:
	if health.is_dead:
		_mover.set_direction(Vector2.ZERO)
		return
	var input := InputActions.move_vector()
	_mover.set_direction(input)
	_visual.set_moving(_mover.is_moving())
	_visual.face(input.x)
	if Input.is_action_just_pressed(InputActions.DASH):
		_dash.try_activate()
	if not _arrow.is_active and _arrow.check_cooldown():
		_arrow.try_activate()


func _on_arrow_fired(direction: Vector2) -> void:
	_visual.face(direction.x)
	_visual.play_once(AnimNames.ATTACK)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	_visual.play_once(AnimNames.DEATH)
	_asc.cleanup.call_deferred()
	died.emit()
```

`src/actors/player/player.tscn`:
```
[gd_scene load_steps=16 format=3]

[ext_resource type="Script" path="res://src/actors/player/player.gd" id="1_player"]
[ext_resource type="PackedScene" path="res://src/gas/actor_asc.tscn" id="2_asc"]
[ext_resource type="Script" path="res://src/actors/player/arrow_ability.gd" id="3_arrow"]
[ext_resource type="Script" path="res://src/actors/player/dash_ability.gd" id="4_dash"]
[ext_resource type="Resource" path="res://data/effects/cooldown_arrow.tres" id="5_cd_arrow"]
[ext_resource type="Resource" path="res://data/effects/cooldown_dash.tres" id="6_cd_dash"]
[ext_resource type="PackedScene" path="res://src/actors/projectile/projectile.tscn" id="7_projectile"]
[ext_resource type="Resource" path="res://data/projectiles/arrow.tres" id="8_arrow_def"]
[ext_resource type="Script" path="res://src/components/health.gd" id="9_health"]
[ext_resource type="Script" path="res://src/components/hurtbox.gd" id="10_hurtbox"]
[ext_resource type="Script" path="res://src/components/mover.gd" id="11_mover"]
[ext_resource type="Script" path="res://src/components/actor_visual.gd" id="12_visual"]
[ext_resource type="Script" path="res://src/actors/pickup/loot_collector.gd" id="13_collector"]

[sub_resource type="CircleShape2D" id="CircleShape2D_body"]
radius = 8.0

[sub_resource type="CircleShape2D" id="CircleShape2D_hurt"]
radius = 10.0

[sub_resource type="CircleShape2D" id="CircleShape2D_magnet"]
radius = 70.0

[node name="Player" type="CharacterBody2D"]
collision_layer = 2
collision_mask = 1
motion_mode = 1
script = ExtResource("1_player")

[node name="CollisionShape2D" type="CollisionShape2D" parent="."]
shape = SubResource("CircleShape2D_body")

[node name="AbilitySystemComponent" parent="." instance=ExtResource("2_asc")]

[node name="ArrowAbility" type="Node" parent="AbilitySystemComponent"]
script = ExtResource("3_arrow")
ability_name = "Arrow"
ability_tag = &"Ability.Arrow"
cooldown_effect = ExtResource("5_cd_arrow")
projectile_scene = ExtResource("7_projectile")
projectile_def = ExtResource("8_arrow_def")

[node name="DashAbility" type="Node" parent="AbilitySystemComponent"]
script = ExtResource("4_dash")
ability_name = "Dash"
ability_tag = &"Ability.Dash"
activation_owned_tags = Array[StringName]([&"State.Invulnerable"])
cooldown_effect = ExtResource("6_cd_dash")

[node name="Health" type="Node" parent="."]
script = ExtResource("9_health")

[node name="Hurtbox" type="Area2D" parent="."]
collision_layer = 8
collision_mask = 0
monitoring = false
script = ExtResource("10_hurtbox")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Hurtbox"]
shape = SubResource("CircleShape2D_hurt")

[node name="Mover" type="Node" parent="."]
script = ExtResource("11_mover")

[node name="Visual" type="AnimatedSprite2D" parent="."]
script = ExtResource("12_visual")

[node name="Collector" type="Area2D" parent="."]
collision_layer = 0
collision_mask = 32
monitorable = false
script = ExtResource("13_collector")

[node name="CollisionShape2D" type="CollisionShape2D" parent="Collector"]
shape = SubResource("CircleShape2D_magnet")

[node name="Camera2D" type="Camera2D" parent="."]
zoom = Vector2(2, 2)
position_smoothing_enabled = true
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/actors/test_player tools/run_tests.sh`
Expected: 7 PASS. Likely snags and their fixes:
- `test_dash_grants_invulnerability...` finds no tag: `activation_owned_tags` is applied inside `try_activate` before `_activate_ability`; if the tag is missing the `.tscn` property did not load — open `player.tscn` in the editor and set it in the inspector.
- `Player` cannot find `$AbilitySystemComponent/ArrowAbility`: children of an instanced scene node must be declared with `parent="AbilitySystemComponent"` exactly as above.
- Abilities granted twice or not at all: `GameplayAbility._ready` self-grants when its parent is an ASC (`gameplay_ability.gd:_ready`); do not call `grant_ability` manually.

- [ ] **Step 6: Run the whole suite and commit**

Run: `tools/run_tests.sh` → all PASS.
```bash
git add src/actors data/projectiles tests/actors
git commit -q -m "feat(player): add Player with auto-fire arrow and invulnerable dash abilities

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Stage systems — clock, flow, run state, factory, directors, arena

**Files:**
- Create: `src/stage/stage_clock.gd`, `stage_flow.gd`, `run_state.gd`, `enemy_factory.gd`, `spawn_director.gd`, `wave_director.gd`, `arena.gd`, `arena.tscn`
- Test: `tests/stage/test_stage_clock.gd`, `test_stage_flow.gd`, `test_run_state.gd`, `test_enemy_factory.gd`, `test_spawn_director.gd`, `test_wave_director.gd`, `test_arena.gd`

**Interfaces:**
- Consumes: Tasks 2–9 (`Timeline`, `SpawnGeometry`, `RunRng`, `Wallet`, `StageDef`, `WaveEntry`, `EnemyDef`, `Enemy`, `TargetProvider`, `RunResult`, `PlayerProfile`).
- Produces:
  - `StageClock` (Node): `signal ticked(elapsed: float)`, `signal ended`; `elapsed`, `duration`; `start(p_duration)`, `stop()`, `is_running() -> bool`, `step(delta)` (called by `_physics_process`; clamps at `duration`, emits `ended` once).
  - `StageFlow` (RefCounted): `enum State { PREP, RUNNING, BOSS, CLEARED, FAILED }`, `signal state_changed(state)`, `state`, `transition_to(to) -> bool` (allowed: PREP→RUNNING, RUNNING→BOSS/CLEARED/FAILED, BOSS→CLEARED/FAILED), `is_terminal() -> bool`.
  - `RunState` (RefCounted): `_init(p_stage_def, p_profile, run_seed := 0)`; `stage_def`, `profile`, `rng: RunRng`, `wallet: Wallet`, `kills: int`, `elapsed: float`; `to_result(outcome: RunResult.Outcome) -> RunResult`.
  - `EnemyFactory` (Node): `signal spawned(enemy: Enemy)`; `bind(layer: Node2D, targets: TargetProvider, rng: RunRng)`; `spawn(def: EnemyDef, position: Vector2) -> Enemy`; `alive_count() -> int`.
  - `SpawnDirector` (Node): `bind(stage_def, clock: StageClock, factory: EnemyFactory, anchor: Node2D, rng, arena_bounds: Rect2)`; `current_interval(elapsed: float) -> float`.
  - `WaveDirector` (Node): `signal wave_started(entry: WaveEntry)`; `timeline: Timeline`; `bind(stage_def, clock, factory, anchor, rng, arena_bounds)`; `mark_times() -> PackedFloat32Array`.
  - `Arena` (Node2D scene `res://src/stage/arena.tscn`): `bounds: Rect2`; `build(size: Vector2, ground_texture: Texture2D)`.

- [ ] **Step 1: Write the failing tests**

`tests/stage/test_stage_clock.gd`:
```gdscript
extends TestCase


func _clock() -> StageClock:
	return adopt(StageClock.new()) as StageClock


func test_ticks_report_elapsed_time() -> void:
	var clock := _clock()
	var seen: Array = []
	clock.ticked.connect(func(elapsed: float) -> void: seen.append(elapsed))
	clock.start(10.0)
	clock.step(1.5)
	clock.step(0.5)
	assert_eq(seen, [1.5, 2.0])


func test_ended_fires_once_and_elapsed_is_clamped() -> void:
	var clock := _clock()
	var ended: Array = []
	clock.ended.connect(func() -> void: ended.append(true))
	clock.start(2.0)
	clock.step(5.0)
	clock.step(5.0)
	assert_eq(ended.size(), 1)
	assert_almost_eq(clock.elapsed, 2.0)
	assert_false(clock.is_running())


func test_stopped_clock_does_not_tick() -> void:
	var clock := _clock()
	var seen: Array = []
	clock.ticked.connect(func(elapsed: float) -> void: seen.append(elapsed))
	clock.start(10.0)
	clock.stop()
	clock.step(1.0)
	assert_true(seen.is_empty())


func test_start_resets_elapsed() -> void:
	var clock := _clock()
	clock.start(10.0)
	clock.step(4.0)
	clock.start(10.0)
	assert_almost_eq(clock.elapsed, 0.0)
	assert_true(clock.is_running())
```

`tests/stage/test_stage_flow.gd`:
```gdscript
extends TestCase


func test_normal_progression_and_signal() -> void:
	var flow := StageFlow.new()
	var seen: Array = []
	flow.state_changed.connect(func(state: StageFlow.State) -> void: seen.append(state))
	assert_true(flow.transition_to(StageFlow.State.RUNNING))
	assert_true(flow.transition_to(StageFlow.State.CLEARED))
	assert_eq(seen, [StageFlow.State.RUNNING, StageFlow.State.CLEARED])
	assert_true(flow.is_terminal())


func test_illegal_transitions_are_refused() -> void:
	var flow := StageFlow.new()
	assert_false(flow.transition_to(StageFlow.State.CLEARED), "cannot clear before running")
	flow.transition_to(StageFlow.State.RUNNING)
	flow.transition_to(StageFlow.State.FAILED)
	assert_false(flow.transition_to(StageFlow.State.RUNNING), "terminal states are final")
	assert_false(flow.transition_to(StageFlow.State.CLEARED))
	assert_eq(flow.state, StageFlow.State.FAILED)


func test_boss_state_sits_between_running_and_the_end() -> void:
	var flow := StageFlow.new()
	flow.transition_to(StageFlow.State.RUNNING)
	assert_true(flow.transition_to(StageFlow.State.BOSS))
	assert_true(flow.transition_to(StageFlow.State.CLEARED))
```

`tests/stage/test_run_state.gd`:
```gdscript
extends TestCase


func test_new_run_starts_clean() -> void:
	var state := RunState.new(DefFactory.stage(), PlayerProfile.new(), 7)
	assert_eq(state.wallet.gold, 0)
	assert_eq(state.wallet.stone, 0)
	assert_eq(state.kills, 0)
	assert_almost_eq(state.elapsed, 0.0)
	assert_eq(state.rng.seed_value, 7)


func test_to_result_reports_the_run() -> void:
	var state := RunState.new(DefFactory.stage(), PlayerProfile.new())
	state.wallet.add(Wallet.Kind.GOLD, 12)
	state.wallet.add(Wallet.Kind.STONE, 3)
	state.kills = 9
	state.elapsed = 42.0
	var result := state.to_result(RunResult.Outcome.CLEARED)
	assert_eq(result.outcome, RunResult.Outcome.CLEARED)
	assert_eq(result.gold, 12)
	assert_eq(result.stone, 3)
	assert_eq(result.kills, 9)
	assert_almost_eq(result.elapsed, 42.0)
```

`tests/stage/test_enemy_factory.gd`:
```gdscript
extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")


func _rig() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var building := adopt(Node2D.new()) as Node2D
	building.global_position = Vector2(0, 500)
	var player := adopt(Node2D.new()) as Node2D
	player.global_position = Vector2(500, 0)
	targets.building = building
	targets.player = player
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, RunRng.new(1))
	return [factory, layer]


func test_spawn_places_a_configured_enemy_in_the_layer() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig[0]
	var layer: Node2D = rig[1]
	var spawned: Array = []
	factory.spawned.connect(func(enemy: Enemy) -> void: spawned.append(enemy))
	var enemy := factory.spawn(DefFactory.enemy(ENEMY_SCENE), Vector2(30, 40))
	assert_eq(enemy.get_parent(), layer)
	assert_eq(enemy.global_position, Vector2(30, 40))
	assert_eq(spawned, [enemy])
	assert_eq(factory.alive_count(), 1)


func test_enemies_chase_the_target_of_their_policy() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig[0]
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.target_policy = EnemyDef.TargetPolicy.PLAYER
	var enemy := factory.spawn(def, Vector2.ZERO)
	await wait_seconds(0.3)
	assert_true(enemy.global_position.x > 3.0, "moved toward the player on +x")
	assert_almost_eq(enemy.global_position.y, 0.0, 0.5)
```

`tests/stage/test_spawn_director.gd`:
```gdscript
extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BIG_ARENA: Rect2 = Rect2(-5000, -5000, 10000, 10000)


func _rig(stage_def: StageDef) -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var anchor := adopt(Node2D.new()) as Node2D
	targets.building = anchor
	targets.player = anchor
	var rng := RunRng.new(2)
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, rng)
	var clock := adopt(StageClock.new()) as StageClock
	var director := adopt(SpawnDirector.new()) as SpawnDirector
	director.bind(stage_def, clock, factory, anchor, rng, BIG_ARENA)
	clock.start(stage_def.duration)
	return [clock, factory, director]


func _stage(cap: int) -> StageDef:
	var def := DefFactory.stage(100.0)
	def.trickle_enemies.append(DefFactory.enemy(ENEMY_SCENE))
	def.trickle_interval_start = 1.0
	def.trickle_interval_end = 1.0
	def.trickle_cap = cap
	def.spawn_radius = 300.0
	return def


func test_interval_interpolates_across_the_stage() -> void:
	var def := _stage(10)
	def.trickle_interval_start = 2.0
	def.trickle_interval_end = 0.5
	var director: SpawnDirector = _rig(def)[2]
	assert_almost_eq(director.current_interval(0.0), 2.0)
	assert_almost_eq(director.current_interval(50.0), 1.25)
	assert_almost_eq(director.current_interval(100.0), 0.5)


func test_spawns_on_the_ring_at_the_configured_interval_and_respects_the_cap() -> void:
	var rig := _rig(_stage(2))
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	clock.step(0.1)
	assert_eq(factory.alive_count(), 1)
	clock.step(0.5)
	assert_eq(factory.alive_count(), 1, "interval not elapsed")
	clock.step(1.0)
	assert_eq(factory.alive_count(), 2)
	clock.step(5.0)
	assert_eq(factory.alive_count(), 2, "cap reached")
	for node in get_tree_enemies():
		assert_almost_eq((node as Node2D).global_position.length(), 300.0, 0.5)


func test_empty_trickle_list_spawns_nothing_and_does_not_fail() -> void:
	var def := _stage(5)
	def.trickle_enemies.clear()
	var rig := _rig(def)
	(rig[0] as StageClock).step(3.0)
	assert_eq((rig[1] as EnemyFactory).alive_count(), 0)


func get_tree_enemies() -> Array[Node]:
	return host.get_tree().get_nodes_in_group(Groups.ENEMIES)
```

`tests/stage/test_wave_director.gd`:
```gdscript
extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BIG_ARENA: Rect2 = Rect2(-5000, -5000, 10000, 10000)


func _entry(time: float, count: int, label: String) -> WaveEntry:
	var entry := WaveEntry.new()
	entry.time = time
	entry.count = count
	entry.label = label
	entry.enemy = DefFactory.enemy(ENEMY_SCENE)
	return entry


func _rig(entries: Array[WaveEntry]) -> Array:
	var def := DefFactory.stage(10.0)
	def.spawn_radius = 300.0
	def.waves.entries.append_array(entries)
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var anchor := adopt(Node2D.new()) as Node2D
	targets.building = anchor
	targets.player = anchor
	var rng := RunRng.new(4)
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, rng)
	var clock := adopt(StageClock.new()) as StageClock
	var director := adopt(WaveDirector.new()) as WaveDirector
	director.bind(def, clock, factory, anchor, rng, BIG_ARENA)
	clock.start(def.duration)
	return [clock, factory, director]


func test_waves_fire_in_time_order_with_their_enemy_counts() -> void:
	var entries: Array[WaveEntry] = [_entry(3.0, 4, "late"), _entry(1.0, 3, "early")]
	var rig := _rig(entries)
	var clock: StageClock = rig[0]
	var factory: EnemyFactory = rig[1]
	var director: WaveDirector = rig[2]
	var labels: Array = []
	director.wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	clock.step(1.5)
	assert_eq(factory.alive_count(), 3)
	clock.step(1.0)
	assert_eq(labels, ["early"])
	clock.step(1.0)
	assert_eq(labels, ["early", "late"])
	assert_eq(factory.alive_count(), 7)


func test_a_huge_time_step_fires_every_wave_exactly_once() -> void:
	var entries: Array[WaveEntry] = [_entry(1.0, 2, "a"), _entry(3.0, 2, "b")]
	var rig := _rig(entries)
	var director: WaveDirector = rig[2]
	var labels: Array = []
	director.wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	(rig[0] as StageClock).step(100.0)
	assert_eq(labels, ["a", "b"])


func test_a_wave_at_time_zero_still_fires() -> void:
	var entries: Array[WaveEntry] = [_entry(0.0, 2, "opener")]
	var rig := _rig(entries)
	var labels: Array = []
	(rig[2] as WaveDirector).wave_started.connect(func(entry: WaveEntry) -> void: labels.append(entry.label))
	(rig[0] as StageClock).step(0.1)
	assert_eq(labels, ["opener"])


func test_mark_times_are_sorted_for_the_hud() -> void:
	var entries: Array[WaveEntry] = [_entry(3.0, 1, "b"), _entry(1.0, 1, "a")]
	var director: WaveDirector = _rig(entries)[2]
	assert_eq(director.mark_times(), PackedFloat32Array([1.0, 3.0]))
```

`tests/stage/test_arena.gd`:
```gdscript
extends TestCase

const ARENA_SCENE: PackedScene = preload("res://src/stage/arena.tscn")


func test_build_centers_the_bounds_and_surrounds_them_with_walls() -> void:
	var arena := adopt(ARENA_SCENE.instantiate()) as Arena
	arena.build(Vector2(400, 300), DefFactory.texture(32, 32))
	assert_eq(arena.bounds, Rect2(-200, -150, 400, 300))
	var walls := arena.get_node("Walls") as StaticBody2D
	assert_eq(walls.get_child_count(), 4)
	var centers: Array = []
	for child in walls.get_children():
		centers.append((child as CollisionShape2D).position)
	assert_true(centers.has(Vector2(0, -250)), "top wall")
	assert_true(centers.has(Vector2(0, 250)), "bottom wall")
	assert_true(centers.has(Vector2(-300, 0)), "left wall")
	assert_true(centers.has(Vector2(300, 0)), "right wall")


func test_rebuilding_replaces_the_walls() -> void:
	var arena := adopt(ARENA_SCENE.instantiate()) as Arena
	arena.build(Vector2(400, 300), DefFactory.texture(32, 32))
	arena.build(Vector2(800, 600), DefFactory.texture(32, 32))
	assert_eq((arena.get_node("Walls") as StaticBody2D).get_child_count(), 4)
	assert_eq(arena.bounds.size, Vector2(800, 600))
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/stage tools/run_tests.sh`
Expected: errors — none of the stage classes exist.

- [ ] **Step 3: Implement the pure/simple modules**

`src/stage/stage_clock.gd`:
```gdscript
class_name StageClock
extends Node
## Elapsed stage time. Everything time-driven (spawning, waves, HUD) subscribes to `ticked`.

signal ticked(elapsed: float)
signal ended

var elapsed: float = 0.0
var duration: float = 0.0
var _running: bool = false


func start(p_duration: float) -> void:
	duration = p_duration
	elapsed = 0.0
	_running = true


func stop() -> void:
	_running = false


func is_running() -> bool:
	return _running


func step(delta: float) -> void:
	if not _running:
		return
	elapsed = minf(elapsed + delta, duration)
	ticked.emit(elapsed)
	if elapsed >= duration:
		_running = false
		ended.emit()


func _physics_process(delta: float) -> void:
	step(delta)
```

`src/stage/stage_flow.gd`:
```gdscript
class_name StageFlow
extends RefCounted
## The stage's lifecycle as an explicit state machine; owns transitions only.

signal state_changed(state: State)

enum State { PREP, RUNNING, BOSS, CLEARED, FAILED }

const ALLOWED: Dictionary = {
	State.PREP: [State.RUNNING],
	State.RUNNING: [State.BOSS, State.CLEARED, State.FAILED],
	State.BOSS: [State.CLEARED, State.FAILED],
}

var state: State = State.PREP


func transition_to(to: State) -> bool:
	if not ALLOWED.get(state, []).has(to):
		return false
	state = to
	state_changed.emit(state)
	return true


func is_terminal() -> bool:
	return state == State.CLEARED or state == State.FAILED
```

`src/stage/run_state.gd`:
```gdscript
class_name RunState
extends RefCounted
## Mutable state of one stage run. Dies with the stage; nothing here is global.

var stage_def: StageDef
var profile: PlayerProfile
var rng: RunRng
var wallet: Wallet = Wallet.new()
var kills: int = 0
var elapsed: float = 0.0


func _init(p_stage_def: StageDef, p_profile: PlayerProfile, run_seed: int = 0) -> void:
	stage_def = p_stage_def
	profile = p_profile
	rng = RunRng.new(run_seed)


func to_result(outcome: RunResult.Outcome) -> RunResult:
	var result := RunResult.new()
	result.outcome = outcome
	result.elapsed = elapsed
	result.kills = kills
	result.gold = wallet.gold
	result.stone = wallet.stone
	return result
```

`src/stage/enemy_factory.gd`:
```gdscript
class_name EnemyFactory
extends Node
## Creates enemies from defs. The single place to add pooling later.

signal spawned(enemy: Enemy)

var _layer: Node2D
var _targets: TargetProvider
var _rng: RunRng


func bind(layer: Node2D, targets: TargetProvider, rng: RunRng) -> void:
	_layer = layer
	_targets = targets
	_rng = rng


func spawn(def: EnemyDef, position: Vector2) -> Enemy:
	var enemy := def.scene.instantiate() as Enemy
	_layer.add_child(enemy)
	enemy.global_position = position
	enemy.setup(def, _targets.target_for(def.target_policy), _rng)
	spawned.emit(enemy)
	return enemy


func alive_count() -> int:
	return get_tree().get_nodes_in_group(Groups.ENEMIES).size()
```

- [ ] **Step 4: Implement the directors and the arena**

`src/stage/spawn_director.gd`:
```gdscript
class_name SpawnDirector
extends Node
## The gentle background trickle of enemies; its pace ramps across the stage.

const EDGE_MARGIN: float = 24.0

var _stage_def: StageDef
var _factory: EnemyFactory
var _anchor: Node2D
var _rng: RunRng
var _bounds: Rect2
var _next_spawn_at: float = 0.0


func bind(stage_def: StageDef, clock: StageClock, factory: EnemyFactory, anchor: Node2D, rng: RunRng, arena_bounds: Rect2) -> void:
	_stage_def = stage_def
	_factory = factory
	_anchor = anchor
	_rng = rng
	_bounds = arena_bounds
	clock.ticked.connect(_on_clock_ticked)


## Seconds between trickle spawns at `elapsed`, interpolated across the stage.
func current_interval(elapsed: float) -> float:
	var progress := clampf(elapsed / _stage_def.duration, 0.0, 1.0)
	return lerpf(_stage_def.trickle_interval_start, _stage_def.trickle_interval_end, progress)


func _on_clock_ticked(elapsed: float) -> void:
	if elapsed < _next_spawn_at:
		return
	_next_spawn_at = elapsed + current_interval(elapsed)
	if _stage_def.trickle_enemies.is_empty() or _factory.alive_count() >= _stage_def.trickle_cap:
		return
	if not is_instance_valid(_anchor):
		return
	var def := _rng.pick(_stage_def.trickle_enemies) as EnemyDef
	var point := SpawnGeometry.ring_point(_anchor.global_position, _stage_def.spawn_radius, _rng.randf() * TAU)
	_factory.spawn(def, SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN))
```

`src/stage/wave_director.gd`:
```gdscript
class_name WaveDirector
extends Node
## Fires the stage's scheduled surges and exposes their times for the timeline bar.

signal wave_started(entry: WaveEntry)

const EDGE_MARGIN: float = 24.0

var timeline: Timeline

var _entries: Array[WaveEntry] = []
var _previous_time: float = -1.0
var _factory: EnemyFactory
var _anchor: Node2D
var _rng: RunRng
var _bounds: Rect2
var _spawn_radius: float = 0.0


func bind(stage_def: StageDef, clock: StageClock, factory: EnemyFactory, anchor: Node2D, rng: RunRng, arena_bounds: Rect2) -> void:
	_factory = factory
	_anchor = anchor
	_rng = rng
	_bounds = arena_bounds
	_spawn_radius = stage_def.spawn_radius
	var times := PackedFloat32Array()
	if stage_def.waves != null:
		_entries = stage_def.waves.sorted_entries()
		times = stage_def.waves.times()
	timeline = Timeline.new(stage_def.duration, times)
	clock.ticked.connect(_on_clock_ticked)


func mark_times() -> PackedFloat32Array:
	return timeline.marks


func _on_clock_ticked(elapsed: float) -> void:
	for index in timeline.crossed(_previous_time, elapsed):
		_launch(_entries[index])
	_previous_time = elapsed


func _launch(entry: WaveEntry) -> void:
	wave_started.emit(entry)
	if not is_instance_valid(_anchor):
		return
	var base_angle := _rng.randf() * TAU
	for i in entry.count:
		var angle := base_angle + TAU * float(i) / float(entry.count)
		var point := SpawnGeometry.ring_point(_anchor.global_position, _spawn_radius, angle)
		_factory.spawn(entry.enemy, SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN))
```

`src/stage/arena.gd`:
```gdscript
class_name Arena
extends Node2D
## The playfield: a tiled ground and four walls around it.

const WALL_THICKNESS: float = 200.0

var bounds: Rect2

@onready var _ground: Sprite2D = $Ground
@onready var _walls: StaticBody2D = $Walls


## Call after the arena has entered the tree.
func build(size: Vector2, ground_texture: Texture2D) -> void:
	bounds = Rect2(-size / 2.0, size)
	_ground.texture = ground_texture
	_ground.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
	_ground.region_enabled = true
	_ground.region_rect = Rect2(Vector2.ZERO, size)
	for child in _walls.get_children():
		_walls.remove_child(child)
		child.free()
	for rect in _wall_rects():
		var shape := CollisionShape2D.new()
		var box := RectangleShape2D.new()
		box.size = rect.size
		shape.shape = box
		shape.position = rect.get_center()
		_walls.add_child(shape)


func _wall_rects() -> Array[Rect2]:
	var t := WALL_THICKNESS
	var width := bounds.size.x + 2.0 * t
	return [
		Rect2(bounds.position.x - t, bounds.position.y - t, width, t),
		Rect2(bounds.position.x - t, bounds.end.y, width, t),
		Rect2(bounds.position.x - t, bounds.position.y, t, bounds.size.y),
		Rect2(bounds.end.x, bounds.position.y, t, bounds.size.y),
	]
```

`src/stage/arena.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://src/stage/arena.gd" id="1_arena"]

[node name="Arena" type="Node2D"]
script = ExtResource("1_arena")

[node name="Ground" type="Sprite2D" parent="."]
z_index = -10

[node name="Walls" type="StaticBody2D" parent="."]
collision_layer = 1
collision_mask = 0
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/stage tools/run_tests.sh`
Expected: all `tests/stage` PASS. If `test_spawns_on_the_ring...` reports ring distances off, `clamp_to_rect` is clamping — keep `BIG_ARENA` in the test.

- [ ] **Step 6: Commit**

```bash
git add src/stage tests/stage
git commit -q -m "feat(stage): add clock, flow, run state, enemy factory, directors and arena

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Game data for stage 1

**Files:**
- Create: `data/characters/soldier_visual.tres`, `data/characters/default_profile.tres`, `data/drops/orc_drops.tres`, `data/enemies/orc_visual.tres`, `data/enemies/orc.tres`, `data/enemies/orc_swarmer.tres`, `data/waves/stage_01_waves.tres`, `data/stages/stage_01.tres`
- Test: `tests/data/test_game_data.gd`

**Interfaces:**
- Consumes: all defs (Task 3), `enemy.tscn` (Task 8), art (Task 7), effects/projectile (Tasks 4, 9).
- Produces: `res://data/stages/stage_01.tres` (StageDef "Training Grounds", 180 s, 2400x1600, building 500 HP, trickle orc + swarmer, five waves at 30/60/90/120/150 s) and `res://data/characters/default_profile.tres` (PlayerProfile: 100 HP, speed 140, Soldier visual).

- [ ] **Step 1: Write the failing test**

`tests/data/test_game_data.gd`:
```gdscript
extends TestCase

const DATA_FILES: PackedStringArray = [
	"res://data/characters/soldier_visual.tres",
	"res://data/characters/default_profile.tres",
	"res://data/drops/orc_drops.tres",
	"res://data/enemies/orc_visual.tres",
	"res://data/enemies/orc.tres",
	"res://data/enemies/orc_swarmer.tres",
	"res://data/waves/stage_01_waves.tres",
	"res://data/stages/stage_01.tres",
	"res://data/projectiles/arrow.tres",
	"res://data/effects/damage_basic.tres",
	"res://data/effects/cooldown_dash.tres",
	"res://data/effects/cooldown_arrow.tres",
]


func test_every_data_file_loads() -> void:
	for path in DATA_FILES:
		assert_not_null(load(path), path)


func test_stage_01_validates() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_not_null(stage)
	assert_no_errors(stage.validate(), "stage_01")


func test_default_profile_validates() -> void:
	var profile := load("res://data/characters/default_profile.tres") as PlayerProfile
	assert_no_errors(profile.validate(), "default_profile")


func test_arrow_projectile_validates() -> void:
	assert_no_errors((load("res://data/projectiles/arrow.tres") as ProjectileDef).validate(), "arrow")


func test_stage_01_schedules_five_waves_inside_the_stage() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	var times := stage.waves.times()
	assert_eq(times.size(), 5)
	assert_true(times[0] > 0.0 and times[times.size() - 1] < stage.duration)


func test_enemy_defs_use_the_generic_enemy_scene() -> void:
	for path in ["res://data/enemies/orc.tres", "res://data/enemies/orc_swarmer.tres"]:
		var def := load(path) as EnemyDef
		assert_eq(def.scene.resource_path, "res://src/actors/enemy/enemy.tscn", path)
		assert_not_null(def.visual.find_strip(AnimNames.DEATH), "%s has a death strip" % path)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/data tools/run_tests.sh`
Expected: FAIL — files missing.

- [ ] **Step 3: Author the visuals**

`data/enemies/orc_visual.tres`:
```
[gd_resource type="Resource" script_class="VisualDef" load_steps=14 format=3]

[ext_resource type="Script" path="res://src/defs/visual_def.gd" id="1_visual"]
[ext_resource type="Script" path="res://src/defs/animation_strip.gd" id="2_strip"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/orc/Orc_Idle.png" id="3_idle"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/orc/Orc_Walk.png" id="4_walk"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/orc/Orc_Attack01.png" id="5_attack"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/orc/Orc_Hurt.png" id="6_hurt"]
[ext_resource type="Texture2D" path="res://assets/art/tinyrpg/orc/Orc_Death.png" id="7_death"]

[sub_resource type="Resource" id="Resource_idle"]
script = ExtResource("2_strip")
animation = &"idle"
texture = ExtResource("3_idle")
frame_count = 6
fps = 8.0
loop = true

[sub_resource type="Resource" id="Resource_walk"]
script = ExtResource("2_strip")
animation = &"walk"
texture = ExtResource("4_walk")
frame_count = 8
fps = 10.0
loop = true

[sub_resource type="Resource" id="Resource_attack"]
script = ExtResource("2_strip")
animation = &"attack"
texture = ExtResource("5_attack")
frame_count = 6
fps = 12.0
loop = false

[sub_resource type="Resource" id="Resource_hurt"]
script = ExtResource("2_strip")
animation = &"hurt"
texture = ExtResource("6_hurt")
frame_count = 4
fps = 12.0
loop = false

[sub_resource type="Resource" id="Resource_death"]
script = ExtResource("2_strip")
animation = &"death"
texture = ExtResource("7_death")
frame_count = 4
fps = 8.0
loop = false

[resource]
script = ExtResource("1_visual")
strips = Array[ExtResource("2_strip")]([SubResource("Resource_idle"), SubResource("Resource_walk"), SubResource("Resource_attack"), SubResource("Resource_hurt"), SubResource("Resource_death")])
offset = Vector2(0, -12)
```

`data/characters/soldier_visual.tres`: identical to the file above except the five texture paths are `res://assets/art/tinyrpg/soldier/Soldier_Idle.png`, `Soldier_Walk.png`, `Soldier_Attack01.png`, `Soldier_Hurt.png`, `Soldier_Death.png`.

`data/characters/default_profile.tres`:
```
[gd_resource type="Resource" script_class="PlayerProfile" load_steps=4 format=3]

[ext_resource type="Script" path="res://src/defs/player_profile.gd" id="1_profile"]
[ext_resource type="Script" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="Resource" path="res://data/characters/soldier_visual.tres" id="3_visual"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
max_health = 100.0
move_speed = 140.0
attack = 1.0

[resource]
script = ExtResource("1_profile")
stats = SubResource("Resource_stats")
visual = ExtResource("3_visual")
```

- [ ] **Step 4: Author drops, enemies, waves, stage**

`data/drops/orc_drops.tres`:
```
[gd_resource type="Resource" script_class="DropTable" load_steps=6 format=3]

[ext_resource type="Script" path="res://src/defs/drop_table.gd" id="1_table"]
[ext_resource type="Script" path="res://src/defs/drop_entry.gd" id="2_entry"]
[ext_resource type="Texture2D" path="res://assets/art/items/coin.png" id="3_coin"]
[ext_resource type="Texture2D" path="res://assets/art/items/stone.png" id="4_stone"]

[sub_resource type="Resource" id="Resource_gold"]
script = ExtResource("2_entry")
kind = 0
icon = ExtResource("3_coin")
chance = 1.0
min_amount = 1
max_amount = 2

[sub_resource type="Resource" id="Resource_stone"]
script = ExtResource("2_entry")
kind = 1
icon = ExtResource("4_stone")
chance = 0.12
min_amount = 1
max_amount = 1

[resource]
script = ExtResource("1_table")
entries = Array[ExtResource("2_entry")]([SubResource("Resource_gold"), SubResource("Resource_stone")])
```

`data/enemies/orc.tres`:
```
[gd_resource type="Resource" script_class="EnemyDef" load_steps=8 format=3]

[ext_resource type="Script" path="res://src/defs/enemy_def.gd" id="1_def"]
[ext_resource type="Script" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="PackedScene" path="res://src/actors/enemy/enemy.tscn" id="3_scene"]
[ext_resource type="Resource" path="res://data/enemies/orc_visual.tres" id="4_visual"]
[ext_resource type="Resource" path="res://data/effects/damage_basic.tres" id="5_damage"]
[ext_resource type="Resource" path="res://data/drops/orc_drops.tres" id="6_drops"]

[sub_resource type="Resource" id="Resource_stats"]
script = ExtResource("2_stats")
max_health = 40.0
move_speed = 55.0
attack = 1.0

[resource]
script = ExtResource("1_def")
id = &"orc"
scene = ExtResource("3_scene")
visual = ExtResource("4_visual")
visual_scale = 1.0
tint = Color(1, 1, 1, 1)
stats = SubResource("Resource_stats")
contact_effect = ExtResource("5_damage")
contact_damage = 8.0
contact_interval = 0.9
target_policy = 0
body_radius = 10.0
drops = ExtResource("6_drops")
```

`data/enemies/orc_swarmer.tres`: same structure as `orc.tres` with `id = &"orc_swarmer"`, stats `max_health = 14.0`, `move_speed = 90.0`, `visual_scale = 0.7`, `tint = Color(1, 0.6, 0.6, 1)`, `contact_damage = 4.0`, `contact_interval = 0.7`, `target_policy = 1` (chases the player), `body_radius = 7.0`.

`data/waves/stage_01_waves.tres`:
```
[gd_resource type="Resource" script_class="WaveSchedule" load_steps=10 format=3]

[ext_resource type="Script" path="res://src/defs/wave_schedule.gd" id="1_schedule"]
[ext_resource type="Script" path="res://src/defs/wave_entry.gd" id="2_entry"]
[ext_resource type="Resource" path="res://data/enemies/orc.tres" id="3_orc"]
[ext_resource type="Resource" path="res://data/enemies/orc_swarmer.tres" id="4_swarmer"]

[sub_resource type="Resource" id="Resource_w1"]
script = ExtResource("2_entry")
time = 30.0
enemy = ExtResource("3_orc")
count = 10
label = "Orc Raid"

[sub_resource type="Resource" id="Resource_w2"]
script = ExtResource("2_entry")
time = 60.0
enemy = ExtResource("4_swarmer")
count = 20
label = "Swarm"

[sub_resource type="Resource" id="Resource_w3"]
script = ExtResource("2_entry")
time = 90.0
enemy = ExtResource("3_orc")
count = 16
label = "Orc Raid II"

[sub_resource type="Resource" id="Resource_w4"]
script = ExtResource("2_entry")
time = 120.0
enemy = ExtResource("4_swarmer")
count = 28
label = "Swarm II"

[sub_resource type="Resource" id="Resource_w5"]
script = ExtResource("2_entry")
time = 150.0
enemy = ExtResource("3_orc")
count = 22
label = "Final Raid"

[resource]
script = ExtResource("1_schedule")
entries = Array[ExtResource("2_entry")]([SubResource("Resource_w1"), SubResource("Resource_w2"), SubResource("Resource_w3"), SubResource("Resource_w4"), SubResource("Resource_w5")])
```

`data/stages/stage_01.tres`:
```
[gd_resource type="Resource" script_class="StageDef" load_steps=10 format=3]

[ext_resource type="Script" path="res://src/defs/stage_def.gd" id="1_stage"]
[ext_resource type="Script" path="res://src/defs/stat_block.gd" id="2_stats"]
[ext_resource type="Script" path="res://src/defs/enemy_def.gd" id="3_enemy_script"]
[ext_resource type="Texture2D" path="res://assets/art/world/grass.png" id="4_grass"]
[ext_resource type="Texture2D" path="res://assets/art/farmrpg/house.png" id="5_house"]
[ext_resource type="Resource" path="res://data/enemies/orc.tres" id="6_orc"]
[ext_resource type="Resource" path="res://data/enemies/orc_swarmer.tres" id="7_swarmer"]
[ext_resource type="Resource" path="res://data/waves/stage_01_waves.tres" id="8_waves"]

[sub_resource type="Resource" id="Resource_building"]
script = ExtResource("2_stats")
max_health = 500.0
move_speed = 0.0
attack = 1.0

[resource]
script = ExtResource("1_stage")
id = &"stage_01"
display_name = "Training Grounds"
duration = 180.0
arena_size = Vector2(2400, 1600)
ground_texture = ExtResource("4_grass")
building_texture = ExtResource("5_house")
building_stats = SubResource("Resource_building")
trickle_enemies = Array[ExtResource("3_enemy_script")]([ExtResource("6_orc"), ExtResource("7_swarmer")])
trickle_interval_start = 2.0
trickle_interval_end = 0.8
trickle_cap = 60
spawn_radius = 520.0
waves = ExtResource("8_waves")
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/data tools/run_tests.sh`
Expected: 6 PASS. (Textures must be imported first — see Task 7 Step 1.) A `null` for `load(...)` plus "Parse Error" in the output points at the offending `.tres`; fix the named property.

- [ ] **Step 6: Commit**

```bash
git add data tests/data
git commit -q -m "feat(data): add stage 1 content (player, orcs, waves, drops)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Stage composition root and HUD

**Files:**
- Create: `src/ui/hud/timeline_bar.gd`, `hud.gd`, `hud.tscn`; `src/stage/stage.gd`, `stage.tscn`
- Test: `tests/ui/test_timeline_bar.gd`, `tests/stage/test_stage.gd`

**Interfaces:**
- Consumes: everything from Tasks 2–11.
- Produces:
  - `TimelineBar` (Control): `configure(duration: float, marks: PackedFloat32Array)`, `set_elapsed(elapsed: float)`, `mark_x(mark: float) -> float` (pixel x of a mark for the current width).
  - `Hud` (CanvasLayer scene `res://src/ui/hud/hud.tscn`): `bind(player_health: Health, building_health: Health, wallet: Wallet, clock: StageClock, waves: WaveDirector)`; `show_result(result: RunResult)`.
  - `Stage` (Node2D scene `res://src/stage/stage.tscn`): `signal finished(result: RunResult)`; public `player: Player`, `building: Building`, `run_state: RunState`; `start(stage_def: StageDef, profile: PlayerProfile, run_seed := 0) -> bool` (false + logged errors when a def is invalid; nothing starts).

- [ ] **Step 1: Write the failing tests**

`tests/ui/test_timeline_bar.gd`:
```gdscript
extends TestCase


func _bar() -> TimelineBar:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(100.0, PackedFloat32Array([25.0, 50.0]))
	return bar


func test_mark_x_scales_with_width_and_duration() -> void:
	var bar := _bar()
	assert_almost_eq(bar.mark_x(25.0), 100.0)
	assert_almost_eq(bar.mark_x(50.0), 200.0)
	assert_almost_eq(bar.mark_x(500.0), 400.0, 0.001, "clamped to the bar")


func test_zero_duration_does_not_divide_by_zero() -> void:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(0.0, PackedFloat32Array([10.0]))
	bar.set_elapsed(5.0)
	assert_almost_eq(bar.mark_x(10.0), 0.0)
```

`tests/stage/test_stage.gd`:
```gdscript
extends TestCase

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stage_def(duration: float) -> StageDef:
	var orc := load("res://data/enemies/orc.tres") as EnemyDef
	var def := DefFactory.stage(duration)
	def.trickle_enemies.append(orc)
	def.trickle_interval_start = 0.3
	def.trickle_interval_end = 0.3
	var entry := WaveEntry.new()
	entry.time = 0.5
	entry.enemy = orc
	entry.count = 3
	def.waves.entries.append(entry)
	return def


func _profile() -> PlayerProfile:
	return load("res://data/characters/default_profile.tres") as PlayerProfile


func _running_stage(duration: float, results: Array) -> Stage:
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	assert_true(stage.start(_stage_def(duration), _profile(), 11))
	return stage


func _kill(stage: Stage, target_asc: AbilitySystemComponent) -> void:
	var attacker := ActorFixture.make(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, 99999.0)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, target_asc)


func test_run_reaches_cleared_when_time_is_up() -> void:
	var results: Array = []
	var stage := _running_stage(1.5, results)
	await wait_seconds(1.0)
	assert_true(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size() > 0, "enemies spawned")
	await wait_seconds(1.2)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_almost_eq((results[0] as RunResult).elapsed, 1.5, 0.15)


func test_destroying_the_building_fails_the_run() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	_kill(stage, stage.building.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_player_death_fails_the_run() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	_kill(stage, stage.player.get_node("AbilitySystemComponent"))
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_finish_is_idempotent() -> void:
	var results: Array = []
	var stage := _running_stage(1.0, results)
	_kill(stage, stage.building.asc)
	await wait_seconds(1.6)
	assert_eq(results.size(), 1, "time-up after failure must not finish again")
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_invalid_defs_are_rejected_before_anything_starts() -> void:
	var results: Array = []
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	var broken := _stage_def(0.0)
	assert_false(stage.start(broken, _profile()))
	await wait_seconds(0.2)
	assert_true(results.is_empty())
	assert_null(stage.run_state)


func test_collected_loot_is_credited_with_the_gain_multiplier() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	stage.player.loot_collected.emit(Wallet.Kind.GOLD, 5)
	stage.player.loot_collected.emit(Wallet.Kind.STONE, 2)
	assert_eq(stage.run_state.wallet.gold, 5)
	assert_eq(stage.run_state.wallet.stone, 2)


func test_killed_enemies_are_counted_and_drop_pickups() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		_kill(stage, victim.get_node("AbilitySystemComponent"))
	await next_frame()
	assert_eq(stage.run_state.kills, victims.size())
	assert_true(stage.get_node("World/Pickups").get_child_count() > 0, "gold dropped")


func test_restart_starts_clean() -> void:
	var first_results: Array = []
	var first := _running_stage(30.0, first_results)
	first.run_state.wallet.add(Wallet.Kind.GOLD, 50)
	_kill(first, first.building.asc)
	first.free()
	await next_frame()
	var second_results: Array = []
	var second := _running_stage(30.0, second_results)
	assert_eq(second.run_state.wallet.gold, 0)
	assert_almost_eq(second.run_state.elapsed, 0.0)
	assert_eq(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size(), 0)
```

- [ ] **Step 2: Run to verify failure**

Run: `FILTER=tests/ui tools/run_tests.sh` and `FILTER=tests/stage/test_stage.gd tools/run_tests.sh`
Expected: errors — `TimelineBar`, `Stage` undefined.

- [ ] **Step 3: Implement the HUD**

`src/ui/hud/timeline_bar.gd`:
```gdscript
class_name TimelineBar
extends Control
## Stage progress with a diamond for every scheduled wave; passed waves turn grey.

@export var track_color: Color = Color(0.12, 0.12, 0.16, 0.85)
@export var fill_color: Color = Color(0.35, 0.7, 1.0)
@export var mark_color: Color = Color(1.0, 0.75, 0.2)
@export var passed_mark_color: Color = Color(0.55, 0.55, 0.55)

const MARK_HALF_WIDTH: float = 5.0

var _duration: float = 1.0
var _marks: PackedFloat32Array = PackedFloat32Array()
var _elapsed: float = 0.0


func configure(duration: float, marks: PackedFloat32Array) -> void:
	_duration = duration
	_marks = marks
	_elapsed = 0.0
	queue_redraw()


func set_elapsed(elapsed: float) -> void:
	_elapsed = elapsed
	queue_redraw()


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
```

`src/ui/hud/hud.gd`:
```gdscript
class_name Hud
extends CanvasLayer
## Read-only view of the stage. It binds to models through signals and never mutates them.

@onready var _player_bar: ProgressBar = %PlayerBar
@onready var _building_bar: ProgressBar = %BuildingBar
@onready var _wallet_label: Label = %WalletLabel
@onready var _timeline: TimelineBar = %Timeline
@onready var _result_label: Label = %ResultLabel


func bind(player_health: Health, building_health: Health, wallet: Wallet, clock: StageClock, waves: WaveDirector) -> void:
	player_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_player_bar, current, maximum))
	building_health.changed.connect(func(current: float, maximum: float) -> void: _set_bar(_building_bar, current, maximum))
	_set_bar(_player_bar, player_health.current(), player_health.maximum())
	_set_bar(_building_bar, building_health.current(), building_health.maximum())
	wallet.changed.connect(_on_wallet_changed)
	_on_wallet_changed(wallet.gold, wallet.stone)
	_timeline.configure(waves.timeline.duration, waves.mark_times())
	clock.ticked.connect(_timeline.set_elapsed)


func show_result(result: RunResult) -> void:
	var title := "STAGE CLEARED" if result.outcome == RunResult.Outcome.CLEARED else "GAME OVER"
	_result_label.text = "%s\nKills %d   Gold %d   Stone %d\nPress R to restart" % [title, result.kills, result.gold, result.stone]
	_result_label.visible = true


func _set_bar(bar: ProgressBar, current: float, maximum: float) -> void:
	bar.max_value = maxf(maximum, 1.0)
	bar.value = current


func _on_wallet_changed(gold: int, stone: int) -> void:
	_wallet_label.text = "Gold %d   Stone %d" % [gold, stone]
```

`src/ui/hud/hud.tscn`:
```
[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/ui/hud/hud.gd" id="1_hud"]
[ext_resource type="Script" path="res://src/ui/hud/timeline_bar.gd" id="2_timeline"]

[node name="Hud" type="CanvasLayer"]
script = ExtResource("1_hud")

[node name="Root" type="Control" parent="."]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2

[node name="Bars" type="VBoxContainer" parent="Root"]
layout_mode = 0
offset_left = 16.0
offset_top = 16.0
offset_right = 296.0
offset_bottom = 90.0

[node name="PlayerBar" type="ProgressBar" parent="Root/Bars"]
unique_name_in_owner = true
self_modulate = Color(0.4, 1, 0.4, 1)
custom_minimum_size = Vector2(280, 16)
layout_mode = 2
show_percentage = false

[node name="BuildingBar" type="ProgressBar" parent="Root/Bars"]
unique_name_in_owner = true
self_modulate = Color(1, 0.65, 0.25, 1)
custom_minimum_size = Vector2(280, 16)
layout_mode = 2
show_percentage = false

[node name="WalletLabel" type="Label" parent="Root/Bars"]
unique_name_in_owner = true
layout_mode = 2

[node name="Timeline" type="Control" parent="Root"]
unique_name_in_owner = true
layout_mode = 1
anchors_preset = 5
anchor_left = 0.5
anchor_right = 0.5
offset_left = -260.0
offset_top = 16.0
offset_right = 260.0
offset_bottom = 40.0
grow_horizontal = 2
script = ExtResource("2_timeline")

[node name="ResultLabel" type="Label" parent="Root"]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -240.0
offset_top = -60.0
offset_right = 240.0
offset_bottom = 60.0
grow_horizontal = 2
grow_vertical = 2
horizontal_alignment = 1
vertical_alignment = 1
```

- [ ] **Step 4: Implement Stage**

`src/stage/stage.gd`:
```gdscript
class_name Stage
extends Node2D
## Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the
## signals between modules, and reports a RunResult. Modules never look each other up.

signal finished(result: RunResult)

const PICKUP_SCENE: PackedScene = preload("res://src/actors/pickup/pickup.tscn")
const PLAYER_START: Vector2 = Vector2(0.0, 150.0)

var run_state: RunState

var _flow := StageFlow.new()

@onready var player: Player = $World/Player
@onready var building: Building = $World/Building
@onready var _world: Node2D = $World
@onready var _arena: Arena = $Arena
@onready var _enemy_layer: Node2D = $World/Enemies
@onready var _projectile_layer: Node2D = $World/Projectiles
@onready var _pickup_layer: Node2D = $World/Pickups
@onready var _clock: StageClock = $Systems/StageClock
@onready var _targets: TargetProvider = $Systems/TargetProvider
@onready var _factory: EnemyFactory = $Systems/EnemyFactory
@onready var _spawner: SpawnDirector = $Systems/SpawnDirector
@onready var _waves: WaveDirector = $Systems/WaveDirector
@onready var _hud: Hud = $Hud


## Returns false, after logging every problem, when a def is invalid; nothing is started then.
func start(stage_def: StageDef, profile: PlayerProfile, run_seed: int = 0) -> bool:
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
	player.setup(profile, _targets, _projectile_layer)
	player.limit_camera(_arena.bounds)
	_targets.building = building
	_targets.player = player
	_factory.bind(_enemy_layer, _targets, run_state.rng)
	_spawner.bind(stage_def, _clock, _factory, player, run_state.rng, _arena.bounds)
	_waves.bind(stage_def, _clock, _factory, player, run_state.rng, _arena.bounds)
	_hud.bind(player.health, building.health, run_state.wallet, _clock, _waves)
	_wire()
	_flow.transition_to(StageFlow.State.RUNNING)
	_clock.start(stage_def.duration)
	return true


func _wire() -> void:
	building.destroyed.connect(_finish.bind(RunResult.Outcome.FAILED))
	player.died.connect(_finish.bind(RunResult.Outcome.FAILED))
	_clock.ended.connect(_finish.bind(RunResult.Outcome.CLEARED))
	_clock.ticked.connect(_on_clock_ticked)
	_factory.spawned.connect(_on_enemy_spawned)
	player.loot_collected.connect(_on_loot_collected)


func _on_clock_ticked(elapsed: float) -> void:
	run_state.elapsed = elapsed


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.died.connect(_on_enemy_died)
	enemy.loot_dropped.connect(_on_loot_dropped)


func _on_enemy_died(_enemy: Enemy) -> void:
	run_state.kills += 1


## Deferred: pickups are collision objects and must not be added while physics signals are flushing.
func _on_loot_dropped(roll: DropRoll, position: Vector2) -> void:
	_spawn_pickup.call_deferred(roll, position)


func _spawn_pickup(roll: DropRoll, position: Vector2) -> void:
	var pickup := PICKUP_SCENE.instantiate() as Pickup
	_pickup_layer.add_child(pickup)
	pickup.global_position = position
	pickup.setup(roll)


func _on_loot_collected(kind: Wallet.Kind, amount: int) -> void:
	run_state.wallet.add(kind, roundi(amount * player.gain_multiplier(kind)))


func _finish(outcome: RunResult.Outcome) -> void:
	if _flow.is_terminal():
		return
	var end_state := StageFlow.State.CLEARED if outcome == RunResult.Outcome.CLEARED else StageFlow.State.FAILED
	_flow.transition_to(end_state)
	_clock.stop()
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	var result := run_state.to_result(outcome)
	_hud.show_result(result)
	finished.emit(result)
```
Note: `_spawn_pickup` after the stage is freed (deferred call on a freed node) is dropped by the engine; no guard is needed.

`src/stage/stage.tscn`:
```
[gd_scene load_steps=14 format=3]

[ext_resource type="Script" path="res://src/stage/stage.gd" id="1_stage"]
[ext_resource type="PackedScene" path="res://src/stage/arena.tscn" id="2_arena"]
[ext_resource type="PackedScene" path="res://src/actors/building/building.tscn" id="3_building"]
[ext_resource type="PackedScene" path="res://src/actors/player/player.tscn" id="4_player"]
[ext_resource type="Script" path="res://src/stage/stage_clock.gd" id="5_clock"]
[ext_resource type="Script" path="res://src/components/target_provider.gd" id="6_targets"]
[ext_resource type="Script" path="res://src/stage/enemy_factory.gd" id="7_factory"]
[ext_resource type="Script" path="res://src/stage/spawn_director.gd" id="8_spawner"]
[ext_resource type="Script" path="res://src/stage/wave_director.gd" id="9_waves"]
[ext_resource type="PackedScene" path="res://src/ui/hud/hud.tscn" id="10_hud"]

[node name="Stage" type="Node2D"]
script = ExtResource("1_stage")

[node name="Arena" parent="." instance=ExtResource("2_arena")]

[node name="World" type="Node2D" parent="."]
y_sort_enabled = true

[node name="Pickups" type="Node2D" parent="World"]
z_index = -1

[node name="Building" parent="World" instance=ExtResource("3_building")]

[node name="Enemies" type="Node2D" parent="World"]
y_sort_enabled = true

[node name="Player" parent="World" instance=ExtResource("4_player")]

[node name="Projectiles" type="Node2D" parent="World"]
y_sort_enabled = true

[node name="Systems" type="Node" parent="."]

[node name="StageClock" type="Node" parent="Systems"]
script = ExtResource("5_clock")

[node name="TargetProvider" type="Node" parent="Systems"]
script = ExtResource("6_targets")

[node name="EnemyFactory" type="Node" parent="Systems"]
script = ExtResource("7_factory")

[node name="SpawnDirector" type="Node" parent="Systems"]
script = ExtResource("8_spawner")

[node name="WaveDirector" type="Node" parent="Systems"]
script = ExtResource("9_waves")

[node name="Hud" parent="." instance=ExtResource("10_hud")]
```

- [ ] **Step 5: Run to verify pass**

Run: `FILTER=tests/ui tools/run_tests.sh`, then `FILTER=tests/stage/test_stage.gd tools/run_tests.sh`
Expected: all PASS (the stage tests take ~10 s in total because they run in real time). Typical fixes:
- `Invalid call. Nonexistent function ... in base 'Nil'` inside `Stage.start` means a `@onready` path in `stage.gd` does not match `stage.tscn`; compare node names.
- "Can't change this state while flushing queries" errors: something adds a physics object from inside a physics signal — route it through `call_deferred` the way `_on_loot_dropped` does.

- [ ] **Step 6: Run the whole suite and commit**

Run: `tools/run_tests.sh` → everything PASS.
```bash
git add src/ui src/stage tests/ui tests/stage
git commit -q -m "feat(stage): add Stage composition root, HUD and timeline bar

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 13: Entry scene and in-editor acceptance

**Files:**
- Create: `src/app/main.gd`, `src/app/main.tscn`
- Modify (editor tools): project setting `rendering/textures/canvas_textures/default_texture_filter`

**Interfaces:**
- Consumes: `Stage`, `STAGE_DEF` (`stage_01.tres`), `PROFILE` (`default_profile.tres`), `InputActions.RESTART`.
- Produces: the project's main scene `res://src/app/main.tscn`; pressing **R** after a run restarts the stage.

- [ ] **Step 1: Implement Main**

`src/app/main.gd`:
```gdscript
extends Node
## Entry point: runs the default stage and restarts it on demand. Phase 4 replaces this with the menu flow.

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")
const PROFILE: PlayerProfile = preload("res://data/characters/default_profile.tres")

var _stage: Stage
var _result: RunResult


func _ready() -> void:
	_start_run()


func _unhandled_input(event: InputEvent) -> void:
	if _result != null and event.is_action_pressed(InputActions.RESTART):
		_start_run()


func _start_run() -> void:
	if _stage != null:
		remove_child(_stage)
		_stage.queue_free()
	_result = null
	_stage = STAGE_SCENE.instantiate() as Stage
	add_child(_stage)
	_stage.finished.connect(_on_finished)
	if not _stage.start(STAGE_DEF, PROFILE):
		get_tree().quit(1)


func _on_finished(result: RunResult) -> void:
	_result = result
	print("[Main] %s after %.1fs — kills %d, gold %d, stone %d" % [
		RunResult.Outcome.keys()[result.outcome], result.elapsed, result.kills, result.gold, result.stone,
	])
```

`src/app/main.tscn`:
```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://src/app/main.gd" id="1_main"]

[node name="Main" type="Node"]
script = ExtResource("1_main")
```

- [ ] **Step 2: Project settings**

Via godot-ai MCP `project_manage` `settings_set`: `application/run/main_scene` = `res://src/app/main.tscn` (if not already set in Task 2) and `rendering/textures/canvas_textures/default_texture_filter` = `0` (Nearest — crisp pixel art).

- [ ] **Step 3: Automated smoke run**

1. `tools/run_tests.sh` → everything PASS.
2. `project_run` (mode `main`); wait ~5 s; `logs_read` → expect **no** errors or warnings from our scripts (engine start-up noise from the editor/plugin is fine).
3. `editor_manage` `game_eval` with `return get_tree().get_nodes_in_group("enemies").size()` → a positive number after a few seconds.
4. Take a screenshot of the running game (godot-ai screenshot tool) and look at it: grass ground, house in the middle, soldier below it, HUD bars top-left, timeline bar top-centre with 5 amber diamonds.
5. `project_manage` `stop`.

- [ ] **Step 4: Manual acceptance checklist** (play it for ~3 minutes; every line is a pass/fail)

- WASD/arrow keys move the soldier and flip the sprite; the camera follows and stops at the arena edge.
- With an enemy within ~260 px the soldier fires arrows automatically (~every 0.7 s); arrows fly toward the nearest enemy and damage it (enemy flashes red).
- Space dashes in the movement direction; enemies' hits do nothing during the dash; a second dash is refused for ~1.2 s.
- Orcs walk to the house and hit it (house bar drains); swarmers chase the soldier.
- Enemies die with their death animation, drop coins (and sometimes crystals); walking near pulls them in and the HUD counters rise.
- The timeline bar fills as time passes; at 30/60/90/120/150 s a visibly larger group arrives and the diamond turns grey.
- Letting the house reach 0 HP (or the soldier dying) shows "GAME OVER"; surviving 180 s shows "STAGE CLEARED"; **R** restarts with an empty wallet.
- Frame rate stays smooth during the biggest wave: `editor_manage` `monitors_get` (FPS) shows ≥ 55 with ~80 enemies; if not, note the number — it decides whether the pooling seam in `EnemyFactory` is needed next.

Record anything that fails as a fix inside this task (use `superpowers:systematic-debugging`) before committing. Typical visual tuning knobs live in data, not code: `VisualDef.offset` (sprite vs. collision circle), `Camera2D.zoom` in `player.tscn`, enemy stats in `data/enemies/*.tres`, wave counts in `data/waves/stage_01_waves.tres`.

- [ ] **Step 5: Commit**

```bash
git add src/app project.godot
git commit -q -m "feat(app): add entry scene with restart, set main scene and pixel filtering

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 14: Documentation sync

**Files:**
- Modify: `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`
- Create: `CLAUDE.md`

- [ ] **Step 1: Update the spec to match what was built**

Edit the spec so it no longer contradicts the code:
1. Section 3.1 folder tree: move `loot_collector` under `actors/pickup/`, `target_provider` under `components/`, replace "SpriteFrames .tres live in assets" with "sprites are described by `AnimationStrip` resources in `VisualDef` and built into `SpriteFrames` at runtime", drop `fx_spawner` from the Phase 1 list (add it to Phase 3), and list `animation_strip`, `stat_block`, `drop_entry`, `drop_roll`, `wave_entry` among the defs.
2. Section 1.3: add default 6 — "player death also ends the run as FAILED".
3. Section 5: replace "framework chosen in the plan" with "an in-repo headless runner (`tests/run_tests.tscn`, `tools/run_tests.sh`); the editor MCP runner cannot call non-`@tool` scripts".
4. Section 4 Phase 1: note "Soldier + Orc + swarmer content, 5 waves, 180 s, no boss yet (time up = CLEARED)".

- [ ] **Step 2: Write `CLAUDE.md`**

```markdown
# vampire-survivor-ai

Top-down 2D vampire-survivor-like prototype in Godot 4.7 (GDScript) on top of GodotGAS.
Design: `docs/superpowers/specs/2026-10-13-vampire-survivor-base-design.md`. Plans: `docs/superpowers/plans/`.

## Commands
- Run all tests: `tools/run_tests.sh` (`FILTER=<path substring>` for one file; `IMPORT=1` if the editor is closed).
- Rebuild gameplay tags after editing `TAGS` in `tools/sync_tags.gd`:
  `Godot --headless --path . -s res://tools/sync_tags.gd`.

## Architecture rules
- Layers (a file may only use layers below it): `core → defs → gas → components → actors → stage/abilities/boss → ui → app/meta`.
  Upward communication is signals only. `Stage` is the composition root and wires everything.
- Components never find their neighbours: actors inject dependencies via `bind(...)`.
- All content is `.tres` in `data/`, typed by `src/defs/`; no balance numbers in code.
- Damage always flows through `EffectBuilder.damage` -> GAS `GameplayEffect`. Never edit `addons/` (symlinked shared plugins).
- Every actor has an `AbilitySystemComponent` child named exactly that (instance `src/gas/actor_asc.tscn`);
  apply stats only after the node is in the tree; call `asc.cleanup()` before freeing.
- Add physics objects from physics callbacks with `call_deferred`.

## Adding an enemy (data only)
1. Duplicate `data/enemies/orc.tres`; change `id`, stats, tint/scale, contact values, `target_policy`.
2. Add it to `StageDef.trickle_enemies` or a `WaveEntry`. `tests/data/test_game_data.gd` validates it.
```

- [ ] **Step 3: Verify and commit**

Run: `tools/run_tests.sh` → all PASS, then:
```bash
git add CLAUDE.md docs
git commit -q -m "docs: sync spec with Phase 1 implementation and add CLAUDE.md

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage (Phase 1 scope, spec §1.1/§3/§4):** player arrow + dash with cooldown and invulnerability (Tasks 4, 9); central building and game over (8, 12); trickle + periodic waves with timeline bar (10, 12); gold/stone drops, pickups, wallet (6, 7, 12); stage CLEARED on time-up (12); stats/damage through GAS (4); data-driven content with validation (3, 11); composition root + typed signals + layering (12); save/meta seam `Stage.start(StageDef, PlayerProfile) -> finished(RunResult)` (12); asset seams `VisualDef`/`ProjectileDef` (3, 11); reproducible `RunRng` (2). Explicitly out of scope until later phases: abilities/grades/shop (Phase 2), events and boss (Phase 3), menus/skill tree/save (Phase 4), sound.

**Review Focus mapping:** 1 → Task 12 `test_finish_is_idempotent`; 2 → Task 3 tests and Task 12 `test_invalid_defs_are_rejected...`; 3 → Task 9 `test_dash_grants_invulnerability_only_while_active`; 4 → Task 7 `test_projectile_survives_freed_shooter`, Task 8 `test_dead_enemy_is_not_targetable`/`test_dead_enemy_stops_dealing_damage`; 5 → Task 9 `test_arrow_without_target_does_not_start_cooldown`; 6 → Task 12 `test_restart_starts_clean`.

**Known risks the executor should expect:** hand-written `.tscn/.tres` text may need small corrections on first load (the plan says what to compare); sprite/collision offsets need visual tuning in Task 13; real-time tests (`wait_seconds`) can be slow but are deterministic in outcome.
