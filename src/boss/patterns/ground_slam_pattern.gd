class_name GroundSlamPattern
extends BossPattern
## Ground slam: a warning circle where the player stands when the slam starts; `warning` seconds later
## an AreaStrike there hits the player (only). Stepping out of the circle in time avoids it.

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var warning: float = 0.0
@export var radius: float = 0.0
@export var damage: float = 0.0
## How long the strike stays active; it must outlive a physics step so overlaps are reported.
@export var strike_seconds: float = 0.0

var _left: float = 0.0
var _point: Vector2 = Vector2.ZERO
var _telegraph: Telegraph


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null:
		errors.append("Ground slam: strike_scene is required")
	else:
		var probe := strike_scene.instantiate()
		if not probe is AreaStrike:
			errors.append("Ground slam: strike_scene root is not an AreaStrike")
		probe.free()
	if hit_effect == null:
		errors.append("Ground slam: hit_effect is required")
	if warning <= 0.0 or radius <= 0.0 or strike_seconds <= 0.0:
		errors.append("Ground slam: warning, radius and strike_seconds must be > 0")
	if damage < 0.0:
		errors.append("Ground slam: damage must be >= 0")
	return errors


func _begin() -> bool:
	var target: Node2D = context.targets.player if context.targets != null else null
	if not is_instance_valid(target):
		return false
	_point = target.global_position
	_telegraph = Telegraph.spawn_circle(context.projectile_layer, _point, radius, warning)
	_left = warning
	return true


func _advance(delta: float) -> void:
	_left -= delta
	if _left > 0.0:
		return
	_free_telegraph()
	var strike := strike_scene.instantiate() as AreaStrike
	context.projectile_layer.add_child(strike)
	strike.global_position = _point
	strike.strike(owner_asc, hit_effect, damage, radius, strike_seconds, PhysicsLayers.ALLY_HURT, context.targets.player)
	_complete()


func _abort() -> void:
	_free_telegraph()


func _free_telegraph() -> void:
	if is_instance_valid(_telegraph):
		_telegraph.queue_free()
	_telegraph = null
