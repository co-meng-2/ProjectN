extends TestCase

const STRIKE_SCENE: PackedScene = preload("res://src/abilities/blade_sweep/melee_strike.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func test_strike_damages_each_hurtbox_in_radius_once_then_expires() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var near_a := ActorFixture.hurt_target(self, Vector2(20, 0))
	var near_b := ActorFixture.hurt_target(self, Vector2(-30, 10))
	var far := ActorFixture.hurt_target(self, Vector2(200, 0))
	var strike := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 50.0, 0.3)
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(near_a), 85.0)
	assert_almost_eq(_health(near_b), 85.0)
	assert_almost_eq(_health(far), 100.0)
	await next_physics_frame()
	await next_physics_frame()
	near_a.global_position = Vector2(400, 0)
	await next_physics_frame()
	await next_physics_frame()
	near_a.global_position = Vector2(20, 0)
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(_health(near_a), 85.0, 0.001, "once per target")
	await wait_seconds(0.4)
	assert_false(is_instance_valid(strike), "freed when the lifetime expires")


func test_strike_radius_is_applied_to_its_own_shape() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var first := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	var second := adopt(STRIKE_SCENE.instantiate()) as MeleeStrike
	first.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 95.0, 0.3)
	second.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 40.0, 0.3)
	assert_almost_eq((first.get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 95.0)
	assert_almost_eq((second.get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 40.0, 0.001, "shapes are local to each instance")
