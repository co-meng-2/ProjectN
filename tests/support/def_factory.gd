class_name DefFactory
extends RefCounted
## Builds minimal valid defs for tests.


static func texture(width: int = 60, height: int = 10) -> Texture2D:
	return ImageTexture.create_from_image(Image.create(width, height, false, Image.FORMAT_RGBA8))


static func visual() -> VisualDef:
	var strip := AnimationStrip.new()
	strip.animation = AnimNames.IDLE
	strip.texture = texture(60, 10)
	strip.frame_count = 6
	var def := VisualDef.new()
	def.strips.append(strip)
	return def


static func enemy(scene: PackedScene = null) -> EnemyDef:
	var def := EnemyDef.new()
	def.id = &"test_enemy"
	def.scene = scene if scene != null else PackedScene.new()
	def.visual = visual()
	def.stats = StatBlock.new()
	def.contact_effect = GameplayEffect.new()
	return def


static func stage(duration: float = 10.0) -> StageDef:
	var def := StageDef.new()
	def.id = &"test_stage"
	def.duration = duration
	def.ground_texture = texture(32, 32)
	def.building_texture = texture(64, 64)
	def.building_stats = StatBlock.new()
	def.waves = WaveSchedule.new()
	return def
