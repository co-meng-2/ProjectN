class_name BladeSweepAbility
extends PeriodicAbility
## A radial melee hit around the owner whenever an enemy is within reach. Radius and damage are curves
## over the grade level; the strike itself is a MeleeStrike spawned on the projectile layer.

@export var strike_scene: PackedScene
@export var hit_effect: GameplayEffect
@export var radius: Curve
@export var damage: Curve
## How long the strike area stays active; it must outlive one physics step so overlaps are reported.
## Set in the scene; no balance numbers in code.
@export var strike_seconds: float = 0.0


func validate() -> PackedStringArray:
	var errors := super.validate()
	if strike_scene == null or hit_effect == null:
		errors.append("Blade Sweep: strike_scene and hit_effect are required")
	if radius == null or damage == null:
		errors.append("Blade Sweep: radius and damage curves are required")
	if strike_seconds <= 0.0:
		errors.append("Blade Sweep: strike_seconds must be > 0")
	return errors


func _activate_ability() -> bool:
	var origin := context.actor.global_position
	var reach := radius.sample(ability_level)
	var target := context.targets.nearest_enemy(origin, reach)
	if target == null:
		return false
	start_cooldown()
	var strike := strike_scene.instantiate() as MeleeStrike
	context.projectile_layer.add_child(strike)
	strike.global_position = origin
	strike.strike(owner_asc, hit_effect, damage.sample(ability_level), reach, strike_seconds)
	animation_requested.emit(AnimNames.ATTACK, (target.global_position - origin).normalized())
	return true
