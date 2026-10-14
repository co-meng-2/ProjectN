extends TestCase

const MAIN_SCENE: PackedScene = preload("res://src/app/main.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_main"
const PATH := "user://test_main/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _main(service: Node, router: Node = null) -> Node:
	var main := MAIN_SCENE.instantiate()
	main.save_service = service
	if router != null:
		main.router = router
	return adopt(main)


func _result(outcome: RunResult.Outcome) -> RunResult:
	var result := RunResult.new()
	result.outcome = outcome
	return result


func _confirm_press() -> InputEventAction:
	var press := InputEventAction.new()
	press.action = InputActions.CONFIRM
	press.pressed = true
	return press


func test_the_default_stage_is_the_shipped_stage_when_no_params_are_given() -> void:
	var main := _main(_service())
	assert_eq(main.stage_def.id, &"stage_01")


func test_a_cleared_run_credits_the_stage_reward_and_saves_it() -> void:
	var service := _service()
	var main := _main(service)
	var reward: int = main.stage_def.gem_reward
	assert_true(reward > 0)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.gem, reward)
	assert_eq(SaveStore.load_or_default(PATH).gem, reward, "persisted to disk")


func test_a_failed_run_credits_nothing() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	assert_eq(service.data.gem, 0)


func test_finishing_twice_credits_only_once() -> void:
	var service := _service()
	var main := _main(service)
	var reward: int = main.stage_def.gem_reward
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.gem, reward)


func test_confirm_after_a_result_returns_to_the_hub() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	main._unhandled_input(_confirm_press())
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func _key_press(keycode: Key) -> InputEventKey:
	var press := InputEventKey.new()
	press.keycode = keycode
	press.pressed = true
	return press


func test_space_is_the_dash_key_and_does_not_leave_the_result_screen() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	main._unhandled_input(_key_press(KEY_SPACE))
	assert_eq(router.changed_to.size(), 0, "mashing dash must not skip the result")


func test_enter_continues_from_the_result_screen() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	main._unhandled_input(_key_press(KEY_ENTER))
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func test_confirm_during_a_run_does_nothing() -> void:
	var router: Node = adopt(Probe.new())
	var main := _main(_service(), router)
	main._unhandled_input(_confirm_press())
	assert_eq(router.changed_to.size(), 0)


func test_a_cleared_run_records_the_clear_and_saves_it() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.cleared_stages, [&"stage_01"])
	assert_eq(SaveStore.load_or_default(PATH).cleared_stages, [&"stage_01"], "persisted to disk")


func test_a_failed_run_records_no_clear() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.FAILED))
	assert_eq(service.data.cleared_stages.size(), 0)


func test_finishing_twice_records_the_clear_once() -> void:
	var service := _service()
	var main := _main(service)
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	main._stage.finished.emit(_result(RunResult.Outcome.CLEARED))
	assert_eq(service.data.cleared_stages.size(), 1)


func test_bought_skill_levels_shape_the_profile_the_stage_starts_with() -> void:
	var service := _service()
	service.data.set_level(&"vitality", 2)
	service.data.set_level(&"extra_slot", 1)
	var main := _main(service)
	assert_almost_eq(main._stage.player.health.maximum(), 120.0, 0.001, "base 100 + vitality level 2 (+20)")
	assert_almost_eq(main._stage.player.health.current(), 120.0, 0.001, "starts at full modified health")
	assert_eq(main._stage.loadout.size(), 7, "6 base slots + extra slot")


func test_without_purchases_the_run_uses_the_base_profile() -> void:
	var main := _main(_service())
	assert_almost_eq(main._stage.player.health.maximum(), 100.0, 0.001)
	assert_eq(main._stage.loadout.size(), 6)


func test_an_invalid_skill_tree_falls_back_to_the_base_profile() -> void:
	var service := _service()
	service.data.set_level(&"vitality", 3)
	var main := MAIN_SCENE.instantiate()
	main.save_service = service
	main.skill_tree = SkillTreeDef.new()
	adopt(main)
	assert_almost_eq(main._stage.player.health.maximum(), 100.0, 0.001, "no bonuses from a broken tree")
	assert_eq(main._stage.loadout.size(), 6)
