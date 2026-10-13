class_name VisualDef
extends Resource
## The set of animations an actor can play. sprite_frames() builds and memoises the SpriteFrames.

@export var strips: Array[AnimationStrip] = []
@export var offset: Vector2 = Vector2.ZERO

var _frames: SpriteFrames


func find_strip(animation: StringName) -> AnimationStrip:
	for strip in strips:
		if strip != null and strip.animation == animation:
			return strip
	return null


## Built on first use and shared by every actor that plays this def. Call validate() first.
func sprite_frames() -> SpriteFrames:
	if _frames == null:
		_frames = _build_frames()
	return _frames


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if find_strip(AnimNames.IDLE) == null:
		errors.append("VisualDef: missing required '%s' strip" % AnimNames.IDLE)
	var seen := {}
	var reported := {}
	for strip in strips:
		if strip == null:
			errors.append("VisualDef: null strip")
			continue
		errors.append_array(strip.validate())
		if seen.has(strip.animation) and not reported.has(strip.animation):
			reported[strip.animation] = true
			errors.append("VisualDef: duplicate strip for animation '%s'" % strip.animation)
		seen[strip.animation] = true
	return errors


func _build_frames() -> SpriteFrames:
	var frames := SpriteFrames.new()
	frames.remove_animation(&"default")
	for strip in strips:
		frames.add_animation(strip.animation)
		frames.set_animation_loop(strip.animation, strip.loop)
		frames.set_animation_speed(strip.animation, strip.fps)
		var frame_width := strip.texture.get_width() / float(strip.frame_count)
		var frame_height := float(strip.texture.get_height())
		for i in strip.frame_count:
			var frame := AtlasTexture.new()
			frame.atlas = strip.texture
			frame.region = Rect2(i * frame_width, 0.0, frame_width, frame_height)
			frames.add_frame(strip.animation, frame)
	return frames
