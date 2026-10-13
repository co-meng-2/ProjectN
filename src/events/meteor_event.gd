class_name MeteorEvent
extends StageEvent
## "Meteor shower": every `interval` seconds a warning circle appears within `landing_spread` of the player;
## `warning` seconds later an AreaStrike hits only the player there. More than `max_hits` landed hits fails
## at once; surviving the time limit succeeds. The event owns a small ASC so the damage has a GAS source.

const ASC_SCENE: PackedScene = preload("res://src/gas/actor_asc.tscn")

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var interval: float = 0.0
@export var warning: float = 0.0
@export var radius: float = 0.0
@export var damage: float = 0.0
@export var landing_spread: float = 0.0
@export var max_hits: int = 0
## How long each landed strike stays active; it must outlive a physics step so overlaps are reported.
@export var strike_seconds: float = 0.0


## A warned meteor that has not landed yet.
class Incoming:
	var position: Vector2
	var left: float
	var telegraph: Telegraph

	func _init(p_position: Vector2, p_left: float, p_telegraph: Telegraph) -> void:
		position = p_position
		left = p_left
		telegraph = p_telegraph


var _source_asc: AbilitySystemComponent
var _next_drop_in: float = 0.0
var _incoming: Array[Incoming] = []
var _hits: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if strike_scene == null:
		errors.append("MeteorEvent: strike_scene is required")
	else:
		var probe := strike_scene.instantiate()
		if not probe is AreaStrike:
			errors.append("MeteorEvent: strike_scene root is not an AreaStrike")
		probe.free()
	if hit_effect == null:
		errors.append("MeteorEvent: hit_effect is required")
	if interval <= 0.0 or warning <= 0.0 or radius <= 0.0 or strike_seconds <= 0.0:
		errors.append("MeteorEvent: interval, warning, radius and strike_seconds must be > 0")
	if damage < 0.0 or landing_spread < 0.0 or max_hits < 0:
		errors.append("MeteorEvent: damage, landing_spread and max_hits must be >= 0")
	return errors


func hits() -> int:
	return _hits


func _on_begin() -> void:
	_source_asc = ASC_SCENE.instantiate() as AbilitySystemComponent
	_source_asc.name = "AbilitySystemComponent"
	add_child(_source_asc)
	_next_drop_in = interval
	_report()


func _on_step(delta: float) -> void:
	for i in range(_incoming.size() - 1, -1, -1):
		var meteor := _incoming[i]
		meteor.left -= delta
		if meteor.left <= 0.0:
			_incoming.remove_at(i)
			_land(meteor)
	_next_drop_in -= delta
	while _next_drop_in <= 0.0:
		_drop()
		_next_drop_in += interval


func _drop() -> void:
	if not is_instance_valid(context.player):
		return
	var point: Vector2 = context.player.global_position + context.rng.offset_in_circle(landing_spread)
	var telegraph := Telegraph.spawn_circle(self, point, radius, warning)
	_incoming.append(Incoming.new(point, warning, telegraph))


func _land(meteor: Incoming) -> void:
	if is_instance_valid(meteor.telegraph):
		meteor.telegraph.queue_free()
	if not is_instance_valid(context.player):
		return  # a freed only_actor would turn the filter off and hit the building
	var strike := strike_scene.instantiate() as AreaStrike
	add_child(strike)
	strike.global_position = meteor.position
	strike.struck.connect(_on_player_struck)
	strike.strike(_source_asc, hit_effect, damage, radius, strike_seconds, PhysicsLayers.ALLY_HURT, context.player)


func _on_player_struck(_hurtbox: Hurtbox) -> void:
	if not is_running():
		return
	_hits += 1
	_report()
	if _hits > max_hits:
		_fail()


## Surviving the shower is the objective.
func _on_time_up() -> void:
	_succeed()


func _report() -> void:
	report_progress("Hits %d/%d" % [_hits, max_hits])


func _cleanup() -> void:
	for meteor in _incoming:
		if is_instance_valid(meteor.telegraph):
			meteor.telegraph.queue_free()
	_incoming.clear()
	if is_instance_valid(_source_asc):
		_source_asc.cleanup.call_deferred()
