extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const SUMMON_SCENE: PackedScene = preload("res://src/boss/patterns/summon_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(100, 50)


func _boss(summon: Callable) -> Boss:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(-500, 0)
	var patterns: Array[PackedScene] = [SUMMON_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT, summon)
	(boss.patterns()[0] as BossPattern).set_physics_process(false)
	return boss


func test_summons_count_enemies_on_the_ring_through_the_context() -> void:
	var calls: Array = []
	var boss := _boss(func(def: EnemyDef, position: Vector2) -> void: calls.append([def, position]))
	var pattern := boss.patterns()[0] as SummonPattern
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon))
	assert_eq(calls.size(), 4)
	var seen: Array = []
	for call_args in calls:
		assert_true(call_args[0] == pattern.enemy)
		assert_almost_eq((call_args[1] as Vector2).distance_to(BOSS_AT), 80.0, 0.01)
		assert_false(seen.has(call_args[1]), "distinct points")
		seen.append(call_args[1])
	assert_false(boss.is_casting())


func test_without_a_summon_callable_the_pattern_does_not_start() -> void:
	var boss := _boss(Callable())
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon)
	assert_false((boss.patterns()[0] as SummonPattern).is_active)
	assert_false(boss.is_casting())


func test_summoned_enemies_come_from_a_real_factory() -> void:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(layer, targets, RunRng.new(3))
	var boss := _boss(func(def: EnemyDef, position: Vector2) -> void: factory.spawn(def, position))
	var pattern := boss.patterns()[0] as SummonPattern
	pattern.enemy = DefFactory.enemy(ENEMY_SCENE)
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon)
	assert_eq(factory.alive_count(), 4)
	assert_eq(layer.get_child_count(), 4)


func test_validate_reports_a_missing_enemy_and_bad_numbers() -> void:
	var summon := SUMMON_SCENE.instantiate() as SummonPattern
	assert_no_errors(summon.validate(), "scene as shipped")
	assert_eq(summon.enemy.id, &"orc_swarmer")
	summon.enemy = null
	summon.radius = 0.0
	assert_eq(summon.validate().size(), 2)
	summon.free()
