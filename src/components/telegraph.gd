class_name Telegraph
extends Node2D
## A warning drawn on the ground — a circle or a line that fills up over `seconds` — then `elapsed` and
## free. Purely visual: whoever shows it keeps its own timer and decides what happens when the warning ends.

signal elapsed

enum Shape { CIRCLE, LINE }

const SEGMENTS: int = 40
const EDGE_WIDTH: float = 2.0

@export var fill_color: Color = Color(1.0, 0.2, 0.15, 0.25)
@export var edge_color: Color = Color(1.0, 0.3, 0.2, 0.9)

var shape: Shape = Shape.CIRCLE
var radius: float = 0.0
var line_offset: Vector2 = Vector2.ZERO
var line_width: float = 0.0
var _duration: float = 0.0
var _left: float = 0.0
var _armed: bool = false


## Adds a circular warning centred on `position` (global) under `parent` and starts it. The parent must be
## in the tree. Returns the telegraph so the caller can connect `elapsed` or free it early.
static func spawn_circle(parent: Node, position: Vector2, p_radius: float, seconds: float) -> Telegraph:
	var telegraph := Telegraph.new()
	parent.add_child(telegraph)
	telegraph.global_position = position
	telegraph.show_circle(p_radius, seconds)
	return telegraph


## Adds a line warning from global `from` to global `to`, `width` px wide, under `parent` and starts it.
## The parent must be in the tree. Returns the telegraph.
static func spawn_line(parent: Node, from: Vector2, to: Vector2, width: float, seconds: float) -> Telegraph:
	var telegraph := Telegraph.new()
	parent.add_child(telegraph)
	telegraph.global_position = from
	telegraph.show_line(to - from, width, seconds)
	return telegraph


func show_circle(p_radius: float, seconds: float) -> void:
	shape = Shape.CIRCLE
	radius = p_radius
	_arm(seconds)


func show_line(offset: Vector2, width: float, seconds: float) -> void:
	shape = Shape.LINE
	line_offset = offset
	line_width = width
	_arm(seconds)


func remaining() -> float:
	return _left


## 0 when shown, 1 when the warning is over.
func progress() -> float:
	if _duration <= 0.0:
		return 1.0
	return clampf(1.0 - _left / _duration, 0.0, 1.0)


func step(delta: float) -> void:
	if not _armed:
		return
	_left = maxf(_left - delta, 0.0)
	queue_redraw()
	if _left <= 0.0:
		_armed = false
		elapsed.emit()
		queue_free()


func _arm(seconds: float) -> void:
	_duration = maxf(seconds, 0.0)
	_left = _duration
	_armed = true
	queue_redraw()


func _physics_process(delta: float) -> void:
	step(delta)


func _draw() -> void:
	match shape:
		Shape.CIRCLE:
			draw_circle(Vector2.ZERO, radius, fill_color)
			draw_circle(Vector2.ZERO, radius * progress(), fill_color)
			draw_arc(Vector2.ZERO, radius, 0.0, TAU, SEGMENTS, edge_color, EDGE_WIDTH)
		Shape.LINE:
			draw_line(Vector2.ZERO, line_offset, fill_color, line_width)
			draw_line(Vector2.ZERO, line_offset * progress(), edge_color, line_width * 0.4)
