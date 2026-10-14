extends TestCase

var _saved_db: float = 0.0


func before_each() -> void:
	_saved_db = AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func after_each() -> void:
	AudioServer.set_bus_volume_db(AudioServer.get_bus_index(&"Master"), _saved_db)


func _master_db() -> float:
	return AudioServer.get_bus_volume_db(AudioServer.get_bus_index(&"Master"))


func test_half_volume_sets_the_matching_decibels() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.5
	AudioSettings.apply(settings)
	assert_almost_eq(_master_db(), linear_to_db(0.5), 0.01)


func test_full_volume_is_zero_db() -> void:
	AudioSettings.apply(SettingsData.new())
	assert_almost_eq(_master_db(), 0.0, 0.01)


func test_zero_volume_is_finite_and_effectively_silent() -> void:
	var settings := SettingsData.new()
	settings.master_volume = 0.0
	AudioSettings.apply(settings)
	assert_true(is_finite(_master_db()), "no -inf")
	assert_true(_master_db() <= -60.0)
