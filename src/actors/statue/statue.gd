class_name Statue
extends Node2D
## A destructible event objective: ASC + Health + a Hurtbox on ENEMY_HURT, in the enemy group so auto-aim
## shoots it. Not an Enemy: no kill count, no Event.Kill, no drops. Drawn as a simple stone pillar that
## darkens as it takes damage.

signal destroyed(statue: Statue)

@export var radius: float = 16.0
@export var stone_color: Color = Color(0.62, 0.6, 0.56)
@export var cracked_color: Color = Color(0.3, 0.26, 0.24)

var _removed: bool = false

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox


## Call after the statue has entered the tree.
func setup(stats: StatBlock) -> void:
	asc.initialize_attribute_overrides(stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	health.changed.connect(_on_health_changed)
	health.died.connect(_on_died)
	add_to_group(Groups.ENEMIES)
	queue_redraw()


## Takes the statue out of play without counting it as destroyed (event cleanup). Safe inside physics callbacks.
func remove() -> void:
	if _removed:
		return
	_removed = true
	remove_from_group(Groups.ENEMIES)
	_hurtbox.set_deferred("monitorable", false)
	asc.cleanup.call_deferred()
	queue_free()


func is_removed() -> bool:
	return _removed


func _on_died() -> void:
	if _removed:
		return
	remove()
	destroyed.emit(self)


func _on_health_changed(_current: float, _maximum: float) -> void:
	queue_redraw()


func _draw() -> void:
	var ratio := 1.0
	if health != null and health.maximum() > 0.0:
		ratio = clampf(health.current() / health.maximum(), 0.0, 1.0)
	var color := cracked_color.lerp(stone_color, ratio)
	draw_rect(Rect2(-radius, -radius * 0.4, radius * 2.0, radius * 0.6), color.darkened(0.25))
	draw_rect(Rect2(-radius * 0.6, -radius * 2.4, radius * 1.2, radius * 2.1), color)
	draw_circle(Vector2(0.0, -radius * 2.6), radius * 0.55, color)
