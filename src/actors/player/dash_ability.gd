class_name DashAbility
extends ActorAbility
## A short burst of speed. Invulnerability comes from `activation_owned_tags` set in the scene.

@export var dash_speed: float = 480.0
@export var dash_duration: float = 0.18


func _activate_ability() -> bool:
	commit_ability()
	context.mover.dash(context.mover.direction(), dash_speed, dash_duration)
	await task_wait_delay(dash_duration)
	return true
