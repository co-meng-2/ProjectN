extends TestCase

const SCREEN_SCENE: PackedScene = preload("res://src/meta/menu/settings_screen.tscn")
const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const Probe := preload("res://tests/support/scene_router_probe.gd")
const DIR := "user://test_settings_screen"
const PATH := "user://test_settings_screen/save.json"

var _saved_db: float = 0.0


func before_each() -> void:
	SaveFiles.reset(DIR)
	_saved_db = AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func after_each() -> void:
	SaveFiles.reset(DIR)
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Master"), _saved_db)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func _screen(service: Node, router: Node) -> Control:
	var screen := SCREEN_SCENE.instantiate() as Control
	screen.save_service = service
	screen.router = router
	return adopt(screen)


func test_the_slider_starts_at_the_saved_volume() -> void:
	var service := _service()
	service.data.settings.master_volume = 0.3
	var screen := _screen(service, adopt(Probe.new()))
	assert_almost_eq(screen.get_node("%VolumeSlider").value, 0.3, 0.001)


func test_moving_the_slider_updates_the_model_and_the_audio_bus() -> void:
	var service := _service()
	var screen := _screen(service, adopt(Probe.new()))
	screen.get_node("%VolumeSlider").value = 0.5
	assert_almost_eq(service.data.settings.master_volume, 0.5, 0.001)
	var bus_db := AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))
	assert_almost_eq(bus_db, linear_to_db(0.5), 0.01)


func test_back_saves_and_returns_to_the_main_menu() -> void:
	var service := _service()
	var router: Node = adopt(Probe.new())
	var screen := _screen(service, router)
	screen.get_node("%VolumeSlider").value = 0.25
	screen.get_node("%BackButton").pressed.emit()
	assert_eq(router.changed_to, [router.path_for(Probe.Route.MAIN_MENU)])
	assert_almost_eq(SaveStore.load_or_default(PATH).settings.master_volume, 0.25, 0.001, "persisted")
