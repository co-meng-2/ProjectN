extends TestCase

const STAGE_01_WAVES: PackedFloat32Array = [30.0, 60.0, 90.0, 120.0, 150.0]


func _pool(time_limit: float = 20.0) -> Array[EventDef]:
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), time_limit)]
	return pool


func _stage_01_like() -> EventSchedule:
	return DefFactory.schedule(_pool(25.0), 3, 20.0, 155.0, 30.0, 5.0)


func test_allowed_seconds_skip_the_wave_clearance_and_stay_in_the_window() -> void:
	var schedule := DefFactory.schedule(_pool(), 1, 20.0, 40.0, 20.0, 5.0)
	var allowed := schedule.allowed_seconds(100.0, PackedFloat32Array([30.0]))
	assert_eq(allowed, PackedInt32Array([20, 21, 22, 23, 24, 36, 37, 38, 39, 40]))


func test_rolled_times_respect_window_gap_and_clearance_for_many_seeds() -> void:
	var schedule := _stage_01_like()
	var allowed := schedule.allowed_seconds(180.0, STAGE_01_WAVES)
	for run_seed in range(1, 41):
		var times := schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(run_seed))
		assert_eq(times.size(), 3, "seed %d" % run_seed)
		for i in times.size():
			assert_true(allowed.has(int(times[i])), "seed %d: %s is allowed" % [run_seed, times[i]])
			assert_almost_eq(times[i], roundf(times[i]), 0.0001, "whole seconds")
			if i > 0:
				assert_true(times[i] - times[i - 1] >= 30.0, "seed %d: gap" % run_seed)


func test_rolling_is_seeded_and_not_always_the_earliest() -> void:
	var schedule := _stage_01_like()
	assert_eq(schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(8)), schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(8)))
	var firsts: Dictionary[float, bool] = {}
	for run_seed in range(1, 21):
		firsts[schedule.roll_times(180.0, STAGE_01_WAVES, RunRng.new(run_seed))[0]] = true
	assert_true(firsts.size() > 3, "first times vary with the seed (got %d distinct)" % firsts.size())


func test_a_tight_schedule_has_exactly_one_placement() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 3, 10.0, 30.0, 10.0)
	for run_seed in range(1, 6):
		assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(run_seed)), PackedFloat32Array([10.0, 20.0, 30.0]))


func test_each_pick_leaves_room_for_the_rest() -> void:
	var schedule := DefFactory.schedule(_pool(20.0), 2, 10.0, 40.0, 25.0)
	for run_seed in range(1, 31):
		var times := schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(run_seed))
		assert_eq(times.size(), 2, "seed %d" % run_seed)
		assert_true(times[0] <= 15.0, "seed %d: the first event must leave room for the second (got %s)" % [run_seed, times[0]])
		assert_true(times[1] >= times[0] + 25.0)


func test_infeasible_schedule_rolls_nothing_and_fails_validation() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 4, 10.0, 30.0, 10.0)
	assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(3)).size(), 0)
	assert_eq(schedule.earliest_times(100.0, PackedFloat32Array()).size(), 3)
	var errors := schedule.validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("cannot place"))


func test_wave_clearance_can_make_a_schedule_infeasible() -> void:
	var schedule := DefFactory.schedule(_pool(10.0), 1, 25.0, 35.0, 10.0, 6.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array([30.0])).size(), 1, "every second is within 6 s of the wave")
	schedule.wave_clearance = 4.0
	assert_no_errors(schedule.validate_against(100.0, PackedFloat32Array([30.0])), "25 and 35 are clear")


func test_validation_rejects_bad_windows_counts_and_short_gaps() -> void:
	var schedule := DefFactory.schedule(_pool(20.0), 1, 0.0, 50.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 1, "window starts at 0")
	schedule = DefFactory.schedule(_pool(20.0), 1, 10.0, 100.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 2, "window reaches the stage end, so no event there could finish either")
	schedule = DefFactory.schedule(_pool(20.0), -1, 10.0, 50.0, 20.0)
	assert_eq(schedule.validate_against(100.0, PackedFloat32Array()).size(), 1, "negative count")
	schedule = DefFactory.schedule(_pool(25.0), 1, 10.0, 50.0, 20.0)
	var errors := schedule.validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 1, "min_gap shorter than the longest time limit")
	assert_true(errors[0].contains("min_gap"))


func test_the_window_must_leave_time_for_the_longest_event_to_finish() -> void:
	var schedule := DefFactory.schedule(_pool(25.0), 1, 20.0, 155.0, 30.0, 5.0)
	assert_no_errors(schedule.validate_against(180.0, STAGE_01_WAVES), "155 + 25 = 180 still finishes in time")
	schedule.window_end = 160.0
	var errors := schedule.validate_against(180.0, STAGE_01_WAVES)
	assert_eq(errors.size(), 1, "fails by this rule only: %s" % ", ".join(errors))
	assert_true(errors[0].contains("finish"))


func test_validation_reports_null_and_invalid_event_defs_and_an_empty_pool() -> void:
	var pool := _pool()
	pool.append(null)
	pool.append(EventDef.new())
	var errors := DefFactory.schedule(pool).validate_against(100.0, PackedFloat32Array())
	assert_eq(errors.size(), 6, "1 null + 5 from the empty def: %s" % ", ".join(errors))
	var empty: Array[EventDef] = []
	assert_eq(DefFactory.schedule(empty).validate_against(100.0, PackedFloat32Array()).size(), 1, "count 1 with nothing to pick")


func test_zero_count_rolls_nothing_and_validates() -> void:
	var schedule := DefFactory.schedule(_pool(), 0)
	assert_eq(schedule.roll_times(100.0, PackedFloat32Array(), RunRng.new(1)).size(), 0)
	assert_no_errors(schedule.validate_against(100.0, PackedFloat32Array()), "no events")


func test_roll_defs_never_repeats_when_there_are_alternatives() -> void:
	var a := DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"a")
	var b := DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"b")
	var pool: Array[EventDef] = [a, b, null]
	var picks := DefFactory.schedule(pool).roll_defs(20, RunRng.new(4))
	assert_eq(picks.size(), 20)
	for i in range(1, picks.size()):
		assert_false(picks[i] == picks[i - 1], "pick %d repeats" % i)
	assert_true(picks.has(a) and picks.has(b))
	var single: Array[EventDef] = [a]
	var repeats := DefFactory.schedule(single).roll_defs(3, RunRng.new(4))
	var expected: Array[EventDef] = [a, a, a]
	assert_eq(repeats, expected, "no alternative: the same def repeats")
