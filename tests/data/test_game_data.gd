extends TestCase

const DATA_FILES: PackedStringArray = [
	"res://data/characters/soldier_visual.tres",
	"res://data/characters/default_profile.tres",
	"res://data/drops/orc_drops.tres",
	"res://data/enemies/orc_visual.tres",
	"res://data/enemies/orc.tres",
	"res://data/enemies/orc_swarmer.tres",
	"res://data/enemies/elite_orc.tres",
	"res://data/waves/stage_01_waves.tres",
	"res://data/stages/stage_01.tres",
	"res://data/grade_table.tres",
	"res://data/projectiles/arrow.tres",
	"res://data/effects/damage_basic.tres",
	"res://data/effects/cooldown_dash.tres",
	"res://data/effects/cooldown_arrow.tres",
	"res://data/abilities/prospector.tres",
	"res://data/abilities/swift_boots.tres",
	"res://data/abilities/vitality.tres",
	"res://data/abilities/might.tres",
	"res://data/abilities/volley.tres",
	"res://data/abilities/blade_sweep.tres",
	"res://data/abilities/bounty.tres",
]


func test_every_data_file_loads() -> void:
	for path in DATA_FILES:
		assert_not_null(load(path), path)


func test_stage_01_validates() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_not_null(stage)
	assert_no_errors(stage.validate(), "stage_01")


func test_default_profile_validates() -> void:
	var profile := load("res://data/characters/default_profile.tres") as PlayerProfile
	assert_no_errors(profile.validate(), "default_profile")


func test_arrow_projectile_validates() -> void:
	assert_no_errors((load("res://data/projectiles/arrow.tres") as ProjectileDef).validate(), "arrow")


func test_stage_01_schedules_five_waves_inside_the_stage() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	var times := stage.waves.times()
	assert_eq(times.size(), 5)
	assert_true(times[0] > 0.0 and times[times.size() - 1] < stage.duration)


func test_enemy_defs_use_the_generic_enemy_scene() -> void:
	for path in ["res://data/enemies/orc.tres", "res://data/enemies/orc_swarmer.tres", "res://data/enemies/elite_orc.tres"]:
		var def := load(path) as EnemyDef
		assert_eq(def.scene.resource_path, "res://src/actors/enemy/enemy.tscn", path)
		assert_not_null(def.visual.find_strip(AnimNames.DEATH), "%s has a death strip" % path)


func test_stage_01_starts_with_the_owner_economy() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_eq(stage.start_gold, 200)
	assert_eq(stage.start_stone, 0)
	assert_not_null(stage.grade_table)
	assert_eq(stage.grade_table.promote_price(Grade.Value.NONE).amount, 100)
	assert_eq(stage.grade_table.promote_price(Grade.Value.LEGENDARY).amount, 10)
	assert_eq(stage.grade_table.reroll_price(Grade.Value.COMMON).amount, 2)


func test_default_profile_offers_the_seven_phase_2_abilities() -> void:
	var profile := load("res://data/characters/default_profile.tres") as PlayerProfile
	assert_eq(profile.slot_count, 6)
	var ids: Array = []
	for ability in profile.abilities:
		ids.append(String(ability.id))
	ids.sort()
	assert_eq(ids, ["blade_sweep", "bounty", "might", "prospector", "swift_boots", "vitality", "volley"])
	assert_no_errors(profile.validate(), "default_profile")


func test_stage_01_uses_the_prototype_drop_multiplier() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	assert_almost_eq(stage.drop_multiplier, 10000.0)


func test_elite_orc_matches_the_spec() -> void:
	var elite := load("res://data/enemies/elite_orc.tres") as EnemyDef
	assert_no_errors(elite.validate(), "elite_orc")
	assert_eq(elite.id, &"elite_orc")
	assert_almost_eq(elite.stats.max_health, 300.0)
	assert_almost_eq(elite.stats.move_speed, 70.0)
	assert_almost_eq(elite.visual_scale, 1.8)
	assert_eq(elite.target_policy, EnemyDef.TargetPolicy.PLAYER)
	assert_almost_eq(elite.contact_damage, 15.0)
	assert_true(elite.tint.r > 0.9 and elite.tint.g < 0.5 and elite.tint.b < 0.5, "red tint")
