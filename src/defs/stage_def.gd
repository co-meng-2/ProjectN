class_name StageDef
extends Resource
## Static description of one stage.

@export var id: StringName
@export var display_name: String = ""
@export var duration: float = 180.0
@export var arena_size: Vector2 = Vector2(2400, 1600)
@export var ground_texture: Texture2D
@export var building_texture: Texture2D
@export var building_stats: StatBlock
@export var trickle_enemies: Array[EnemyDef] = []
@export var trickle_interval_start: float = 2.0
@export var trickle_interval_end: float = 0.8
@export var trickle_cap: int = 60
@export var spawn_radius: float = 520.0
@export var waves: WaveSchedule


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if duration <= 0.0:
		errors.append("StageDef '%s': duration must be > 0" % id)
	if arena_size.x <= 0.0 or arena_size.y <= 0.0 or spawn_radius <= 0.0:
		errors.append("StageDef '%s': arena_size and spawn_radius must be > 0" % id)
	if trickle_interval_start <= 0.0 or trickle_interval_end <= 0.0 or trickle_cap < 0:
		errors.append("StageDef '%s': trickle intervals must be > 0 and trickle_cap >= 0" % id)
	if ground_texture == null or building_texture == null:
		errors.append("StageDef '%s': ground_texture and building_texture are required" % id)
	if building_stats == null:
		errors.append("StageDef '%s': building_stats is null" % id)
	else:
		errors.append_array(building_stats.validate())
	for enemy in trickle_enemies:
		if enemy == null:
			errors.append("StageDef '%s': null trickle enemy" % id)
		else:
			errors.append_array(enemy.validate())
	if waves != null:
		errors.append_array(waves.validate_against(duration))
	return errors
