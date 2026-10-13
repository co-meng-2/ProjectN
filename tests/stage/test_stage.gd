extends TestCase

const STAGE_SCENE: PackedScene = preload("res://src/stage/stage.tscn")
const VITALITY: AbilityDef = preload("res://data/abilities/vitality.tres")
const VOLLEY: AbilityDef = preload("res://data/abilities/volley.tres")
const BOUNTY: AbilityDef = preload("res://data/abilities/bounty.tres")
const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const PROBE_EVENT: PackedScene = preload("res://tests/support/probe_event.tscn")
const SUMMON_SCENE: PackedScene = preload("res://src/boss/patterns/summon_pattern.tscn")


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



## A stage without trickle or waves whose clock the test drives by hand.
func _manual_stage(def: StageDef, results: Array) -> Stage:
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	stage.finished.connect(func(result: RunResult) -> void: results.append(result))
	assert_true(stage.start(def, _profile(), 11))
	stage.clock.set_physics_process(false)
	return stage


func _quiet_def(duration: float) -> StageDef:
	var def := _stage_def(duration)
	def.trickle_enemies.clear()
	def.waves.entries.clear()
	return def


func _boss_def(pattern: PackedScene = PROBE_PATTERN) -> BossDef:
	var patterns: Array[PackedScene] = [pattern]
	return DefFactory.boss(BOSS_SCENE, patterns)


## One endless probe event somewhere in [window_start, window_end]. Its time limit is 1 s so the schedule
## passes the window_end + time_limit <= duration rule even on a 5 s stage; being endless, it keeps running
## past that limit until the test (or the boss) ends it.
func _probe_events(window_start: float, window_end: float) -> EventSchedule:
	var probe := PROBE_EVENT.instantiate() as ProbeEvent
	probe.endless = true
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(probe), 1.0, &"probe")]
	return DefFactory.schedule(pool, 1, window_start, window_end, 10.0)


func test_time_up_brings_the_boss_and_its_death_clears_the_stage() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	var results: Array = []
	var stage := _manual_stage(def, results)
	var appeared: Array = []
	stage.boss_appeared.connect(func(health: Health, display_name: String) -> void: appeared.append([health, display_name]))
	stage.clock.step(5.0)
	assert_true(results.is_empty(), "time up starts the boss fight instead of clearing")
	assert_eq(stage.flow_state(), StageFlow.State.BOSS)
	var boss := stage.current_boss()
	assert_not_null(boss)
	assert_eq(appeared, [[boss.health, "Test Boss"]])
	assert_almost_eq(boss.global_position.distance_to(stage.building.global_position), def.spawn_radius, 1.0)
	assert_true(boss.get_node_or_null("Brain") is BossBrain, "the Stage attached the brain")
	stage.clock.step(30.0)
	assert_true(results.is_empty(), "the boss fight has no time limit")
	assert_almost_eq(stage.clock.elapsed, 35.0)
	ActorFixture.kill(self, boss.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_eq(stage.run_state.kills, 0, "the boss is not a kill")


func test_without_a_boss_time_up_clears_at_once() -> void:
	var results: Array = []
	var stage := _manual_stage(_quiet_def(5.0), results)
	stage.clock.step(5.0)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.CLEARED)
	assert_null(stage.current_boss())
	assert_eq(stage.flow_state(), StageFlow.State.CLEARED)


func test_player_death_during_the_boss_fight_fails_the_run() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(5.0)
	ActorFixture.kill(self, stage.player.asc)
	assert_eq(results.size(), 1)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)


func test_boss_appearance_aborts_a_running_event_without_reward() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def()
	def.events = _probe_events(1.0, 3.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	var ended: Array = []
	stage.events.event_ended.connect(func(event_def: EventDef, success: bool) -> void: ended.append([event_def.id, success]))
	stage.clock.step(3.5)
	var probe := stage.events.running() as ProbeEvent
	assert_not_null(probe, "the event is running")
	assert_eq(probe.get_parent(), stage.get_node("World/Events"))
	stage.clock.step(1.5)
	assert_not_null(stage.current_boss())
	assert_eq(ended, [[&"probe", false]])
	assert_eq(probe.cleanups, 1, "its objects were removed")
	assert_null(stage.events.running())
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_eq(stage.run_state.wallet.tickets, 0)


func test_a_successful_event_credits_its_reward() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	(stage.events.running() as ProbeEvent).finish(true)
	assert_eq(stage.run_state.wallet.gold, 150)
	assert_eq(stage.run_state.wallet.stone, 2)
	assert_eq(stage.run_state.wallet.tickets, 1)


func test_a_failed_event_pays_nothing() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	(stage.events.running() as ProbeEvent).finish(false)
	assert_eq(stage.run_state.wallet.gold, 0)
	assert_eq(stage.run_state.wallet.tickets, 0)


func test_the_run_ending_aborts_a_running_event() -> void:
	var def := _quiet_def(60.0)
	def.events = _probe_events(5.0, 10.0)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(11.0)
	var probe := stage.events.running() as ProbeEvent
	ActorFixture.kill(self, stage.building.asc)
	assert_eq((results[0] as RunResult).outcome, RunResult.Outcome.FAILED)
	assert_eq(probe.cleanups, 1)
	assert_null(stage.events.running())


func test_boss_summons_spawn_through_the_factory_and_count_as_kills() -> void:
	var def := _quiet_def(5.0)
	def.boss = _boss_def(SUMMON_SCENE)
	var results: Array = []
	var stage := _manual_stage(def, results)
	stage.clock.step(5.0)
	var boss := stage.current_boss()
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Summon))
	var summoned: Array = []
	for node in host.get_tree().get_nodes_in_group(Groups.ENEMIES):
		if node is Enemy:
			summoned.append(node)
	assert_eq(summoned.size(), 4)
	assert_eq((summoned[0] as Enemy).get_parent(), stage.get_node("World/Enemies"))
	ActorFixture.kill(self, (summoned[0] as Enemy).get_node("AbilitySystemComponent"))
	assert_eq(stage.run_state.kills, 1, "summons are ordinary kills")


func test_event_controllers_must_be_stage_events() -> void:
	var def := _quiet_def(60.0)
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 10.0, &"plain")]
	def.events = DefFactory.schedule(pool, 1, 5.0, 10.0, 10.0)
	assert_no_errors(def.validate(), "the def alone cannot see the problem")
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, _profile(), 11))
	assert_null(stage.run_state)


func test_boss_patterns_must_be_actor_abilities() -> void:
	var def := _quiet_def(60.0)
	def.boss = _boss_def(DefFactory.packed(BareGameplayAbility.new()))
	assert_no_errors(def.validate(), "the def alone cannot see the problem")
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, _profile(), 11))
	assert_null(stage.run_state)


func test_boss_scene_must_be_a_boss() -> void:
	var def := _quiet_def(60.0)
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	def.boss = DefFactory.boss(DefFactory.packed(Node2D.new()), patterns)
	assert_no_errors(def.validate(), "the def alone cannot see the problem")
	var stage := adopt(STAGE_SCENE.instantiate()) as Stage
	assert_false(stage.start(def, _profile(), 11))
	assert_null(stage.run_state)
