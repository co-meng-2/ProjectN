extends TestCase

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _stage_def(duration: float) -> StageDef:
	var orc := load("res://data/enemies/orc.tres") as EnemyDef
	var def := DefFactory.stage(duration)
	def.trickle_enemies.append(orc)
	def.trickle_interval_start = 0.3
	def.trickle_interval_end = 0.3
	var entry := WaveEntry.new()
	entry.time = 0.5
	entry.enemy = orc
	entry.count = 3
	def.waves.entries.append(entry)
	return def


func _profile() -> PlayerProfile:
	return load("res://data/characters/default_profile.tres") as PlayerProfile


func _running_stage(duration: float, results: Array) -> Stage:
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	assert_true(stage.start(_stage_def(duration), _profile(), 11))
	return stage


func _kill(stage: Stage, target_asc: AbilitySystemComponent) -> void:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, 99999.0)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, target_asc)


func test_run_reaches_cleared_when_time_is_up() -> void:
	var results: Array = []
	var stage := _running_stage(1.5, results)
	await wait_seconds(1.0)
	assert_true(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size() > 0, "enemies spawned")
	await wait_seconds(1.2)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_almost_eq((results[0] as RunResult).elapsed, 1.5, 0.15)


func test_destroying_the_building_fails_the_run() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	_kill(stage, stage.building.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_player_death_fails_the_run() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	_kill(stage, stage.player.get_node("AbilitySystemComponent"))
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_finish_is_idempotent_failure_first() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	_kill(stage, stage.building.asc)
	stage.player.died.emit()
	stage.clock.ended.emit()
	assert_eq(results.size(), 1, "later finish paths must hit the terminal guard")
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_finish_is_idempotent_cleared_first() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	stage.clock.ended.emit()
	_kill(stage, stage.building.asc)
	stage.player.died.emit()
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)


func test_invalid_defs_are_rejected_before_anything_starts() -> void:
	var results: Array = []
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	var broken := _stage_def(0.0)
	assert_false(stage.start(broken, _profile()))
	await wait_seconds(0.2)
	assert_true(results.is_empty())
	assert_null(stage.run_state)


func test_collected_loot_is_credited_with_the_gain_multiplier() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	var overrides: Dictionary[String, float] = {"gold_gain": 2.0, "stone_gain": 1.0}
	(stage.player.get_node("AbilitySystemComponent") as AbilitySystemComponent).initialize_attribute_overrides(overrides)
	stage.player.loot_collected.emit(Wallet.Kind.GOLD, 5)
	stage.player.loot_collected.emit(Wallet.Kind.STONE, 2)
	assert_eq(stage.run_state.wallet.gold, 10)
	assert_eq(stage.run_state.wallet.stone, 2)


func test_killed_enemies_are_counted_and_drop_pickups() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		_kill(stage, victim.get_node("AbilitySystemComponent"))
	await next_frame()
	await next_frame()
	assert_eq(stage.run_state.kills, victims.size())
	assert_true(stage.get_node("World/Pickups").get_child_count() > 0, "gold dropped")


func test_restart_starts_clean() -> void:
	var first_results: Array = []
	var first := _running_stage(30.0, first_results)
	first.run_state.wallet.add(Wallet.Kind.GOLD, 50)
	_kill(first, first.building.asc)
	first.free()
	await next_frame()
	var second_results: Array = []
	var second := _running_stage(30.0, second_results)
	assert_eq(second.run_state.wallet.gold, 0)
	assert_almost_eq(second.clock.elapsed, 0.0)
	assert_eq(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size(), 0)
