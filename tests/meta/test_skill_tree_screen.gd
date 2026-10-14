extends TestCase

const SCREEN_SCENE: PackedScene = preload("res://src/meta/skill_tree/skill_tree_screen.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_skill_tree_screen"
const PATH := "user://test_skill_tree_screen/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service(gem: int = 0) -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	adopt(service)
	service.data.gem = gem
	return service


## a: 2 levels costs 10/20. b: 1 level cost 5, requires a.
func _tree() -> SkillTreeDef:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"a"), DefFactory.skill_node(&"b", [&"a"], 1, PackedInt32Array([5]))]
	return tree


func _screen(tree: SkillTreeDef, service: Node, router: Node) -> Control:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.tree = tree
	screen.save_service = service
	screen.router = router
	return adopt(screen)


func _row(screen: Control, index: int) -> Node:
	return screen.get_node("%NodeList").get_child(index)


func _buy_button(screen: Control, index: int) -> Button:
	return _row(screen, index).get_node("%BuyButton") as Button


func test_one_row_per_node_and_the_gem_balance() -> void:
	var screen := _screen(_tree(), _service(25), adopt(Probe.new()))
	assert_eq(screen.get_node("%NodeList").get_child_count(), 2)
	assert_true(screen.get_node("%GemLabel").text.contains("25"))


func test_row_states_for_available_locked_and_unaffordable() -> void:
	var screen := _screen(_tree(), _service(10), adopt(Probe.new()))
	assert_false(_buy_button(screen, 0).disabled, "a is affordable at 10")
	assert_true(_buy_button(screen, 1).disabled, "b is locked")
	assert_true(_row(screen, 1).get_node("%DetailLabel").text.contains("Requires"))
	var poor := _screen(_tree(), _service(9), adopt(Probe.new()))
	assert_true(_buy_button(poor, 0).disabled, "a is not affordable at 9")


func test_buying_updates_gem_level_children_and_saves() -> void:
	var service := _service(25)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	_buy_button(screen, 0).pressed.emit()
	assert_eq(service.data.gem, 15)
	assert_eq(service.data.level_of(&"a"), 1)
	assert_true(screen.get_node("%GemLabel").text.contains("15"))
	assert_false(_buy_button(screen, 1).disabled, "b is now open and affordable")
	assert_eq(SaveStore.load_or_default(PATH).level_of(&"a"), 1, "persisted to disk")


func test_a_maxed_node_cannot_be_bought_again() -> void:
	var service := _service(100)
	service.data.set_level(&"a", 2)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	assert_true(_buy_button(screen, 0).disabled)
	assert_true(_row(screen, 0).get_node("%DetailLabel").text.contains("Max"))


func test_the_handler_refuses_locked_unaffordable_and_maxed_nodes() -> void:
	var service := _service(1000)
	var screen := _screen(_tree(), service, adopt(Probe.new()))
	screen._on_buy_pressed(1)
	assert_eq(service.data.gem, 1000, "locked b")
	assert_eq(service.data.level_of(&"b"), 0)
	var poor := _service(3)
	var poor_screen := _screen(_tree(), poor, adopt(Probe.new()))
	poor_screen._on_buy_pressed(0)
	assert_eq(poor.data.gem, 3, "short on gem")
	assert_eq(poor.data.level_of(&"a"), 0)


func test_back_returns_to_the_hub() -> void:
	var router: Node = adopt(Probe.new())
	var screen := _screen(_tree(), _service(), router)
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func test_an_invalid_tree_shows_an_empty_state_and_back_still_works() -> void:
	var router: Node = adopt(Probe.new())
	var screen := _screen(SkillTreeDef.new(), _service(), router)
	assert_eq(screen.get_node("%NodeList").get_child_count(), 0)
	assert_true(screen.get_node("%EmptyLabel").visible)
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.HUB)])


func test_focus_starts_on_the_first_buyable_row_else_back() -> void:
	var screen := _screen(_tree(), _service(10), adopt(Probe.new()))
	assert_true(screen.get_viewport().gui_get_focus_owner() == _buy_button(screen, 0))
	var poor := _screen(_tree(), _service(0), adopt(Probe.new()))
	assert_true(poor.get_viewport().gui_get_focus_owner() == poor.get_node("%BackButton"))


func test_the_shipped_tree_is_the_default() -> void:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.save_service = _service()
	screen.router = adopt(Probe.new())
	adopt(screen)
	assert_eq(screen.get_node("%NodeList").get_child_count(), 4)


func test_focus_stays_on_the_row_just_bought_so_enter_levels_it_again() -> void:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"first"), DefFactory.skill_node(&"second")]
	var screen := _screen(tree, _service(100), adopt(Probe.new()))
	assert_true(screen.get_viewport().gui_get_focus_owner() == _buy_button(screen, 0), "starts on the first buyable row")
	_buy_button(screen, 1).pressed.emit()
	assert_true(screen.get_viewport().gui_get_focus_owner() == _buy_button(screen, 1), "stays on the bought row, not row 0")


func test_focus_falls_back_when_the_bought_row_can_no_longer_be_bought() -> void:
	var tree := SkillTreeDef.new()
	tree.nodes = [DefFactory.skill_node(&"first"), DefFactory.skill_node(&"second", [], 1, PackedInt32Array([10]))]
	var screen := _screen(tree, _service(100), adopt(Probe.new()))
	_buy_button(screen, 1).pressed.emit()
	assert_true(_buy_button(screen, 1).disabled, "second is maxed at one level")
	assert_true(screen.get_viewport().gui_get_focus_owner() == _buy_button(screen, 0), "falls back to the first buyable row")
