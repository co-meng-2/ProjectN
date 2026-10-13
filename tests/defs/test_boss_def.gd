extends TestCase

const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")


func _valid() -> BossDef:
	var patterns: Array[PackedScene] = [PROBE_SCENE]
	return DefFactory.boss(null, patterns)


func test_factory_boss_is_valid() -> void:
	assert_no_errors(_valid().validate(), "boss")


func test_empty_boss_def_reports_every_missing_part() -> void:
	var errors := BossDef.new().validate()
	assert_eq(errors.size(), 8, "id, name, scene, contact effect, visual, stats, gap, patterns: %s" % ", ".join(errors))


func test_numbers_must_be_positive() -> void:
	var def := _valid()
	def.pattern_gap = 0.0
	assert_eq(def.validate().size(), 1, "pattern_gap")
	def = _valid()
	def.contact_interval = 0.0
	def.contact_damage = -1.0
	assert_eq(def.validate().size(), 1, "one combined actor-number error")


func test_nested_stats_and_visual_errors_are_reported() -> void:
	var def := _valid()
	def.stats.max_health = 0.0
	def.visual = VisualDef.new()
	assert_eq(def.validate().size(), 2)


func test_pattern_needs_a_gameplay_ability_scene_and_a_positive_weight() -> void:
	var pattern := BossPatternDef.new()
	assert_eq(pattern.validate().size(), 1, "no scene")
	pattern.ability_scene = PROBE_SCENE
	assert_no_errors(pattern.validate(), "probe")
	pattern.weight = 0.0
	assert_eq(pattern.validate().size(), 1, "weight 0")
	pattern.weight = 1.0
	pattern.ability_scene = DefFactory.packed(Node.new())
	assert_eq(pattern.validate().size(), 1, "root is not an ability")


func test_pattern_root_may_reject_itself() -> void:
	var probe := ProbeAbility.new()
	probe.reject_reason = "probe says no"
	var pattern := BossPatternDef.new()
	pattern.ability_scene = DefFactory.packed(probe)
	var errors := pattern.validate()
	assert_eq(errors.size(), 1)
	assert_true(errors[0].contains("probe says no"))


func test_boss_reports_null_and_invalid_patterns() -> void:
	var def := _valid()
	def.patterns.append(null)
	def.patterns.append(BossPatternDef.new())
	assert_eq(def.validate().size(), 2)
