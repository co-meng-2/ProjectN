class_name StageProgress
extends RefCounted
## Which stages are locked, available or cleared. Derived from the catalog order and the save; stores nothing.

enum Status { LOCKED, AVAILABLE, CLEARED }

var _catalog: StageCatalog
var _save: SaveData


func _init(catalog: StageCatalog, save: SaveData) -> void:
	_catalog = catalog
	_save = save


## CLEARED if the stage id is saved as cleared; else AVAILABLE if it is first or the previous stage is cleared; else LOCKED.
func status(index: int) -> Status:
	if index < 0 or index >= _catalog.stages.size():
		return Status.LOCKED
	if _save.cleared_stages.has(_catalog.stages[index].id):
		return Status.CLEARED
	if index == 0 or _save.cleared_stages.has(_catalog.stages[index - 1].id):
		return Status.AVAILABLE
	return Status.LOCKED


## A cleared stage stays playable.
func is_unlocked(index: int) -> bool:
	return status(index) != Status.LOCKED
