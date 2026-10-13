class_name ObjectiveArrows
extends Control
## Arrows at the screen edge pointing at the running event's objectives that are off-screen.

@export var margin: float = 28.0
@export var arrow_size: float = 12.0
@export var arrow_color: Color = Color(1.0, 0.85, 0.3, 0.9)

var _event: StageEvent
var _positions := PackedVector2Array()
var _directions := PackedVector2Array()


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func track(event: StageEvent) -> void:
	_event = event


func clear() -> void:
	_event = null
	_positions.clear()
	_directions.clear()
	queue_redraw()


func arrow_positions() -> PackedVector2Array:
	return _positions


## `canvas_transform` maps world positions to the screen (the camera); `view_size` is the screen size.
func refresh(canvas_transform: Transform2D, view_size: Vector2) -> void:
	_positions.clear()
	_directions.clear()
	if is_instance_valid(_event):
		var view := Rect2(Vector2.ZERO, view_size)
		for objective in _event.objectives():
			var screen := canvas_transform * objective.global_position
			if view.has_point(screen):
				continue
			_positions.append(edge_point(screen, view, margin))
			_directions.append((screen - view.get_center()).normalized())
	queue_redraw()


## Where the ray from the view centre to `target` leaves the view shrunk by `edge_margin`.
static func edge_point(target: Vector2, view: Rect2, edge_margin: float) -> Vector2:
	var inner := view.grow(-edge_margin)
	var center := inner.get_center()
	var offset := target - center
	var limit := 1.0
	if absf(offset.x) > 0.0:
		limit = minf(limit, inner.size.x * 0.5 / absf(offset.x))
	if absf(offset.y) > 0.0:
		limit = minf(limit, inner.size.y * 0.5 / absf(offset.y))
	return center + offset * limit


func _process(_delta: float) -> void:
	refresh(get_viewport().get_canvas_transform(), get_viewport_rect().size)


func _draw() -> void:
	for i in _positions.size():
		var tip := _positions[i]
		var direction := _directions[i]
		var side := direction.orthogonal() * arrow_size * 0.6
		var back := tip - direction * arrow_size
		draw_colored_polygon(PackedVector2Array([tip, back + side, back - side]), arrow_color)
