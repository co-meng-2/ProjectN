class_name PlayerProfile
extends Resource
## The player's starting stats, look and ability catalog for a run. Phase 4 derives this from the skill tree.

@export var stats: StatBlock
@export var visual: VisualDef
## Ability slots shown in the HUD.
@export var slot_count: int = 6
## The catalog the grade pools are built from.
@export var abilities: Array[AbilityDef] = []


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
	if slot_count < 1:
		errors.append("PlayerProfile: slot_count must be >= 1")
	var seen: Dictionary[StringName, bool] = {}
	for ability in abilities:
		if ability == null:
			errors.append("PlayerProfile: null ability")
			continue
		if seen.has(ability.id):
			errors.append("PlayerProfile: duplicate ability id '%s'" % ability.id)
		seen[ability.id] = true
		errors.append_array(ability.validate())
	return errors
