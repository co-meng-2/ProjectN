class_name BossPatternDef
extends Resource
## One attack in a boss's repertoire: the ability scene that performs it and its weight in RANDOM order.
## The root must extend ActorAbility with a unique ability_tag; defs/ cannot name ActorAbility, so that
## check is BossBrain.validate_def (run by Stage.start). Here: the root is a GameplayAbility and passes
## its own validate().

@export var ability_scene: PackedScene
@export var weight: float = 1.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if weight <= 0.0:
		errors.append("BossPatternDef: weight must be > 0")
	if ability_scene == null:
		errors.append("BossPatternDef: ability_scene is null")
		return errors
	var root := ability_scene.instantiate()
	if root == null:
		errors.append("BossPatternDef: ability_scene cannot be instantiated")
		return errors
	if not (root is GameplayAbility):
		errors.append("BossPatternDef: ability_scene root must extend GameplayAbility")
	elif root.has_method("validate"):
		errors.append_array(root.call("validate") as PackedStringArray)
	root.free()
	return errors
