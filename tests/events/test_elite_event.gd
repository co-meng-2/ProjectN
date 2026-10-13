extends TestCase

const ELITE_EVENT: PackedScene = preload("res://src/events/elite_event.tscn")
const BUILDING_AT: Vector2 = Vector2(50, 40)


func _rig() -> Dictionary:
	var def := DefFactory.event(ELITE_EVENT, 25.0, &"elites")
	var context := ActorFixture.event_context(self, def, null, BUILDING_AT)
	var event := adopt(ELITE_EVENT.instantiate()) as EliteEvent
	event.set_physics_process(false)
	var record := ActorFixture.watch_event(event)
	event.begin(context)
	return {"event": event, "context": context, "log": record}


func _elites(event: EliteEvent) -> Array[Enemy]:
	var found: Array[Enemy] = []
	for node in event.objectives():
		found.append(node as Enemy)
	return found


func test_spawns_the_elites_through_the_factory_on_the_spawn_ring() -> void:
	var rig := _rig()
	var context: EventContext = rig["context"]
	var elites := _elites(rig["event"])
	assert_eq(elites.size(), 3)
	assert_eq(context.factory.alive_count(), 3, "the factory made them, so kills and drops work")
	for elite in elites:
		assert_eq(elite.def.id, &"elite_orc")
		assert_almost_eq(elite.global_position.distance_to(BUILDING_AT), 300.0, 0.5)
	assert_eq(rig["log"]["progress"], ["Elites 0/3"])


func test_killing_every_elite_succeeds() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	var elites := _elites(event)
	ActorFixture.kill(self, elites[0].get_node("AbilitySystemComponent"))
	ActorFixture.kill(self, elites[1].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["progress"] as Array).back(), "Elites 2/3")
	assert_eq(event.objectives().size(), 1)
	assert_true(event.is_running())
	ActorFixture.kill(self, elites[2].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["succeeded"] as Array).size(), 1)
	assert_true((rig["log"]["failed"] as Array).is_empty())


func test_time_up_fails_and_the_survivors_stay() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	var elites := _elites(event)
	ActorFixture.kill(self, elites[0].get_node("AbilitySystemComponent"))
	event.step(25.5)
	assert_eq((rig["log"]["failed"] as Array).size(), 1)
	await next_frame()
	for i in range(1, 3):
		assert_true(is_instance_valid(elites[i]) and not elites[i].is_queued_for_deletion(), "elite %d keeps fighting" % i)
		assert_true(elites[i].is_in_group(Groups.ENEMIES))
	ActorFixture.kill(self, elites[1].get_node("AbilitySystemComponent"))
	assert_eq((rig["log"]["progress"] as Array).back(), "Elites 1/3", "kills after the end no longer count")
	assert_true((rig["log"]["succeeded"] as Array).is_empty())


func test_abort_leaves_the_elites_in_the_fight() -> void:
	var rig := _rig()
	var event: EliteEvent = rig["event"]
	event.abort()
	assert_true((rig["log"]["failed"] as Array).is_empty())
	assert_eq((rig["context"] as EventContext).factory.alive_count(), 3)
	assert_true(event.objectives().is_empty())


func test_validate_reports_a_missing_or_invalid_elite_and_a_bad_count() -> void:
	var event := ELITE_EVENT.instantiate() as EliteEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.count = 0
	event.elite_enemy = null
	assert_eq(event.validate().size(), 2)
	event.free()
