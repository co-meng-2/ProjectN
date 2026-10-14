class_name StageCatalog
extends Resource
## The ordered list of stages. Order is unlock order: a stage opens once the one before it is cleared.

@export var stages: Array[StageDef] = []


## Index of the stage with this id, or -1.
func index_of(id: StringName) -> int:
	for index in stages.size():
		if stages[index] != null and stages[index].id == id:
			return index
	return -1


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if stages.is_empty():
		errors.append("StageCatalog: no stages")
	var seen: Dictionary[StringName, bool] = {}
	for stage in stages:
		if stage == null:
			errors.append("StageCatalog: null stage")
			continue
		if seen.has(stage.id):
			errors.append("StageCatalog: duplicate stage id '%s'" % stage.id)
		seen[stage.id] = true
		errors.append_array(stage.validate())
	return errors
