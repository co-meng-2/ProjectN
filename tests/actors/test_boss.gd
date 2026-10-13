extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")


func _def() -> BossDef:
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def := DefFactory.boss(BOSS_SCENE, patterns)
	def.stats.move_speed = 120.0
	return def


func _rig(summon: Callable = Callable()) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(300, 60)
	var boss := ActorFixture.boss(self, player, _def(), Vector2(-200, 60), summon)
	var pattern := boss.patterns()[0] as ProbePattern
	pattern.set_physics_process(false)
	return {"player": player, "boss": boss, "pattern": pattern}


func test_setup_applies_stats_joins_the_enemy_group_and_grants_the_patterns() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var pattern: ProbePattern = rig["pattern"]
	assert_almost_eq(boss.health.current(), 500.0)
	assert_true(boss.is_in_group(Groups.ENEMIES))
	assert_eq(boss.patterns().size(), 1)
	assert_eq(boss.pattern_weights().size(), 1)
	assert_almost_eq(boss.pattern_weights()[0], 1.0)
	assert_true(pattern.owner_asc == boss.asc, "granted to the boss's ASC")
	assert_true(pattern.context == boss.ability_context)
	assert_true(pattern.context.actor == boss)
	assert_true(pattern.context.targets == player.ability_context.targets)
	assert_null(pattern.context.wallet, "the boss has no wallet")
	assert_false(pattern.context.summon.is_valid(), "no summon injected")


func test_the_injected_summon_reaches_the_patterns() -> void:
	var calls: Array = []
	var summon := func(def: EnemyDef, position: Vector2) -> void: calls.append([def, position])
	var rig := _rig(summon)
	var pattern: ProbePattern = rig["pattern"]
	pattern.context.summon.call(null, Vector2(7, 8))
	assert_eq(calls, [[null, Vector2(7, 8)]])


func test_chases_its_target_and_stands_still_while_casting() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	await wait_seconds(0.3)
	assert_true(boss.global_position.x > -190.0, "moved toward the player on +x")
	boss.asc.add_tag(GameplayTags.State_Casting)
	await next_physics_frame()
	var held := boss.global_position
	await wait_seconds(0.3)
	assert_almost_eq(boss.global_position.distance_to(held), 0.0, 0.5, "no chasing while casting")
	assert_true(boss.is_casting())


func test_death_aborts_patterns_leaves_the_group_and_frees_itself() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var pattern: ProbePattern = rig["pattern"]
	pattern.hold = true
	pattern.set_physics_process(true)  # so the post-death assertion can fail
	assert_true(boss.asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"))
	assert_true(boss.is_casting())
	var deaths: Array = []
	boss.died.connect(func(found: Boss) -> void: deaths.append(found))
	ActorFixture.kill(self, boss.asc)
	assert_eq(deaths, [boss])
	assert_eq(pattern.aborts, 1)
	assert_false(pattern.is_active)
	assert_false(boss.is_casting(), "State.Casting stripped by the abort")
	assert_false(boss.is_in_group(Groups.ENEMIES))
	assert_false(pattern.is_physics_processing())
	ActorFixture.kill(self, boss.asc)
	assert_eq(deaths.size(), 1, "dies once")
	await wait_seconds(1.3)
	assert_false(is_instance_valid(boss))
