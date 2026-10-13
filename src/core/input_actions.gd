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
