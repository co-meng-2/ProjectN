extends TestCase


func test_marks_are_sorted() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([60.0, 20.0, 40.0]))
	assert_eq(timeline.marks, PackedFloat32Array([20.0, 40.0, 60.0]))


func test_progress_is_clamped() -> void:
	var timeline := Timeline.new(100.0)
	assert_almost_eq(timeline.progress(25.0), 0.25)
	assert_almost_eq(timeline.progress(-5.0), 0.0)
	assert_almost_eq(timeline.progress(500.0), 1.0)


func test_progress_with_zero_duration_is_complete() -> void:
	assert_almost_eq(Timeline.new(0.0).progress(3.0), 1.0)


func test_next_after_skips_current_and_past_marks() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([20.0, 40.0]))
	assert_almost_eq(timeline.next_after(0.0), 20.0)
	assert_almost_eq(timeline.next_after(20.0), 40.0)
	assert_almost_eq(timeline.next_after(40.0), -1.0)


func test_crossed_excludes_start_and_includes_end() -> void:
	var timeline := Timeline.new(100.0, PackedFloat32Array([20.0, 40.0, 60.0]))
	assert_eq(timeline.crossed(20.0, 40.0), PackedInt32Array([1]))
	assert_eq(timeline.crossed(0.0, 100.0), PackedInt32Array([0, 1, 2]))
	assert_eq(timeline.crossed(41.0, 59.0), PackedInt32Array())
