extends TestCase

const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func _begun(time_limit: float = 12.0) -> Dictionary:
	var def := DefFactory.event(PROBE, time_limit)
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	var record := ActorFixture.watch_event(event)
	event.begin(ActorFixture.event_context(self, def))
	return {"event": event, "record": record, "def": def}


func test_begin_starts_the_time_limit_and_reports_progress() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	assert_true(event.is_running())
	assert_almost_eq(event.remaining_time(), 12.0)
	assert_eq(event.begins, 1)
	assert_eq(rig["record"]["progress"], ["Probe probe"])
	assert_true(event.context.def == rig["def"])


func test_running_out_of_time_fails_once_and_cleans_up() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	event.step(5.0)
	assert_almost_eq(event.remaining_time(), 7.0)
	assert_true(event.is_running())
	event.step(8.0)
	assert_eq((rig["record"]["failed"] as Array).size(), 1)
	assert_true((rig["record"]["succeeded"] as Array).is_empty())
	assert_eq(event.cleanups, 1)
	assert_false(event.is_running())
	assert_true(event.has_ended())
	event.step(5.0)
	assert_eq((rig["record"]["failed"] as Array).size(), 1, "no second ending")


func test_success_ends_exactly_once() -> void:
	var rig := _begun()
	var event: ProbeEvent = rig["event"]
	event.finish(true)
	event.finish(true)
	event.finish(false)
	event.step(100.0)
	assert_eq((rig["record"]["succeeded"] as Array).size(), 1)
	assert_true((rig["record"]["failed"] as Array).is_empty())
	assert_eq(event.cleanups, 1)


func test_abort_cleans_up_without_a_result() -> void:
	var rig := _begun()
	var event: ProbeEvent = rig["event"]
	event.abort()
	event.abort()
	event.finish(true)
	assert_eq(event.cleanups, 1)
	assert_true((rig["record"]["succeeded"] as Array).is_empty())
	assert_true((rig["record"]["failed"] as Array).is_empty())
	assert_true(event.has_ended())


func test_begin_twice_is_ignored() -> void:
	var rig := _begun(12.0)
	var event: ProbeEvent = rig["event"]
	event.step(4.0)
	event.begin(ActorFixture.event_context(self, DefFactory.event(PROBE, 99.0)))
	assert_eq(event.begins, 1)
	assert_almost_eq(event.remaining_time(), 8.0)


func test_an_override_of_time_up_keeps_the_event_running() -> void:
	var def := DefFactory.event(PROBE, 3.0)
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	event.endless = true
	event.begin(ActorFixture.event_context(self, def))
	event.step(10.0)
	assert_true(event.is_running(), "the subclass decided what time up means")
	assert_almost_eq(event.remaining_time(), 0.0)
