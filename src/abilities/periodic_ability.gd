@abstract
class_name PeriodicAbility
extends ActorAbility
## An auto-firing ability whose cooldown is a Curve sampled at `ability_level`. It keeps its own timer
## instead of a GAS cooldown effect, so the cooldown can scale with the grade and tests can drive it
## through `step(delta)` after `set_physics_process(false)`.

@export var cooldown: Curve
@export var auto_fire: bool = true

var _cooldown_left: float = 0.0


func cooldown_seconds() -> float:
	return cooldown.sample(ability_level) if cooldown != null else INF


## Called by AbilityDef.validate() on an instance of the scene (not in the tree).
func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if cooldown == null:
		errors.append("%s: cooldown curve is null" % ability_name)
	return errors


func step(delta: float) -> void:
	_cooldown_left = maxf(_cooldown_left - delta, 0.0)
	if not auto_fire or context == null or is_active or _cooldown_left > 0.0:
		return
	try_activate()


## Subclasses call this once they have actually fired; a miss costs nothing.
func start_cooldown() -> void:
	_cooldown_left = cooldown_seconds()


func _physics_process(delta: float) -> void:
	step(delta)
