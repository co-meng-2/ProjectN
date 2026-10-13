class_name ChargePattern
extends BossPattern
## Charge: a warning line from the boss toward the player for `warning` seconds, then the boss dashes
## `distance` at `speed`. An AreaStrike riding on the boss hits the player (only) once per charge.

enum Phase { WARNING, CHARGING }

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var warning: float = 0.0
@export var speed: float = 0.0
@export var distance: float = 0.0
@export var damage: float = 0.0
## Contact radius of the charging boss.
@export var hit_radius: float = 0.0
@export var line_width: float = 0.0

var _phase: Phase = Phase.WARNING
var _left: float = 0.0
var _direction: Vector2 = Vector2.RIGHT
var _telegraph: Telegraph
var _strike: AreaStrike


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null:
		errors.append("Charge: strike_scene is required")
	else:
		var probe := strike_scene.instantiate()
		if not probe is AreaStrike:
			errors.append("Charge: strike_scene root is not an AreaStrike")
		probe.free()
	if hit_effect == null:
		errors.append("Charge: hit_effect is required")
	if warning <= 0.0 or speed <= 0.0 or distance <= 0.0 or hit_radius <= 0.0 or line_width <= 0.0:
		errors.append("Charge: warning, speed, distance, hit_radius and line_width must be > 0")
	if damage < 0.0:
		errors.append("Charge: damage must be >= 0")
	return errors


func charge_seconds() -> float:
	return distance / speed if speed > 0.0 else 0.0


func _begin() -> bool:
	var target: Node2D = context.targets.player if context.targets != null else null
	if not is_instance_valid(target):
		return false
	var origin := context.actor.global_position
	_direction = (target.global_position - origin).normalized()
	if _direction == Vector2.ZERO:
		_direction = Vector2.RIGHT
	_telegraph = Telegraph.spawn_line(context.projectile_layer, origin, origin + _direction * distance, line_width, warning)
	_phase = Phase.WARNING
	_left = warning
	return true


func _advance(delta: float) -> void:
	_left -= delta
	if _left > 0.0:
		return
	if _phase == Phase.WARNING:
		_start_charge()
	else:
		_complete()


func _start_charge() -> void:
	_free_telegraph()
	_phase = Phase.CHARGING
	_left = charge_seconds()
	context.mover.dash(_direction, speed, _left)
	_strike = strike_scene.instantiate() as AreaStrike
	context.actor.add_child(_strike)  # rides along with the boss
	_strike.strike(owner_asc, hit_effect, damage, hit_radius, _left, PhysicsLayers.ALLY_HURT, context.targets.player)


func _abort() -> void:
	_free_telegraph()
	if is_instance_valid(_strike):
		_strike.queue_free()
	_strike = null


func _free_telegraph() -> void:
	if is_instance_valid(_telegraph):
		_telegraph.queue_free()
	_telegraph = null
