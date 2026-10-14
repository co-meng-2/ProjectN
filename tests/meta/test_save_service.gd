extends TestCase

const SERVICE_SCRIPT := preload("res://src/meta/save_service.gd")
const DIR := "user://test_save_service"
const PATH := "user://test_save_service/save.json"


func before_each() -> void:
	SaveFiles.reset(DIR)


func after_each() -> void:
	SaveFiles.reset(DIR)


func _service() -> Node:
	var service: Node = SERVICE_SCRIPT.new()
	service.path = PATH
	return adopt(service)


func test_starts_from_defaults_without_a_file() -> void:
	assert_eq(_service().data.gem, 0)


func test_add_gem_accumulates_and_emits_changed() -> void:
	var service := _service()
	var emitted: Array = []
	service.changed.connect(func() -> void: emitted.append(true))
	service.add_gem(3)
	service.add_gem(4)
	assert_eq(service.data.gem, 7)
	assert_eq(emitted.size(), 2)


func test_negative_gem_is_ignored() -> void:
	var service := _service()
	service.add_gem(5)
	service.add_gem(-3)
	assert_eq(service.data.gem, 5)


func test_save_then_reload_keeps_gem_and_volume() -> void:
	var service := _service()
	service.add_gem(9)
	service.data.settings.master_volume = 0.3
	assert_true(service.save())
	var fresh := _service()
	assert_eq(fresh.data.gem, 9)
	assert_almost_eq(fresh.data.settings.master_volume, 0.3)
