class_name StatBlock
extends Resource
## Base stats of an actor, applied to its ASC as attribute overrides.

@export var max_health: float = 100.0
@export var move_speed: float = 100.0
## Damage multiplier applied to hits this actor lands (1.0 = base damage).
@export var attack: float = 1.0


## max_health must precede health: health's clamp reads max_health.current_value.
func to_overrides() -> Dictionary[String, float]:
	var overrides: Dictionary[String, float] = {}
	overrides["max_health"] = max_health
	overrides["health"] = max_health
	overrides["move_speed"] = move_speed
	overrides["attack"] = attack
	return overrides


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if max_health <= 0.0:
		errors.append("StatBlock: max_health must be > 0")
	if move_speed < 0.0:
		errors.append("StatBlock: move_speed must be >= 0")
	return errors
