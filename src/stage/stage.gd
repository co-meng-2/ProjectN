class_name Stage
extends Node2D
## Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the
## signals between modules, and reports a RunResult. Modules never look each other up.
## The stage knows nothing about UI: whoever presents it binds to the public models below.

signal finished(result: RunResult)

const PLAYER_START: Vector2 = Vector2(0.0, 150.0)

var run_state: RunState

var _flow := StageFlow.new()

@onready var player: Player = $World/Player
@onready var building: Building = $World/Building
@onready var clock: StageClock = $Systems/StageClock
@onready var waves: WaveDirector = $Systems/WaveDirector
@onready var _world: Node2D = $World
@onready var _arena: Arena = $Arena
@onready var _enemy_layer: Node2D = $World/Enemies
@onready var _projectile_layer: Node2D = $World/Projectiles
@onready var _pickup_layer: Node2D = $World/Pickups
@onready var _targets: TargetProvider = $Systems/TargetProvider
@onready var _factory: EnemyFactory = $Systems/EnemyFactory
@onready var _spawner: SpawnDirector = $Systems/SpawnDirector
@onready var _loot: LootDirector = $Systems/LootDirector


## Returns false, after logging every problem, when a def is invalid; nothing is started then.
func start(stage_def: StageDef, profile: PlayerProfile, run_seed: int = 0) -> bool:
	if run_state != null:
		push_error("Stage.start: already started; a Stage runs once")
		return false
	var errors := stage_def.validate()
	errors.append_array(profile.validate())
	if not errors.is_empty():
		for error in errors:
			push_error("Stage.start: %s" % error)
		return false
	run_state = RunState.new(stage_def, profile, run_seed)
	_arena.build(stage_def.arena_size, stage_def.ground_texture)
	building.setup(stage_def.building_texture, stage_def.building_stats)
	player.global_position = PLAYER_START
	player.setup(profile, _targets, _projectile_layer, run_state.wallet)
	player.limit_camera(_arena.bounds)
	_targets.building = building
	_targets.player = player
	_factory.bind(_enemy_layer, _targets, run_state.rng)
	# Enemies appear on a ring around the defended building, not around the roaming player.
	_spawner.bind(stage_def, clock, _factory, building, run_state.rng, _arena.bounds)
	waves.bind(stage_def, clock, _factory, building, run_state.rng, _arena.bounds)
	_loot.bind(_factory, _pickup_layer, player, run_state.wallet, stage_def.drop_multiplier)
	_wire()
	_flow.transition_to(StageFlow.State.RUNNING)
	clock.start(stage_def.duration)
	return true


func _wire() -> void:
	building.destroyed.connect(_finish.bind(RunResult.Outcome.FAILED))
	player.died.connect(_finish.bind(RunResult.Outcome.FAILED))
	clock.ended.connect(_finish.bind(RunResult.Outcome.CLEARED))
	_factory.spawned.connect(_on_enemy_spawned)


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.died.connect(_on_enemy_died)


func _on_enemy_died(_enemy: Enemy) -> void:
	run_state.kills += 1


func _finish(outcome: RunResult.Outcome) -> void:
	if _flow.is_terminal():
		return
	var end_state := StageFlow.State.CLEARED if outcome == RunResult.Outcome.CLEARED else StageFlow.State.FAILED
	_flow.transition_to(end_state)
	clock.stop()
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	finished.emit(run_state.to_result(outcome, clock.elapsed))
