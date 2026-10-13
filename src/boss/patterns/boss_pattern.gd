@abstract
class_name BossPattern
extends ActorAbility
## Base of a boss attack. One activation runs one attack: `_begin()` starts it (false = cannot run now,
## nothing happens), `step(delta)` advances it through `_advance()` (physics in play, by hand in tests),
## `_complete()` ends it. Aborting the ability (boss death, ASC cleanup) calls `_abort()` so telegraphs and
## strikes are removed. Scenes set `activation_owned_tags = [State.Casting]`, which stops the boss chasing.

signal completed

var _running: bool = false


func step(delta: float) -> void:
	if _running:
		_advance(delta)


func is_running() -> bool:
	return _running


func _activate_ability() -> bool:
	if context == null:
		return false
	_running = true
	if not _begin():
		_running = false
		return false
	if _running:
		ability_ended.connect(_on_ability_ended, CONNECT_ONE_SHOT)
		await completed
	return true


## Starts the attack; return false when it cannot run now (no target, no summon callable).
func _begin() -> bool:
	return true


func _advance(_delta: float) -> void:
	pass


## Removes whatever the attack placed (telegraphs, strikes) when it is interrupted.
func _abort() -> void:
	pass


func _complete() -> void:
	if not _running:
		return
	_running = false
	completed.emit()


func _on_ability_ended(_was_cancelled: bool) -> void:
	if not _running:
		return  # normal completion: try_activate ended the ability after `completed`
	_running = false
	_abort()
	completed.emit()


func _physics_process(delta: float) -> void:
	step(delta)
