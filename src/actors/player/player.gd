class_name Player
extends CharacterBody2D
## The player's actor: input -> Mover, dash key -> ability tag, loot magnet, camera.
## It does not know which abilities it has: every ActorAbility on its ASC gets the same context.

signal died
signal loot_collected(kind: Wallet.Kind, amount: int)

@onready var health: Health = $Health
@onready var _asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _collector: LootCollector = $Collector
@onready var _camera: Camera2D = $Camera2D


## Call after the player has entered the tree.
func setup(profile: PlayerProfile, targets: TargetProvider, projectile_layer: Node) -> void:
	_asc.initialize_attribute_overrides(profile.stats.to_overrides())
	health.bind(_asc)
	_hurtbox.bind(_asc, self)
	_mover.bind(self, _asc)
	_visual.apply(profile.visual)
	_collector.bind(self)
	_bind_abilities(AbilityContext.new(self, _mover, targets, projectile_layer))
	_collector.collected.connect(loot_collected.emit)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	add_to_group(Groups.PLAYER)


func limit_camera(bounds: Rect2) -> void:
	_camera.limit_left = int(bounds.position.x)
	_camera.limit_top = int(bounds.position.y)
	_camera.limit_right = int(bounds.end.x)
	_camera.limit_bottom = int(bounds.end.y)


func gain_multiplier(kind: Wallet.Kind) -> float:
	var attribute := "gold_gain" if kind == Wallet.Kind.GOLD else "stone_gain"
	return _asc.get_attribute(attribute).current_value


func _physics_process(_delta: float) -> void:
	if health.is_dead:
		_mover.set_direction(Vector2.ZERO)
		return
	var input := InputActions.move_vector()
	_mover.set_direction(input)
	_visual.set_moving(_mover.is_moving())
	_visual.face(input.x)
	if Input.is_action_just_pressed(InputActions.DASH):
		_asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash)


func _bind_abilities(context: AbilityContext) -> void:
	for child in _asc.get_children():
		var ability := child as ActorAbility
		if ability != null:
			ability.bind_context(context)
			ability.animation_requested.connect(_on_animation_requested)


func _on_animation_requested(animation: StringName, facing: Vector2) -> void:
	_visual.face(facing.x)
	_visual.play_once(animation)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	for child in _asc.get_children():
		child.set_physics_process(false)
	_visual.play_once(AnimNames.DEATH)
	_asc.cleanup.call_deferred()
	died.emit()
