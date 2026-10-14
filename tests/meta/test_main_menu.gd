extends TestCase

const MENU_SCENE: PackedScene = preload("res://src/meta/menu/main_menu.tscn")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _menu(router: Node) -> Control:
	var menu := MENU_SCENE.instantiate() as Control
	menu.router = router
	return adopt(menu)


func test_start_goes_to_the_stage_with_the_shipped_stage_def() -> void:
	var router: Node = adopt(Probe.new())
	var menu := _menu(router)
	menu.get_node("%StartButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])
	var params: Dictionary = router.take_params()
	var stage := params.get(Probe.PARAM_STAGE_DEF) as StageDef
	assert_not_null(stage)
	assert_eq(stage.id, &"stage_01")


func test_settings_goes_to_the_settings_screen() -> void:
	var router: Node = adopt(Probe.new())
	var menu := _menu(router)
	menu.get_node("%SettingsButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.SETTINGS)])


func test_the_menu_shows_a_quit_button() -> void:
	var menu := _menu(adopt(Probe.new()))
	assert_not_null(menu.get_node_or_null("%QuitButton"))
