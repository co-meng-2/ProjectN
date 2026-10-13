class_name Lifetime
extends Node
## Counts down and announces expiry; the owner decides what to do about it.

signal expired

@export var seconds: float = 1.0

var _remaining: float = 0.0
var _has_expired: bool = false


func _ready() -> void:
	_remaining = seconds


func restart(new_seconds: float) -> void:
	seconds = new_seconds
	_remaining = new_seconds
	_has_expired = false


func step(delta: float) -> void:
	if _has_expired:
		return
	_remaining -= delta
	if _remaining <= 0.0:
		_has_expired = true
		expired.emit()


func _physics_process(delta: float) -> void:
	step(delta)
