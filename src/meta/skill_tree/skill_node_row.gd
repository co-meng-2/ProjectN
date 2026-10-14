class_name SkillNodeRow
extends HBoxContainer
## One skill-tree row: name, detail line and a Buy button. It only reports the click.

signal buy_pressed(index: int)

var _index: int = -1

@onready var _name_label: Label = %NameLabel
@onready var _detail_label: Label = %DetailLabel
@onready var _buy_button: Button = %BuyButton


func _ready() -> void:
	_buy_button.pressed.connect(_on_buy_pressed)


## Call after the row is in the tree.
func setup(index: int, title: String, tooltip: String, detail: String, buy_text: String, buy_enabled: bool) -> void:
	_index = index
	_name_label.text = title
	_name_label.tooltip_text = tooltip
	_detail_label.text = detail
	_buy_button.text = buy_text
	_buy_button.disabled = not buy_enabled


func focus_buy() -> void:
	_buy_button.grab_focus()


func _on_buy_pressed() -> void:
	buy_pressed.emit(_index)
