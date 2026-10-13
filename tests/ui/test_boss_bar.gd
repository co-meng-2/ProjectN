extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func test_hidden_until_the_boss_appears_then_follows_its_health() -> void:
	var bar := adopt(BossBar.new()) as BossBar
	assert_false(bar.visible)
	var stats := StatBlock.new()
	stats.max_health = 2500.0
	var boss := ActorFixture.bare_actor(self, stats)
	var health := Health.new()
	boss.add_child(health)
	health.bind(ActorFixture.asc_of(boss))
	bar.show_boss(health, "Orc Warlord")
	assert_true(bar.visible)
	assert_eq(bar.boss_name(), "Orc Warlord")
	assert_almost_eq(bar.maximum(), 2500.0)
	assert_almost_eq(bar.value(), 2500.0)
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(EffectBuilder.damage(attacker, attacker, DAMAGE, 400.0), ActorFixture.asc_of(boss))
	assert_almost_eq(bar.value(), 2100.0)
