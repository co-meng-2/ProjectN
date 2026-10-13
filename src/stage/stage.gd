class_name Stage
extends Node2D
## Composition root of one stage run. Builds everything from (StageDef, PlayerProfile), wires the
## signals between modules, and reports a RunResult. Modules never look each other up.
## The stage knows nothing about UI: whoever presents it binds to the public models below.
## As the composition root it may reference abilities/ (the shop) and boss/ (the brain it attaches).

signal finished(result: RunResult)
## The boss arrived; relayed from the BossDirector for the HUD.
signal boss_appeared(boss_health: Health, display_name: String)

const PLAYER_START: Vector2 = Vector2(0.0, 150.0)
## Keeps summoned enemies inside the arena walls.
const SUMMON_EDGE_MARGIN: float = 24.0

var run_state: RunState
## The ability slots and the shop rules over them; the HUD binds to these through Main.
var loadout: Loadout
var grade_service: GradeService

var _flow := StageFlow.new()

@onready var player: Player = $World/Player
@onready var building: Building = $World/Building
@onready var clock: StageClock = $Systems/StageClock
@onready var waves: WaveDirector = $Systems/WaveDirector
## The stage's events: their times for the timeline and start/progress/end signals for the HUD.
@onready var events: EventDirector = $Systems/EventDirector
@onready var _bosses: BossDirector = $Systems/BossDirector
@onready var _world: Node2D = $World
@onready var _arena: Arena = $Arena
@onready var _enemy_layer: Node2D = $World/Enemies
@onready var _event_layer: Node2D = $World/Events
@onready var _projectile_layer: Node2D = $World/Projectiles
@onready var _pickup_layer: Node2D = $World/Pickups
@onready var _targets: TargetProvider = $Systems/TargetProvider
@onready var _factory: EnemyFactory = $Systems/EnemyFactory
@onready var _spawner: SpawnDirector = $Systems/SpawnDirector
@onready var _loot: LootDirector = $Systems/LootDirector
@onready var _installer: AbilityInstaller = $Systems/AbilityInstaller


## Returns false, after logging every problem, when a def is invalid; nothing is started then.
func start(stage_def: StageDef, profile: PlayerProfile, run_seed: int = 0) -> bool:
	if run_state != null:
		push_error("Stage.start: already started; a Stage runs once")
		return false
	var errors := stage_def.validate()
	errors.append_array(profile.validate())
	errors.append_array(AbilityInstaller.validate_defs(profile.abilities))
	errors.append_array(EventDirector.validate_defs(stage_def.events))
	errors.append_array(BossDirector.validate_def(stage_def.boss))
	errors.append_array(BossBrain.validate_def(stage_def.boss))
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
	events.bind(stage_def, clock, _event_context, run_state.rng, waves.mark_times())
	_bosses.bind(stage_def.boss, _flow, events, _enemy_layer, building, stage_def.spawn_radius, _arena.bounds, run_state.rng, _targets, _projectile_layer, _summon)
	_loot.bind(_factory, _pickup_layer, player, run_state.wallet, stage_def.drop_multiplier)
	# The shop: pure rules over the run's wallet and rng; the installer mirrors slots onto the player.
	loadout = Loadout.new(profile.slot_count)
	grade_service = GradeService.new(loadout, AbilityPool.new(profile.abilities), stage_def.grade_table, run_state.wallet, run_state.rng)
	_installer.bind(loadout, player)
	_wire()
	_flow.transition_to(StageFlow.State.RUNNING)
	clock.start(stage_def.duration)
	return true


func flow_state() -> StageFlow.State:
	return _flow.state


## The boss once it has appeared; null before (or on a stage without one).
func current_boss() -> Boss:
	return _bosses.boss


func _wire() -> void:
	building.destroyed.connect(_finish.bind(RunResult.Outcome.FAILED))
	player.died.connect(_finish.bind(RunResult.Outcome.FAILED))
	clock.ended.connect(_on_clock_ended)
	_factory.spawned.connect(_on_enemy_spawned)
	events.event_ended.connect(_on_event_ended)
	_bosses.boss_spawned.connect(_on_boss_spawned)
	_bosses.boss_appeared.connect(boss_appeared.emit)
	_bosses.boss_defeated.connect(_finish.bind(RunResult.Outcome.CLEARED))


## Time up: the boss fight starts when the stage has a boss; otherwise the stage is cleared.
func _on_clock_ended() -> void:
	if _flow.is_terminal():
		return
	if _bosses.has_boss():
		_bosses.appear()
	else:
		_finish(RunResult.Outcome.CLEARED)


func _event_context(def: EventDef) -> EventContext:
	return EventContext.new(_event_layer, _factory, _targets, player, building, run_state.rng, _arena.bounds, run_state.stage_def.spawn_radius, def)


## Boss summons go through the factory like every enemy, so they count as kills and drop loot.
func _summon(def: EnemyDef, position: Vector2) -> void:
	_factory.spawn(def, SpawnGeometry.clamp_to_rect(position, _arena.bounds, SUMMON_EDGE_MARGIN))


func _on_boss_spawned(boss: Boss) -> void:
	BossBrain.attach_to(boss, run_state.rng)


## Only success pays; a failed or aborted event credits nothing.
func _on_event_ended(def: EventDef, success: bool) -> void:
	if not success or def.reward == null:
		return
	run_state.wallet.add(Wallet.Kind.GOLD, def.reward.gold)
	run_state.wallet.add(Wallet.Kind.STONE, def.reward.stone)
	run_state.wallet.add(Wallet.Kind.TICKET, def.reward.tickets)


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.died.connect(_on_enemy_died)


## Every kill is also a gameplay event on the player's ASC, so on-kill abilities react through GAS.
func _on_enemy_died(enemy: Enemy) -> void:
	run_state.kills += 1
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, enemy)


func _finish(outcome: RunResult.Outcome) -> void:
	if _flow.is_terminal():
		return
	var end_state := StageFlow.State.CLEARED if outcome == RunResult.Outcome.CLEARED else StageFlow.State.FAILED
	_flow.transition_to(end_state)
	events.abort_running()
	clock.stop()
	_world.process_mode = Node.PROCESS_MODE_DISABLED
	finished.emit(run_state.to_result(outcome, clock.elapsed))
