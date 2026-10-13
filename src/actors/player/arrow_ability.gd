class_name ArrowAbility
extends ActorAbility
## Fires a short-range arrow at the nearest enemy. With `auto_fire` it retries every physics frame;
## it fails without starting a cooldown when nothing is in range.

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var base_damage: float = 12.0
@export var attack_range: float = 260.0
@export var auto_fire: bool = true


func _physics_process(_delta: float) -> void:
	if not auto_fire or context == null or is_active or not check_cooldown():
		return
	if context.targets.nearest_enemy(context.actor.global_position, attack_range) != null:
		try_activate()


func _activate_ability() -> bool:
	var origin := context.actor.global_position
	var target := context.targets.nearest_enemy(origin, attack_range)
	if target == null:
		return false
	commit_ability()
	var direction := (target.global_position - origin).normalized()
	var projectile := projectile_scene.instantiate() as Projectile
	context.projectile_layer.add_child(projectile)
	projectile.global_position = origin
	projectile.launch(projectile_def, owner_asc, base_damage * ability_level, direction)
	animation_requested.emit(AnimNames.ATTACK, direction)
	return true
