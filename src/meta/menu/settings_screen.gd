extends Control
## Master-volume slider. Changes apply immediately; leaving the screen saves once.

## Default to the autoloads in _ready; tests set them first.
var save_service: Node
var router: Node

@onready var _volume_slider: HSlider = %VolumeSlider
@onready var _back_button: Button = %BackButton


func _ready() -> void:
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	_volume_slider.value = save_service.data.settings.master_volume
	_volume_slider.value_changed.connect(_on_volume_changed)
	_back_button.pressed.connect(_on_back_pressed)
	_back_button.grab_focus()


func _on_volume_changed(value: float) -> void:
	save_service.data.settings.master_volume = value
	AudioSettings.apply(save_service.data.settings)


func _on_back_pressed() -> void:
	save_service.save()
	router.go(SceneRouter.Route.MAIN_MENU)
