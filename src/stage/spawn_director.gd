class_name SpawnDirector
extends Node
## The gentle background trickle of enemies; its pace ramps across the stage.

const EDGE_MARGIN: float = 24.0

var _stage_def: StageDef
var _factory: EnemyFactory
var _anchor: Node2D
var _rng: RunRng
var _bounds: Rect2
var _next_spawn_at: float = 0.0


func bind(stage_def: StageDef, clock: StageClock, factory: EnemyFactory, anchor: Node2D, rng: RunRng, arena_bounds: Rect2) -> void:
	_stage_def = stage_def
	_factory = factory
	_anchor = anchor
	_rng = rng
	_bounds = arena_bounds
	clock.ticked.connect(_on_clock_ticked)


## Seconds between trickle spawns at `elapsed`, interpolated across the stage.
func current_interval(elapsed: float) -> float:
	var progress := clampf(elapsed / _stage_def.duration, 0.0, 1.0)
	return lerpf(_stage_def.trickle_interval_start, _stage_def.trickle_interval_end, progress)


func _on_clock_ticked(elapsed: float) -> void:
	if elapsed < _next_spawn_at:
		return
	_next_spawn_at = elapsed + current_interval(elapsed)
	if _stage_def.trickle_enemies.is_empty() or _factory.alive_count() >= _stage_def.trickle_cap:
		return
	if not is_instance_valid(_anchor):
		return
	var def := _rng.pick(_stage_def.trickle_enemies) as EnemyDef
	var point := SpawnGeometry.ring_point(_anchor.global_position, _stage_def.spawn_radius, _rng.randf() * TAU)
	_factory.spawn(def, SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN))
