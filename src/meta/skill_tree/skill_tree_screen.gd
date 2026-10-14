extends Control
## The skill-tree screen: gem balance, one row per node, Back. It owns no state: purchases go through
## SkillTreeProgress, then the save is written once and the rows are rebuilt.

const ROW_SCENE: PackedScene = preload("res://src/meta/skill_tree/skill_node_row.tscn")
const DEFAULT_TREE: SkillTreeDef = preload("res://data/skill_tree/skill_tree.tres")

## Default to the shipped tree and the autoloads in _ready; tests set them first.
var tree: SkillTreeDef
var save_service: Node
var router: Node

var _progress: SkillTreeProgress

@onready var _gem_label: Label = %GemLabel
@onready var _node_list: VBoxContainer = %NodeList
@onready var _empty_label: Label = %EmptyLabel
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if tree == null:
		tree = DEFAULT_TREE
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_back_button.pressed.connect(_on_back_pressed)
	var errors := tree.validate()
	if not errors.is_empty():
		push_error("SkillTreeScreen: unusable skill tree: %s" % ", ".join(errors))
		_empty_label.visible = true
		_gem_label.text = "Gem %d" % save_service.data.gem
		_back_button.grab_focus()
		return
	_progress = SkillTreeProgress.new(tree, save_service.data)
	_rebuild()


## `preferred` is the row to keep focused (the one just bought) while it can still be bought, so repeated Enter
## levels the same node instead of jumping to another one.
func _rebuild(preferred: int = -1) -> void:
	for child in _node_list.get_children():
		_node_list.remove_child(child)
		child.queue_free()
	_gem_label.text = "Gem %d" % save_service.data.gem
	var focus_row: SkillNodeRow = null
	for index in tree.nodes.size():
		var node := tree.nodes[index]
		var row := ROW_SCENE.instantiate() as SkillNodeRow
		_node_list.add_child(row)
		var can_buy := _progress.can_buy(index)
		row.setup(index, node.display_name, node.description, _detail_text(index), _buy_text(index), can_buy)
		row.buy_pressed.connect(_on_buy_pressed)
		if can_buy and (focus_row == null or index == preferred):
			focus_row = row
	if focus_row != null:
		focus_row.focus_buy()
	else:
		_back_button.grab_focus()


func _detail_text(index: int) -> String:
	var node := tree.nodes[index]
	match _progress.status(index):
		SkillTreeProgress.Status.LOCKED:
			var names: PackedStringArray = []
			for required in node.requires:
				var required_node := tree.node_by_id(required)
				names.append(required_node.display_name if required_node != null else String(required))
			return "Requires: %s" % ", ".join(names)
		SkillTreeProgress.Status.MAXED:
			return "Lv %d/%d   Max" % [node.max_level, node.max_level]
		_:
			return "Lv %d/%d   Cost %d" % [_progress.level(index), node.max_level, _progress.next_cost(index)]


func _buy_text(index: int) -> String:
	return "Max" if _progress.status(index) == SkillTreeProgress.Status.MAXED else "Buy"


func _on_buy_pressed(index: int) -> void:
	if _progress == null or not _progress.buy(index):
		return
	save_service.save()
	_rebuild(index)


func _on_back_pressed() -> void:
	router.go(SceneRouter.Route.HUB)
