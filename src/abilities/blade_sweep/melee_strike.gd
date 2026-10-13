class_name MeleeStrike
extends Node2D
## A short-lived circular hit area: damages every enemy hurtbox it overlaps once, draws a ring, then
## frees itself. Reuses Hitbox + EffectApplier + Lifetime so damage takes the same path as projectiles.

const RING_COLOR: Color = Color(1.0, 0.9, 0.6, 0.8)
const RING_WIDTH: float = 2.0
const RING_SEGMENTS: int = 32

var _effect: GameplayEffect
var _damage: float = 0.0
var _radius: float = 0.0

@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the strike has entered the tree.
func strike(attacker_asc: AbilitySystemComponent, effect: GameplayEffect, damage: float, radius: float, seconds: float) -> void:
	_effect = effect
	_damage = damage
	_radius = radius
	(_hit_shape.shape as CircleShape2D).radius = radius
	_applier.bind(attacker_asc)
	_lifetime.restart(seconds)
	queue_redraw()


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	_applier.hit(hurtbox, _effect, _damage, self)


func _draw() -> void:
	if _radius > 0.0:
		draw_arc(Vector2.ZERO, _radius, 0.0, TAU, RING_SEGMENTS, RING_COLOR, RING_WIDTH)
