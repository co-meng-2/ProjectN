class_name AreaStrike
extends Node2D
## A short-lived circular hit: damages every hurtbox on `target_mask` it overlaps, once each, draws a ring,
## then frees itself after `seconds`. `only_actor` narrows it to one actor (meteors and the boss's slam and
## charge hit the player but not the building). The one area-hit path: Blade Sweep (MeleeStrike), meteors
## and boss patterns all damage through Hitbox + EffectApplier, i.e. EffectBuilder.damage.

signal struck(hurtbox: Hurtbox)

const RING_WIDTH: float = 2.0
const RING_SEGMENTS: int = 32

@export var ring_color: Color = Color(1.0, 0.9, 0.6, 0.8)

var _effect: GameplayEffect
var _damage: float = 0.0
var _radius: float = 0.0
var _only_actor: Node2D
var _filtered: bool = false

@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the strike has entered the tree.
func strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float, target_mask: int = PhysicsLayers.ENEMY_HURT, only_actor: Node2D = null) -> void:
	_effect = effect
	_damage = damage
	_radius = radius
	_only_actor = only_actor
	_filtered = only_actor != null
	_hitbox.collision_mask = target_mask
	(_hit_shape.shape as CircleShape2D).radius = radius
	_applier.bind(attacker_asc)
	_lifetime.restart(seconds)
	queue_redraw()


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	if _filtered and (not is_instance_valid(_only_actor) or hurtbox.actor != _only_actor):
		return
	if _applier.hit(hurtbox, _effect, _damage, self):
		struck.emit(hurtbox)


func _draw() -> void:
	if _radius > 0.0:
		draw_arc(Vector2.ZERO, _radius, 0.0, TAU, RING_SEGMENTS, ring_color, RING_WIDTH)
