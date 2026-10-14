class_name SkillNodeDef
extends Resource
## One skill-tree node. Buying a level costs gem (`costs[level]` for the next level). A node's `effect` is an INFINITE
## GameplayEffect authored like an ability passive: modifiers scale with the purchased level through `scaling_curve`
## with magnitude 1.0. `slot_bonus` (per level) raises the player's ability slot count, which is not a GAS attribute.

@export var id: StringName
@export var display_name: String = ""
@export_multiline var description: String = ""
## Ids of nodes that must have level >= 1 before this one can be bought. Only nodes listed earlier in the tree.
@export var requires: Array[StringName] = []
@export var max_level: int = 1
## Gem cost of each level: costs[0] buys level 1.
@export var costs: PackedInt32Array = PackedInt32Array()
@export var effect: GameplayEffect
@export var slot_bonus: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("SkillNodeDef: id is empty")
	if display_name.is_empty():
		errors.append("SkillNodeDef '%s': display_name is empty" % id)
	if max_level < 1:
		errors.append("SkillNodeDef '%s': max_level must be >= 1" % id)
	elif costs.size() != max_level:
		errors.append("SkillNodeDef '%s': costs needs one entry per level (%d)" % [id, max_level])
	for cost in costs:
		if cost <= 0:
			errors.append("SkillNodeDef '%s': every cost must be > 0" % id)
			break
	if slot_bonus < 0:
		errors.append("SkillNodeDef '%s': slot_bonus must be >= 0" % id)
	if effect == null and slot_bonus <= 0:
		errors.append("SkillNodeDef '%s': needs an effect and/or a slot_bonus" % id)
	if requires.has(id):
		errors.append("SkillNodeDef '%s': cannot require itself" % id)
	if effect != null:
		errors.append_array(PassiveEffectCheck.validate(effect, "SkillNodeDef '%s': effect" % id))
	return errors
