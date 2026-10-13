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


func next_physics_frame() -> void:
	await host.get_tree().physics_frame


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
