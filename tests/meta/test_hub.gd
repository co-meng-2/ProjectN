extends TestCase

const HUB_SCENE: PackedScene = preload("res://src/meta/hub/hub.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_hub"
const PATH := "user://test_hub/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _catalog(count: int) -> StageCatalog:
	var catalog := StageCatalog.new()
	for index in count:
		var stage := DefFactory.stage()
		stage.id = StringName("s%d" % (index + 1))
		stage.display_name = "Stage %d" % (index + 1)
		catalog.stages.append(stage)
	return catalog


func _hub(catalog: StageCatalog, service: Node, router: Node) -> Control:
	var hub := HUB_SCENE.instantiate() as Control
	hub.catalog = catalog
	hub.save_service = service
	hub.router = router
	return adopt(hub)


func _card(hub: Control, index: int) -> Node:
	return hub.get_node("%StageList").get_child(index)


func _play_button(hub: Control, index: int) -> Button:
	return _card(hub, index).get_node("%PlayButton") as Button


func test_one_card_per_stage_with_its_name() -> void:
	var hub := _hub(_catalog(3), _service(), adopt(Probe.new()))
	assert_eq(hub.get_node("%StageList").get_child_count(), 3)
	assert_eq(_card(hub, 1).get_node("%NameLabel").text, "Stage 2")


func test_only_the_first_stage_is_playable_on_a_fresh_save() -> void:
	var hub := _hub(_catalog(3), _service(), adopt(Probe.new()))
	assert_false(_play_button(hub, 0).disabled)
	assert_true(_play_button(hub, 1).disabled)
	assert_true(_play_button(hub, 2).disabled)
	assert_eq(_card(hub, 1).get_node("%StatusLabel").text, "Locked")


func test_a_cleared_stage_opens_the_next_and_shows_cleared() -> void:
	var service := _service()
	service.record_clear(&"s1")
	var hub := _hub(_catalog(3), service, adopt(Probe.new()))
	assert_eq(_card(hub, 0).get_node("%StatusLabel").text, "Cleared")
	assert_false(_play_button(hub, 0).disabled, "a cleared stage can be replayed")
	assert_false(_play_button(hub, 1).disabled)
	assert_true(_play_button(hub, 2).disabled)


func test_play_goes_to_the_stage_with_that_stage_def() -> void:
	var catalog := _catalog(2)
	var router: Node = adopt(Probe.new())
	var hub := _hub(catalog, _service(), router)
	_play_button(hub, 0).pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.STAGE)])
	assert_true(router.take_params().get(Probe.PARAM_STAGE_DEF) == catalog.stages[0])


func test_a_locked_stage_does_not_start_even_when_the_handler_is_called() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(_catalog(2), _service(), router)
	hub._on_play_pressed(1)
	assert_eq(router.changed_to.size(), 0)


func test_back_goes_to_the_main_menu() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(_catalog(1), _service(), router)
	hub.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_the_skill_tree_button_is_reserved_but_disabled() -> void:
	var hub := _hub(_catalog(1), _service(), adopt(Probe.new()))
	assert_true(hub.get_node("%SkillTreeButton").disabled)


func test_an_empty_catalog_shows_an_empty_state_and_back_still_works() -> void:
	var router: Node = adopt(Probe.new())
	var hub := _hub(StageCatalog.new(), _service(), router)
	assert_eq(hub.get_node("%StageList").get_child_count(), 0)
	assert_true(hub.get_node("%EmptyLabel").visible)
	hub.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_an_invalid_catalog_does_not_crash_the_hub() -> void:
	var catalog := _catalog(2)
	catalog.stages[1].duration = 0.0
	var hub := _hub(catalog, _service(), adopt(Probe.new()))
	assert_true(hub.get_node("%EmptyLabel").visible)
	assert_eq(hub.get_node("%StageList").get_child_count(), 0)


func test_the_shipped_catalog_is_the_default() -> void:
	var hub := HUB_SCENE.instantiate() as Control
	hub.save_service = _service()
	hub.router = adopt(Probe.new())
	adopt(hub)
	assert_eq(hub.get_node("%StageList").get_child_count(), 2)


func _focus_owner(hub: Control) -> Control:
	return hub.get_viewport().gui_get_focus_owner()


func test_focus_starts_on_the_first_stage_so_enter_plays_it() -> void:
	var hub := _hub(_catalog(2), _service(), adopt(Probe.new()))
	assert_true(_focus_owner(hub) == _play_button(hub, 0))


func test_focus_moves_to_the_next_available_stage_after_a_clear() -> void:
	var service := _service()
	service.record_clear(&"s1")
	var hub := _hub(_catalog(3), service, adopt(Probe.new()))
	assert_true(_focus_owner(hub) == _play_button(hub, 1))


func test_focus_falls_back_to_the_first_stage_when_everything_is_cleared() -> void:
	var service := _service()
	service.record_clear(&"s1")
	service.record_clear(&"s2")
	var hub := _hub(_catalog(2), service, adopt(Probe.new()))
	assert_true(_focus_owner(hub) == _play_button(hub, 0))


func test_focus_is_on_back_when_there_are_no_stages() -> void:
	var hub := _hub(StageCatalog.new(), _service(), adopt(Probe.new()))
	assert_true(_focus_owner(hub) == hub.get_node("%BackButton"))
