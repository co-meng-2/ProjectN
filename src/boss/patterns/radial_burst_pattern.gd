class_name RadialBurstPattern
extends BossPattern
## Radial burst: `count` projectiles fired evenly around the boss. The projectile def must hit ALLIES
## (the player and the building); speed and lifetime live in that def.

@export var projectile_scene: PackedScene
@export var projectile_def: ProjectileDef
@export var count: int = 0
@export var damage: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if projectile_scene == null or projectile_def == null:
		errors.append("Radial burst: projectile_scene and projectile_def are required")
	else:
		var probe := projectile_scene.instantiate()
		if not probe is Projectile:
			errors.append("Radial burst: projectile_scene root is not a Projectile")
		probe.free()
		if projectile_def.hits != ProjectileDef.Team.ALLIES:
			errors.append("Radial burst: projectile_def must hit ALLIES")
	if count < 1:
		errors.append("Radial burst: count must be >= 1")
	if damage < 0.0:
		errors.append("Radial burst: damage must be >= 0")
	return errors


func _begin() -> bool:
	var origin := context.actor.global_position
	for i in count:
		var direction := Vector2.from_angle(TAU * float(i) / float(count))
		var projectile := projectile_scene.instantiate() as Projectile
		context.projectile_layer.add_child(projectile)
		projectile.global_position = origin
		projectile.launch(projectile_def, owner_asc, damage, direction)
	_complete()
	return true
