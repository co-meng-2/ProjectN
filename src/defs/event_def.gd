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
