class_name StatueEvent
extends StageEvent
## "Destroy the statues": spawns statues at random arena points at least `min_distance` from the building
## and the player. All destroyed in time -> success; time up -> the rest are removed and the event fails.

@export var statue_scene: PackedScene
@export var statue_count: int = 0
@export var statue_stats: StatBlock
@export var min_distance: float = 0.0
## Minimum distance between two statues of the same event.
@export var statue_spacing: float = 0.0
## Keeps statues off the arena walls.
@export var edge_margin: float = 0.0
## Random points tried per statue before settling for the best one found.
@export var placement_tries: int = 0

var _statues: Array[Statue] = []
var _destroyed: int = 0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if statue_scene == null:
		errors.append("StatueEvent: statue_scene is null")
	else:
		var probe := statue_scene.instantiate()
		if not probe is Statue:
			errors.append("StatueEvent: statue_scene root is not a Statue")
		probe.free()
	if statue_count < 1:
		errors.append("StatueEvent: statue_count must be >= 1")
	if statue_stats == null:
		errors.append("StatueEvent: statue_stats is null")
	else:
		errors.append_array(statue_stats.validate())
	if min_distance < 0.0 or edge_margin < 0.0 or statue_spacing < 0.0:
		errors.append("StatueEvent: min_distance, statue_spacing and edge_margin must be >= 0")
	if placement_tries < 1:
		errors.append("StatueEvent: placement_tries must be >= 1")
	return errors


func objectives() -> Array[Node2D]:
	var found: Array[Node2D] = []
	for statue in _statues:
		if is_instance_valid(statue):
			found.append(statue)
	return found


func _on_begin() -> void:
	for i in statue_count:
		var statue := statue_scene.instantiate() as Statue
		add_child(statue)
		statue.global_position = _pick_point()
		statue.setup(statue_stats)
		statue.destroyed.connect(_on_statue_destroyed)
		_statues.append(statue)
	_report()


func _pick_point() -> Vector2:
	var area := context.bounds.grow(-edge_margin)
	var best := area.get_center()
	var best_margin := -INF
	for attempt in placement_tries:
		var point := Vector2(context.rng.randf_range(area.position.x, area.end.x), context.rng.randf_range(area.position.y, area.end.y))
		var margin := _margin(point)
		if margin >= 0.0:
			return point
		if margin > best_margin:
			best = point
			best_margin = margin
	return best


## How far `point` is inside the placement rules: the smaller of (distance to the nearer of the building
## and the player - min_distance) and (distance to the nearest placed statue - statue_spacing). >= 0 is valid.
func _margin(point: Vector2) -> float:
	var margin := INF
	for anchor in [context.building, context.player]:
		if is_instance_valid(anchor):
			margin = minf(margin, point.distance_to((anchor as Node2D).global_position) - min_distance)
	for statue in _statues:
		if is_instance_valid(statue):
			margin = minf(margin, point.distance_to(statue.global_position) - statue_spacing)
	return margin


func _on_statue_destroyed(statue: Statue) -> void:
	if not is_running():
		return
	_statues.erase(statue)
	_destroyed += 1
	_report()
	if _destroyed >= statue_count:
		_succeed()


func _report() -> void:
	report_progress("Statues %d/%d" % [_destroyed, statue_count])


func _cleanup() -> void:
	for statue in _statues:
		if is_instance_valid(statue):
			statue.remove()
	_statues.clear()
