extends Control
## Boot screen: shows the title briefly, then goes to the main menu. Any key press skips ahead.

const HOLD_SECONDS: float = 1.5

## Defaults to the SceneRouter autoload in _ready; tests set it first.
var router: Node

var _done: bool = false


func _ready() -> void:
	if router == null:
		router = SceneRouter
	get_tree().create_timer(HOLD_SECONDS).timeout.connect(_proceed)


func _unhandled_input(event: InputEvent) -> void:
	if event.is_pressed():
		_proceed()


func _proceed() -> void:
	if _done:
		return
	_done = true
	router.go(SceneRouter.Route.MAIN_MENU)
