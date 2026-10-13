class_name Mover
extends Node
## Drives a CharacterBody2D at the speed of the ASC's `move_speed` attribute, with a dash override.

signal dash_started
signal dash_finished

var last_direction: Vector2 = Vector2.RIGHT
var _body: CharacterBody2D
var _asc: AbilitySystemComponent
var _direction: Vector2 = Vector2.ZERO
var _dash_velocity: Vector2 = Vector2.ZERO
var _dash_time_left: float = 0.0


func bind(body: CharacterBody2D, asc: AbilitySystemComponent) -> void:
	_body = body
	_asc = asc


func set_direction(heading: Vector2) -> void:
	_direction = heading.limit_length(1.0)
	if _direction != Vector2.ZERO:
		last_direction = _direction.normalized()


func direction() -> Vector2:
	return _direction


func is_dashing() -> bool:
	return _dash_time_left > 0.0


func is_moving() -> bool:
	return _direction != Vector2.ZERO or is_dashing()


func dash(dash_direction: Vector2, speed: float, duration: float) -> void:
	var heading := dash_direction.normalized() if dash_direction != Vector2.ZERO else last_direction
	_dash_velocity = heading * speed
	_dash_time_left = duration
	dash_started.emit()


## Ends a running dash at once (the actor stops dashing; normal movement resumes next step).
func cancel_dash() -> void:
	if _dash_time_left <= 0.0:
		return
	_dash_time_left = 0.0
	dash_finished.emit()


func step(delta: float) -> void:
	if _body == null:
		return
	if _dash_time_left > 0.0:
		_dash_time_left -= delta
		_body.velocity = _dash_velocity
		if _dash_time_left <= 0.0:
			dash_finished.emit()
	else:
		_body.velocity = _direction * _asc.get_attribute("move_speed").current_value
	_body.move_and_slide()


func _physics_process(delta: float) -> void:
	step(delta)
