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


func test_nearest_enemies_returns_up_to_count_sorted_by_distance() -> void:
	var provider := _provider()
	var fifty := _enemy(Vector2(50, 0))
	_enemy(Vector2(150, 0))
	_enemy(Vector2(400, 0))
	var ninety := _enemy(Vector2(0, 90))
	var two := provider.nearest_enemies(Vector2.ZERO, 300.0, 2)
	assert_eq(two.size(), 2)
	assert_eq(two[0], fifty)
	assert_eq(two[1], ninety)
	assert_eq(provider.nearest_enemies(Vector2.ZERO, 300.0, 10).size(), 3, "the one at 400 is out of range")
	assert_true(provider.nearest_enemies(Vector2.ZERO, 300.0, 0).is_empty())


func test_nearest_enemies_skips_doomed_nodes() -> void:
	var provider := _provider()
	var doomed := _enemy(Vector2(10, 0))
	var other := _enemy(Vector2(80, 0))
	doomed.queue_free()
	assert_eq(provider.nearest_enemies(Vector2.ZERO, 300.0, 5), [other])
