extends TestCase

const METEOR_EVENT: PackedScene = preload("res://src/events/meteor_event.tscn")
const STATUE_SCENE: PackedScene = preload("res://src/actors/statue/statue.tscn")
const PLAYER_AT: Vector2 = Vector2(300, 200)


## The player stands at PLAYER_AT; landing_spread 0 makes every meteor land exactly on it.
func _rig(max_hits: int = 3) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var def := DefFactory.event(METEOR_EVENT, 20.0, &"meteors")
	var context := ActorFixture.event_context(self, def, player)
	var event := adopt(METEOR_EVENT.instantiate()) as MeteorEvent
	event.set_physics_process(false)
	event.landing_spread = 0.0
	event.max_hits = max_hits
	var record := ActorFixture.watch_event(event)
	event.begin(context)
	return {"event": event, "player": player, "record": record}


func _telegraphs(event: MeteorEvent) -> Array:
	return ActorFixture.live_children(event, Telegraph)


func _strikes(event: MeteorEvent) -> Array:
	return ActorFixture.live_children(event, AreaStrike)


func test_a_warning_appears_every_interval_and_hits_only_after_it() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	var player: Player = rig["player"]
	assert_eq(rig["record"]["progress"], ["Hits 0/3"])
	event.step(1.1)
	assert_true(_telegraphs(event).is_empty(), "nothing before the first interval")
	event.step(0.15)
	var warnings := _telegraphs(event)
	assert_eq(warnings.size(), 1)
	assert_eq((warnings[0] as Telegraph).global_position, PLAYER_AT)
	assert_almost_eq((warnings[0] as Telegraph).radius, 60.0)
	event.step(0.5)
	await settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "still only a warning")
	event.step(0.5)
	assert_eq(_strikes(event).size(), 1, "landed")
	assert_true(_telegraphs(event).is_empty(), "the warning is gone")
	await settle()
	assert_almost_eq(player.health.current(), 85.0, 0.001)
	assert_eq(event.hits(), 1)
	assert_eq((rig["record"]["progress"] as Array).back(), "Hits 1/3")


func test_more_than_max_hits_fails_at_once() -> void:
	var rig := _rig(1)
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	event.step(0.9)
	await settle()
	assert_eq(event.hits(), 1)
	assert_true(event.is_running(), "max_hits itself is allowed")
	event.step(0.35)
	event.step(0.9)
	await settle()
	assert_eq(event.hits(), 2)
	assert_eq((rig["record"]["failed"] as Array).size(), 1)
	assert_false(event.is_running())


func test_dodged_meteors_do_not_count_and_surviving_succeeds() -> void:
	var rig := _rig(0)
	var event: MeteorEvent = rig["event"]
	var player: Player = rig["player"]
	player.asc.add_tag(GameplayTags.State_Invulnerable)
	event.step(1.2)
	event.step(0.9)
	await settle()
	assert_eq(event.hits(), 0, "an invulnerable dash dodges the meteor")
	assert_almost_eq(player.health.current(), 100.0, 0.001)
	event.step(18.0)
	assert_eq((rig["record"]["succeeded"] as Array).size(), 1, "survived the 20 s")
	assert_true((rig["record"]["failed"] as Array).is_empty())


func test_a_hit_after_the_event_ended_changes_nothing() -> void:
	var rig := _rig(0)
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	event.step(0.9)
	event.step(18.0)
	assert_eq((rig["record"]["succeeded"] as Array).size(), 1, "time is up before the strike reports")
	await settle()
	assert_true((rig["record"]["failed"] as Array).is_empty(), "a late hit cannot fail a finished event")
	assert_eq(event.hits(), 0)


func test_meteors_spare_the_building() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	var building := ActorFixture.hurt_target(self, PLAYER_AT + Vector2(15, 0), PhysicsLayers.ALLY_HURT)
	event.step(1.2)
	event.step(0.9)
	await settle()
	assert_almost_eq((rig["player"] as Player).health.current(), 85.0, 0.001)
	assert_almost_eq(ActorFixture.asc_of(building).get_attribute("health").current_value, 100.0, 0.001, "ally hurt layer, but not the player")


func test_abort_clears_pending_warnings_without_a_result() -> void:
	var rig := _rig()
	var event: MeteorEvent = rig["event"]
	event.step(1.2)
	assert_eq(_telegraphs(event).size(), 1)
	event.abort()
	assert_true(_telegraphs(event).is_empty())
	event.step(5.0)
	assert_true(_strikes(event).is_empty(), "nothing lands after the abort")
	assert_true((rig["record"]["succeeded"] as Array).is_empty())
	assert_true((rig["record"]["failed"] as Array).is_empty())


func test_validate_flags_missing_parts_and_bad_numbers() -> void:
	var event := METEOR_EVENT.instantiate() as MeteorEvent
	assert_no_errors(event.validate(), "scene as shipped")
	event.strike_scene = null
	event.interval = 0.0
	event.max_hits = -1
	assert_eq(event.validate().size(), 3)
	event.strike_scene = STATUE_SCENE
	assert_eq(event.validate().size(), 3, "a scene whose root is not an AreaStrike is rejected")
	event.free()
