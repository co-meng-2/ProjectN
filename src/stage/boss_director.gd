class_name BossDirector
extends Node
## Brings the boss in when the stage time is up: switches the flow to BOSS, aborts a running event, spawns
## the boss on the spawn ring around the anchor (the building) and announces it. The boss's death is
## reported as `boss_defeated`; the Stage turns that into CLEARED. The boss is a scene from BossDef.scene.

signal boss_spawned(boss: Boss)
signal boss_appeared(boss_health: Health, display_name: String)
signal boss_defeated

const EDGE_MARGIN: float = 24.0

var boss: Boss

var _def: BossDef
var _flow: StageFlow
var _events: EventDirector
var _layer: Node2D
var _anchor: Node2D
var _spawn_radius: float = 0.0
var _bounds: Rect2
var _rng: RunRng
var _targets: TargetProvider
var _projectile_layer: Node
var _summon: Callable


func bind(boss_def: BossDef, flow: StageFlow, events: EventDirector, layer: Node2D, anchor: Node2D, spawn_radius: float, bounds: Rect2, rng: RunRng, targets: TargetProvider, projectile_layer: Node, summon: Callable) -> void:
	_def = boss_def
	_flow = flow
	_events = events
	_layer = layer
	_anchor = anchor
	_spawn_radius = spawn_radius
	_bounds = bounds
	_rng = rng
	_targets = targets
	_projectile_layer = projectile_layer
	_summon = summon


## Stage-layer check run by Stage.start: the boss scene root must be a Boss.
static func validate_def(def: BossDef) -> PackedStringArray:
	var errors := PackedStringArray()
	if def == null or def.scene == null:
		return errors  # BossDef.validate reports a missing scene
	var root := def.scene.instantiate()
	if root == null:
		return errors
	if not (root is Boss):
		errors.append("boss '%s': scene root is not a Boss" % def.id)
	root.free()
	return errors


func has_boss() -> bool:
	return _def != null


## Spawns the boss once, from RUNNING only. Returns it, or null when there is none or the run is over.
func appear() -> Boss:
	if boss != null:
		return boss
	if _def == null or not _flow.transition_to(StageFlow.State.BOSS):
		return null
	_events.abort_running()
	boss = _def.scene.instantiate() as Boss
	_layer.add_child(boss)
	var center := _anchor.global_position if is_instance_valid(_anchor) else Vector2.ZERO
	var point := SpawnGeometry.ring_point(center, _spawn_radius, _rng.randf() * TAU)
	boss.global_position = SpawnGeometry.clamp_to_rect(point, _bounds, EDGE_MARGIN)
	boss.setup(_def, _targets.player, _targets, _projectile_layer, _summon)
	boss.died.connect(_on_boss_died)
	boss_spawned.emit(boss)
	boss_appeared.emit(boss.health, _def.display_name)
	return boss


func _on_boss_died(_boss: Boss) -> void:
	boss_defeated.emit()
