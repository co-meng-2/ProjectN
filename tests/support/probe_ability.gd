class_name ProbeAbility
extends ActorAbility
## Test double: counts activations, optionally stays active for `hold_seconds`, and reports
## `reject_reason` from validate() so def validation can be tested.

@export var hold_seconds: float = 0.0
@export var reject_reason: String = ""

var activations: int = 0
var physics_ticks: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if reject_reason != "":
		errors.append(reject_reason)
	return errors


func _physics_process(_delta: float) -> void:
	physics_ticks += 1


func _activate_ability() -> bool:
	activations += 1
	if hold_seconds > 0.0:
		await task_wait_delay(hold_seconds)
	return true
