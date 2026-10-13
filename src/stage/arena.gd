class_name Arena
extends Node2D
## The playfield: a tiled ground and four walls around it.

const WALL_THICKNESS: float = 200.0

var bounds: Rect2

@onready var _ground: Sprite2D = $Ground
@onready var _walls: StaticBody2D = $Walls


## Call after the arena has entered the tree.
func build(size: Vector2, ground_texture: Texture2D) -> void:
	bounds = Rect2(-size / 2.0, size)
	_ground.texture = ground_texture
	_ground.texture_repeat = CanvasItem.TEXTURE_REPEAT_ENABLED
	_ground.region_enabled = true
	_ground.region_rect = Rect2(Vector2.ZERO, size)
	for child in _walls.get_children():
		_walls.remove_child(child)
		child.free()
	for rect in _wall_rects():
		var shape := CollisionShape2D.new()
		var box := RectangleShape2D.new()
		box.size = rect.size
		shape.shape = box
		shape.position = rect.get_center()
		_walls.add_child(shape)


func _wall_rects() -> Array[Rect2]:
	var t := WALL_THICKNESS
	var width := bounds.size.x + 2.0 * t
	return [
		Rect2(bounds.position.x - t, bounds.position.y - t, width, t),
		Rect2(bounds.position.x - t, bounds.end.y, width, t),
		Rect2(bounds.position.x - t, bounds.position.y, t, bounds.size.y),
		Rect2(bounds.end.x, bounds.position.y, t, bounds.size.y),
	]
