extends TestCase

const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _table(chance: float) -> DropTable:
	var entry := DropEntry.new()
	entry.kind = Wallet.Kind.GOLD
	entry.chance = chance
	entry.min_amount = 3
	entry.max_amount = 3
	var table := DropTable.new()
	table.entries.append(entry)
	return table


func _kill(table: DropTable) -> Array:
	var stats := StatBlock.new()
	stats.max_health = 10.0
	var killer := ActorFixture.bare_actor(self, stats)
	var victim := ActorFixture.bare_actor(self, stats)
	victim.position = Vector2(40, 60)
	var health := Health.new()
	victim.add_child(health)
	health.bind(ActorFixture.asc_of(victim))
	var dropper := LootDropper.new()
	victim.add_child(dropper)
	dropper.bind(health, table, RunRng.new(3), victim)
	var drops: Array = []
	dropper.drop_requested.connect(func(roll: DropRoll, position: Vector2) -> void: drops.append([roll, position]))
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, ActorFixture.asc_of(victim))
	return drops


func test_death_requests_the_rolled_drops_at_the_anchor() -> void:
	var drops := _kill(_table(1.0))
	assert_eq(drops.size(), 1)
	assert_eq(drops[0][0].amount, 3)
	assert_true((drops[0][1] as Vector2).distance_to(Vector2(40, 60)) <= 12.0, "scattered near the anchor")


func test_no_table_means_no_drops() -> void:
	assert_true(_kill(null).is_empty())


func test_failed_chance_means_no_drops() -> void:
	assert_true(_kill(_table(0.0)).is_empty())
