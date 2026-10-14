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


func test_cleared_stages_default_to_empty() -> void:
	assert_eq(SaveData.new().cleared_stages.size(), 0)


func test_mark_cleared_ignores_an_id_already_present() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	data.mark_cleared(&"stage_01")
	assert_eq(data.cleared_stages, [&"stage_01"])


func test_cleared_stages_round_trip() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	data.mark_cleared(&"stage_02")
	var restored := SaveData.from_dict(data.to_dict())
	assert_not_null(restored)
	assert_eq(restored.cleared_stages, [&"stage_01", &"stage_02"])


func test_cleared_stages_are_stored_as_plain_strings() -> void:
	var data := SaveData.new()
	data.mark_cleared(&"stage_01")
	var stored: Array = data.to_dict()["cleared_stages"]
	assert_eq(typeof(stored[0]), TYPE_STRING)


func test_an_unknown_stage_id_survives_load_and_save() -> void:
	var restored := SaveData.from_dict({"cleared_stages": ["ghost_stage"]})
	assert_not_null(restored)
	assert_eq(restored.to_dict()["cleared_stages"], ["ghost_stage"])


func test_invalid_cleared_stages_are_rejected() -> void:
	assert_null(SaveData.from_dict({"cleared_stages": "stage_01"}), "not an array")
	assert_null(SaveData.from_dict({"cleared_stages": [1]}), "number entry")
	assert_null(SaveData.from_dict({"cleared_stages": ["stage_01", null]}), "null entry")
