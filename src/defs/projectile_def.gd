class_name ProjectileDef
extends Resource
## Data for a straight-flying projectile.

## Which side a projectile damages: the player's arrows hit ENEMIES; boss bolts hit ALLIES (the player
## and the building).
enum Team { ENEMIES, ALLIES }

@export var texture: Texture2D
@export var hit_effect: GameplayEffect
@export var speed: float = 400.0
@export var lifetime: float = 0.7
## Extra enemies the projectile passes through (0 = stops at the first hit).
@export var pierce: int = 0
@export var hit_radius: float = 6.0
@export var hits: Team = Team.ENEMIES


func hit_mask() -> int:
	return PhysicsLayers.ALLY_HURT if hits == Team.ALLIES else PhysicsLayers.ENEMY_HURT


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if texture == null:
		errors.append("ProjectileDef: texture is null")
	if hit_effect == null:
		errors.append("ProjectileDef: hit_effect is null")
	if speed <= 0.0 or lifetime <= 0.0 or hit_radius <= 0.0:
		errors.append("ProjectileDef: speed, lifetime and hit_radius must be > 0")
	return errors
