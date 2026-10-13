class_name TimelineBar
extends Control
## Stage progress: a diamond for every wave, a circle for every event (passed marks turn grey), and a
## "BOSS" label once the boss fight starts.

const MARK_HALF_WIDTH: float = 5.0

@export var track_color: Color = Color(0.12, 0.12, 0.16, 0.85)
@export var fill_color: Color = Color(0.35, 0.7, 1.0)
@export var mark_color: Color = Color(1.0, 0.75, 0.2)
@export var event_mark_color: Color = Color(0.55, 1.0, 0.45)
@export var passed_mark_color: Color = Color(0.55, 0.55, 0.55)
@export var boss_text_color: Color = Color(1.0, 0.35, 0.3)

var _duration: float = 1.0
var _marks: PackedFloat32Array = PackedFloat32Array()
var _event_marks: PackedFloat32Array = PackedFloat32Array()
var _elapsed: float = 0.0
var _boss_label: Label


func _ready() -> void:
	_boss_label = Label.new()
	_boss_label.name = "BossLabel"
	_boss_label.text = "BOSS"
	_boss_label.visible = false
	_boss_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_boss_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	_boss_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_boss_label.add_theme_color_override("font_color", boss_text_color)
	add_child(_boss_label)
	_boss_label.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)


func configure(duration: float, marks: PackedFloat32Array, p_event_marks: PackedFloat32Array = PackedFloat32Array()) -> void:
	_duration = duration
	_marks = marks
	_event_marks = p_event_marks
	_elapsed = 0.0
	queue_redraw()


func event_marks() -> PackedFloat32Array:
	return _event_marks


func set_elapsed(elapsed: float) -> void:
	_elapsed = elapsed
	queue_redraw()


func show_boss() -> void:
	_boss_label.visible = true
	queue_redraw()


func is_showing_boss() -> bool:
	return _boss_label != null and _boss_label.visible


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
	for mark in _event_marks:
		var color := passed_mark_color if mark <= _elapsed else event_mark_color
		draw_circle(Vector2(mark_x(mark), size.y * 0.5), MARK_HALF_WIDTH, color)
