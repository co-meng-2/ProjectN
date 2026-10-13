extends TestCase


func test_valid_visual_has_no_errors() -> void:
	assert_no_errors(DefFactory.visual().validate(), "factory visual")


func test_missing_idle_strip_is_an_error() -> void:
	var def := DefFactory.visual()
	def.strips[0].animation = AnimNames.WALK
	assert_eq(def.validate().size(), 1)


func test_strip_width_must_divide_by_frame_count() -> void:
	var def := DefFactory.visual()
	def.strips[0].frame_count = 7
	assert_eq(def.validate().size(), 1)


func test_zero_frame_count_is_an_error_without_crashing() -> void:
	var def := DefFactory.visual()
	def.strips[0].frame_count = 0
	assert_eq(def.validate().size(), 1)


func test_non_positive_fps_is_an_error() -> void:
	var def := DefFactory.visual()
	def.strips[0].fps = 0.0
	assert_eq(def.validate().size(), 1)


func test_duplicate_animation_names_are_reported_once_per_name() -> void:
	var def := DefFactory.visual()
	def.strips.append(def.strips[0].duplicate())
	def.strips.append(def.strips[0].duplicate())
	assert_eq(def.validate().size(), 1)


func test_null_texture_is_an_error() -> void:
	var def := DefFactory.visual()
	def.strips[0].texture = null
	assert_eq(def.validate().size(), 1)


func test_find_strip_returns_null_when_absent() -> void:
	assert_null(DefFactory.visual().find_strip(AnimNames.DEATH))


func test_sprite_frames_are_built_from_strips_and_shared() -> void:
	var def := DefFactory.visual()
	var frames := def.sprite_frames()
	assert_eq(frames.get_frame_count(AnimNames.IDLE), 6)
	assert_false(frames.has_animation(&"default"))
	assert_true(def.sprite_frames() == frames, "memoised: one SpriteFrames per def")
