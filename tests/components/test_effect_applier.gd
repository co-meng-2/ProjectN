extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return stats


func _hurtbox_for(actor: Node2D) -> Hurtbox:
	var hurtbox := Hurtbox.new()
	actor.add_child(hurtbox)
	hurtbox.bind(ActorFixture.asc_of(actor), actor)
	return hurtbox


func _applier_for(actor: Node2D) -> EffectApplier:
	var applier := EffectApplier.new()
	actor.add_child(applier)
	applier.bind(ActorFixture.asc_of(actor))
	return applier


func test_hit_damages_the_target() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(100.0))
	assert_true(_applier_for(attacker).hit(_hurtbox_for(victim), DAMAGE, 15.0))
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 85.0)


func test_hit_reports_false_when_target_is_invulnerable() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(100.0))
	ActorFixture.asc_of(victim).add_tag(GameplayTags.State_Invulnerable)
	assert_false(_applier_for(attacker).hit(_hurtbox_for(victim), DAMAGE, 15.0))


func test_hit_with_a_freed_source_is_a_safe_no_op() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(100.0))
	var applier := _applier_for(attacker)
	var hurtbox := _hurtbox_for(victim)
	ActorFixture.asc_of(attacker).free()
	assert_false(applier.hit(hurtbox, DAMAGE, 15.0))


func test_hit_with_an_unbound_hurtbox_is_a_safe_no_op() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var hurtbox := adopt(Hurtbox.new()) as Hurtbox
	assert_false(_applier_for(attacker).hit(hurtbox, DAMAGE, 15.0))
