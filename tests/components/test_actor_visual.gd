extends TestCase


func _strip(animation: StringName, frames: int, loop: bool) -> AnimationStrip:
	var strip := AnimationStrip.new()
	strip.animation = animation
	strip.texture = DefFactory.texture(frames * 10, 10)
	strip.frame_count = frames
	strip.loop = loop
	return strip


func _def() -> VisualDef:
	var def := VisualDef.new()
	def.strips.append(_strip(AnimNames.IDLE, 6, true))
	def.strips.append(_strip(AnimNames.WALK, 8, true))
	def.strips.append(_strip(AnimNames.HURT, 4, false))
	def.strips.append(_strip(AnimNames.DEATH, 5, false))
	return def


func _visual() -> ActorVisual:
	var def := _def()
	var visual := adopt(ActorVisual.new()) as ActorVisual
	visual.apply(def, 2.0, Color.RED)
	return visual


func test_apply_builds_frames_from_strips() -> void:
	var visual := _visual()
	assert_eq(visual.sprite_frames.get_frame_count(AnimNames.IDLE), 6)
	assert_eq(visual.sprite_frames.get_frame_count(AnimNames.WALK), 8)
	assert_false(visual.sprite_frames.get_animation_loop(AnimNames.HURT))
	assert_eq(visual.animation, AnimNames.IDLE)
	assert_eq(visual.scale, Vector2(2, 2))
	assert_eq(visual.modulate, Color.RED)


func test_set_moving_switches_between_walk_and_idle() -> void:
	var visual := _visual()
	visual.set_moving(true)
	assert_eq(visual.animation, AnimNames.WALK)
	visual.set_moving(false)
	assert_eq(visual.animation, AnimNames.IDLE)


func test_one_shot_is_not_interrupted_by_locomotion() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.HURT)
	visual.set_moving(true)
	assert_eq(visual.animation, AnimNames.HURT)


func test_missing_one_shot_animation_is_ignored() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.ATTACK)
	assert_eq(visual.animation, AnimNames.IDLE)


func test_face_flips_horizontally_only_with_real_input() -> void:
	var visual := _visual()
	visual.face(-1.0)
	assert_true(visual.flip_h)
	visual.face(0.0)
	assert_true(visual.flip_h, "zero input keeps the facing")
	visual.face(1.0)
	assert_false(visual.flip_h)


func test_apply_again_resets_one_shot_and_moving_state() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.DEATH)
	visual.apply(_def(), 1.0)
	visual.set_moving(true)
	assert_eq(visual.animation, AnimNames.WALK)


func test_death_is_terminal() -> void:
	var visual := _visual()
	visual.play_once(AnimNames.DEATH)
	var tint := visual.modulate
	visual.play_once(AnimNames.HURT)
	visual.flash_hit()
	assert_eq(visual.animation, AnimNames.DEATH)
	assert_eq(visual.modulate, tint)
