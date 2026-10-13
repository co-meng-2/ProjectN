extends TestCase

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const VITALITY: AbilityDef = preload("res://data/abilities/vitality.tres")
const VOLLEY: AbilityDef = preload("res://data/abilities/volley.tres")
const BOUNTY: AbilityDef = preload("res://data/abilities/bounty.tres")


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
	ActorFixture.kill(self, stage.building.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_player_death_fails_the_run() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	ActorFixture.kill(self, stage.player.get_node("AbilitySystemComponent"))
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_finish_is_idempotent_failure_first() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	ActorFixture.kill(self, stage.building.asc)
	stage.player.died.emit()
	stage.clock.ended.emit()
	assert_eq(results.size(), 1, "later finish paths must hit the terminal guard")
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_finish_is_idempotent_cleared_first() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	stage.clock.ended.emit()
	ActorFixture.kill(self, stage.building.asc)
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
		ActorFixture.kill(self, victim.get_node("AbilitySystemComponent"))
	await next_frame()
	await next_frame()
	assert_eq(stage.run_state.kills, victims.size())
	assert_true(stage.get_node("World/Pickups").get_child_count() > 0, "gold dropped")


func test_restart_starts_clean() -> void:
	var first_results: Array = []
	var first := _running_stage(30.0, first_results)
	first.run_state.wallet.add(Wallet.Kind.GOLD, 50)
	ActorFixture.kill(self, first.building.asc)
	first.free()
	await next_frame()
	var second_results: Array = []
	var second := _running_stage(30.0, second_results)
	assert_eq(second.run_state.wallet.gold, 0)
	assert_almost_eq(second.clock.elapsed, 0.0)
	assert_eq(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size(), 0)


func test_second_start_is_rejected_and_does_not_double_wire() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	var first_state := stage.run_state
	assert_false(stage.start(_stage_def(30.0), _profile(), 12))
	assert_true(stage.run_state == first_state, "run state untouched by the rejected start")
	stage.clock.ended.emit()
	assert_eq(results.size(), 1)


func test_every_enemy_death_sends_event_kill_to_the_player() -> void:
	var results: Array = []
	var stage := _running_stage(30.0, results)
	var events: Array = []
	stage.player.asc.gameplay_event_received.connect(func(tag: StringName, payload: Variant) -> void: events.append([tag, payload]))
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		ActorFixture.kill(self, victim.get_node("AbilitySystemComponent"))
	assert_eq(events.size(), victims.size())
	assert_eq(events[0][0], GameplayTags.Event_Kill)
	assert_true(events[0][1] is Enemy, "payload is the dead enemy")
	assert_eq(stage.run_state.kills, victims.size())


## A stage whose profile offers `abilities`; `quiet` removes every enemy so nothing dies or drops.
func _shop_stage(abilities: Array[AbilityDef], gold: int, stone: int, quiet: bool = true) -> Stage:
	var def := _stage_def(60.0)
	if quiet:
		def.trickle_enemies.clear()
		def.waves.entries.clear()
	def.start_gold = gold
	def.start_stone = stone
	var profile := DefFactory.profile()
	profile.slot_count = 2
	profile.abilities = abilities
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_true(stage.start(def, profile, 11))
	return stage


func test_stage_builds_the_loadout_and_grade_service_from_the_profile() -> void:
	var defs: Array[AbilityDef] = [VITALITY]
	var stage := _shop_stage(defs, 200, 0)
	assert_eq(stage.loadout.size(), 2)
	assert_eq(stage.run_state.wallet.gold, 200)
	assert_eq(stage.grade_service.promote_cost(0).amount, 100)
	assert_null(stage.grade_service.reroll_cost(0))


func test_buying_promoting_and_revealing_through_the_service_changes_the_player() -> void:
	var defs: Array[AbilityDef] = [VITALITY]
	var stage := _shop_stage(defs, 1500, 20)
	var asc := stage.player.asc
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.gold, 1400)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 120.0)
	for i in 4:
		assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.loadout.slot(0).grade, Grade.Value.LEGENDARY)
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 260.0)
	assert_eq(stage.grade_service.reroll(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.stone, 18)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 260.0, 0.001, "reinstalled, not stacked")
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.loadout.slot(0).grade, Grade.Value.REVEALED)
	assert_eq(stage.loadout.slot(0).def, VITALITY)
	assert_eq(stage.run_state.wallet.stone, 8)
	assert_almost_eq(asc.get_attribute("max_health").current_value, 340.0)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.NOT_APPLICABLE)
	assert_eq(stage.run_state.wallet.stone, 8)


func test_an_attack_ability_is_granted_to_the_player_asc() -> void:
	var defs: Array[AbilityDef] = [VOLLEY]
	var stage := _shop_stage(defs, 100, 0)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	var volley: VolleyAbility = null
	for child in stage.player.asc.get_children():
		if child is VolleyAbility:
			volley = child
	assert_not_null(volley)
	assert_almost_eq(volley.ability_level, 1.0)
	assert_true(volley.context == stage.player.ability_context)
	assert_true(volley.is_physics_processing(), "auto-fires in the running stage")


func test_bounty_pays_gold_when_the_stage_reports_a_kill() -> void:
	var defs: Array[AbilityDef] = [BOUNTY]
	var stage := _shop_stage(defs, 100, 0, false)
	assert_eq(stage.grade_service.promote(0).outcome, GradeResult.Outcome.OK)
	assert_eq(stage.run_state.wallet.gold, 0)
	await wait_seconds(0.9)
	var victims := host.get_tree().get_nodes_in_group(Groups.ENEMIES)
	assert_true(victims.size() > 0)
	for victim in victims:
		ActorFixture.kill(self, victim.get_node("AbilitySystemComponent"))
	assert_eq(stage.run_state.wallet.gold, victims.size(), "1 gold per kill at COMMON; the drops are never collected")


func test_an_ability_def_with_a_non_ability_scene_is_rejected_before_anything_starts() -> void:
	var root := BareGameplayAbility.new()  # passes AbilityDef validation; only validate_defs rejects it
	var scene := PackedScene.new()
	assert_eq(scene.pack(root), OK)
	root.free()
	var broken := DefFactory.ability(&"broken", null, scene)
	var defs: Array[AbilityDef] = [broken]
	var def := _stage_def(60.0)
	var profile := DefFactory.profile()
	profile.slot_count = 2
	profile.abilities = defs
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, profile, 11))
	assert_null(stage.run_state)
	assert_null(stage.loadout)
