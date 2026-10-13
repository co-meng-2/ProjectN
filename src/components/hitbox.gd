class_name Hitbox
extends Area2D
## Hit-detecting area. Configure in the scene: collision_layer = 0, collision_mask = the target's hurt layer,
## monitorable = false.

signal hurtbox_entered(hurtbox: Hurtbox)

## When true, each hurtbox is reported once until reset() (projectiles). Contact damage polls instead.
@export var once_per_target: bool = true

var _already_hit: Dictionary[int, bool] = {}


func _ready() -> void:
	area_entered.connect(_on_area_entered)


func reset() -> void:
	_already_hit.clear()


func overlapping_hurtboxes() -> Array[Hurtbox]:
	var result: Array[Hurtbox] = []
	for area in get_overlapping_areas():
		var hurtbox := area as Hurtbox
		if hurtbox != null:
			result.append(hurtbox)
	return result


func _on_area_entered(area: Area2D) -> void:
	var hurtbox := area as Hurtbox
	if hurtbox == null:
		return
	if once_per_target:
		var id := hurtbox.get_instance_id()
		if _already_hit.has(id):
			return
		_already_hit[id] = true
	hurtbox_entered.emit(hurtbox)
