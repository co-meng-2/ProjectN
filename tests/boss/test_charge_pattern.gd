extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const CHARGE_SCENE: PackedScene = preload("res://src/boss/patterns/charge_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(-100, 40)
const PLAYER_AT: Vector2 = Vector2(150, 40)


func _rig() -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = PLAYER_AT
	var patterns: Array[PackedScene] = [CHARGE_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT)
	var charge := boss.patterns()[0] as ChargePattern
	charge.set_physics_process(false)
	return {"player": player, "boss": boss, "charge": charge, "layer": player.ability_context.projectile_layer}


func test_warns_along_a_line_then_charges_through_the_player_once() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	var charge: ChargePattern = rig["charge"]
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge))
	assert_true(boss.is_casting())
	var warnings := ActorFixture.live_children(rig["layer"], Telegraph)
	assert_eq(warnings.size(), 1)
	assert_eq(warnings[0].shape, Telegraph.Shape.LINE)
	assert_eq(warnings[0].global_position, BOSS_AT)
	assert_almost_eq(warnings[0].line_offset.x, 360.0, 0.5, "toward the player, full distance")
	assert_almost_eq(warnings[0].line_offset.y, 0.0, 0.5)
	charge.step(0.5)
	await wait_seconds(0.2)
	assert_almost_eq(boss.global_position.x, BOSS_AT.x, 0.5, "no movement during the warning")
	assert_almost_eq(player.health.current(), 100.0)
	charge.step(0.3)
	assert_true(ActorFixture.live_children(rig["layer"], Telegraph).is_empty(), "the warning is gone once the charge starts")
	await wait_seconds(0.9)
	assert_true(boss.global_position.x > 200.0, "dashed about 360 px (now at %s)" % boss.global_position.x)
	assert_almost_eq(player.health.current(), 75.0, 0.001, "25 damage, exactly once")
	charge.step(charge.charge_seconds())
	assert_false(charge.is_active)
	assert_false(boss.is_casting())


func test_abort_during_the_warning_removes_the_telegraph() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	var charge: ChargePattern = rig["charge"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge)
	charge.abort_ability()
	assert_true(ActorFixture.live_children(rig["layer"], Telegraph).is_empty())
	assert_false(boss.is_casting())
	charge.step(1.0)
	await wait_seconds(0.3)
	assert_true(boss.global_position.distance_to(BOSS_AT) < 60.0, "no dash after the abort (only the chase, about 30 px)")


func test_without_a_player_the_charge_does_not_start() -> void:
	var rig := _rig()
	var boss: Boss = rig["boss"]
	(rig["player"] as Player).ability_context.targets.player = null
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Charge)
	assert_false((rig["charge"] as ChargePattern).is_active)
	assert_true(ActorFixture.live_children(rig["layer"], Telegraph).is_empty())


func test_validate_reports_missing_parts_and_bad_numbers() -> void:
	var charge := CHARGE_SCENE.instantiate() as ChargePattern
	assert_no_errors(charge.validate(), "scene as shipped")
	assert_almost_eq(charge.charge_seconds(), 360.0 / 520.0)
	charge.strike_scene = null
	charge.speed = 0.0
	assert_eq(charge.validate().size(), 2)
	charge.strike_scene = CHARGE_SCENE
	assert_eq(charge.validate().size(), 2, "a strike scene whose root is not an AreaStrike is rejected")
	charge.free()
