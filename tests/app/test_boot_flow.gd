extends TestCase
## Walks the real scenes through the real router table: menu -> stage, with a temp save.

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_boot_flow"
const PATH := "user://test_boot_flow/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func test_start_from_the_menu_runs_the_shipped_stage_and_pays_gem_that_survives_a_relaunch() -> void:
	var router: Node = adopt(Probe.new())
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	adopt(service)

	var menu: Node = load(router.path_for(Probe.Route.MAIN_MENU)).instantiate()
	menu.router = router
	adopt(menu)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])

	var main: Node = load(router.path_for(Probe.Route.STAGE)).instantiate()
	main.save_service = service
	main.router = router
	# The probe never changes scenes, so hand Main the params the router kept for it.
	main.stage_def = router.take_params().get(Probe.PARAM_STAGE_DEF) as StageDef
	adopt(main)
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	main._stage.finished.emit(result)

	var reward: int = main.stage_def.gem_reward
	assert_true(reward > 0)
	assert_eq(service.data.gem, reward)
	var relaunched: Node = SERVICE_SCRIPT.new()
	relaunched.path = PATH
	adopt(relaunched)
	assert_eq(relaunched.data.gem, reward, "a fresh session reads the saved gem")
