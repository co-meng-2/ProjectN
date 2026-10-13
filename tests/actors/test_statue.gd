extends TestCase

const STATUE_SCENE: PackedScene = preload("res://src/actors/statue/statue.tscn")


func _statue(at: Vector2, max_health: float = 60.0) -> Statue:
	var statue := adopt(STATUE_SCENE.instantiate()) as Statue
	statue.global_position = at
	var stats := StatBlock.new()
	stats.max_health = max_health
	stats.move_speed = 0.0
	statue.setup(stats)
	return statue


func test_setup_applies_stats_and_makes_it_a_target() -> void:
	var statue := _statue(Vector2(240, -130), 60.0)
	assert_almost_eq(statue.health.current(), 60.0)
	assert_true(statue.is_in_group(Groups.ENEMIES))
	var targets := adopt(TargetProvider.new()) as TargetProvider
	assert_eq(targets.nearest_enemy(Vector2(200, -100), 100.0), statue, "auto-aim sees it")


func test_destroying_it_announces_once_and_frees_it() -> void:
	var statue := _statue(Vector2(-300, 80))
	var destroyed: Array = []
	statue.destroyed.connect(func(found: Statue) -> void: destroyed.append(found))
	ActorFixture.kill(self, statue.asc)
	assert_eq(destroyed, [statue])
	assert_false(statue.is_in_group(Groups.ENEMIES))
	assert_true(statue.is_removed())
	ActorFixture.kill(self, statue.asc)
	assert_eq(destroyed.size(), 1, "dead once")
	await next_frame()
	await next_frame()
	assert_false(is_instance_valid(statue))


func test_remove_takes_it_out_without_counting_as_destroyed() -> void:
	var statue := _statue(Vector2(90, 410))
	var destroyed: Array = []
	statue.destroyed.connect(func(found: Statue) -> void: destroyed.append(found))
	statue.remove()
	statue.remove()
	assert_true(destroyed.is_empty())
	assert_false(statue.is_in_group(Groups.ENEMIES))
	await next_frame()
	await next_frame()
	assert_false(is_instance_valid(statue))


func test_a_projectile_on_the_enemy_layer_can_damage_it() -> void:
	var statue := _statue(Vector2(500, 300))
	var shooter := ActorFixture.bare_actor(self, StatBlock.new())
	var projectile := adopt(preload("res://src/actors/projectile/projectile.tscn").instantiate()) as Projectile
	projectile.global_position = Vector2(490, 284)
	projectile.launch(preload("res://data/projectiles/arrow.tres"), ActorFixture.asc_of(shooter), 12.0, Vector2.RIGHT)
	await settle()
	await next_physics_frame()
	assert_almost_eq(statue.health.current(), 48.0)
