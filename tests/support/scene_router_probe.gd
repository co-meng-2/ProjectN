extends "res://src/app/scene_router.gd"
## Test double: records scene changes instead of performing them.

var changed_to: Array[String] = []
var next_error: Error = OK


func _change_scene(path: String) -> Error:
	changed_to.append(path)
	return next_error
