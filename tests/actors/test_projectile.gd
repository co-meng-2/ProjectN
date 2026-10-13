extends TestCase

const PROJECTILE_SCENE: PackedScene = preload("res://src/actors/projectile/projectile.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _def(pierce: int = 0, lifetime: float = 1.0) -> ProjectileDef:
	var def := ProjectileDef.new()
	def.texture = DefFactory.texture(32, 32)
	def.hit_effect = DAMAGE
	def.speed = 50.0
	def.lifetime = lifetime
	def.pierce = pierce
	def.hit_radius = 8.0
	return def


func _actor(max_health: float = 100.0) -> Node2D:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return ActorFixture.bare_actor(self, stats)


func _victim() -> Node2D:
	var victim := _actor()
	var hurtbox := Hurtbox.new()
	hurtbox.collision_layer = PhysicsLayers.ENEMY_HURT
	hurtbox.collision_mask = 0
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 12.0
	shape.shape = circle
	hurtbox.add_child(shape)
	victim.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(victim), victim)
	return victim


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func _fire(shooter: Node2D, def: ProjectileDef) -> Projectile:
	var projectile := adopt(PROJECTILE_SCENE.instantiate()) as Projectile
	projectile.launch(def, ActorFixture.asc_of(shooter), 10.0, Vector2.RIGHT)
	return projectile


func test_projectile_damages_a_victim_and_is_consumed() -> void:
	var shooter := _actor()
	var victim := _victim()
	var projectile := _fire(shooter, _def())
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(victim), 90.0)
	assert_true(_consumed(projectile))


func test_projectile_without_pierce_hits_only_one_of_two_overlapping_victims() -> void:
	var shooter := _actor()
	var first := _victim()
	var second := _victim()
	_fire(shooter, _def(0))
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	var damaged := 0
	for victim in [first, second]:
		if _health(victim) < 100.0:
			damaged += 1
	assert_eq(damaged, 1)


func test_projectile_with_pierce_hits_both() -> void:
	var shooter := _actor()
	var first := _victim()
	var second := _victim()
	_fire(shooter, _def(1))
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(first), 90.0)
	assert_almost_eq(_health(second), 90.0)


func test_projectile_survives_freed_shooter() -> void:
	var shooter := _actor()
	var victim := _victim()
	var projectile := _fire(shooter, _def())
	ActorFixture.asc_of(shooter).free()
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(victim), 100.0, 0.001, "no damage without a living source")
	assert_true(_consumed(projectile), "still consumed")


func test_projectile_expires_after_its_lifetime() -> void:
	var shooter := _actor()
	var projectile := _fire(shooter, _def(0, 0.1))
	await wait_seconds(0.4)
	assert_true(_consumed(projectile))


func test_projectile_flies_along_its_direction() -> void:
	var shooter := _actor()
	var projectile := _fire(shooter, _def())
	var start := projectile.global_position
	await wait_seconds(0.2)
	assert_true(projectile.global_position.x > start.x + 3.0)
	assert_almost_eq(projectile.global_position.y, start.y, 0.01)


func _consumed(projectile: Variant) -> bool:
	return not is_instance_valid(projectile) or projectile.is_queued_for_deletion()


func _victim_on(layer: int, at: Vector2) -> Node2D:
	return ActorFixture.hurt_target(self, at, layer)


func test_an_ally_hitting_projectile_damages_allies_and_passes_enemies() -> void:
	var shooter := _actor()
	var def := _def(1)
	def.hits = ProjectileDef.Team.ALLIES
	var ally := _victim_on(PhysicsLayers.ALLY_HURT, Vector2(4, 0))
	var enemy := _victim_on(PhysicsLayers.ENEMY_HURT, Vector2(-4, 0))
	var projectile := _fire(shooter, def)
	assert_eq((projectile.get_node("Hitbox") as Area2D).collision_mask, PhysicsLayers.ALLY_HURT)
	await next_physics_frame()
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(ally), 90.0)
	assert_almost_eq(_health(enemy), 100.0, 0.001, "pierce 1 would have hit it too if the mask matched")


func test_projectiles_hit_enemies_by_default() -> void:
	assert_eq(ProjectileDef.new().hit_mask(), PhysicsLayers.ENEMY_HURT)
	var projectile := adopt(PROJECTILE_SCENE.instantiate()) as Projectile
	var hitbox := projectile.get_node("Hitbox") as Area2D
	hitbox.collision_mask = PhysicsLayers.ALLY_HURT
	projectile.launch(_def(), ActorFixture.asc_of(_actor()), 10.0, Vector2.RIGHT)
	assert_eq(hitbox.collision_mask, PhysicsLayers.ENEMY_HURT, "launch resets the mask from the def")
