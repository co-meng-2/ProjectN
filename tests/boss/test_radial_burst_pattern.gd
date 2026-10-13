extends TestCase

const BOSS_SCENE: PackedScene = preload("res://src/actors/boss/boss.tscn")
const BURST_SCENE: PackedScene = preload("res://src/boss/patterns/radial_burst_pattern.tscn")
const BOSS_AT: Vector2 = Vector2(40, -30)


func _rig(player_at: Vector2) -> Dictionary:
	var player := ActorFixture.player(self)
	player.global_position = player_at
	var patterns: Array[PackedScene] = [BURST_SCENE]
	var boss := ActorFixture.boss(self, player, DefFactory.boss(BOSS_SCENE, patterns), BOSS_AT)
	(boss.patterns()[0] as BossPattern).set_physics_process(false)
	return {"player": player, "boss": boss, "layer": player.ability_context.projectile_layer}


func test_fires_count_ally_hitting_bolts_evenly_around_the_boss() -> void:
	var rig := _rig(Vector2(600, 600))
	var boss: Boss = rig["boss"]
	assert_true(boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Burst))
	assert_false(boss.is_casting(), "the burst completes at once")
	var bolts := ActorFixture.live_children(rig["layer"], Projectile)
	assert_eq(bolts.size(), 12)
	var angles: Array[float] = []
	for bolt in bolts:
		assert_eq(bolt.global_position, BOSS_AT)
		assert_eq((bolt.get_node("Hitbox") as Area2D).collision_mask, PhysicsLayers.ALLY_HURT)
		angles.append(fposmod(bolt.rotation, TAU))
	angles.sort()
	for i in range(1, angles.size()):
		assert_almost_eq(angles[i] - angles[i - 1], TAU / 12.0, 0.001, "even spacing")


func test_a_bolt_damages_the_player_but_not_the_boss() -> void:
	var rig := _rig(BOSS_AT + Vector2(45, 0))
	var boss: Boss = rig["boss"]
	var player: Player = rig["player"]
	boss.asc.try_activate_abilities_by_tag(GameplayTags.Ability_Boss_Burst)
	await wait_seconds(0.4)
	assert_almost_eq(player.health.current(), 90.0, 0.001, "one bolt flies along +x into the player")
	assert_almost_eq(boss.health.current(), 500.0, 0.001, "bolts ignore the enemy hurt layer")


func test_validate_requires_an_ally_hitting_projectile() -> void:
	var burst := BURST_SCENE.instantiate() as RadialBurstPattern
	assert_no_errors(burst.validate(), "scene as shipped")
	burst.projectile_def = load("res://data/projectiles/arrow.tres")
	assert_eq(burst.validate().size(), 1, "the player's arrow hits enemies")
	burst.count = 0
	assert_eq(burst.validate().size(), 2)
	burst.free()


func test_validate_rejects_a_scene_whose_root_is_not_a_projectile() -> void:
	var burst := BURST_SCENE.instantiate() as RadialBurstPattern
	burst.projectile_scene = DefFactory.packed(Node2D.new())
	assert_eq(burst.validate().size(), 1)
	burst.free()
