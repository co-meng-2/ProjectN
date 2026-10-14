class_name Player
extends CharacterBody2D
## The player's actor: input -> Mover, dash key -> ability tag, loot magnet, camera.
## It does not know which abilities it has: every ActorAbility on its ASC gets the same context,
## whether placed in the scene (arrow, dash) or granted at runtime by the loadout.

signal died
signal loot_collected(kind: Wallet.Kind, amount: int)

var ability_context: AbilityContext

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _mover: Mover = $Mover
@onready var _visual: ActorVisual = $Visual
@onready var _collector: LootCollector = $Collector
@onready var _camera: Camera2D = $Camera2D


## Call after the player has entered the tree.
func setup(profile: PlayerProfile, targets: TargetProvider, projectile_layer: Node, wallet: Wallet) -> void:
	asc.initialize_attribute_overrides(profile.stats.to_overrides())
	_apply_skill_bonuses(profile.skill_bonuses)
	health.bind(asc)
	_hurtbox.bind(asc, self)
	_mover.bind(self, asc)
	_visual.apply(profile.visual)
	_collector.bind(self)
	ability_context = AbilityContext.new(self, _mover, targets, projectile_layer, wallet)
	for child in asc.get_children():
		var ability := child as ActorAbility
		if ability != null:
			_attach(ability)
	_collector.collected.connect(loot_collected.emit)
	health.damaged.connect(_on_damaged)
	health.died.connect(_on_died)
	add_to_group(Groups.PLAYER)


## Permanent skill-tree effects, applied at their purchased level. Health starts full AFTER them: the stats override
## above filled health to the base max, and an INFINITE max_health bonus does not heal.
func _apply_skill_bonuses(bonuses: Array[SkillBonus]) -> void:
	if bonuses.is_empty():
		return
	for bonus in bonuses:
		asc.apply_gameplay_effect(bonus.effect, asc, float(bonus.level))
	var full_health: Dictionary[String, float] = {"health": asc.get_attribute("max_health").current_value}
	asc.initialize_attribute_overrides(full_health)


func limit_camera(bounds: Rect2) -> void:
	_camera.limit_left = int(bounds.position.x)
	_camera.limit_top = int(bounds.position.y)
	_camera.limit_right = int(bounds.end.x)
	_camera.limit_bottom = int(bounds.end.y)


func gain_multiplier(kind: Wallet.Kind) -> float:
	var attribute := "gold_gain" if kind == Wallet.Kind.GOLD else "stone_gain"
	return asc.get_attribute(attribute).current_value


## Adds a runtime ability under the ASC. GameplayAbility._ready grants itself when its parent is an ASC,
## so no explicit grant call is needed (that would emit `ability_granted` twice).
func grant_ability(ability: ActorAbility) -> void:
	if ability.get_parent() != null:
		return  # already granted here, or owned by someone else: never re-parent
	_attach(ability)
	asc.add_child(ability)
	if ability.owner_asc == null:
		asc.grant_ability(ability)
	if health.is_dead:
		ability.set_physics_process(false)


## Ends and removes a runtime ability. ASC.remove_ability only untracks and queue_frees: an active
## ability must be aborted first so its activation_owned_tags come off.
func revoke_ability(ability: ActorAbility) -> void:
	if not is_instance_valid(ability) or ability.get_parent() != asc:
		return
	ability.abort_ability()
	asc.remove_ability(ability)
	asc.remove_child(ability)


func _physics_process(_delta: float) -> void:
	if health.is_dead:
		_mover.set_direction(Vector2.ZERO)
		return
	var input := InputActions.move_vector()
	_mover.set_direction(input)
	_visual.set_moving(_mover.is_moving())
	_visual.face(input.x)
	if Input.is_action_just_pressed(InputActions.DASH):
		asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash)


func _attach(ability: ActorAbility) -> void:
	ability.bind_context(ability_context)
	if not ability.animation_requested.is_connected(_on_animation_requested):
		ability.animation_requested.connect(_on_animation_requested)


func _on_animation_requested(animation: StringName, facing: Vector2) -> void:
	_visual.face(facing.x)
	_visual.play_once(animation)


func _on_damaged(_amount: float, _instigator: Node) -> void:
	_visual.flash_hit()


func _on_died() -> void:
	for child in asc.get_children():
		child.set_physics_process(false)
	_visual.play_once(AnimNames.DEATH)
	asc.cleanup.call_deferred()
	died.emit()
