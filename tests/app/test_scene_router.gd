extends TestCase

const Router := preload("res://src/app/scene_router.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _probe() -> Node:
	return adopt(Probe.new())


func test_go_changes_to_the_route_scene() -> void:
	var router := _probe()
	assert_true(router.go(Router.Route.MAIN_MENU))
	assert_eq(router.changed_to, [Router.ROUTES[Router.Route.MAIN_MENU]])


func test_params_are_handed_over_exactly_once() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"a": 1})
	assert_eq(router.take_params(), {"a": 1})
	assert_eq(router.take_params(), {}, "second read is empty")


func test_a_new_go_replaces_unread_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"a": 1})
	router.go(Router.Route.MAIN_MENU)
	assert_eq(router.take_params(), {})


func test_unknown_route_changes_nothing_and_keeps_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"keep": true})
	assert_false(router.call("go", 999, {"x": 1}))
	assert_eq(router.changed_to.size(), 1, "no second scene change")
	assert_eq(router.take_params(), {"keep": true})


func test_failed_scene_change_reports_false_and_keeps_previous_params() -> void:
	var router := _probe()
	router.go(Router.Route.STAGE, {"keep": true})
	router.next_error = ERR_CANT_OPEN
	assert_false(router.go(Router.Route.MAIN_MENU, {"lost": true}))
	assert_eq(router.take_params(), {"keep": true})


func test_path_for_an_unknown_route_is_empty() -> void:
	assert_eq(_probe().call("path_for", 999), "")


func test_the_stage_param_key_is_a_stable_name() -> void:
	assert_eq(Router.PARAM_STAGE_DEF, &"stage_def")


func test_every_route_has_a_scene_that_loads() -> void:
	assert_eq(Router.ROUTES.size(), Router.Route.size(), "every Route enum value has a path")
	for route in Router.ROUTES:
		var path: String = Router.ROUTES[route]
		assert_true(load(path) is PackedScene, path)
