class_name EventContext
extends RefCounted
## What a StageEvent may use. The Stage builds one per event; event code never imports Stage.

## Where the event controller lives; the objects it places (statues, warnings, strikes) are its children.
var layer: Node2D
var factory: EnemyFactory
var targets: TargetProvider
var player: Node2D
var building: Node2D
var rng: RunRng
var bounds: Rect2
var spawn_radius: float
var def: EventDef


func _init(p_layer: Node2D, p_factory: EnemyFactory, p_targets: TargetProvider, p_player: Node2D, p_building: Node2D, p_rng: RunRng, p_bounds: Rect2, p_spawn_radius: float, p_def: EventDef) -> void:
	layer = p_layer
	factory = p_factory
	targets = p_targets
	player = p_player
	building = p_building
	rng = p_rng
	bounds = p_bounds
	spawn_radius = p_spawn_radius
	def = p_def
