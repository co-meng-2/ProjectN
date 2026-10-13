class_name TargetProvider
extends Node
## Answers targeting questions: who an enemy should chase, who an ability should shoot.

var building: Node2D
var player: Node2D


func target_for(policy: EnemyDef.TargetPolicy) -> Node2D:
	var preferred := player if policy == EnemyDef.TargetPolicy.PLAYER else building
	var fallback := building if policy == EnemyDef.TargetPolicy.PLAYER else player
	if is_instance_valid(preferred):
		return preferred
	if is_instance_valid(fallback):
		return fallback
	return null


func nearest_enemy(origin: Vector2, max_range: float) -> Node2D:
	var nearest: Node2D = null
	var nearest_distance := max_range * max_range
	for node in get_tree().get_nodes_in_group(Groups.ENEMIES):
		var enemy := node as Node2D
		if enemy == null or enemy.is_queued_for_deletion():
			continue
		var distance := origin.distance_squared_to(enemy.global_position)
		if distance <= nearest_distance:
			nearest = enemy
			nearest_distance = distance
	return nearest
