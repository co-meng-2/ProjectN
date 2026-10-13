class_name StageClock
extends Node
## Elapsed stage time. Everything time-driven (spawning, waves, HUD) subscribes to `ticked`.

signal ticked(elapsed: float)
signal ended

var elapsed: float = 0.0
var duration: float = 0.0
var _running: bool = false


func start(p_duration: float) -> void:
	duration = p_duration
	elapsed = 0.0
	_running = true


func stop() -> void:
	_running = false


func is_running() -> bool:
	return _running


func step(delta: float) -> void:
	if not _running:
		return
	elapsed = minf(elapsed + delta, duration)
	ticked.emit(elapsed)
	if elapsed >= duration:
		_running = false
		ended.emit()


func _physics_process(delta: float) -> void:
	step(delta)
