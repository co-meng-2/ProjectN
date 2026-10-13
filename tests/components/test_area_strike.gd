extends TestCase

const STRIKE_SCENE: PackedScene = preload("res://src/components/area_strike.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func _strike(at: Vector2) -> AreaStrike:
	var strike := adopt(STRIKE_SCENE.instantiate()) as AreaStrike
	strike.global_position = at
	return strike


func test_the_mask_selects_which_side_is_hit() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var ally := ActorFixture.hurt_target(self, Vector2(310, 200), PhysicsLayers.ALLY_HURT)
	var enemy := ActorFixture.hurt_target(self, Vector2(290, 200), PhysicsLayers.ENEMY_HURT)
	_strike(Vector2(300, 200)).strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await settle()
	assert_almost_eq(_health(ally), 85.0)
	assert_almost_eq(_health(enemy), 100.0, 0.001, "enemy hurt layer not in the mask")


func test_only_actor_narrows_the_strike_to_one_actor() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var player := ActorFixture.hurt_target(self, Vector2(-120, 40), PhysicsLayers.ALLY_HURT)
	var building := ActorFixture.hurt_target(self, Vector2(-100, 40), PhysicsLayers.ALLY_HURT)
	var struck: Array = []
	var strike := _strike(Vector2(-110, 40))
	strike.struck.connect(func(hurtbox: Hurtbox) -> void: struck.append(hurtbox.actor))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 30.0, 60.0, 0.3, PhysicsLayers.ALLY_HURT, player)
	await settle()
	assert_almost_eq(_health(player), 70.0)
	assert_almost_eq(_health(building), 100.0, 0.001, "the building is spared")
	assert_eq(struck, [player])


func test_a_refused_hit_is_not_reported_as_struck() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var dodger := ActorFixture.hurt_target(self, Vector2(50, -60), PhysicsLayers.ALLY_HURT)
	ActorFixture.asc_of(dodger).add_tag(GameplayTags.State_Invulnerable)
	var struck: Array = []
	var strike := _strike(Vector2(50, -60))
	strike.struck.connect(func(hurtbox: Hurtbox) -> void: struck.append(hurtbox))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await settle()
	assert_almost_eq(_health(dodger), 100.0)
	assert_true(struck.is_empty(), "an invulnerable target was not struck")


func test_the_attackers_attack_scales_the_damage() -> void:
	var stats := StatBlock.new()
	stats.attack = 2.0
	var attacker := ActorFixture.bare_actor(self, stats)
	var victim := ActorFixture.hurt_target(self, Vector2(400, 0), PhysicsLayers.ALLY_HURT)
	_strike(Vector2(400, 0)).strike(ActorFixture.asc_of(attacker), DAMAGE, 15.0, 40.0, 0.3, PhysicsLayers.ALLY_HURT)
	await settle()
	assert_almost_eq(_health(victim), 70.0, 0.001, "15 x attack 2")


func test_the_strike_frees_itself_after_its_seconds() -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var strike := _strike(Vector2(10, 10))
	strike.strike(ActorFixture.asc_of(attacker), DAMAGE, 1.0, 20.0, 0.1)
	await wait_seconds(0.3)
	assert_false(is_instance_valid(strike))
