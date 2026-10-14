extends Control
## The hub: one card per catalog stage, Back, and a reserved Skill Tree button (4c). It owns no state: it asks the
## router to move on. A stage opens once the previous one is cleared (see StageProgress).

const STAGE_CARD_SCENE: PackedScene = preload("res://src/meta/hub/stage_card.tscn")
const DEFAULT_CATALOG: StageCatalog = preload("res://data/stages/stage_catalog.tres")

## Default to the shipped catalog and the autoloads in _ready; tests set them first.
var catalog: StageCatalog
var save_service: Node
var router: Node

var _progress: StageProgress

@onready var _stage_list: VBoxContainer = %StageList
@onready var _empty_label: Label = %EmptyLabel
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if catalog == null:
		catalog = DEFAULT_CATALOG
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_back_button.pressed.connect(_on_back_pressed)
	_back_button.grab_focus()
	var errors := catalog.validate()
	if not errors.is_empty():
		push_error("Hub: unusable stage catalog: %s" % ", ".join(errors))
		_empty_label.visible = true
		return
	_progress = StageProgress.new(catalog, save_service.data)
	for index in catalog.stages.size():
		var card := STAGE_CARD_SCENE.instantiate() as StageCard
		_stage_list.add_child(card)
		card.setup(index, catalog.stages[index].display_name, _progress.status(index))
		card.play_pressed.connect(_on_play_pressed)


func _on_play_pressed(index: int) -> void:
	if _progress == null or not _progress.is_unlocked(index):
		return
	router.go(SceneRouter.Route.STAGE, {SceneRouter.PARAM_STAGE_DEF: catalog.stages[index]})


func _on_back_pressed() -> void:
	router.go(SceneRouter.Route.MAIN_MENU)
