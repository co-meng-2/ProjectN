extends TestCase
## Walks the real scenes through the real router table and shipped catalog, with a temp save:
## menu -> hub -> stage_01 cleared -> hub shows stage_02 open.

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_boot_flow"
const PATH := "user://test_boot_flow/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _hub(service: Node, router: Node) -> Node:
	var hub: Node = load(router.path_for(Probe.Route.HUB)).instantiate()
	hub.save_service = service
	hub.router = router
	return adopt(hub)


func _play_button(hub: Node, index: int) -> Button:
	return hub.get_node("%StageList").get_child(index).get_node("%PlayButton") as Button


func test_clearing_stage_01_from_the_hub_pays_gem_unlocks_stage_02_and_survives_a_relaunch() -> void:
	var router: Node = adopt(Probe.new())
	var service := _service()

	var menu: Node = load(router.path_for(Probe.Route.MAIN_MENU)).instantiate()
	menu.router = router
	adopt(menu)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])

	var hub := _hub(service, router)
	assert_false(_play_button(hub, 0).disabled)
	assert_true(_play_button(hub, 1).disabled, "stage_02 starts locked")
	_play_button(hub, 0).pressed.emit()
	assert_eq(router.changed_to.back(), router.path_for(Probe.Route.STAGE))

	var main: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	main.save_service = service
	main.router = router
	# The probe never changes scenes, so hand Main the params the router kept for it.
	main.stage_def = router.take_params().get(Probe.PARAM_STAGE_DEF) as StageDef
	adopt(main)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	main._stage.finished.emit(result)
	assert_eq(service.data.gem, main.stage_def.gem_reward)

	var relaunched := _service()
	assert_eq(relaunched.data.cleared_stages, [&"stage_01"], "a fresh session reads the saved clear")
	var reopened_hub := _hub(relaunched, router)
	assert_false(_play_button(reopened_hub, 1).disabled, "stage_02 is open after clearing stage_01")


func test_gem_from_a_clear_buys_a_skill_that_strengthens_the_next_run() -> void:
	var router: Node = adopt(Probe.new())
	var service := _service()

	var first: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	first.save_service = service
	first.router = router
	adopt(first)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	first._stage.finished.emit(result)
	assert_eq(service.data.gem, first.stage_def.gem_reward)

	var screen: Node = load(router.path_for(Probe.Route.SKILL_TREE)).instantiate()
	screen.save_service = service
	screen.router = router
	adopt(screen)
	screen.get_node("%NodeList").get_child(0).get_node("%BuyButton").pressed.emit()
	assert_eq(service.data.level_of(&"vitality"), 1)
	assert_eq(service.data.gem, first.stage_def.gem_reward - 10, "vitality level 1 costs 10")

	var second: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	second.save_service = service
	second.router = router
	adopt(second)
	assert_almost_eq(second._stage.player.health.maximum(), 110.0, 0.001, "base 100 + vitality level 1 (+10)")
	assert_almost_eq(second._stage.player.health.current(), 110.0, 0.001)
