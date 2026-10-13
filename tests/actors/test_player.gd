extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _profile(max_health: float = 100.0, move_speed: float = 140.0) -> PlayerProfile:
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.stats.max_health = max_health
	profile.stats.move_speed = move_speed
	profile.visual = DefFactory.visual()
	return profile


func _player() -> Array:
	var layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(_profile(), targets, layer)
	targets.player = player
	return [player, layer, targets]


func _enemy_at(position: Vector2) -> Node2D:
	var enemy := adopt(Node2D.new()) as Node2D
	enemy.add_to_group(Groups.ENEMIES)
	enemy.global_position = position
	return enemy


func _asc(player: Player) -> AbilitySystemComponent:
	return player.get_node("AbilitySystemComponent") as AbilitySystemComponent


## Manual-activation tests switch auto-fire off so the per-frame retry cannot interfere.
func _manual_arrow(player: Player) -> ArrowAbility:
	var arrow := _asc(player).get_node("ArrowAbility") as ArrowAbility
	arrow.auto_fire = false
	return arrow


func _hit_player(player: Player, amount: float) -> ActiveGameplayEffect:
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, amount)
	return ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, _asc(player))


func test_setup_applies_profile_stats_and_joins_group() -> void:
	var player: Player = _player()[0]
	assert_almost_eq(player.health.current(), 100.0)
	assert_almost_eq(_asc(player).get_attribute("move_speed").current_value, 140.0)
	assert_true(player.is_in_group(Groups.PLAYER))
	assert_almost_eq(player.gain_multiplier(Wallet.Kind.GOLD), 1.0)


func test_every_actor_ability_on_the_asc_receives_the_context() -> void:
	var player: Player = _player()[0]
	for ability_name in ["ArrowAbility", "DashAbility"]:
		var ability := _asc(player).get_node(ability_name) as ActorAbility
		assert_not_null(ability.context, ability_name)
		assert_eq(ability.context.actor, player, ability_name)


func test_arrow_without_target_does_not_start_cooldown() -> void:
	var rig := _player()
	var player: Player = rig[0]
	var arrow := _manual_arrow(player)
	for i in 3:
		var fired: bool = await arrow.try_activate()
		assert_false(fired)
	assert_false(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	assert_eq((rig[1] as Node2D).get_child_count(), 0)


func test_arrow_fires_at_the_nearest_enemy_then_cools_down() -> void:
	var rig := _player()
	var player: Player = rig[0]
	_enemy_at(Vector2(400, 0))
	_enemy_at(Vector2(120, 0))
	var arrow := _manual_arrow(player)
	var facings: Array = []
	arrow.animation_requested.connect(func(animation: StringName, facing: Vector2) -> void: facings.append([animation, facing]))
	var first: bool = await arrow.try_activate()
	assert_true(first)
	assert_eq((rig[1] as Node2D).get_child_count(), 1, "one projectile spawned")
	assert_eq(facings[0][0], AnimNames.ATTACK)
	assert_almost_eq((facings[0][1] as Vector2).x, 1.0, 0.001)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	var second: bool = await arrow.try_activate()
	assert_false(second, "blocked by cooldown")


func test_arrow_ignores_enemies_beyond_its_range() -> void:
	var rig := _player()
	_enemy_at(Vector2(900, 0))
	var arrow := _manual_arrow(rig[0] as Player)
	var fired: bool = await arrow.try_activate()
	assert_false(fired)


func test_arrow_auto_fires_when_an_enemy_comes_into_range() -> void:
	var rig := _player()
	var player: Player = rig[0]
	await wait_seconds(0.2)
	assert_eq((rig[1] as Node2D).get_child_count(), 0, "nothing to shoot yet")
	_enemy_at(Vector2(100, 0))
	await wait_seconds(0.3)
	assert_true((rig[1] as Node2D).get_child_count() >= 1)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))


func test_dash_grants_invulnerability_only_while_active() -> void:
	var player: Player = _player()[0]
	var dash := _asc(player).get_node("DashAbility") as DashAbility
	dash.try_activate()
	await next_frame()
	assert_true(_asc(player).has_tag(GameplayTags.State_Invulnerable))
	assert_null(_hit_player(player, 10.0), "damage refused while dashing")
	assert_almost_eq(player.health.current(), 100.0)
	await wait_seconds(0.5)
	assert_false(_asc(player).has_tag(GameplayTags.State_Invulnerable))
	assert_not_null(_hit_player(player, 10.0))
	assert_almost_eq(player.health.current(), 90.0)


func test_dash_can_be_triggered_by_its_tag_and_then_cools_down() -> void:
	var player: Player = _player()[0]
	var asc := _asc(player)
	assert_true(asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash))
	await next_frame()
	assert_true(asc.has_tag(GameplayTags.Cooldown_Dash))
	await wait_seconds(0.4)
	assert_false(asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash), "on cooldown")


func test_lethal_damage_emits_died_and_stops_the_abilities() -> void:
	var rig := _player()
	var player: Player = rig[0]
	var deaths: Array = []
	player.died.connect(func() -> void: deaths.append(true))
	_hit_player(player, 9999.0)
	assert_eq(deaths.size(), 1)
	_enemy_at(Vector2(100, 0))
	await wait_seconds(0.3)
	assert_eq((rig[1] as Node2D).get_child_count(), 0, "a dead player no longer shoots")
