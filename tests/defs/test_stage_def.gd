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


func test_stage_requires_a_grade_table_and_a_sane_economy() -> void:
	var def := DefFactory.stage()
	def.grade_table = null
	assert_eq(def.validate().size(), 1, "grade_table required")
	def.grade_table = DefFactory.grade_table()
	def.start_gold = -1
	def.drop_multiplier = 0.0
	assert_eq(def.validate().size(), 2, "negative start gold, zero multiplier")


func test_stage_collects_grade_table_errors() -> void:
	var def := DefFactory.stage()
	def.grade_table.promote_gold = PackedInt32Array([1])
	assert_eq(def.validate().size(), 1)


func test_stage_validates_its_event_schedule_against_duration_and_waves() -> void:
	var def := DefFactory.stage(60.0)
	var entry := WaveEntry.new()
	entry.time = 30.0
	entry.count = 3
	entry.enemy = DefFactory.enemy()
	def.waves.entries.append(entry)
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 10.0)]
	def.events = DefFactory.schedule(pool, 1, 25.0, 35.0, 10.0, 6.0)
	assert_eq(def.validate().size(), 1, "every allowed second collides with the wave at 30 s")
	def.events.wave_clearance = 4.0
	assert_no_errors(def.validate(), "clearance 4 leaves 25 and 35")
	def.events = null
	assert_no_errors(def.validate(), "events are optional")


func test_null_wave_entry_with_events_reports_errors_without_crashing() -> void:
	var def := DefFactory.stage(60.0)
	def.waves.entries.append(null)
	var pool: Array[EventDef] = [DefFactory.event(DefFactory.packed(Node2D.new()), 10.0)]
	def.events = DefFactory.schedule(pool, 1, 25.0, 35.0, 10.0)
	var errors := def.validate()
	assert_eq(errors.size(), 1, ", ".join(errors))
	assert_true(errors[0].contains("null entry"))


func test_stage_validates_its_optional_boss() -> void:
	var def := DefFactory.stage()
	assert_null(def.boss, "no boss by default")
	def.boss = BossDef.new()
	assert_eq(def.validate().size(), 8, "the boss's errors are the stage's errors")
	var patterns: Array[PackedScene] = [preload("res://tests/support/probe_ability.tscn")]
	def.boss = DefFactory.boss(null, patterns)
	assert_no_errors(def.validate(), "valid boss")
