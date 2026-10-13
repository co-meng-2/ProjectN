extends TestCase

const VOLLEY_SCENE: PackedScene = preload("res://src/abilities/volley/volley_ability.tscn")


func _volley(player: Player, level: float) -> VolleyAbility:
	var ability := VOLLEY_SCENE.instantiate() as VolleyAbility
	ability.ability_level = level
	player.grant_ability(ability)
	ability.set_physics_process(false)  # tests drive step() by hand
	return ability


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _projectiles(layer: Node) -> Array[Projectile]:
	var found: Array[Projectile] = []
	for child in layer.get_children():
		if child is Projectile:
			found.append(child)
	return found


func test_fires_one_arrow_at_each_of_the_nearest_enemies_up_to_the_level_count() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var volley := _volley(player, 2.0)  # arrow_count 3
	_enemy_at(Vector2(100, 0))
	_enemy_at(Vector2(-120, 0))
	_enemy_at(Vector2(0, 140))
	_enemy_at(Vector2(0, -160))
	_enemy_at(Vector2(500, 0))
	var animations: Array = []
	volley.animation_requested.connect(func(animation: StringName, _facing: Vector2) -> void: animations.append(animation))
	volley.step(0.1)
	var arrows := _projectiles(layer)
	assert_eq(arrows.size(), 3)
	var headings: Array = []
	for arrow in arrows:
		headings.append(snappedf(rad_to_deg(arrow.rotation), 1.0))
	headings.sort()
	assert_eq(headings, [0.0, 90.0, 180.0], "toward the three nearest (the one at -160 and the far one are skipped)")
	assert_eq(animations, [AnimNames.ATTACK])


func test_respects_the_cooldown_curve() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var volley := _volley(player, 2.0)  # cooldown 2.1 s
	_enemy_at(Vector2(100, 0))
	_enemy_at(Vector2(-120, 0))
	_enemy_at(Vector2(0, 140))
	volley.step(0.1)
	assert_eq(_projectiles(layer).size(), 3)
	volley.step(1.0)
	assert_eq(_projectiles(layer).size(), 3, "still cooling down")
	volley.step(1.2)
	assert_eq(_projectiles(layer).size(), 6, "fired again after 2.3 s")


func test_does_not_fire_or_start_a_cooldown_without_targets() -> void:
	var player := ActorFixture.player(self)
	var layer := player.ability_context.projectile_layer
	var volley := _volley(player, 1.0)
	volley.step(0.1)
	volley.step(0.1)
	assert_true(_projectiles(layer).is_empty())
	_enemy_at(Vector2(100, 0))
	_enemy_at(Vector2(0, 100))
	volley.step(0.0)
	assert_eq(_projectiles(layer).size(), 2, "fires at once: a miss started no cooldown")


func test_level_six_fires_eight_arrows() -> void:
	var player := ActorFixture.player(self)
	var volley := _volley(player, 6.0)
	for i in 9:
		_enemy_at(Vector2.from_angle(i * TAU / 9.0) * 120.0)
	volley.step(0.1)
	assert_eq(_projectiles(player.ability_context.projectile_layer).size(), 8)


func test_arrow_damage_follows_the_damage_curve() -> void:
	var player := ActorFixture.player(self)
	var volley := _volley(player, 3.0)  # damage 13
	var victim := ActorFixture.hurt_target(self, Vector2(60, 0))
	volley.step(0.1)
	await wait_seconds(0.4)
	assert_almost_eq(ActorFixture.asc_of(victim).get_attribute("health").current_value, 87.0)


func test_validate_reports_missing_curves_and_scene_data() -> void:
	var volley := VOLLEY_SCENE.instantiate() as VolleyAbility
	assert_no_errors(volley.validate(), "scene as shipped")
	volley.cooldown = null
	assert_eq(volley.validate().size(), 1)
	volley.arrow_count = null
	volley.projectile_def = null
	assert_eq(volley.validate().size(), 3)
	volley.attack_range = 0.0
	assert_eq(volley.validate().size(), 4)
	volley.free()
