class_name TimelineBar
extends Control
## Stage progress with a diamond for every scheduled wave; passed waves turn grey.

@export var track_color: Color = Color(0.12, 0.12, 0.16, 0.85)
@export var fill_color: Color = Color(0.35, 0.7, 1.0)
@export var mark_color: Color = Color(1.0, 0.75, 0.2)
@export var passed_mark_color: Color = Color(0.55, 0.55, 0.55)

const MARK_HALF_WIDTH: float = 5.0

var _duration: float = 1.0
var _marks: PackedFloat32Array = PackedFloat32Array()
var _elapsed: float = 0.0


func configure(duration: float, marks: PackedFloat32Array) -> void:
	_duration = duration
	_marks = marks
	_elapsed = 0.0
	queue_redraw()


func set_elapsed(elapsed: float) -> void:
	_elapsed = elapsed
	queue_redraw()


func mark_x(mark: float) -> float:
	if _duration <= 0.0:
		return 0.0
	return size.x * clampf(mark / _duration, 0.0, 1.0)


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), track_color)
	var ratio := 1.0 if _duration <= 0.0 else clampf(_elapsed / _duration, 0.0, 1.0)
	draw_rect(Rect2(Vector2.ZERO, Vector2(size.x * ratio, size.y)), fill_color)
	for mark in _marks:
		var x := mark_x(mark)
		var color := passed_mark_color if mark <= _elapsed else mark_color
		draw_colored_polygon(PackedVector2Array([
			Vector2(x, 0.0),
			Vector2(x + MARK_HALF_WIDTH, size.y * 0.5),
			Vector2(x, size.y),
			Vector2(x - MARK_HALF_WIDTH, size.y * 0.5),
		]), color)
