class_name WaveSchedule
extends Resource
## Ordered surges of a stage. Entries may be authored in any order.

@export var entries: Array[WaveEntry] = []


func sorted_entries() -> Array[WaveEntry]:
	var sorted: Array[WaveEntry] = entries.duplicate()
	sorted.sort_custom(func(a: WaveEntry, b: WaveEntry) -> bool: return a.time < b.time)
	return sorted


func times() -> PackedFloat32Array:
	var result := PackedFloat32Array()
	for entry in sorted_entries():
		result.append(entry.time)
	return result


func validate_against(duration: float) -> PackedStringArray:
	var errors := PackedStringArray()
	for entry in entries:
		if entry == null:
			errors.append("WaveSchedule: null entry")
			continue
		if entry.enemy == null:
			errors.append("WaveEntry '%s': enemy is null" % entry.label)
		else:
			errors.append_array(entry.enemy.validate())
		if entry.count <= 0:
			errors.append("WaveEntry '%s': count must be > 0" % entry.label)
		if entry.time < 0.0 or entry.time >= duration:
			errors.append("WaveEntry '%s': time %s is outside [0, stage duration %s)" % [entry.label, entry.time, duration])
	return errors
