extends TestCase

const ARENA_SCENE: PackedScene = preload("res://src/stage/arena.tscn")


func test_build_centers_the_bounds_and_surrounds_them_with_walls() -> void:
	var arena := adopt(ARENA_SCENE.instantiate()) as Arena
	arena.build(Vector2(400, 300), DefFactory.texture(32, 32))
	assert_eq(arena.bounds, Rect2(-200, -150, 400, 300))
	var walls := arena.get_node("Walls") as StaticBody2D
	assert_eq(walls.get_child_count(), 4)
	var centers: Array = []
	for child in walls.get_children():
		centers.append((child as CollisionShape2D).position)
	assert_true(centers.has(Vector2(0, -250)), "top wall")
	assert_true(centers.has(Vector2(0, 250)), "bottom wall")
	assert_true(centers.has(Vector2(-300, 0)), "left wall")
	assert_true(centers.has(Vector2(300, 0)), "right wall")


func test_rebuilding_replaces_the_walls() -> void:
	var arena := adopt(ARENA_SCENE.instantiate()) as Arena
	arena.build(Vector2(400, 300), DefFactory.texture(32, 32))
	arena.build(Vector2(800, 600), DefFactory.texture(32, 32))
	assert_eq((arena.get_node("Walls") as StaticBody2D).get_child_count(), 4)
	assert_eq(arena.bounds.size, Vector2(800, 600))
