@abstract
class_name StageEvent
extends Node2D
## Base of an event controller (the root of an EventDef.controller_scene). The base owns the time limit
## (EventDef.time_limit) and guarantees the event ends exactly once: `_succeed()` / `_fail()` emit their
## signal after `_cleanup()`, `abort()` only cleans up. Running out of time calls `_on_time_up()`, which
## fails unless a subclass overrides it. Physics drives `step()` in play; tests call it by hand.

signal succeeded
signal failed
signal progress_changed(text: String)

var context: EventContext

var _time_left: float = 0.0
var _running: bool = false
var _ended: bool = false


func begin(p_context: EventContext) -> void:
	if _running or _ended:
		return
	context = p_context
	_time_left = p_context.def.time_limit
	_running = true
	_on_begin()


## Ends the event without a result (boss appearance, run end): cleanup only, no signal.
func abort() -> void:
	if not _running:
		return
	_running = false
	_ended = true
	_cleanup()


func step(delta: float) -> void:
	if not _running:
		return
	_time_left = maxf(_time_left - delta, 0.0)
	_on_step(delta)
	if _running and _time_left <= 0.0:
		_on_time_up()


## Things the HUD points at when they are off-screen.
func objectives() -> Array[Node2D]:
	var none: Array[Node2D] = []
	return none


func remaining_time() -> float:
	return _time_left


func is_running() -> bool:
	return _running


func has_ended() -> bool:
	return _ended


func report_progress(text: String) -> void:
	progress_changed.emit(text)


func _succeed() -> void:
	_finish(true)


func _fail() -> void:
	_finish(false)


## Subclass hooks.
func _on_begin() -> void:
	pass


func _on_step(_delta: float) -> void:
	pass


func _on_time_up() -> void:
	_fail()


func _cleanup() -> void:
	pass


func _finish(success: bool) -> void:
	if not _running:
		return
	_running = false
	_ended = true
	_cleanup()
	if success:
		succeeded.emit()
	else:
		failed.emit()


func _physics_process(delta: float) -> void:
	step(delta)
