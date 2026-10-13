extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const ANCHOR_AT: Vector2 = Vector2(80, -40)


func _rig(with_boss: bool = true) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = Vector2(80, 110)
	var targets := player.ability_context.targets
	var anchor := adopt(Node2D.new()) as Node2D
	anchor.global_position = ANCHOR_AT
	var layer := adopt(Node2D.new()) as Node2D
	var flow := StageFlow.new()
	flow.transition_to(StageFlow.State.RUNNING)
	var events := adopt(EventDirector.new()) as EventDirector
	var director := adopt(BossDirector.new()) as BossDirector
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def: BossDef = DefFactory.boss(BOSS_SCENE, patterns) if with_boss else null
	var record := {"spawned": [], "appeared": [], "defeated": []}
	director.boss_spawned.connect(func(boss: Boss) -> void: (record["spawned"] as Array).append(boss))
	director.boss_appeared.connect(func(health: Health, display_name: String) -> void: (record["appeared"] as Array).append([health, display_name]))
	director.boss_defeated.connect(func() -> void: (record["defeated"] as Array).append(true))
	director.bind(def, flow, events, layer, anchor, 300.0, Rect2(-1200, -800, 2400, 1600), RunRng.new(6), targets, player.ability_context.projectile_layer, Callable())
	return {"director": director, "flow": flow, "layer": layer, "player": player, "record": record}


func test_appear_switches_to_boss_and_spawns_it_on_the_ring() -> void:
	var rig := _rig()
	var director: BossDirector = rig["director"]
	var boss := director.appear()
	assert_not_null(boss)
	assert_eq((rig["flow"] as StageFlow).state, StageFlow.State.BOSS)
	assert_eq(boss.get_parent(), rig["layer"])
	assert_almost_eq(boss.global_position.distance_to(ANCHOR_AT), 300.0, 0.5)
	assert_eq(rig["record"]["spawned"], [boss])
	assert_eq(rig["record"]["appeared"], [[boss.health, "Test Boss"]])
	assert_true(boss.ability_context.targets.player == rig["player"], "chases and targets the player")


func test_appear_happens_once_and_only_from_running() -> void:
	var rig := _rig()
	var director: BossDirector = rig["director"]
	var first := director.appear()
	assert_true(director.appear() == first, "a second call returns the same boss")
	assert_eq((rig["record"]["spawned"] as Array).size(), 1)
	var finished := _rig()
	(finished["flow"] as StageFlow).transition_to(StageFlow.State.FAILED)
	assert_null((finished["director"] as BossDirector).appear(), "the run is over")
	assert_true((finished["record"]["spawned"] as Array).is_empty())


func test_boss_death_is_reported_as_defeated() -> void:
	var rig := _rig()
	var boss := (rig["director"] as BossDirector).appear()
	ActorFixture.kill(self, boss.asc)
	assert_eq((rig["record"]["defeated"] as Array).size(), 1)


func test_without_a_boss_nothing_appears() -> void:
	var rig := _rig(false)
	var director: BossDirector = rig["director"]
	assert_false(director.has_boss())
	assert_null(director.appear())
	assert_eq((rig["flow"] as StageFlow).state, StageFlow.State.RUNNING, "flow untouched")


func test_validate_def_requires_a_boss_scene_root() -> void:
	assert_no_errors(BossDirector.validate_def(DefFactory.boss(BOSS_SCENE)), "a real boss scene")
	assert_no_errors(BossDirector.validate_def(null), "no boss")
	var errors := BossDirector.validate_def(DefFactory.boss(DefFactory.packed(Node2D.new())))
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("test_boss"))
