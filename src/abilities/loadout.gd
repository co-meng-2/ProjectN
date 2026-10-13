class_name Loadout
extends RefCounted
## The player's N ability slots. Writing a slot announces it; the installer and the HUD react to the signal.

signal slot_changed(index: int, slot: AbilitySlot)

var _slots: Array[AbilitySlot] = []


func _init(slot_count: int) -> void:
	for i in maxi(slot_count, 0):
		_slots.append(AbilitySlot.new())


func size() -> int:
	return _slots.size()


## null when `index` is out of range.
func slot(index: int) -> AbilitySlot:
	if index < 0 or index >= _slots.size():
		return null
	return _slots[index]


## Returns false (and changes nothing) for an out-of-range index. `def == null` empties the slot.
## Always emits on success, even when the content is identical: a reroll onto the same ability reinstalls it.
func set_slot(index: int, def: AbilityDef, grade: Grade.Value) -> bool:
	var target := slot(index)
	if target == null:
		return false
	target.def = def
	target.grade = grade if def != null else Grade.Value.NONE
	slot_changed.emit(index, target)
	return true
