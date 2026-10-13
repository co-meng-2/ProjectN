class_name Enemy
extends CharacterBody2D
## Generic enemy configured by an EnemyDef: chases a target and damages allies it touches.

signal died(enemy: Enemy)
signal loot_dropped(roll: DropRoll, position: Vector2)

const FREE_DELAY_AFTER_DEATH: float = 0.6
const HURTBOX_PADDING: float = 2.0
const CONTACT_PADDING: float = 4.0

var def: EnemyDef
var _target: Node2D

@onready var _asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var _health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _contact_hitbox: Hitbox = $ContactHitbox
@onready var _applier: EffectApplier = $EffectApplier
@onready var _contact: ContactDamage = $ContactDamage
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _dropper: LootDropper = $LootDropper
@onready var _body_shape: CollisionShape2D = $CollisionShape2D
@onready var _hurt_shape: CollisionShape2D = $Hurtbox/CollisionShape2D
@onready var _contact_shape: CollisionShape2D = $ContactHitbox/CollisionShape2D


## Call after the enemy has entered the tree.
func setup(p_def: EnemyDef, target: Node2D, rng: RunRng) -> void:
	def = p_def
	_target = target
	_asc.initialize_attribute_overrides(def.stats.to_overrides())
	_health.bind(_asc)
	_hurtbox.bind(_asc, self)
	_applier.bind(_asc)
	_mover.bind(self, _asc)
	_contact.bind(_contact_hitbox, _applier, def.contact_effect, def.contact_damage, def.contact_interval)
	_dropper.bind(_health, def.drops, rng, self)
	_visual.apply(def.visual, def.visual_scale, def.tint)
	(_body_shape.shape as CircleShape2D).radius = def.body_radius
	(_hurt_shape.shape as CircleShape2D).radius = def.body_radius + HURTBOX_PADDING
	(_contact_shape.shape as CircleShape2D).radius = def.body_radius + CONTACT_PADDING
	_health.damaged.connect(_on_damaged)
	_health.died.connect(_on_died)
	_dropper.drop_requested.connect(loot_dropped.emit)
	_contact.attacked.connect(_on_attacked)
	add_to_group(Groups.ENEMIES)


func _physics_process(_delta: float) -> void:
	if not is_instance_valid(_target):
		_mover.set_direction(Vector2.ZERO)
		_visual.set_moving(false)
		return
	var to_target := _target.global_position - global_position
	_mover.set_direction(to_target.normalized())
	_visual.face(to_target.x)
	_visual.set_moving(true)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_attacked(_targets: Array[Hurtbox]) -> void:
	_visual.play_once(AnimNames.ATTACK)


func _on_died() -> void:
	remove_from_group(Groups.ENEMIES)
	set_physics_process(false)
	_mover.set_physics_process(false)
	_contact.set_physics_process(false)
	_hurtbox.set_deferred("monitorable", false)
	_contact_hitbox.set_deferred("monitoring", false)
	_body_shape.set_deferred("disabled", true)
	_asc.cleanup.call_deferred()
	_visual.play_once(AnimNames.DEATH)
	died.emit(self)
	get_tree().create_timer(FREE_DELAY_AFTER_DEATH).timeout.connect(queue_free)
