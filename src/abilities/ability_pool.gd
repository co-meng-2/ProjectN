class_name AbilityPool
extends RefCounted
## Which abilities a grade can roll: every def whose `min_grade` is at or below that grade.

var _defs: Array[AbilityDef] = []


func _init(defs: Array[AbilityDef]) -> void:
	for def in defs:
		if def != null:
			_defs.append(def)


func defs_for(grade: Grade.Value) -> Array[AbilityDef]:
	var result: Array[AbilityDef] = []
	if not Grade.is_rollable(grade):
		return result
	for def in _defs:
		if def.min_grade <= grade:
			result.append(def)
	return result


## A random def of the grade's pool, or null when it is empty. With `exclude`, a different def is
## preferred, but the excluded one is returned when it is the only candidate.
func roll(grade: Grade.Value, rng: RunRng, exclude: AbilityDef = null) -> AbilityDef:
	var candidates := defs_for(grade)
	if exclude != null and candidates.size() > 1:
		candidates.erase(exclude)
	return rng.pick(candidates) as AbilityDef
