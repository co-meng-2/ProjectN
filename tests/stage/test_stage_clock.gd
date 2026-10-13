extends TestCase


func _clock() -> StageClock:
	return adopt(StageClock.new()) as StageClock


func test_ticks_report_elapsed_time() -> void:
	var clock := _clock()
	var seen: Array = []
	clock.ticked.connect(func(elapsed: float) -> void: seen.append(elapsed))
	clock.start(10.0)
	clock.step(1.5)
	clock.step(0.5)
	assert_eq(seen, [1.5, 2.0])


func test_ended_fires_once_and_elapsed_is_clamped() -> void:
	var clock := _clock()
	var ended: Array = []
	clock.ended.connect(func() -> void: ended.append(true))
	clock.start(2.0)
	clock.step(5.0)
	clock.step(5.0)
	assert_eq(ended.size(), 1)
	assert_almost_eq(clock.elapsed, 2.0)
	assert_false(clock.is_running())


func test_stopped_clock_does_not_tick() -> void:
	var clock := _clock()
	var seen: Array = []
	clock.ticked.connect(func(elapsed: float) -> void: seen.append(elapsed))
	clock.start(10.0)
	clock.stop()
	clock.step(1.0)
	assert_true(seen.is_empty())


func test_start_resets_elapsed() -> void:
	var clock := _clock()
	clock.start(10.0)
	clock.step(4.0)
	clock.start(10.0)
	assert_almost_eq(clock.elapsed, 0.0)
	assert_true(clock.is_running())
