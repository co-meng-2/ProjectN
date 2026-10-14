extends TestCase

const LOGO_SCENE: PackedScene = preload("res://src/app/logo.tscn")
const Probe := preload("res://tests/support/scene_router_probe.gd")


func _key_press() -> InputEventKey:
	var event := InputEventKey.new()
	event.pressed = true
	event.keycode = KEY_SPACE
	return event


func _logo(router: Node) -> Control:
	var logo := LOGO_SCENE.instantiate() as Control
	logo.router = router
	return adopt(logo)


func test_a_key_press_skips_to_the_main_menu() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	logo._unhandled_input(_key_press())
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])


func test_proceeding_twice_changes_scene_once() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	logo._unhandled_input(_key_press())
	logo._unhandled_input(_key_press())
	assert_eq(router.changed_to.size(), 1)


func test_a_key_release_does_not_skip() -> void:
	var router: Node = adopt(Probe.new())
	var logo := _logo(router)
	var release := _key_press()
	release.pressed = false
	logo._unhandled_input(release)
	assert_eq(router.changed_to.size(), 0)
