extends TestCase


func test_factory_stage_is_valid_even_without_trickle_enemies() -> void:
	assert_no_errors(DefFactory.stage().validate(), "empty trickle list is allowed")


func test_stage_collects_errors_from_nested_defs() -> void:
	var def := DefFactory.stage()
	def.building_stats.max_health = 0.0
	var broken_enemy := DefFactory.enemy()
	broken_enemy.stats = null
	def.trickle_enemies.append(broken_enemy)
	assert_true(def.validate().size() >= 2)


func test_stage_requires_positive_duration_and_textures() -> void:
	var def := DefFactory.stage(0.0)
	def.ground_texture = null
	assert_eq(def.validate().size(), 2)


func test_stage_validates_waves_against_its_own_duration() -> void:
	var def := DefFactory.stage(60.0)
	var entry := WaveEntry.new()
	entry.time = 90.0
	entry.count = 3
	entry.enemy = DefFactory.enemy()
	def.waves.entries.append(entry)
	assert_eq(def.validate().size(), 1)
