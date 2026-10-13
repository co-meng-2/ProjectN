extends TestCase

const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")
const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")


## `count` probe patterns (tags Ability.Probe.P0..), instantly completing unless `hold`, on a bare actor;
## the brain uses gap 2 s and RunRng(9). `started` records the index of every pattern the brain starts.
func _rig(count: int, order: BossDef.Order, weights: Array = [], hold: bool = false) -> Dictionary:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	var asc := ActorFixture.asc_of(actor)
	var context := AbilityContext.new(actor, null, null, null, null)
	var patterns: Array[ActorAbility] = []
	var typed_weights: Array[float] = []
	for i in count:
		var pattern := PROBE_PATTERN.instantiate() as ProbePattern
		pattern.ability_tag = StringName("Ability.Probe.P%d" % i)
		pattern.hold = hold
		pattern.bind_context(context)
		asc.add_child(pattern)
		pattern.set_physics_process(false)
		patterns.append(pattern)
		typed_weights.append(float(weights[i]) if i < weights.size() else 1.0)
	var brain := adopt(BossBrain.new()) as BossBrain
	brain.set_physics_process(false)
	var started: Array = []
	brain.pattern_started.connect(func(ability: ActorAbility) -> void: started.append(patterns.find(ability)))
	brain.bind(asc, patterns, typed_weights, order, 2.0, RunRng.new(9))
	return {"brain": brain, "patterns": patterns, "started": started, "asc": asc}


func test_waits_one_gap_before_the_first_pattern() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	brain.step(1.9)
	assert_true((rig["started"] as Array).is_empty())
	brain.step(0.2)
	assert_eq(rig["started"], [0])
	assert_eq((rig["patterns"][0] as ProbePattern).starts, 1, "activated through the ASC")


func test_sequence_order_wraps_around() -> void:
	var rig := _rig(3, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	for i in 5:
		brain.step(2.0)
	assert_eq(rig["started"], [0, 1, 2, 0, 1])


func test_waits_for_the_running_pattern_before_the_next_gap() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE, [], true)
	var brain: BossBrain = rig["brain"]
	var asc: AbilitySystemComponent = rig["asc"]
	var first: ProbePattern = rig["patterns"][0]
	brain.step(2.0)
	assert_true(asc.has_tag(GameplayTags.State_Casting))
	assert_true(brain.casting() == first)
	brain.step(10.0)
	assert_eq(rig["started"], [0], "nothing new while a pattern runs")
	first.release()
	assert_null(brain.casting())
	brain.step(1.9)
	assert_eq(rig["started"], [0], "the gap starts when the pattern ends")
	brain.step(0.2)
	assert_eq(rig["started"], [0, 1])


func test_random_order_never_repeats_and_follows_the_weights() -> void:
	var rig := _rig(3, BossDef.Order.RANDOM, [6.0, 1.0, 1.0])
	var brain: BossBrain = rig["brain"]
	for i in 60:
		brain.step(2.0)
	var started: Array = rig["started"]
	assert_eq(started.size(), 60)
	var counts := [0, 0, 0]
	for i in started.size():
		counts[started[i]] += 1
		if i > 0:
			assert_false(started[i] == started[i - 1], "pick %d repeats pattern %d" % [i, started[i]])
	assert_true(counts[0] > counts[1] and counts[0] > counts[2], "weight 6 beats weight 1: %s" % str(counts))
	assert_true(counts[1] > 0 and counts[2] > 0, "every pattern gets a turn: %s" % str(counts))


func test_random_with_a_single_pattern_repeats_it() -> void:
	var rig := _rig(1, BossDef.Order.RANDOM)
	var brain: BossBrain = rig["brain"]
	for i in 3:
		brain.step(2.0)
	assert_eq(rig["started"], [0, 0, 0])


func test_a_pattern_that_cannot_start_does_not_stall_the_brain() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	(rig["patterns"][0] as ProbePattern).refuse = true
	brain.step(2.0)
	assert_null(brain.casting(), "the refusal ended the attempt")
	brain.step(1.0)
	assert_eq(rig["started"], [0], "a full gap follows a refusal")
	brain.step(1.0)
	assert_eq(rig["started"], [0, 1])
	assert_eq((rig["patterns"][1] as ProbePattern).starts, 1)


func test_stop_halts_the_brain() -> void:
	var rig := _rig(2, BossDef.Order.SEQUENCE)
	var brain: BossBrain = rig["brain"]
	brain.step(2.0)
	brain.stop()
	for i in 5:
		brain.step(2.0)
	assert_eq(rig["started"], [0])


func test_attach_to_binds_a_boss_and_runs_its_patterns() -> void:
	var player := ActorFixture.player(self)
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def := DefFactory.boss(BOSS_SCENE, patterns)
	def.pattern_gap = 1.5
	var boss := ActorFixture.boss(self, player, def, Vector2(-250, 120))
	var pattern := boss.patterns()[0] as ProbePattern
	pattern.set_physics_process(false)
	var brain := BossBrain.attach_to(boss, RunRng.new(1))
	brain.set_physics_process(false)
	assert_eq(brain.get_parent(), boss)
	assert_eq(brain.name, "Brain")
	brain.step(1.4)
	assert_eq(pattern.starts, 0, "the def's gap, not a default")
	brain.step(0.2)
	assert_eq(pattern.starts, 1)


func test_validate_def_requires_actor_ability_roots_with_distinct_tags() -> void:
	var one: Array[PackedScene] = [PROBE_PATTERN]
	assert_no_errors(BossBrain.validate_def(DefFactory.boss(null, one)), "one probe")
	var twice: Array[PackedScene] = [PROBE_PATTERN, PROBE_PATTERN]
	var errors := BossBrain.validate_def(DefFactory.boss(null, twice))
	assert_eq(errors.size(), 1, "two patterns share Ability.Probe.Pattern")
	assert_true(errors[0].contains("Ability.Probe.Pattern"))
	var bare: Array[PackedScene] = [DefFactory.packed(BareGameplayAbility.new())]
	assert_eq(BossBrain.validate_def(DefFactory.boss(null, bare)).size(), 1, "not an ActorAbility")
	var untagged := PROBE_PATTERN.instantiate() as ProbePattern
	untagged.ability_tag = &"Ability.None"
	var no_tag: Array[PackedScene] = [DefFactory.packed(untagged)]
	assert_eq(BossBrain.validate_def(DefFactory.boss(null, no_tag)).size(), 1, "the default tag cannot be activated")
	assert_no_errors(BossBrain.validate_def(null), "no boss")


func test_the_boss_dying_mid_gap_stops_the_brain() -> void:
	var player := ActorFixture.player(self)
	var patterns: Array[PackedScene] = [PROBE_PATTERN]
	var def := DefFactory.boss(BOSS_SCENE, patterns)
	def.pattern_gap = 1.5
	var boss := ActorFixture.boss(self, player, def, Vector2(-250, 120))
	var pattern := boss.patterns()[0] as ProbePattern
	var brain := BossBrain.attach_to(boss, RunRng.new(1))
	brain.set_physics_process(false)
	brain.step(1.0)
	ActorFixture.kill(self, boss.asc)
	pattern.set_physics_process(false)
	brain.step(5.0)
	assert_eq(pattern.starts, 0, "no pattern activates after the boss died")
