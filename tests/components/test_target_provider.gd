extends TestCase


func _provider() -> TargetProvider:
	return adopt(TargetProvider.new()) as TargetProvider


func _enemy(at: Vector2) -> Node2D:
	var node := adopt(Node2D.new()) as Node2D
	node.add_to_group(Groups.ENEMIES)
	node.global_position = at
	return node


func test_nearest_enemy_picks_closest_within_range() -> void:
	var provider := _provider()
	var far := _enemy(Vector2(200, 0))
	var near := _enemy(Vector2(50, 0))
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 300.0), near)
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 100.0), near)
	far.queue_free()


func test_nearest_enemy_is_null_when_nothing_is_in_range() -> void:
	var provider := _provider()
	_enemy(Vector2(500, 0))
	assert_null(provider.nearest_enemy(Vector2.ZERO, 100.0))


func test_nearest_enemy_skips_queued_for_deletion() -> void:
	var provider := _provider()
	var doomed := _enemy(Vector2(10, 0))
	var other := _enemy(Vector2(80, 0))
	doomed.queue_free()
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 300.0), other)


func test_nearest_enemy_skips_nodes_that_left_the_group() -> void:
	var provider := _provider()
	var dying := _enemy(Vector2(10, 0))
	dying.remove_from_group(Groups.ENEMIES)
	assert_null(provider.nearest_enemy(Vector2.ZERO, 300.0))


func test_target_for_prefers_policy_target_then_falls_back() -> void:
	var provider := _provider()
	var building := adopt(Node2D.new()) as Node2D
	var player := adopt(Node2D.new()) as Node2D
	provider.building = building
	provider.player = player
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.BUILDING), building)
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.PLAYER), player)
	player.free()
	assert_eq(provider.target_for(EnemyDef.TargetPolicy.PLAYER), building)
	building.free()
	assert_null(provider.target_for(EnemyDef.TargetPolicy.BUILDING))
