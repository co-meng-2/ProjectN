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
