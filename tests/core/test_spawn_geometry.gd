extends TestCase


func test_ring_point_is_radius_away() -> void:
	var point := SpawnGeometry.ring_point(Vector2(10, 10), 50.0, 0.0)
	assert_almost_eq(point.x, 60.0)
	assert_almost_eq(point.y, 10.0)


func test_clamp_to_rect_respects_margin() -> void:
	var bounds := Rect2(-100, -100, 200, 200)
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(500, 0), bounds, 10.0), Vector2(90, 0))
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(0, -500), bounds), Vector2(0, -100))
	assert_eq(SpawnGeometry.clamp_to_rect(Vector2(5, 5), bounds), Vector2(5, 5))
