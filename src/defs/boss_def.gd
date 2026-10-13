class_name BossDef
extends Resource
## The end-of-stage boss: a generic boss actor scene configured like an EnemyDef, plus its attack
## patterns and how the brain picks them (SEQUENCE = list order, RANDOM = weighted, no immediate repeats).

enum Order { SEQUENCE, RANDOM }

@export var id: StringName
@export var display_name: String = ""
@export var scene: PackedScene
@export var visual: VisualDef
@export var visual_scale: float = 1.0
@export var tint: Color = Color.WHITE
@export var stats: StatBlock
@export var contact_effect: GameplayEffect
@export var contact_damage: float = 0.0
@export var contact_interval: float = 1.0
@export var body_radius: float = 10.0
@export var patterns: Array[BossPatternDef] = []
@export var order: Order = Order.RANDOM
## Seconds of chasing between the end of one pattern and the start of the next.
@export var pattern_gap: float = 0.0


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("BossDef: id is empty")
	if display_name.is_empty():
		errors.append("BossDef '%s': display_name is empty" % id)
	if scene == null:
		errors.append("BossDef '%s': scene is null" % id)
	if contact_effect == null:
		errors.append("BossDef '%s': contact_effect is null" % id)
	if body_radius <= 0.0 or contact_damage < 0.0 or contact_interval <= 0.0 or visual_scale <= 0.0:
		errors.append("BossDef '%s': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0" % id)
	if visual == null:
		errors.append("BossDef '%s': visual is null" % id)
	else:
		errors.append_array(visual.validate())
	if stats == null:
		errors.append("BossDef '%s': stats is null" % id)
	else:
		errors.append_array(stats.validate())
	if pattern_gap <= 0.0:
		errors.append("BossDef '%s': pattern_gap must be > 0" % id)
	if patterns.is_empty():
		errors.append("BossDef '%s': needs at least one pattern" % id)
	for pattern in patterns:
		if pattern == null:
			errors.append("BossDef '%s': null pattern" % id)
		else:
			for error in pattern.validate():
				errors.append("BossDef '%s': %s" % [id, error])
	return errors
