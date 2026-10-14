class_name StageCard
extends HBoxContainer
## One stage row in the hub: name, status and a Play button. It only reports the click.

signal play_pressed(index: int)

var _index: int = -1

@onready var _name_label: Label = %NameLabel
@onready var _status_label: Label = %StatusLabel
@onready var _play_button: Button = %PlayButton


func _ready() -> void:
	_play_button.pressed.connect(_on_play_pressed)


## Call after the card is in the tree.
func setup(index: int, stage_name: String, status: StageProgress.Status) -> void:
	_index = index
	_name_label.text = stage_name
	_status_label.text = _status_text(status)
	_play_button.disabled = status == StageProgress.Status.LOCKED


func focus_play() -> void:
	_play_button.grab_focus()


func _status_text(status: StageProgress.Status) -> String:
	match status:
		StageProgress.Status.LOCKED:
			return "Locked"
		StageProgress.Status.CLEARED:
			return "Cleared"
		_:
			return ""


func _on_play_pressed() -> void:
	play_pressed.emit(_index)
