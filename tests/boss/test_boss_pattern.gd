extends TestCase

const PROBE_PATTERN: PackedScene = preload("res://tests/support/probe_pattern.tscn")


func _pattern(hold: bool) -> Dictionary:
	var actor := ActorFixture.bare_actor(self, StatBlock.new())
	var asc := ActorFixture.asc_of(actor)
	var pattern := PROBE_PATTERN.instantiate() as ProbePattern
	pattern.hold = hold
	pattern.bind_context(AbilityContext.new(actor, null, null, null, null))
	asc.add_child(pattern)
	pattern.set_physics_process(false)
	var ended: Array = []
	pattern.ability_ended.connect(func(was_cancelled: bool) -> void: ended.append(was_cancelled))
	return {"pattern": pattern, "asc": asc, "ended": ended}


func test_an_instant_pattern_runs_once_and_ends() -> void:
	var rig := _pattern(false)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	assert_true(asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"))
	assert_eq(pattern.starts, 1)
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [false])


func test_a_held_pattern_casts_until_it_completes() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	assert_true(pattern.is_active)
	assert_true(pattern.is_running())
	assert_true(asc.has_tag(GameplayTags.State_Casting), "owned tag while running")
	pattern.step(0.25)
	pattern.step(0.5)
	assert_almost_eq(pattern.stepped, 0.75)
	pattern.release()
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [false])
	pattern.step(1.0)
	assert_almost_eq(pattern.stepped, 0.75, 0.001, "a finished pattern does not advance")


func test_abort_calls_the_abort_hook_and_strips_the_casting_tag() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	pattern.abort_ability()
	assert_eq(pattern.aborts, 1)
	assert_false(pattern.is_running())
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [true])
	pattern.release()
	assert_eq(rig["ended"], [true], "releasing after the abort changes nothing")
	assert_true(asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern"), "can run again")
	assert_eq(pattern.starts, 2)


func test_a_refusing_pattern_never_casts() -> void:
	var rig := _pattern(true)
	var pattern: ProbePattern = rig["pattern"]
	var asc: AbilitySystemComponent = rig["asc"]
	pattern.refuse = true
	asc.try_activate_abilities_by_tag(&"Ability.Probe.Pattern")
	assert_eq(pattern.starts, 0)
	assert_false(pattern.is_active)
	assert_false(asc.has_tag(GameplayTags.State_Casting))
	assert_eq(rig["ended"], [true], "ends as cancelled")
