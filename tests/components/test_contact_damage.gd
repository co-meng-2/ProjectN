extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stats() -> StatBlock:
	var stats := StatBlock.new()
	stats.max_health = 100.0
	return stats


func _area(area: Area2D, layer: int, mask: int, parent: Node) -> Area2D:
	area.collision_layer = layer
	area.collision_mask = mask
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 10.0
	shape.shape = circle
	area.add_child(shape)
	parent.add_child(area)
	return area


func _rig() -> Array:
	var attacker := ActorFixture.bare_actor(self, _stats())
	var victim := ActorFixture.bare_actor(self, _stats())
	var hitbox := _area(Hitbox.new(), 0, PhysicsLayers.ALLY_HURT, attacker) as Hitbox
	hitbox.once_per_target = false
	var hurtbox := _area(Hurtbox.new(), PhysicsLayers.ALLY_HURT, 0, victim) as Hurtbox
	hurtbox.bind(ActorFixture.asc_of(victim), victim)
	var applier := EffectApplier.new()
	attacker.add_child(applier)
	applier.bind(ActorFixture.asc_of(attacker))
	var contact := ContactDamage.new()
	attacker.add_child(contact)
	contact.bind(hitbox, applier, DAMAGE, 10.0, 1.0)
	return [contact, victim]


func _health(victim: Node2D) -> float:
	return ActorFixture.asc_of(victim).get_attribute("health").current_value


func test_damages_overlapping_hurtbox_then_waits_for_interval() -> void:
	var rig := _rig()
	var contact: ContactDamage = rig[0]
	var victim: Node2D = rig[1]
	await next_physics_frame()
	await next_physics_frame()
	var attacks: Array = []
	contact.attacked.connect(func(targets: Array[Hurtbox]) -> void: attacks.append(targets.size()))
	contact.step(0.0)
	assert_almost_eq(_health(victim), 90.0)
	contact.step(0.5)
	assert_almost_eq(_health(victim), 90.0, 0.001, "still cooling down")
	contact.step(0.6)
	assert_almost_eq(_health(victim), 80.0)
	assert_eq(attacks, [1, 1])


func test_nothing_happens_without_overlap() -> void:
	var rig := _rig()
	var contact: ContactDamage = rig[0]
	var victim: Node2D = rig[1]
	(victim.get_child(-1) as Node2D).position = Vector2(1000, 0)
	await next_physics_frame()
	await next_physics_frame()
	contact.step(0.0)
	assert_almost_eq(_health(victim), 100.0)
