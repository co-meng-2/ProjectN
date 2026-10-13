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


static func profile() -> PlayerProfile:
	var result := PlayerProfile.new()
	result.stats = StatBlock.new()
	result.visual = visual()
	return result


static func stage(duration: float = 10.0) -> StageDef:
	var def := StageDef.new()
	def.id = &"test_stage"
	def.duration = duration
	def.ground_texture = texture(32, 32)
	def.building_texture = texture(64, 64)
	def.building_stats = StatBlock.new()
	def.waves = WaveSchedule.new()
	def.grade_table = grade_table()
	return def


## A curve with one point per grade level: values[0] at x = 1 ... values[n-1] at x = n.
static func curve(values: PackedFloat32Array) -> Curve:
	var result := Curve.new()
	var lowest := 0.0
	var highest := 0.0
	for value in values:
		lowest = minf(lowest, value)
		highest = maxf(highest, value)
	result.min_domain = 1.0
	result.max_domain = float(maxi(values.size(), 2))
	result.min_value = lowest
	result.max_value = maxf(highest, lowest + 1.0)
	for i in values.size():
		result.add_point(Vector2(float(i + 1), values[i]))
	return result


## An INFINITE effect with one curved modifier (magnitude 1.0: GAS multiplies the curve sample by it).
static func passive_effect(attribute: String, operation: GameplayEffectModifier.Operation, per_level: PackedFloat32Array) -> GameplayEffect:
	var modifier := GameplayEffectModifier.new()
	modifier.attribute_name = attribute
	modifier.operation = operation
	modifier.magnitude = 1.0
	modifier.scaling_curve = curve(per_level)
	var effect := GameplayEffect.new()
	effect.policy = GameplayEffect.DurationPolicy.INFINITE
	effect.modifiers.append(modifier)
	return effect


static func ability(id: StringName, passive: GameplayEffect = null, scene: PackedScene = null, min_grade: Grade.Value = Grade.Value.COMMON) -> AbilityDef:
	var def := AbilityDef.new()
	def.id = id
	def.display_name = String(id).capitalize()
	def.icon = texture(16, 16)
	def.min_grade = min_grade
	def.passive_effect = passive
	def.ability_scene = scene
	return def


## max_health +20/40/70/110/160/240 over the six grade levels.
static func vitality(per_level: PackedFloat32Array = PackedFloat32Array([20.0, 40.0, 70.0, 110.0, 160.0, 240.0])) -> AbilityDef:
	return ability(&"vitality", passive_effect("max_health", GameplayEffectModifier.Operation.ADD, per_level))


static func grade_table() -> GradeTable:
	var table := GradeTable.new()
	table.promote_gold = PackedInt32Array([100, 150, 250, 400, 600])
	table.reveal_stone = 10
	table.reroll_stone = 2
	return table


## Packs `node` into a scene and frees it (the scene keeps its own copy).
static func packed(node: Node) -> PackedScene:
	var scene := PackedScene.new()
	scene.pack(node)
	node.free()
	return scene


static func reward(gold: int = 150, stone: int = 2, tickets: int = 1) -> RewardDef:
	var result := RewardDef.new()
	result.gold = gold
	result.stone = stone
	result.tickets = tickets
	return result


static func event(scene: PackedScene, time_limit: float = 20.0, id: StringName = &"probe") -> EventDef:
	var def := EventDef.new()
	def.id = id
	def.display_name = String(id).capitalize()
	def.controller_scene = scene
	def.time_limit = time_limit
	def.reward = reward()
	return def


static func schedule(pool: Array[EventDef], count: int = 1, window_start: float = 10.0, window_end: float = 50.0, min_gap: float = 20.0, wave_clearance: float = 0.0) -> EventSchedule:
	var result := EventSchedule.new()
	result.pool = pool
	result.count = count
	result.window_start = window_start
	result.window_end = window_end
	result.min_gap = min_gap
	result.wave_clearance = wave_clearance
	return result
