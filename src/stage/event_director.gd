class_name EventDirector
extends Node
## Rolls the stage's event times once (seeded), starts each event on the clock tick that crosses its time
## and relays its signals. At most one event runs: an event whose time comes while another runs waits and
## starts when that one ends. Events are EventDef.controller_scene instances; this file never names a
## concrete event.

signal event_started(def: EventDef, event: StageEvent)
signal event_progressed(def: EventDef, text: String)
## A real result only (success or failure). An event cut short by the boss or the run end emits `event_aborted`.
signal event_ended(def: EventDef, success: bool)
signal event_aborted(def: EventDef)

var _timeline: Timeline = Timeline.new(0.0)
var _defs: Array[EventDef] = []
var _context_factory: Callable
var _previous_time: float = -1.0
var _waiting: Array[int] = []
var _running: StageEvent
var _running_def: EventDef


## `context_factory` is `func(def: EventDef) -> EventContext`.
func bind(stage_def: StageDef, clock: StageClock, context_factory: Callable, rng: RunRng, wave_times: PackedFloat32Array) -> void:
	_context_factory = context_factory
	var times := PackedFloat32Array()
	if stage_def.events != null:
		times = stage_def.events.roll_times(stage_def.duration, wave_times, rng)
		_defs = stage_def.events.roll_defs(times.size(), rng)
	_timeline = Timeline.new(stage_def.duration, times)
	clock.ticked.connect(_on_clock_ticked)


## Rolled start times, ascending; the HUD draws them on the timeline.
func event_times() -> PackedFloat32Array:
	return _timeline.marks


func planned_defs() -> Array[EventDef]:
	return _defs.duplicate()


func running() -> StageEvent:
	return _running


## Ends the running event without reward (boss appearance, run end) and drops any waiting one.
func abort_running() -> void:
	_waiting.clear()
	if _running == null:
		return
	var event := _running
	var def := _running_def
	_running = null
	_running_def = null
	event.abort()
	event.queue_free()
	event_aborted.emit(def)


## Stage-layer check run by Stage.start: every controller root must extend StageEvent.
static func validate_defs(schedule: EventSchedule) -> PackedStringArray:
	var errors := PackedStringArray()
	if schedule == null:
		return errors
	for def in schedule.pool:
		if def == null or def.controller_scene == null:
			continue  # EventDef.validate reports these
		var root := def.controller_scene.instantiate()
		if root == null:
			continue
		if not (root is StageEvent):
			errors.append("event '%s': controller_scene root is not a StageEvent" % def.id)
		root.free()
	return errors


func _on_clock_ticked(elapsed: float) -> void:
	for index in _timeline.crossed(_previous_time, elapsed):
		_waiting.append(index)
	_previous_time = elapsed
	_start_next()


func _start_next() -> void:
	if _running != null or _waiting.is_empty():
		return
	var def := _defs[_waiting.pop_front()]
	var event := def.controller_scene.instantiate() as StageEvent
	var context := _context_factory.call(def) as EventContext
	context.layer.add_child(event)
	_running = event
	_running_def = def
	event.progress_changed.connect(_on_progress_changed.bind(def))
	event.succeeded.connect(_on_event_finished.bind(event, def, true))
	event.failed.connect(_on_event_finished.bind(event, def, false))
	event_started.emit(def, event)
	# A listener may have aborted the event already; it must not begin after that.
	if _running != event:
		return
	event.begin(context)


func _on_progress_changed(text: String, def: EventDef) -> void:
	event_progressed.emit(def, text)


func _on_event_finished(event: StageEvent, def: EventDef, success: bool) -> void:
	if event != _running:
		return
	_running = null
	_running_def = null
	event.queue_free()
	event_ended.emit(def, success)
	# Deferred: events usually end inside a physics callback (a hit), where new areas must not be added.
	_start_next.call_deferred()
