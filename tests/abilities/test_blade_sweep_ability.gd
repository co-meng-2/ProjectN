extends TestCase

const SWEEP_SCENE: PackedScene = preload("res://src/abilities/blade_sweep/blade_sweep_ability.tscn")


func _sweep(player: Player, level: float) -> BladeSweepAbility:
	var ability := SWEEP_SCENE.instantiate() as BladeSweepAbility
	ability.ability_level = level
	player.grant_ability(ability)
	ability.set_physics_process(false)  # tests drive step() by hand
	return ability


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _strikes(layer: Node) -> Array[MeleeStrike]:
	var found: Array[MeleeStrike] = []
	for child in layer.get_children():
		if child is MeleeStrike:
			found.append(child)
	return found


func test_sweeps_only_when_an_enemy_is_within_the_level_radius() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var sweep := _sweep(player, 1.0)  # radius 40
	var enemy := _enemy_at(Vector2(60, 0))
	sweep.step(0.1)
	assert_true(_strikes(layer).is_empty(), "60 px is outside a 40 px sweep")
	enemy.global_position = Vector2(30, 0)
	sweep.step(0.1)
	var strikes := _strikes(layer)
	assert_eq(strikes.size(), 1)
	assert_almost_eq((strikes[0].get_node("Hitbox/CollisionShape2D").shape as CircleShape2D).radius, 40.0)
	assert_almost_eq(strikes[0].global_position.x, player.global_position.x, 0.001, "centred on the player")


func test_cooldown_follows_the_curve() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var sweep := _sweep(player, 2.0)  # cooldown 1.4 s
	_enemy_at(Vector2(30, 0))
	sweep.step(0.1)
	assert_eq(_strikes(layer).size(), 1)
	sweep.step(1.0)
	assert_eq(_strikes(layer).size(), 1, "still cooling down")
	sweep.step(0.5)
	assert_eq(_strikes(layer).size(), 2)


func test_damage_and_radius_scale_with_level() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var sweep := _sweep(player, 6.0)  # radius 95, damage 45
	var victim := ActorFixture.hurt_target(self, Vector2(80, 0))
	sweep.step(0.1)
	assert_eq(_strikes(layer).size(), 1, "80 px is inside the level-6 radius")
	await next_physics_frame()
	await next_physics_frame()
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 55.0)


func test_validate_reports_missing_data() -> void:
	var sweep := SWEEP_SCENE.instantiate() as BladeSweepAbility
	assert_no_errors(sweep.validate(), "scene as shipped")
	sweep.radius = null
	sweep.hit_effect = null
	assert_eq(sweep.validate().size(), 2)
	sweep.free()
