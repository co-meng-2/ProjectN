extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _endless_scene() -> PackedScene:
	var probe := PROBE.instantiate() as ProbeEvent
	probe.endless = true
	return DefFactory.packed(probe)


## A director over `pool` with `count` events in [10, 60], gap 20 (time limits 20), stage 100 s.
func _rig(pool: Array[EventDef], count: int, run_seed: int = 5) -> Dictionary:
	var stage_def := DefFactory.stage(100.0)
	stage_def.events = DefFactory.schedule(pool, count, 10.0, 60.0, 20.0)
	var layer := adopt(Node2D.new()) as Node2D
	var clock := adopt(StageClock.new()) as StageClock
	clock.set_physics_process(false)
	var director := adopt(EventDirector.new()) as EventDirector
	var record := {"started": [], "progress": [], "ended": [], "aborted": []}
	director.event_started.connect(func(def: EventDef, event: StageEvent) -> void: (record["started"] as Array).append([def, event]))
	director.event_progressed.connect(func(def: EventDef, text: String) -> void: (record["progress"] as Array).append([def, text]))
	director.event_ended.connect(func(def: EventDef, success: bool) -> void: (record["ended"] as Array).append([def, success]))
	director.event_aborted.connect(func(def: EventDef) -> void: (record["aborted"] as Array).append(def))
	var context_factory := func(def: EventDef) -> EventContext:
		return EventContext.new(layer, null, null, null, null, RunRng.new(1), Rect2(), 0.0, def)
	director.bind(stage_def, clock, context_factory, RunRng.new(run_seed), PackedFloat32Array())
	clock.start(100.0)
	return {"director": director, "clock": clock, "layer": layer, "record": record}


func _pool(scene: PackedScene = PROBE) -> Array[EventDef]:
	var pool: Array[EventDef] = [DefFactory.event(scene, 20.0)]
	return pool


func test_times_are_rolled_once_from_the_seeded_schedule() -> void:
	var first: EventDirector = _rig(_pool(), 2, 5)["director"]
	var times := first.event_times()
	assert_eq(times.size(), 2)
	assert_true(times[0] >= 10.0 and times[1] <= 60.0)
	assert_true(times[1] - times[0] >= 20.0)
	assert_eq((_rig(_pool(), 2, 5)["director"] as EventDirector).event_times(), times, "same seed, same times")
	assert_eq(first.planned_defs().size(), 2)


func test_an_event_starts_on_the_tick_that_crosses_its_time() -> void:
	var pool := _pool()
	var rig := _rig(pool, 1)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	var started: Array = rig["record"]["started"]
	var time := director.event_times()[0]
	clock.step(time - 0.5)
	assert_true(started.is_empty())
	clock.step(1.0)
	assert_eq(started.size(), 1)
	assert_true(started[0][0] == pool[0])
	var event := started[0][1] as ProbeEvent
	assert_eq(event.get_parent(), rig["layer"], "placed on the context's layer")
	assert_eq(event.begins, 1)
	assert_true(director.running() == event)
	assert_true(event.context.def == pool[0])
	assert_eq(rig["record"]["progress"], [[pool[0], "Probe probe"]], "progress is relayed")


func test_events_run_one_at_a_time() -> void:
	var rig := _rig(_pool(_endless_scene()), 2)
	var director: EventDirector = rig["director"]
	var started: Array = rig["record"]["started"]
	(rig["clock"] as StageClock).step(director.event_times()[1] + 1.0)
	assert_eq(started.size(), 1, "the second waits while the first runs")
	var first := started[0][1] as ProbeEvent
	first.finish(true)
	assert_eq(rig["record"]["ended"], [[started[0][0], true]])
	await next_frame()
	assert_eq(started.size(), 2, "the waiting event starts once the first has ended")
	assert_true(director.running() == started[1][1])
	assert_true(is_instance_valid(started[1][1]) and (started[1][1] as ProbeEvent).is_running())


func test_success_and_failure_are_relayed_and_the_event_is_freed() -> void:
	var rig := _rig(_pool(), 1)
	var director: EventDirector = rig["director"]
	(rig["clock"] as StageClock).step(director.event_times()[0] + 0.1)
	var event := (rig["record"]["started"] as Array)[0][1] as ProbeEvent
	event.finish(false)
	assert_eq((rig["record"]["ended"] as Array).size(), 1)
	assert_false((rig["record"]["ended"] as Array)[0][1], "failure")
	assert_null(director.running())
	assert_true(event.is_queued_for_deletion())


func test_abort_running_ends_without_reward_and_drops_waiting_events() -> void:
	var rig := _rig(_pool(_endless_scene()), 2)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	clock.step(director.event_times()[1] + 1.0)
	var first := (rig["record"]["started"] as Array)[0][1] as ProbeEvent
	director.abort_running()
	assert_eq(first.cleanups, 1)
	assert_true(first.is_queued_for_deletion(), "the aborted controller is freed")
	assert_true((rig["record"]["ended"] as Array).is_empty(), "an aborted event is not a result: no success, no failure")
	assert_eq((rig["record"]["aborted"] as Array).size(), 1)
	assert_eq((rig["record"]["aborted"] as Array)[0], first.context.def)
	assert_null(director.running())
	director.abort_running()
	assert_eq((rig["record"]["aborted"] as Array).size(), 1, "nothing left to abort")
	clock.step(5.0)
	await next_frame()
	assert_eq((rig["record"]["started"] as Array).size(), 1, "the waiting event was dropped")


func test_no_event_starts_in_overtime() -> void:
	var rig := _rig(_pool(), 1)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	var times_before := director.event_times()
	clock.step(times_before[0] + 0.1)
	((rig["record"]["started"] as Array)[0][1] as ProbeEvent).finish(true)
	for i in 10:
		clock.step(20.0)
	await next_frame()
	assert_true(clock.elapsed > 100.0, "well past the stage end")
	assert_eq((rig["record"]["started"] as Array).size(), 1)
	assert_null(director.running())
	assert_eq(director.event_times(), times_before, "the plan is untouched by overtime")


func test_without_a_schedule_there_are_no_events() -> void:
	var stage_def := DefFactory.stage(100.0)
	var clock := adopt(StageClock.new()) as StageClock
	clock.set_physics_process(false)
	var director := adopt(EventDirector.new()) as EventDirector
	var started: Array = []
	director.event_started.connect(func(def: EventDef, _event: StageEvent) -> void: started.append(def))
	director.bind(stage_def, clock, func(_def: EventDef) -> EventContext: return null, RunRng.new(1), PackedFloat32Array())
	clock.start(100.0)
	clock.step(150.0)
	assert_eq(director.event_times().size(), 0)
	assert_true(started.is_empty())


func test_validate_defs_requires_a_stage_event_root() -> void:
	var good := _pool()
	assert_no_errors(EventDirector.validate_defs(DefFactory.schedule(good)), "probe")
	var bad: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 20.0, &"plain")]
	var errors := EventDirector.validate_defs(DefFactory.schedule(bad))
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("plain"))
	assert_no_errors(EventDirector.validate_defs(null), "no schedule")


func test_aborting_from_an_event_started_listener_never_begins_the_event() -> void:
	var rig := _rig(_pool(_endless_scene()), 1)
	var director: EventDirector = rig["director"]
	var clock: StageClock = rig["clock"]
	director.event_started.connect(func(_def: EventDef, _event: StageEvent) -> void: director.abort_running())
	clock.step(director.event_times()[0] + 1.0)
	var event := (rig["record"]["started"] as Array)[0][1] as ProbeEvent
	assert_eq(event.begins, 0, "aborted before it began")
	assert_eq(event.cleanups, 0)
	assert_false(event.is_running())
	assert_true(event.is_queued_for_deletion())
	assert_eq((rig["record"]["aborted"] as Array).size(), 1)
	assert_true((rig["record"]["ended"] as Array).is_empty())
	assert_null(director.running())
