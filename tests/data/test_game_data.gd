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
	"res://data/projectiles/boss_bolt.tres",
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
	"res://data/events/statues.tres",
	"res://data/events/meteors.tres",
	"res://data/events/elites.tres",
	"res://data/events/stage_01_events.tres",
	"res://data/bosses/orc_warlord.tres",
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


func test_stage_01_schedules_three_events_and_the_orc_warlord() -> void:
	var stage := load("res://data/stages/stage_01.tres") as StageDef
	var events := stage.events
	assert_not_null(events)
	assert_eq(events.count, 3)
	assert_almost_eq(events.window_start, 20.0)
	assert_almost_eq(events.window_end, 155.0)
	assert_almost_eq(events.min_gap, 30.0)
	assert_almost_eq(events.wave_clearance, 5.0)
	var ids: Array = []
	for def in events.pool:
		ids.append(String(def.id))
	ids.sort()
	assert_eq(ids, ["elites", "meteors", "statues"])
	assert_no_errors(EventDirector.validate_defs(events), "event controllers")
	assert_no_errors(BossBrain.validate_def(stage.boss), "boss patterns")
	assert_no_errors(BossDirector.validate_def(stage.boss), "boss director")
	for run_seed in range(1, 11):
		assert_eq(events.roll_times(stage.duration, stage.waves.times(), RunRng.new(run_seed)).size(), 3, "seed %d" % run_seed)
	assert_eq(stage.boss.display_name, "Orc Warlord")


func test_event_limits_and_rewards_match_the_spec() -> void:
	var expected := {
		"statues": [25.0, 150, 2, 1],
		"meteors": [20.0, 100, 3, 1],
		"elites": [25.0, 200, 2, 1],
	}
	for id: String in expected:
		var def := load("res://data/events/%s.tres" % id) as EventDef
		var row: Array = expected[id]
		assert_no_errors(def.validate(), id)
		assert_eq(String(def.id), id)
		assert_almost_eq(def.time_limit, row[0], 0.001, id)
		assert_eq([def.reward.gold, def.reward.stone, def.reward.tickets], [row[1], row[2], row[3]], id)


func test_event_controllers_carry_the_spec_numbers() -> void:
	var statues := (load("res://src/events/statue_event.tscn") as PackedScene).instantiate() as StatueEvent
	assert_eq(statues.statue_count, 4)
	assert_almost_eq(statues.statue_stats.max_health, 60.0)
	assert_almost_eq(statues.min_distance, 250.0)
	statues.free()
	var meteors := (load("res://src/events/meteor_event.tscn") as PackedScene).instantiate() as MeteorEvent
	assert_eq([meteors.interval, meteors.warning, meteors.radius, meteors.damage, meteors.landing_spread], [1.2, 0.9, 60.0, 15.0, 120.0])
	assert_eq(meteors.max_hits, 3)
	meteors.free()
	var elites := (load("res://src/events/elite_event.tscn") as PackedScene).instantiate() as EliteEvent
	assert_eq(elites.count, 3)
	assert_eq(elites.elite_enemy.id, &"elite_orc")
	elites.free()


func test_orc_warlord_matches_the_spec() -> void:
	var boss := load("res://data/bosses/orc_warlord.tres") as BossDef
	assert_no_errors(boss.validate(), "orc_warlord")
	assert_almost_eq(boss.stats.max_health, 2500.0)
	assert_almost_eq(boss.stats.move_speed, 60.0)
	assert_almost_eq(boss.visual_scale, 3.0)
	assert_almost_eq(boss.contact_damage, 20.0)
	assert_almost_eq(boss.pattern_gap, 2.0)
	assert_eq(boss.order, BossDef.Order.RANDOM)
	assert_eq(boss.scene.resource_path, "res://src/actors/boss/boss.tscn")
	assert_true(boss.tint.v < 0.7, "dark tint")
	var tags: Array = []
	for pattern in boss.patterns:
		assert_almost_eq(pattern.weight, 1.0)
		var ability := pattern.ability_scene.instantiate() as BossPattern
		tags.append(String(ability.ability_tag))
		assert_true(ability.activation_owned_tags.has(GameplayTags.State_Casting), String(ability.ability_tag))
		ability.free()
	tags.sort()
	assert_eq(tags, ["Ability.Boss.Burst", "Ability.Boss.Charge", "Ability.Boss.Slam", "Ability.Boss.Summon"])


func test_boss_patterns_carry_the_spec_numbers() -> void:
	var charge := (load("res://src/boss/patterns/charge_pattern.tscn") as PackedScene).instantiate() as ChargePattern
	assert_eq([charge.warning, charge.speed, charge.distance, charge.damage], [0.7, 520.0, 360.0, 25.0])
	charge.free()
	var burst := (load("res://src/boss/patterns/radial_burst_pattern.tscn") as PackedScene).instantiate() as RadialBurstPattern
	assert_eq(burst.count, 12)
	assert_almost_eq(burst.damage, 10.0)
	assert_almost_eq(burst.projectile_def.speed, 220.0)
	assert_almost_eq(burst.projectile_def.lifetime, 2.0)
	assert_eq(burst.projectile_def.hits, ProjectileDef.Team.ALLIES)
	burst.free()
	var summon := (load("res://src/boss/patterns/summon_pattern.tscn") as PackedScene).instantiate() as SummonPattern
	assert_eq(summon.count, 4)
	assert_almost_eq(summon.radius, 80.0)
	assert_eq(summon.enemy.id, &"orc_swarmer")
	summon.free()
	var slam := (load("res://src/boss/patterns/ground_slam_pattern.tscn") as PackedScene).instantiate() as GroundSlamPattern
	assert_eq([slam.warning, slam.radius, slam.damage], [1.0, 90.0, 30.0])
	slam.free()
