class_name AnimationStrip
extends Resource
## One animation stored as a horizontal strip of equally sized frames.

@export var animation: StringName = AnimNames.IDLE
@export var texture: Texture2D
@export_range(1, 64) var frame_count: int = 1
@export var fps: float = 10.0
@export var loop: bool = true


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if texture == null:
		errors.append("AnimationStrip '%s': texture is null" % animation)
	elif texture.get_width() % frame_count != 0:
		errors.append("AnimationStrip '%s': width %d is not divisible by frame_count %d" % [animation, texture.get_width(), frame_count])
	return errors
