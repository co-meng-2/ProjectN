extends TestCase


func _telegraph() -> Telegraph:
	var telegraph := adopt(Telegraph.new()) as Telegraph
	telegraph.set_physics_process(false)
	return telegraph


func test_circle_counts_down_then_announces_and_frees_itself() -> void:
	var telegraph := _telegraph()
	var elapsed: Array = []
	telegraph.elapsed.connect(func() -> void: elapsed.append(true))
	telegraph.show_circle(60.0, 1.0)
	assert_eq(telegraph.shape, Telegraph.Shape.CIRCLE)
	assert_almost_eq(telegraph.radius, 60.0)
	telegraph.step(0.4)
	assert_almost_eq(telegraph.remaining(), 0.6)
	assert_almost_eq(telegraph.progress(), 0.4)
	assert_true(elapsed.is_empty())
	telegraph.step(0.7)
	assert_eq(elapsed.size(), 1)
	assert_true(telegraph.is_queued_for_deletion())
	telegraph.step(0.5)
	assert_eq(elapsed.size(), 1, "announces once")


func test_line_keeps_its_offset_and_width() -> void:
	var telegraph := _telegraph()
	telegraph.show_line(Vector2(360, -40), 24.0, 0.7)
	assert_eq(telegraph.shape, Telegraph.Shape.LINE)
	assert_eq(telegraph.line_offset, Vector2(360, -40))
	assert_almost_eq(telegraph.line_width, 24.0)
	assert_almost_eq(telegraph.remaining(), 0.7)


func test_an_unarmed_telegraph_does_nothing() -> void:
	var telegraph := _telegraph()
	var elapsed: Array = []
	telegraph.elapsed.connect(func() -> void: elapsed.append(true))
	telegraph.step(5.0)
	assert_true(elapsed.is_empty())
	assert_false(telegraph.is_queued_for_deletion())


func test_spawn_circle_places_a_running_telegraph_under_the_parent() -> void:
	var parent := adopt(Node2D.new()) as Node2D
	var telegraph := Telegraph.spawn_circle(parent, Vector2(130, -70), 55.0, 0.9)
	assert_eq(telegraph.get_parent(), parent)
	assert_eq(telegraph.global_position, Vector2(130, -70))
	assert_eq(telegraph.shape, Telegraph.Shape.CIRCLE)
	assert_almost_eq(telegraph.radius, 55.0)
	assert_almost_eq(telegraph.remaining(), 0.9)
	assert_eq(ActorFixture.live_children(parent, Telegraph), [telegraph])
	telegraph.step(1.0)
	assert_true(ActorFixture.live_children(parent, Telegraph).is_empty(), "a finished telegraph is not live")


func test_spawn_line_runs_from_the_start_to_the_end_point() -> void:
	var parent := adopt(Node2D.new()) as Node2D
	var telegraph := Telegraph.spawn_line(parent, Vector2(-50, 20), Vector2(310, -20), 24.0, 0.7)
	assert_eq(telegraph.global_position, Vector2(-50, 20))
	assert_eq(telegraph.shape, Telegraph.Shape.LINE)
	assert_eq(telegraph.line_offset, Vector2(360, -40))
	assert_almost_eq(telegraph.line_width, 24.0)
	assert_almost_eq(telegraph.remaining(), 0.7)
