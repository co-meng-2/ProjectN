extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const BUILDING_SCENE: PackedScene = preload("res://src/actors/building/building.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _def(max_health: float = 30.0, with_drop: bool = false) -> EnemyDef:
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.stats.max_health = max_health
	def.stats.move_speed = 120.0
	def.contact_effect = DAMAGE
	def.contact_damage = 10.0
	if with_drop:
		var entry := DropEntry.new()
		entry.chance = 1.0
		entry.icon = DefFactory.texture(8, 8)
		def.drops = DropTable.new()
		def.drops.entries.append(entry)
	return def


func _enemy(def: EnemyDef, target: Node2D, at: Vector2 = Vector2.ZERO) -> Enemy:
	var enemy := adopt(ENEMY_SCENE.instantiate()) as Enemy
	enemy.global_position = at
	enemy.setup(def, target, RunRng.new(5))
	return enemy


func _kill(enemy: Enemy) -> void:
	var stats := StatBlock.new()
	var killer := ActorFixture.bare_actor(self, stats)
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 9999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, enemy.get_node("AbilitySystemComponent"))


func test_setup_applies_stats_and_joins_the_enemy_group() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(45.0), target)
	assert_almost_eq((enemy.get_node("Health") as Health).current(), 45.0)
	assert_true(enemy.is_in_group(Groups.ENEMIES))


func test_enemy_walks_toward_its_target() -> void:
	var target := adopt(Node2D.new()) as Node2D
	target.global_position = Vector2(300, 0)
	var enemy := _enemy(_def(), target)
	await wait_seconds(0.3)
	assert_true(enemy.global_position.x > 5.0, "moved toward +x")
	assert_almost_eq(enemy.global_position.y, 0.0, 0.5)


func test_enemy_without_a_valid_target_stands_still() -> void:
	var target := Node2D.new()
	var enemy := _enemy(_def(), target)
	target.free()
	await wait_seconds(0.2)
	assert_almost_eq(enemy.global_position.length(), 0.0, 0.5)


func test_enemy_damages_an_overlapping_ally_hurtbox() -> void:
	var stats := StatBlock.new()
	stats.max_health = 200.0
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	_enemy(_def(), building)
	await wait_seconds(0.4)
	assert_true(building.health.current() < 200.0)


func test_death_emits_signals_leaves_the_group_and_frees_itself() -> void:
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(30.0, true), target, Vector2(10, 20))
	var deaths: Array = []
	var drops: Array = []
	enemy.died.connect(func(found: Enemy) -> void: deaths.append(found))
	enemy.loot_dropped.connect(func(roll: DropRoll, position: Vector2) -> void: drops.append([roll, position]))
	_kill(enemy)
	assert_eq(deaths, [enemy])
	assert_eq(drops.size(), 1)
	assert_false(enemy.is_in_group(Groups.ENEMIES))
	await wait_seconds(0.9)
	assert_false(is_instance_valid(enemy), "freed after the death delay")


func test_dead_enemy_is_not_targetable() -> void:
	var provider := adopt(TargetProvider.new()) as TargetProvider
	var target := adopt(Node2D.new()) as Node2D
	var enemy := _enemy(_def(), target, Vector2(40, 0))
	assert_eq(provider.nearest_enemy(Vector2.ZERO, 200.0), enemy)
	_kill(enemy)
	assert_null(provider.nearest_enemy(Vector2.ZERO, 200.0))


func test_dead_enemy_stops_dealing_damage() -> void:
	var stats := StatBlock.new()
	stats.max_health = 200.0
	var building := adopt(BUILDING_SCENE.instantiate()) as Building
	building.setup(DefFactory.texture(64, 64), stats)
	var def := _def()
	def.contact_interval = 0.2
	var enemy := _enemy(def, building)
	await wait_seconds(0.3)
	assert_true(building.health.current() < 200.0, "enemy hit the building before dying")
	_kill(enemy)
	var health_at_death := building.health.current()
	await wait_seconds(0.5)
	assert_almost_eq(building.health.current(), health_at_death)
