extends TestCase


func test_defaults() -> void:
	var data := SaveData.new()
	assert_eq(data.gem, 0)
	assert_almost_eq(data.settings.master_volume, 1.0)


func test_to_dict_carries_the_current_version() -> void:
	assert_eq(SaveData.new().to_dict().get("version"), SaveMigrations.CURRENT_VERSION)


func test_round_trip() -> void:
	var data := SaveData.new()
	data.gem = 42
	data.settings.master_volume = 0.5
	var restored := SaveData.from_dict(data.to_dict())
	assert_not_null(restored)
	assert_eq(restored.gem, 42)
	assert_almost_eq(restored.settings.master_volume, 0.5)


func test_missing_fields_get_defaults() -> void:
	var restored := SaveData.from_dict({"version": 1})
	assert_not_null(restored)
	assert_eq(restored.gem, 0)
	assert_almost_eq(restored.settings.master_volume, 1.0)


func test_gem_written_as_a_json_float_loads_as_an_int() -> void:
	var restored := SaveData.from_dict({"gem": 5.0})
	assert_not_null(restored)
	assert_eq(restored.gem, 5)
	assert_eq(typeof(restored.gem), TYPE_INT)


func test_invalid_gem_is_rejected() -> void:
	assert_null(SaveData.from_dict({"gem": 2.5}), "fraction")
	assert_null(SaveData.from_dict({"gem": "5"}), "string")
	assert_null(SaveData.from_dict({"gem": -1}), "negative")
	assert_null(SaveData.from_dict({"gem": 1.0e30}), "huge")


func test_invalid_settings_are_rejected() -> void:
	assert_null(SaveData.from_dict({"settings": 3}), "not a dictionary")
	assert_null(SaveData.from_dict({"settings": {"master_volume": "x"}}), "bad field")
