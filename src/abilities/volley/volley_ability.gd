class_name VolleyAbility
extends PeriodicAbility
## Fires one arrow at each of the N nearest enemies in range. N and damage are curves over the grade level.

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var arrow_count: Curve
@export var damage: Curve
## Set in the scene; no balance numbers in code.
@export var attack_range: float = 0.0


func validate() -> PackedStringArray:
	var errors := super.validate()
	if projectile_scene == null or projectile_def == null:
		errors.append("Volley: projectile_scene and projectile_def are required")
	if arrow_count == null or damage == null:
		errors.append("Volley: arrow_count and damage curves are required")
	if attack_range <= 0.0:
		errors.append("Volley: attack_range must be > 0")
	return errors


func _activate_ability() -> bool:
	var origin := context.actor.global_position
	var targets := context.targets.nearest_enemies(origin, attack_range, roundi(arrow_count.sample(ability_level)))
	if targets.is_empty():
		return false
	start_cooldown()
	var amount := damage.sample(ability_level)
	for target in targets:
		var projectile := projectile_scene.instantiate() as Projectile
		context.projectile_layer.add_child(projectile)
		projectile.global_position = origin
		projectile.launch(projectile_def, owner_asc, amount, (target.global_position - origin).normalized())
	animation_requested.emit(AnimNames.ATTACK, (targets[0].global_position - origin).normalized())
	return true
