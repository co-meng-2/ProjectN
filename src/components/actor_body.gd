class_name ActorBody
extends RefCounted
## The body plumbing Enemy and Boss share: sizing the three collision circles from one radius, wiring the
## contact-damage components, and the physical part of dying. The actor keeps its own signals and extras.

const HURTBOX_PADDING: float = 2.0
const CONTACT_PADDING: float = 4.0

var _actor: Node2D
var _asc: AbilitySystemComponent
var _visual: ActorVisual
var _hurtbox: Hurtbox
var _contact_hitbox: Hitbox
var _body_shape: CollisionShape2D


func _init(actor: Node2D, asc: AbilitySystemComponent, visual: ActorVisual, hurtbox: Hurtbox, contact_hitbox: Hitbox, body_shape: CollisionShape2D) -> void:
	_actor = actor
	_asc = asc
	_visual = visual
	_hurtbox = hurtbox
	_contact_hitbox = contact_hitbox
	_body_shape = body_shape


## Sizes the body, hurtbox and contact circles (each a CircleShape2D) around `radius`.
func size_shapes(radius: float, hurt_shape: CollisionShape2D, contact_shape: CollisionShape2D) -> void:
	(_body_shape.shape as CircleShape2D).radius = radius
	(hurt_shape.shape as CircleShape2D).radius = radius + HURTBOX_PADDING
	(contact_shape.shape as CircleShape2D).radius = radius + CONTACT_PADDING


func wire_contact(contact: ContactDamage, applier: EffectApplier, effect: GameplayEffect, damage: float, interval: float) -> void:
	contact.bind(_contact_hitbox, applier, effect, damage, interval)


## Leaves the enemy group, stops the actor and every node in `stopped` processing, disables the colliders,
## schedules the ASC cleanup, plays the death animation and frees the actor after `free_delay`.
## The caller emits its own `died` afterwards.
func die(stopped: Array[Node], free_delay: float) -> void:
	_actor.remove_from_group(Groups.ENEMIES)
	_actor.set_physics_process(false)
	for node in stopped:
		node.set_physics_process(false)
	_hurtbox.set_deferred("monitorable", false)
	_contact_hitbox.set_deferred("monitoring", false)
	_body_shape.set_deferred("disabled", true)
	_asc.cleanup.call_deferred()
	_visual.play_once(AnimNames.DEATH)
	_actor.get_tree().create_timer(free_delay).timeout.connect(_actor.queue_free)
