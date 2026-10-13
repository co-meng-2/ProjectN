extends TestCase


func test_layers_are_distinct_single_bits() -> void:
	var layers := [
		PhysicsLayers.WORLD, PhysicsLayers.ALLY_BODY, PhysicsLayers.ENEMY_BODY,
		PhysicsLayers.ALLY_HURT, PhysicsLayers.ENEMY_HURT, PhysicsLayers.PICKUP,
	]
	var combined := 0
	for layer: int in layers:
		assert_eq(layer & (layer - 1), 0, "single bit")
		assert_eq(combined & layer, 0, "overlap")
		combined |= layer
