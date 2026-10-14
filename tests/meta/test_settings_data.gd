extends TestCase


func test_default_volume_is_full() -> void:
	assert_almost_eq(SettingsData.new().master_volume, 1.0)


func test_volume_is_clamped_to_zero_one() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 3.0
	assert_almost_eq(settings.master_volume, 1.0)
	settings.master_volume = -2.0
	assert_almost_eq(settings.master_volume, 0.0)


func test_dict_round_trip() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.25
	var restored := SettingsData.from_dict(settings.to_dict())
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 0.25)


func test_missing_key_keeps_the_default() -> void:
	var restored := SettingsData.from_dict({})
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 1.0)


func test_wrong_type_is_rejected() -> void:
	assert_null(SettingsData.from_dict({"master_volume": "loud"}))


func test_out_of_range_volume_from_a_file_is_clamped_not_rejected() -> void:
	var restored := SettingsData.from_dict({"master_volume": 7})
	assert_not_null(restored)
	assert_almost_eq(restored.master_volume, 1.0)
