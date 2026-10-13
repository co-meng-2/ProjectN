extends TestCase

const STATUE_EVENT: PackedScene = preload("res://src/events/statue_event.tscn")
const BUILDING_AT: Vector2 = Vector2(100, -50)


func _rig(min_distance: float = -1.0) -> Dictionary:
	var def := DefFactory.event(STATUE_EVENT, 25.0, &"statues")
	var context := ActorFixture.event_context(self, def, null, BUILDING_AT)
	var event := adopt(STATUE_EVENT.instantiate()) as StatueEvent
	event.set_physics_process(false)
	if min_distance >= 0.0:
		event.min_distance = min_distance
	var record := ActorFixture.watch_event(event)
	event.begin(context)
	return {"event": event, "context": context, "record": record}


func _statues(event: StatueEvent) -> Array[Statue]:
	var found: Array[Statue] = []
	for node in event.objectives():
		found.append(node as Statue)
	return found


func test_spawns_the_statues_away_from_the_building_and_the_player() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var context: EventContext = rig["context"]
	var statues := _statues(event)
	assert_eq(statues.size(), 4)
	var inner := context.bounds.grow(-48.0)
	for statue in statues:
		assert_true(statue.global_position.distance_to(context.building.global_position) >= 250.0, "far from the building")
		assert_true(statue.global_position.distance_to(context.player.global_position) >= 250.0, "far from the player")
		assert_true(inner.has_point(statue.global_position), "inside the arena margin")
		assert_almost_eq(statue.health.current(), 60.0)
		assert_eq(statue.get_parent(), event, "event-owned")
	assert_eq(rig["record"]["progress"], ["Statues 0/4"])


func test_destroying_every_statue_succeeds_once() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	for i in 3:
		ActorFixture.kill(self, statues[i].asc)
	assert_eq((rig["record"]["progress"] as Array).back(), "Statues 3/4")
	assert_eq(event.objectives().size(), 1)
	assert_true(event.is_running())
	ActorFixture.kill(self, statues[3].asc)
	assert_eq((rig["record"]["succeeded"] as Array).size(), 1)
	assert_true((rig["record"]["failed"] as Array).is_empty())
	assert_false(event.is_running())


func test_time_up_removes_the_remaining_statues_and_fails() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	ActorFixture.kill(self, statues[0].asc)
	event.step(24.0)
	assert_true(event.is_running())
	event.step(1.5)
	assert_eq((rig["record"]["failed"] as Array).size(), 1)
	for i in range(1, 4):
		assert_true(statues[i].is_removed(), "statue %d removed" % i)
	assert_true(event.objectives().is_empty())
	await settle()
	assert_eq(host.get_tree().get_nodes_in_group(Groups.ENEMIES).size(), 0)


func test_abort_removes_the_statues_without_a_result() -> void:
	var rig := _rig()
	var event: StatueEvent = rig["event"]
	var statues := _statues(event)
	event.abort()
	assert_true((rig["record"]["succeeded"] as Array).is_empty())
	assert_true((rig["record"]["failed"] as Array).is_empty())
	for statue in statues:
		assert_true(statue.is_removed())


func test_impossible_spacing_still_places_every_statue() -> void:
	var rig := _rig(100000.0)
	assert_eq(_statues(rig["event"]).size(), 4, "falls back to the best spot found, never hangs")


func test_validate_reports_missing_scene_and_bad_numbers() -> void:
	var event := STATUE_EVENT.instantiate() as StatueEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.statue_scene = null
	event.statue_count = 0
	event.min_distance = -1.0
	assert_eq(event.validate().size(), 3)
	event.free()


func test_validate_rejects_a_scene_whose_root_is_not_a_statue() -> void:
	var event := STATUE_EVENT.instantiate() as StatueEvent
	event.statue_scene = preload("res://src/actors/projectile/projectile.tscn")
	var errors := event.validate()
	assert_eq(errors.size(), 1)
	assert_true("not a Statue" in errors[0])
	event.free()


func test_a_wide_spacing_is_honoured_by_retrying() -> void:
	var rig := _rig(900.0)
	var context: EventContext = rig["context"]
	var statues := _statues(rig["event"])
	assert_eq(statues.size(), 4)
	for statue in statues:
		assert_true(statue.global_position.distance_to(context.building.global_position) >= 900.0, "far from the building")
		assert_true(statue.global_position.distance_to(context.player.global_position) >= 900.0, "far from the player")
