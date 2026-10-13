class_name Projectile
extends Node2D
## A straight-flying projectile that applies its def's effect to hurtboxes it touches.

var _def: ProjectileDef
var _damage: float = 0.0
var _velocity: Vector2 = Vector2.ZERO
var _pierce_left: int = 0
var _spent: bool = false

@onready var _sprite: Sprite2D = $Sprite2D
@onready var _hitbox: Hitbox = $Hitbox
@onready var _hit_shape: CollisionShape2D = $Hitbox/CollisionShape2D
@onready var _applier: EffectApplier = $EffectApplier
@onready var _lifetime: Lifetime = $Lifetime


func _ready() -> void:
	_hitbox.hurtbox_entered.connect(_on_hurtbox_entered)
	_lifetime.expired.connect(queue_free)


## Call after the projectile has entered the tree.
func launch(def: ProjectileDef, shooter_asc: AbilitySystemComponent, damage: float, direction: Vector2) -> void:
	_def = def
	_damage = damage
	_pierce_left = def.pierce
	_velocity = direction.normalized() * def.speed
	rotation = direction.angle()
	_sprite.texture = def.texture
	(_hit_shape.shape as CircleShape2D).radius = def.hit_radius
	_hitbox.collision_mask = def.hit_mask()
	_applier.bind(shooter_asc)
	_lifetime.restart(def.lifetime)


func _physics_process(delta: float) -> void:
	global_position += _velocity * delta


func _on_hurtbox_entered(hurtbox: Hurtbox) -> void:
	if _spent:
		return
	_applier.hit(hurtbox, _def.hit_effect, _damage, self)
	if _pierce_left > 0:
		_pierce_left -= 1
		return
	_spent = true
	queue_free()
