extends TestCase


func _bar() -> TimelineBar:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(100.0, PackedFloat32Array([25.0, 50.0]))
	return bar


func test_mark_x_scales_with_width_and_duration() -> void:
	var bar := _bar()
	assert_almost_eq(bar.mark_x(25.0), 100.0)
	assert_almost_eq(bar.mark_x(50.0), 200.0)
	assert_almost_eq(bar.mark_x(500.0), 400.0, 0.001, "clamped to the bar")


func test_zero_duration_does_not_divide_by_zero() -> void:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(0.0, PackedFloat32Array([10.0]))
	bar.set_elapsed(5.0)
	assert_almost_eq(bar.mark_x(10.0), 0.0)


func test_event_marks_are_kept_apart_from_wave_marks() -> void:
	var bar := adopt(TimelineBar.new()) as TimelineBar
	bar.size = Vector2(400, 20)
	bar.configure(100.0, PackedFloat32Array([25.0]), PackedFloat32Array([40.0, 70.0]))
	assert_eq(bar.event_marks(), PackedFloat32Array([40.0, 70.0]))
	assert_almost_eq(bar.mark_x(70.0), 280.0)
	bar.configure(100.0, PackedFloat32Array([25.0]))
	assert_eq(bar.event_marks().size(), 0, "reconfiguring without events clears them")


func test_the_boss_label_appears_once_the_boss_does() -> void:
	var bar := _bar()
	assert_false(bar.is_showing_boss())
	bar.show_boss()
	assert_true(bar.is_showing_boss())
	var label := bar.get_node("BossLabel") as Label
	assert_true(label.visible)
	assert_eq(label.text, "BOSS")
