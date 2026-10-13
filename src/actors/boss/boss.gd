class_name Boss
extends CharacterBody2D
## The end-of-stage boss, configured by a BossDef: chases its target, damages allies it touches and owns
## its pattern abilities (granted from the def). It stands still while it owns State.Casting. When a
## pattern runs is decided by a BossBrain the Stage attaches. Not an Enemy: no kill count, no drops.

signal died(boss: Boss)

const FREE_DELAY_AFTER_DEATH: float = 1.0

var def: BossDef
var ability_context: AbilityContext
var _target: Node2D
var _body: ActorBody
var _patterns: Array[ActorAbility] = []
var _weights: Array[float] = []

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _contact_hitbox: Hitbox = $ContactHitbox
@onready var _applier: EffectApplier = $EffectApplier
@onready var _contact: ContactDamage = $ContactDamage
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _body_shape: CollisionShape2D = $CollisionShape2D
@onready var _hurt_shape: CollisionShape2D = $Hurtbox/CollisionShape2D
@onready var _contact_shape: CollisionShape2D = $ContactHitbox/CollisionShape2D


## Call after the boss has entered the tree. `summon` is `func(def: EnemyDef, position: Vector2) -> void`.
func setup(p_def: BossDef, target: Node2D, targets: TargetProvider, projectile_layer: Node, summon: Callable = Callable()) -> void:
	def = p_def
	_target = target
	asc.initialize_attribute_overrides(def.stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	_applier.bind(asc)
	_mover.bind(self, asc)
	_body = ActorBody.new(self, asc, _visual, _hurtbox, _contact_hitbox, _body_shape)
	_body.wire_contact(_contact, _applier, def.contact_effect, def.contact_damage, def.contact_interval)
	_visual.apply(def.visual, def.visual_scale, def.tint)
	_body.size_shapes(def.body_radius, _hurt_shape, _contact_shape)
	ability_context = AbilityContext.new(self, _mover, targets, projectile_layer, null)
	ability_context.summon = summon
	for pattern_def in def.patterns:
		if pattern_def == null or pattern_def.ability_scene == null:
			continue
		var ability := pattern_def.ability_scene.instantiate() as ActorAbility
		if ability == null:
			push_error("Boss '%s': a pattern scene root is not an ActorAbility" % def.id)
			continue
		ability.bind_context(ability_context)
		asc.add_child(ability)  # GameplayAbility._ready grants it to the ASC
		_patterns.append(ability)
		_weights.append(pattern_def.weight)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	add_to_group(Groups.ENEMIES)


func patterns() -> Array[ActorAbility]:
	return _patterns.duplicate()


func pattern_weights() -> Array[float]:
	return _weights.duplicate()


func is_casting() -> bool:
	return asc.has_tag(GameplayTags.State_Casting)


func _physics_process(_delta: float) -> void:
	if is_casting() or not is_instance_valid(_target):
		_mover.set_direction(Vector2.ZERO)
		_visual.set_moving(_mover.is_dashing())
		return
	var to_target := _target.global_position - global_position
	_mover.set_direction(to_target.normalized())
	_visual.face(to_target.x)
	_visual.set_moving(true)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	for ability in _patterns:
		if is_instance_valid(ability):
			ability.abort_ability()
			ability.set_physics_process(false)
	var stopped: Array[Node] = []
	stopped.assign(get_children())  # mover, contact damage, the attached brain
	_body.die(stopped, FREE_DELAY_AFTER_DEATH)
	died.emit(self)
