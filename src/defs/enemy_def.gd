class_name EnemyDef
extends Resource
## Everything that distinguishes one enemy type; the actor scene itself is generic.

enum TargetPolicy { BUILDING, PLAYER }

@export var id: StringName
@export var scene: PackedScene
@export var visual: VisualDef
@export var visual_scale: float = 1.0
@export var tint: Color = Color.WHITE
@export var stats: StatBlock
@export var contact_effect: GameplayEffect
@export var contact_damage: float = 8.0
@export var contact_interval: float = 0.9
@export var target_policy: TargetPolicy = TargetPolicy.BUILDING
@export var body_radius: float = 10.0
@export var drops: DropTable


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if id == &"":
		errors.append("EnemyDef: id is empty")
	if scene == null:
		errors.append("EnemyDef '%s': scene is null" % id)
	if contact_effect == null:
		errors.append("EnemyDef '%s': contact_effect is null" % id)
	if body_radius <= 0.0 or contact_damage < 0.0 or contact_interval <= 0.0 or visual_scale <= 0.0:
		errors.append("EnemyDef '%s': body_radius, contact_interval and visual_scale must be > 0, contact_damage >= 0" % id)
	if visual == null:
		errors.append("EnemyDef '%s': visual is null" % id)
	else:
		errors.append_array(visual.validate())
	if stats == null:
		errors.append("EnemyDef '%s': stats is null" % id)
	else:
		errors.append_array(stats.validate())
	if drops != null:
		errors.append_array(drops.validate())
	return errors
