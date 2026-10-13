extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats(max_health: float) -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = max_health
	return stats


func _hit(attacker: Node2D, victim: Node2D, amount: float) -> void:
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))


func _health_for(actor: Node2D) -> Health:
	var health := Health.new()
	actor.add_child(health)
	return health


func test_bind_emits_initial_values() -> void:
	var victim := ActorFixture.bare_actor(self, _stats(50.0))
	var health := _health_for(victim)
	var seen: Array = []
	health.changed.connect(func(current: float, maximum: float) -> void: seen.append([current, maximum]))
	health.bind(ActorFixture.asc_of(victim))
	assert_eq(seen, [[50.0, 50.0]])


func test_damage_emits_damaged_with_instigator_and_changed() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(50.0))
	var health := _health_for(victim)
	health.bind(ActorFixture.asc_of(victim))
	var damages: Array = []
	var changes: Array = []
	health.damaged.connect(func(amount: float, instigator: Node) -> void: damages.append([amount, instigator]))
	health.changed.connect(func(current: float, _maximum: float) -> void: changes.append(current))
	_hit(attacker, victim, 12.0)
	assert_eq(damages, [[12.0, attacker]])
	assert_eq(changes, [38.0])
	assert_almost_eq(health.current(), 38.0)


func test_lethal_damage_emits_died_exactly_once() -> void:
	var attacker := ActorFixture.bare_actor(self, _stats(100.0))
	var victim := ActorFixture.bare_actor(self, _stats(20.0))
	var health := _health_for(victim)
	health.bind(ActorFixture.asc_of(victim))
	var deaths: Array = []
	health.died.connect(func() -> void: deaths.append(true))
	_hit(attacker, victim, 500.0)
	_hit(attacker, victim, 500.0)
	assert_eq(deaths.size(), 1)
	assert_true(health.is_dead)
