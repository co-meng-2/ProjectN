extends Node
## Entry point: runs the default stage with its HUD, routes HUD requests to the stage, and restarts
## on demand. Phase 4 replaces this with the menu flow.

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const HUD_SCENE: PackedScene = preload("res://src/ui/hud/hud.tscn")
const STAGE_DEF: StageDef = preload("res://data/stages/stage_01.tres")
const PROFILE: PlayerProfile = preload("res://data/characters/default_profile.tres")

const BUILDING_BAR_OFFSET := Vector2(0, -60)

var _stage: Stage
var _hud: Hud
var _building_bar: WorldHealthBar
var _result: RunResult


func _ready() -> void:
	_start_run()


func _unhandled_input(event: InputEvent) -> void:
	if _result != null and event.is_action_pressed(InputActions.RESTART):
		_start_run()


func _start_run() -> void:
	_discard_run()
	_result = null
	_stage = STAGE_SCENE.instantiate() as Stage
	_hud = HUD_SCENE.instantiate() as Hud
	_building_bar = WorldHealthBar.new()
	add_child(_stage)
	add_child(_building_bar)
	add_child(_hud)
	if not _stage.start(STAGE_DEF, PROFILE):
		get_tree().quit(1)
		return
	_hud.bind(_stage.player.health, _stage.building.health, _stage.run_state.wallet, _stage.clock, _stage.waves.timeline, _stage.events.event_times())
	_stage.events.event_started.connect(_hud.show_event)
	_stage.events.event_progressed.connect(_hud.show_event_progress)
	_stage.events.event_ended.connect(_hud.show_event_result)
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
	_result = result
	_hud.show_result(result)
	print("[Main] %s after %.1fs - kills %d, gold %d, stone %d" % [
		RunResult.Outcome.keys()[result.outcome], result.elapsed, result.kills, result.gold, result.stone,
	])
