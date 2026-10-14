class_name SkillTreeProgress
extends RefCounted
## Which skill nodes are locked, available or maxed, and the purchase rule. Derived from the tree and the save; it
## stores nothing itself (levels and gem live in SaveData).

enum Status { LOCKED, AVAILABLE, MAXED }

var _tree: SkillTreeDef
var _save: SaveData


func _init(tree: SkillTreeDef, save: SaveData) -> void:
	_tree = tree
	_save = save


func level(index: int) -> int:
	if not _in_range(index):
		return 0
	return _save.level_of(_tree.nodes[index].id)


## MAXED at max level (a saved level above the max counts); else AVAILABLE when every required node has level >= 1; else LOCKED.
func status(index: int) -> Status:
	if not _in_range(index):
		return Status.LOCKED
	var node := _tree.nodes[index]
	if _save.level_of(node.id) >= node.max_level:
		return Status.MAXED
	for required in node.requires:
		if _save.level_of(required) < 1:
			return Status.LOCKED
	return Status.AVAILABLE


## Gem cost of the next level, or -1 when maxed, out of range, or the costs list is shorter than the level.
func next_cost(index: int) -> int:
	if not _in_range(index):
		return -1
	var node := _tree.nodes[index]
	var current := _save.level_of(node.id)
	if current >= node.max_level or current >= node.costs.size():
		return -1
	return node.costs[current]


func can_buy(index: int) -> bool:
	return status(index) == Status.AVAILABLE and next_cost(index) >= 0 and _save.gem >= next_cost(index)


## All-or-nothing: deducts the gem and raises the level together, or changes nothing.
func buy(index: int) -> bool:
	if not can_buy(index):
		return false
	var node := _tree.nodes[index]
	var current := _save.level_of(node.id)
	if not _save.spend_gem(next_cost(index)):
		return false
	_save.set_level(node.id, current + 1)
	return true


func _in_range(index: int) -> bool:
	return index >= 0 and index < _tree.nodes.size() and _tree.nodes[index] != null
