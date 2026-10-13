extends TestCase


func _entry(time: float, count: int = 5) -> WaveEntry:
	var entry := WaveEntry.new()
	entry.time = time
	entry.count = count
	entry.enemy = DefFactory.enemy()
	return entry


func test_sorted_entries_orders_by_time() -> void:
	var schedule := WaveSchedule.new()
	schedule.entries.append(_entry(40.0))
	schedule.entries.append(_entry(10.0))
	assert_eq(schedule.sorted_entries()[0].time, 10.0)
	assert_eq(schedule.times(), PackedFloat32Array([10.0, 40.0]))


func test_wave_after_stage_end_is_an_error() -> void:
	var schedule := WaveSchedule.new()
	schedule.entries.append(_entry(500.0))
	assert_eq(schedule.validate_against(100.0).size(), 1)


func test_wave_at_exactly_the_stage_end_is_an_error() -> void:
	var schedule := WaveSchedule.new()
	schedule.entries.append(_entry(100.0))
	assert_eq(schedule.validate_against(100.0).size(), 1)


func test_non_positive_count_and_null_enemy_are_errors() -> void:
	var schedule := WaveSchedule.new()
	var bad_count := _entry(5.0, 0)
	var no_enemy := _entry(6.0)
	no_enemy.enemy = null
	schedule.entries.append(bad_count)
	schedule.entries.append(no_enemy)
	assert_eq(schedule.validate_against(100.0).size(), 2)
