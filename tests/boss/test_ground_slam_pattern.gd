extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const SLAM_SCENE: PackedScene = preload("res://src/boss/patterns/ground_slam_pattern.tscn")
const PLAYER_AT: Vector2 = Vector2(150, -60)


func _rig() -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var patterns: Array[PackedScene] = [SLAM_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), Vector2(-400, 0))
	var slam := boss.patterns()[0] as GroundSlamPattern
	slam.set_physics_process(false)
	return {"player": player, "boss": boss, "slam": slam, "layer": player.ability_context.projectile_layer}


func test_slam_hits_the_player_only_after_the_warning() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var slam: GroundSlamPattern = rig["slam"]
	var building := ActorFixture.hurt_target(self, PLAYER_AT + Vector2(30, 0), PhysicsLayers.ALLY_HURT)
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Slam))
	assert_eq(ActorFixture.live_children(rig["layer"], Telegraph).size(), 1)
	slam.step(0.6)
	await settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "still a warning")
	assert_eq(ActorFixture.live_children(rig["layer"], AreaStrike).size(), 0)
	assert_true(boss.is_casting())
	slam.step(0.5)
	assert_eq(ActorFixture.live_children(rig["layer"], AreaStrike).size(), 1)
	assert_eq(ActorFixture.live_children(rig["layer"], Telegraph).size(), 0)
	assert_false(boss.is_casting(), "the slam completes when it strikes")
	await settle()
	assert_almost_eq(player.health.current(), 70.0)
	assert_almost_eq(ActorFixture.asc_of(building).get_attribute("health").current_value, 100.0, 0.001, "the building is spared")


func test_slam_lands_where_the_player_was() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var slam: GroundSlamPattern = rig["slam"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Slam)
	player.global_position = PLAYER_AT + Vector2(400, 0)
	slam.step(1.1)
	await settle()
	assert_almost_eq(player.health.current(), 100.0, 0.001, "stepped out of the circle")


func test_player_gone_during_the_warning_hits_nobody() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var slam: GroundSlamPattern = rig["slam"]
	var building := ActorFixture.hurt_target(self, PLAYER_AT + Vector2(20, 0), PhysicsLayers.ALLY_HURT)
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Slam)
	(rig["player"] as Player).ability_context.targets.player = null
	slam.step(1.1)
	await settle()
	assert_eq(ActorFixture.live_children(rig["layer"], AreaStrike).size(), 0, "no unfiltered strike")
	assert_false(boss.is_casting())
	assert_almost_eq(ActorFixture.asc_of(building).get_attribute("health").current_value, 100.0, 0.001)


func test_validate_reports_missing_parts_and_bad_numbers() -> void:
	var slam := SLAM_SCENE.instantiate() as GroundSlamPattern
	assert_no_errors(slam.validate(), "scene as shipped")
	slam.hit_effect = null
	slam.radius = 0.0
	assert_eq(slam.validate().size(), 2)
	slam.hit_effect = load("res://data/effects/damage_basic.tres")
	slam.radius = 90.0
	slam.strike_scene = SLAM_SCENE
	assert_eq(slam.validate().size(), 1, "a strike scene whose root is not an AreaStrike is rejected")
	slam.free()
