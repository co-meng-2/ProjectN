extends Node
## The stage screen: runs a stage with its HUD, routes HUD requests to the stage, pays the run's gem into the
## save, and leaves for the main menu on request. Reached through SceneRouter (params: the StageDef).

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const HUD_SCENE: PackedScene = preload("res://src/ui/hud/hud.tscn")
## Used when the scene runs standalone (F6) with no params.
const DEFAULT_STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")
const PROFILE: PlayerProfile = preload("res://data/characters/default_profile.tres")

const BUILDING_BAR_OFFSET := Vector2(0, -60)

## Collaborators default to the autoloads in _ready; tests set them first.
var save_service: Node
var router: Node
var stage_def: StageDef

var _stage: Stage
var _hud: Hud
var _building_bar: WorldHealthBar
var _result: RunResult


func _ready() -> void:
	if save_service == null:
		save_service = SaveService
	if router == null:
		router = SceneRouter
	if stage_def == null:
		stage_def = router.take_params().get(SceneRouter.PARAM_STAGE_DEF, DEFAULT_STAGE_DEF) as StageDef
	_start_run()


func _unhandled_input(event: InputEvent) -> void:
	if _result == null:
		return
	if event.is_action_pressed(InputActions.RESTART):
		_start_run()
	elif event.is_action_pressed(InputActions.CONFIRM):
		router.go(SceneRouter.Route.MAIN_MENU)


func _start_run() -> void:
	_discard_run()
	_result = null
	_stage = STAGE_SCENE.instantiate() as Stage
	_hud = HUD_SCENE.instantiate() as Hud
	_building_bar = WorldHealthBar.new()
	add_child(_stage)
	add_child(_building_bar)
	add_child(_hud)
	if not _stage.start(stage_def, PROFILE):
		get_tree().quit(1)
		return
	_hud.bind(_stage.player.health, _stage.building.health, _stage.run_state.wallet, _stage.clock, _stage.waves.timeline, _stage.events.event_times())
	_stage.events.event_started.connect(_hud.show_event)
	_stage.events.event_progressed.connect(_hud.show_event_progress)
	_stage.events.event_ended.connect(_hud.show_event_result)
	_stage.events.event_aborted.connect(_hud.show_event_aborted)
	_stage.boss_appeared.connect(_hud.show_boss)
	_hud.ability_bar.bind(_stage.loadout, _stage.run_state.wallet, _stage.grade_service)
	_hud.ability_bar.promote_requested.connect(_on_promote_requested)
	_hud.ability_bar.reroll_requested.connect(_on_reroll_requested)
	_building_bar.bind(_stage.building.health, _stage.building, BUILDING_BAR_OFFSET)
	_stage.finished.connect(_on_finished)


func _discard_run() -> void:
	for node: Node in [_stage, _building_bar, _hud]:
		if node != null:
			remove_child(node)
			node.queue_free()


## The HUD only asks; the app decides. The shop closes with the run.
func _on_promote_requested(index: int) -> void:
	if _result == null:
		_stage.grade_service.promote(index)


func _on_reroll_requested(index: int) -> void:
	if _result == null:
		_stage.grade_service.reroll(index)


func _on_finished(result: RunResult) -> void:
	if _result != null:
		return
	result.gem = stage_def.gem_for(result.outcome)
	_result = result
	save_service.add_gem(result.gem)
	save_service.save()
	_hud.show_result(result)
	print("[Main] %s after %.1fs - kills %d, gold %d, stone %d, gem %d" % [
		RunResult.Outcome.keys()[result.outcome], result.elapsed, result.kills, result.gold, result.stone, result.gem,
	])
