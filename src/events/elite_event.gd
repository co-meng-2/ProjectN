class_name EliteEvent
extends StageEvent
## "Elite hunt": `count` elite enemies arrive on the spawn ring around the building through the EnemyFactory,
## so kills, Event.Kill and drops work as for any enemy. All dead in time -> success; time up -> failure,
## and the survivors stay in the fight (also on abort: once spawned they are ordinary enemies).

@export var elite_enemy: EnemyDef
@export var count: int = 0
## Keeps the spawn points off the arena walls.
@export var edge_margin: float = 0.0

var _alive: Array[Enemy] = []
var _killed: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if elite_enemy == null:
		errors.append("EliteEvent: elite_enemy is null")
	else:
		errors.append_array(elite_enemy.validate())
	if count < 1:
		errors.append("EliteEvent: count must be >= 1")
	if edge_margin < 0.0:
		errors.append("EliteEvent: edge_margin must be >= 0")
	return errors


func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for elite in _alive:
		if is_instance_valid(elite):
			found.append(elite)
	return found


func _on_begin() -> void:
	var center := context.building.global_position if is_instance_valid(context.building) else Vector2.ZERO
	var base_angle := context.rng.randf() * TAU
	for i in count:
		var angle := base_angle + TAU * float(i) / float(count)
		var point := SpawnGeometry.ring_point(center, context.spawn_radius, angle)
		var elite := context.factory.spawn(elite_enemy, SpawnGeometry.clamp_to_rect(point, context.bounds, edge_margin))
		elite.died.connect(_on_elite_died)
		_alive.append(elite)
	_report()


func _on_elite_died(elite: Enemy) -> void:
	if not is_running():
		return
	_alive.erase(elite)
	_killed += 1
	_report()
	if _killed >= count:
		_succeed()


func _report() -> void:
	report_progress("Elites %d/%d" % [_killed, count])


## The survivors are not removed: they keep fighting as ordinary enemies.
func _cleanup() -> void:
	for elite in _alive:
		if is_instance_valid(elite) and elite.died.is_connected(_on_elite_died):
			elite.died.disconnect(_on_elite_died)
	_alive.clear()
