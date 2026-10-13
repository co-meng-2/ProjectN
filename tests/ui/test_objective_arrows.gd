extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")
const VIEW: Vector2 = Vector2(800, 600)


func _arrows() -> ObjectiveArrows:
	var arrows := adopt(ObjectiveArrows.new()) as ObjectiveArrows
	arrows.set_process(false)
	arrows.margin = 20.0
	return arrows


func _probe_with(points: Array[Vector2]) -> ProbeEvent:
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	for point in points:
		var node := adopt(Node2D.new()) as Node2D
		node.global_position = point
		event.objective_nodes.append(node)
	return event


func test_edge_point_clamps_to_the_inner_rect_along_the_ray_from_the_centre() -> void:
	var view := Rect2(Vector2.ZERO, VIEW)
	assert_eq(ObjectiveArrows.edge_point(Vector2(1600, 300), view, 20.0), Vector2(780, 300))
	assert_eq(ObjectiveArrows.edge_point(Vector2(400, -900), view, 20.0), Vector2(400, 20))
	var corner := ObjectiveArrows.edge_point(Vector2(1200, 1200), view, 20.0)
	assert_almost_eq(corner.x, 400.0 + 800.0 * 280.0 / 900.0, 0.01)
	assert_almost_eq(corner.y, 580.0, 0.01)


func test_only_off_screen_objectives_get_arrows() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(100, 100), Vector2(2000, 300)]
	arrows.track(_probe_with(points))
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions(), PackedVector2Array([Vector2(780, 300)]))


func test_the_camera_transform_decides_what_is_on_screen() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(1400, 300)]
	arrows.track(_probe_with(points))
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 1, "off-screen without a camera offset")
	arrows.refresh(Transform2D(0.0, Vector2(-1000, 0)), VIEW)
	assert_eq(arrows.arrow_positions().size(), 0, "the camera moved right: now on screen")


func test_cleared_or_freed_objectives_have_no_arrows() -> void:
	var arrows := _arrows()
	var points: Array[Vector2] = [Vector2(-900, 300)]
	var event := _probe_with(points)
	arrows.track(event)
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 1)
	event.objective_nodes[0].free()
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 0)
	arrows.clear()
	arrows.refresh(Transform2D.IDENTITY, VIEW)
	assert_eq(arrows.arrow_positions().size(), 0)
