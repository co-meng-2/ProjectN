extends Node
## Headless test runner.
## Usage: Godot --headless --path . res://tests/run_tests.tscn -- --filter=substring

const TEST_ROOT: String = "res://tests"
const TEST_FILE_PREFIX: String = "test_"
const TEST_METHOD_PREFIX: String = "test_"
const SUPPORT_DIR: String = "support"
const TEST_TIMEOUT_SECONDS: float = 10.0

var _token: int = 0
var _finished_token: int = -1


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
		if await _run_with_timeout(test, method_name):
			test.failures.append("timed out after %.0f s" % TEST_TIMEOUT_SECONDS)
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


## Runs one test; returns true when it did not finish within TEST_TIMEOUT_SECONDS.
## A timed-out coroutine is abandoned; the per-test token keeps its late finish from counting for the next test.
func _run_with_timeout(test: TestCase, method_name: String) -> bool:
	_token += 1
	var token := _token
	_run_body(test, method_name, token)
	var timer := get_tree().create_timer(TEST_TIMEOUT_SECONDS)
	while _finished_token != token and timer.time_left > 0.0:
		await get_tree().process_frame
	return _finished_token != token


func _run_body(test: TestCase, method_name: String, token: int) -> void:
	await test.call(method_name)
	_finished_token = token
