class_name WorldHealthBar
extends Node2D
## Health bar drawn in world space above an actor. Read-only: it observes `Health` and follows its anchor.

@export var size: Vector2 = Vector2(80, 8)
@export var background_colour: Color = Color(0.1, 0.1, 0.1, 0.8)
@export var border_colour: Color = Color(0, 0, 0, 1)
@export var fill_colour_high: Color = Color(0.3, 0.8, 0.3)
@export var fill_colour_mid: Color = Color(0.95, 0.6, 0.15)
@export var fill_colour_low: Color = Color(0.85, 0.15, 0.15)
@export var always_visible: bool = true

var _current: float = 0.0
var _maximum: float = 0.0
var _anchor: Node2D
var _offset: Vector2 = Vector2.ZERO


func bind(health: Health, anchor: Node2D, offset: Vector2) -> void:
	_anchor = anchor
	_offset = offset
	health.changed.connect(_on_health_changed)
	_on_health_changed(health.current(), health.maximum())
	step()


func _process(_delta: float) -> void:
	step()


## Follows the anchor and refreshes visibility; `_process` calls it every frame.
func step() -> void:
	if _anchor == null or not is_instance_valid(_anchor):
		visible = false
		return
	global_position = _anchor.global_position + _offset
	visible = always_visible or fill_ratio() < 1.0


func fill_ratio() -> float:
	if _maximum <= 0.0:
		return 0.0
	return clampf(_current / _maximum, 0.0, 1.0)


func colour_for(ratio: float) -> Color:
	if ratio > 0.6:
		return fill_colour_high
	if ratio > 0.3:
		return fill_colour_mid
	return fill_colour_low


func _on_health_changed(current: float, maximum: float) -> void:
	_current = current
	_maximum = maximum
	queue_redraw()


func _draw() -> void:
	var rect := Rect2(-size.x * 0.5, -size.y * 0.5, size.x, size.y)
	draw_rect(rect, background_colour)
	var ratio := fill_ratio()
	draw_rect(Rect2(rect.position, Vector2(rect.size.x * ratio, rect.size.y)), colour_for(ratio))
	draw_rect(rect, border_colour, false, 1.0)
