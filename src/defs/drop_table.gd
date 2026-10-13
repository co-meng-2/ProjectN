class_name DropTable
extends Resource
## What an enemy may drop on death.

@export var entries: Array[DropEntry] = []


func roll(rng: RunRng) -> Array[DropRoll]:
	var rolls: Array[DropRoll] = []
	for entry in entries:
		if not rng.chance(entry.chance):
			continue
		var drop := DropRoll.new()
		drop.kind = entry.kind
		drop.icon = entry.icon
		drop.amount = rng.randi_range(entry.min_amount, entry.max_amount)
		rolls.append(drop)
	return rolls


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	for entry in entries:
		if entry == null:
			errors.append("DropTable: null entry")
		else:
			errors.append_array(entry.validate())
	return errors
