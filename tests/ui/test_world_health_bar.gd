extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _health_for(actor: Node2D) -> Health:
	var health := Health.new()
	actor.add_child(health)
	health.bind(ActorFixture.asc_of(actor))
	return health


func _rig(max_health: float = 50.0) -> Dictionary:
	var stats := StatBlock.new()
	stats.max_health = max_health
	var actor := ActorFixture.bare_actor(self, stats)
	var health := _health_for(actor)
	var bar := adopt(WorldHealthBar.new()) as WorldHealthBar
	bar.bind(health, actor, Vector2(0, -60))
	return {"bar": bar, "actor": actor, "health": health}


func test_fill_ratio_follows_health_changed() -> void:
	var rig := _rig()
	var bar: WorldHealthBar = rig["bar"]
	assert_almost_eq(bar.fill_ratio(), 1.0)
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, 25.0)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(rig["actor"]))
	assert_almost_eq(bar.fill_ratio(), 0.5)


func test_fill_ratio_is_zero_when_maximum_is_zero() -> void:
	var bar := adopt(WorldHealthBar.new()) as WorldHealthBar
	assert_almost_eq(bar.fill_ratio(), 0.0)
	var health := adopt(Health.new()) as Health
	bar.bind(health, null, Vector2.ZERO)
	health.changed.emit(10.0, 0.0)
	assert_almost_eq(bar.fill_ratio(), 0.0)


func test_fill_ratio_is_clamped() -> void:
	var rig := _rig()
	var bar: WorldHealthBar = rig["bar"]
	var health: Health = rig["health"]
	health.changed.emit(80.0, 50.0)
	assert_almost_eq(bar.fill_ratio(), 1.0)
	health.changed.emit(-5.0, 50.0)
	assert_almost_eq(bar.fill_ratio(), 0.0)


func test_bind_positions_at_anchor_plus_offset_and_follows_the_anchor() -> void:
	var rig := _rig()
	var bar: WorldHealthBar = rig["bar"]
	var actor: Node2D = rig["actor"]
	actor.global_position = Vector2(100, 40)
	bar.step()
	assert_eq(bar.global_position, Vector2(100, -20))
	actor.global_position = Vector2(-30, 10)
	bar.step()
	assert_eq(bar.global_position, Vector2(-30, -50))


func test_freed_anchor_hides_the_bar_without_error() -> void:
	var rig := _rig()
	var bar: WorldHealthBar = rig["bar"]
	assert_true(bar.visible)
	(rig["actor"] as Node2D).free()
	bar.step()
	assert_false(bar.visible)


func test_fill_colour_shifts_green_orange_red() -> void:
	var bar := adopt(WorldHealthBar.new()) as WorldHealthBar
	assert_eq(bar.colour_for(1.0), bar.fill_colour_high)
	assert_eq(bar.colour_for(0.5), bar.fill_colour_mid)
	assert_eq(bar.colour_for(0.1), bar.fill_colour_low)


func test_hides_at_full_health_when_not_always_visible() -> void:
	var rig := _rig()
	var bar: WorldHealthBar = rig["bar"]
	bar.always_visible = false
	bar.step()
	assert_false(bar.visible)
	(rig["health"] as Health).changed.emit(10.0, 50.0)
	bar.step()
	assert_true(bar.visible)
