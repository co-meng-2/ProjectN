class_name AbilitySlot
extends RefCounted
## One HUD slot's content: which ability (null = empty) at which grade.

var def: AbilityDef
var grade: Grade.Value = Grade.Value.NONE


func is_empty() -> bool:
	return def == null
