class_name WaveDirector
extends Node
## Fires the stage's scheduled surges and exposes their times for the timeline bar.

signal wave_started(entry: WaveEntry)

const EDGE_MARGIN: float = 24.0

var timeline: Timeline

var _entries: Array[WaveEntry] = []
var _previous_time: float = -1.0
var _factory: EnemyFactory
var _anchor: Node2D
var _rng: RunRng
var _bounds: Rect2
var _spawn_radius: float = 0.0


func bind(stage_def: StageDef, clock: StageClock, factory: EnemyFactory, anchor: Node2D, rng: RunRng, arena_bounds: Rect2) -> void:
	_factory = factory
	_anchor = anchor
	_rng = rng
	_bounds = arena_bounds
	_spawn_radius = stage_def.spawn_radius
	var times := PackedFloat32Array()
	if stage_def.waves != null:
		_entries = stage_def.waves.sorted_entries()
		times = stage_def.waves.times()
	timeline = Timeline.new(stage_def.duration, times)
	clock.ticked.connect(_on_clock_ticked)


func mark_times() -> PackedFloat32Array:
	return timeline.marks


func _on_clock_ticked(elapsed: float) -> void:
	for index in timeline.crossed(_previous_time, elapsed):
		_launch(_entries[index])
	_previous_time = elapsed


func _launch(entry: WaveEntry) -> void:
	wave_started.emit(entry)
	if not is_instance_valid(_anchor):
		return
	var base_angle := _rng.randf() * TAU
	for i in entry.count:
		var angle := base_angle + TAU * float(i) / float(entry.count)
		var point := SpawnGeometry.ring_point(_anchor.global_position, _spawn_radius, angle)
		_factory.spawn(entry.enemy, SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN))
