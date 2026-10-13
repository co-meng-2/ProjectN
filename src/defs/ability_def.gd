class_name AbilityDef
extends Resource
## One ability the player can hold in a slot. A passive is an INFINITE GameplayEffect whose modifiers
## scale with the grade level through `scaling_curve`; an active ability is a scene whose root extends
## GameplayAbility (ActorAbility in this project). A hybrid has both.

@export var id: StringName
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var icon: Texture2D
## Optional icon of the revealed form; without it the view tints `icon`.
@export var revealed_icon: Texture2D
## Lowest grade whose pool offers this ability.
@export var min_grade: Grade.Value = Grade.Value.COMMON
@export var passive_effect: GameplayEffect
@export var ability_scene: PackedScene


func icon_for(grade: Grade.Value) -> Texture2D:
	if grade == Grade.Value.REVEALED and revealed_icon != null:
		return revealed_icon
	return icon


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("AbilityDef: id is empty")
	if display_name.is_empty():
		errors.append("AbilityDef '%s': display_name is empty" % id)
	if icon == null:
		errors.append("AbilityDef '%s': icon is null" % id)
	if not Grade.is_rollable(min_grade):
		errors.append("AbilityDef '%s': min_grade must be COMMON..LEGENDARY" % id)
	if passive_effect == null and ability_scene == null:
		errors.append("AbilityDef '%s': needs a passive_effect and/or an ability_scene" % id)
	if passive_effect != null:
		errors.append_array(_validate_passive())
	if ability_scene != null:
		errors.append_array(_validate_scene())
	return errors


func _validate_passive() -> PackedStringArray:
	var errors := PackedStringArray()
	if passive_effect.policy != GameplayEffect.DurationPolicy.INFINITE:
		errors.append("AbilityDef '%s': passive_effect must be INFINITE" % id)
	for modifier in passive_effect.modifiers:
		if modifier == null:
			errors.append("AbilityDef '%s': null modifier" % id)
		elif modifier.attribute_name == "":
			errors.append("AbilityDef '%s': modifier without attribute_name" % id)
		elif modifier.scaling_curve == null:
			errors.append("AbilityDef '%s': modifier '%s' needs a scaling_curve over the grade level" % [id, modifier.attribute_name])
		elif is_zero_approx(modifier.magnitude):
			# GAS multiplies the curve sample by `magnitude`; 0 would silently zero every grade.
			errors.append("AbilityDef '%s': modifier '%s' has magnitude 0 (use 1.0 with a curve)" % [id, modifier.attribute_name])
	return errors


func _validate_scene() -> PackedStringArray:
	var errors := PackedStringArray()
	var root := ability_scene.instantiate()
	if root == null:
		errors.append("AbilityDef '%s': ability_scene cannot be instantiated" % id)
		return errors
	if not (root is GameplayAbility):
		errors.append("AbilityDef '%s': ability_scene root must extend GameplayAbility" % id)
	elif root.has_method("validate"):
		errors.append_array(root.call("validate") as PackedStringArray)
	root.free()
	return errors
