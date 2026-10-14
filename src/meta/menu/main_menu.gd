extends Control
## Start / Settings / Quit. Holds no state: it asks the router to move on. 4b replaces Start with the hub.

const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")

## Defaults to the SceneRouter autoload in _ready; tests set it first.
var router: Node

@onready var _start_button: Button = %StartButton
@onready var _settings_button: Button = %SettingsButton
@onready var _quit_button: Button = %QuitButton


func _ready() -> void:
	if router == null:
		router = SceneRouter
	_start_button.pressed.connect(_on_start_pressed)
	_settings_button.pressed.connect(_on_settings_pressed)
	_quit_button.pressed.connect(_on_quit_pressed)
	_start_button.grab_focus()


func _on_start_pressed() -> void:
	router.go(SceneRouter.Route.STAGE, {SceneRouter.PARAM_STAGE_DEF: STAGE_DEF})


func _on_settings_pressed() -> void:
	router.go(SceneRouter.Route.SETTINGS)


func _on_quit_pressed() -> void:
	get_tree().quit()
