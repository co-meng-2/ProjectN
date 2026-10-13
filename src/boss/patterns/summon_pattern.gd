class_name SummonPattern
extends BossPattern
## Summon: `count` enemies of `enemy` evenly on a ring of `radius` around the boss, through the Stage's
## AbilityContext.summon (it spawns with the EnemyFactory, so summoned enemies count as kills and drop loot).

@export var enemy: EnemyDef
@export var count: int = 0
@export var radius: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if enemy == null:
		errors.append("Summon: enemy is null")
	else:
		errors.append_array(enemy.validate())
	if count < 1 or radius <= 0.0:
		errors.append("Summon: count must be >= 1 and radius > 0")
	return errors


func _begin() -> bool:
	if not context.summon.is_valid():
		return false
	var origin := context.actor.global_position
	for i in count:
		context.summon.call(enemy, SpawnGeometry.ring_point(origin, radius, TAU * float(i) / float(count)))
	_complete()
	return true
