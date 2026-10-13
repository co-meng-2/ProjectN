class_name BossBar
extends VBoxContainer
## The boss's name and health bar, shown once the boss appears. A view over its Health.

@export var fill_color: Color = Color(0.85, 0.2, 0.2)
@export var bar_size: Vector2 = Vector2(400, 14)

var _name_label := Label.new()
var _bar := ProgressBar.new()
var _health: Health


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_bar.custom_minimum_size = bar_size
	_bar.show_percentage = false
	_bar.self_modulate = fill_color
	add_child(_name_label)
	add_child(_bar)
	visible = false


func show_boss(health: Health, display_name: String) -> void:
	if is_instance_valid(_health) and _health.changed.is_connected(_on_health_changed):
		_health.changed.disconnect(_on_health_changed)
	_health = health
	_name_label.text = display_name
	health.changed.connect(_on_health_changed)
	_on_health_changed(health.current(), health.maximum())
	visible = true


func boss_name() -> String:
	return _name_label.text


func value() -> float:
	return _bar.value


func maximum() -> float:
	return _bar.max_value


func _on_health_changed(current: float, maximum_value: float) -> void:
	_bar.max_value = maxf(maximum_value, 1.0)
	_bar.value = current
