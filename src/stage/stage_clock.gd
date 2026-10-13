class_name StageClock
extends Node
## Elapsed stage time. Everything time-driven (spawning, waves, events, HUD) subscribes to `ticked`.
## `ended` fires once when `elapsed` reaches `duration`; the clock then keeps ticking (overtime: the boss
## fight) until stopped, so the trickle continues while waves and events, scheduled inside `duration`,
## never fire again.

signal ticked(elapsed: float)
signal ended

var elapsed: float = 0.0
var duration: float = 0.0
var _running: bool = false
var _ended: bool = false


func start(p_duration: float) -> void:
	duration = p_duration
	elapsed = 0.0
	_running = true
	_ended = false


func stop() -> void:
	_running = false


func is_running() -> bool:
	return _running


func has_ended() -> bool:
	return _ended


func step(delta: float) -> void:
	if not _running:
		return
	elapsed += delta
	ticked.emit(elapsed)
	if not _ended and elapsed >= duration:
		_ended = true
		ended.emit()


func _physics_process(delta: float) -> void:
	step(delta)
