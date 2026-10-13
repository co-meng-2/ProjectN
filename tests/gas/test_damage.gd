extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float, attack: float = 1.0) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	stats.attack = attack
	return stats


func _hit(attacker: Node2D, victim: Node2D, amount: float) -> ActiveGameplayEffect:
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	return ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))


func _health(actor: Node2D) -> float:
	return ActorFixture.asc_of(actor).get_attribute("health").current_value


func test_overrides_set_health_and_max_health() -> void:
	var actor := ActorFixture.bare_actor(self, _stats(50.0))
	assert_almost_eq(_health(actor), 50.0)
	assert_almost_eq(ActorFixture.asc_of(actor).get_attribute("max_health").current_value, 50.0)


func test_health_is_clamped_to_max_health() -> void:
	var actor := ActorFixture.bare_actor(self, _stats(50.0))
	var overrides: Dictionary[String, float] = {"health": 80.0}
	ActorFixture.asc_of(actor).initialize_attribute_overrides(overrides)
	assert_almost_eq(_health(actor), 50.0)


func test_damage_scales_with_source_attack() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0, 2.0))
	var victim := ActorFixture.bare_actor(self, _stats(100.0))
	assert_not_null(_hit(attacker, victim, 10.0))
	assert_almost_eq(_health(victim), 80.0)


func test_damage_cannot_push_health_below_zero() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(30.0))
	_hit(attacker, victim, 500.0)
	assert_almost_eq(_health(victim), 0.0)


func test_invulnerable_target_takes_no_damage_until_the_tag_is_removed() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(100.0))
	var victim_asc := ActorFixture.asc_of(victim)
	victim_asc.add_tag(GameplayTags.State_Invulnerable)
	assert_null(_hit(attacker, victim, 10.0), "blocked hits return null")
	assert_almost_eq(_health(victim), 100.0)
	victim_asc.remove_tag(GameplayTags.State_Invulnerable)
	_hit(attacker, victim, 10.0)
	assert_almost_eq(_health(victim), 90.0)


func test_attribute_sets_are_not_shared_between_actors() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var first := ActorFixture.bare_actor(self, _stats(100.0))
	var second := ActorFixture.bare_actor(self, _stats(100.0))
	_hit(attacker, first, 40.0)
	assert_almost_eq(_health(first), 60.0)
	assert_almost_eq(_health(second), 100.0)
