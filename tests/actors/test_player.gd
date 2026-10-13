extends TestCase

const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")
const PROBE_SCENE: PackedScene = preload("res://tests/support/probe_ability.tscn")


func _layer(player: Player) -> Node2D:
	return player.ability_context.projectile_layer as Node2D


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
	var profile := DefFactory.profile()
	profile.stats.max_health = 77.0
	profile.stats.move_speed = 140.0
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(profile, adopt(TargetProvider.new()) as TargetProvider, adopt(Node2D.new()), Wallet.new())
	assert_almost_eq(player.health.current(), 77.0)
	assert_almost_eq(_asc(player).get_attribute("move_speed").current_value, 140.0)
	assert_true(player.is_in_group(Groups.PLAYER))
	assert_almost_eq(player.gain_multiplier(Wallet.Kind.GOLD), 1.0)


func test_every_actor_ability_on_the_asc_receives_the_context() -> void:
	var player := ActorFixture.player(self)
	for ability_name in ["ArrowAbility", "DashAbility"]:
		var ability := _asc(player).get_node(ability_name) as ActorAbility
		assert_not_null(ability.context, ability_name)
		assert_eq(ability.context.actor, player, ability_name)


func test_arrow_without_target_does_not_start_cooldown() -> void:
	var player := ActorFixture.player(self)
	var arrow := _manual_arrow(player)
	for i in 3:
		var fired: bool = await arrow.try_activate()
		assert_false(fired)
	assert_false(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	assert_eq(_layer(player).get_child_count(), 0)


func test_arrow_fires_at_the_nearest_enemy_then_cools_down() -> void:
	var player := ActorFixture.player(self)
	_enemy_at(Vector2(400, 0))
	_enemy_at(Vector2(120, 0))
	var arrow := _manual_arrow(player)
	var facings: Array = []
	arrow.animation_requested.connect(func(animation: StringName, facing: Vector2) -> void: facings.append([animation, facing]))
	var first: bool = await arrow.try_activate()
	assert_true(first)
	assert_eq(_layer(player).get_child_count(), 1, "one projectile spawned")
	assert_eq(facings[0][0], AnimNames.ATTACK)
	assert_almost_eq((facings[0][1] as Vector2).x, 1.0, 0.001)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))
	var second: bool = await arrow.try_activate()
	assert_false(second, "blocked by cooldown")


func test_arrow_ignores_enemies_beyond_its_range() -> void:
	var player := ActorFixture.player(self)
	_enemy_at(Vector2(900, 0))
	var arrow := _manual_arrow(player)
	var fired: bool = await arrow.try_activate()
	assert_false(fired)


func test_arrow_auto_fires_when_an_enemy_comes_into_range() -> void:
	var player := ActorFixture.player(self, Wallet.new(), false)
	await wait_seconds(0.2)
	assert_eq(_layer(player).get_child_count(), 0, "nothing to shoot yet")
	_enemy_at(Vector2(100, 0))
	await wait_seconds(0.3)
	assert_true(_layer(player).get_child_count() >= 1)
	assert_true(_asc(player).has_tag(GameplayTags.Cooldown_Arrow))


func test_dash_grants_invulnerability_only_while_active() -> void:
	var player := ActorFixture.player(self)
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
	var player := ActorFixture.player(self)
	var asc := _asc(player)
	assert_true(asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash))
	await next_frame()
	assert_true(asc.has_tag(GameplayTags.Cooldown_Dash))
	await wait_seconds(0.4)
	assert_false(asc.try_activate_abilities_by_tag(GameplayTags.Ability_Dash), "on cooldown")


func test_lethal_damage_emits_died_and_stops_the_abilities() -> void:
	var player := ActorFixture.player(self, Wallet.new(), false)
	var deaths: Array = []
	player.died.connect(func() -> void: deaths.append(true))
	_hit_player(player, 9999.0)
	assert_eq(deaths.size(), 1)
	_enemy_at(Vector2(100, 0))
	await wait_seconds(0.3)
	assert_eq(_layer(player).get_child_count(), 0, "a dead player no longer shoots")


func test_context_carries_the_wallet_and_is_shared_by_every_ability() -> void:
	var wallet := Wallet.new()
	var player := ActorFixture.player(self, wallet)
	assert_true(player.ability_context.wallet == wallet, "the run's wallet")
	assert_eq(player.ability_context.actor, player)
	var arrow := player.asc.get_node("ArrowAbility") as ArrowAbility
	assert_true(arrow.context == player.ability_context, "same context object")


func test_grant_ability_registers_it_with_the_asc_and_binds_the_context() -> void:
	var player := ActorFixture.player(self)
	var granted: Array = []
	player.asc.ability_granted.connect(func(ability: GameplayAbility) -> void: granted.append(ability))
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	assert_eq(probe.owner_asc, player.asc)
	assert_eq(probe.get_parent(), player.asc)
	assert_true(probe.context == player.ability_context)
	assert_eq(granted, [probe], "granted exactly once")
	assert_true(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"))
	await next_frame()
	assert_eq(probe.activations, 1)


func test_granting_the_same_ability_twice_is_ignored() -> void:
	var player := ActorFixture.player(self)
	var granted: Array = []
	player.asc.ability_granted.connect(func(ability: GameplayAbility) -> void: granted.append(ability))
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	player.grant_ability(probe)
	assert_eq(granted.size(), 1)
	assert_eq(probe.get_parent(), player.asc)


func test_revoke_ability_untracks_and_frees_it() -> void:
	var player := ActorFixture.player(self)
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	player.revoke_ability(probe)
	assert_null(player.asc.get_node_or_null("ProbeAbility"), "gone from the ASC immediately")
	assert_false(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"), "no longer granted")
	await next_frame()
	assert_false(is_instance_valid(probe), "freed")


func test_revoke_mid_activation_strips_its_owned_tags() -> void:
	var player := ActorFixture.player(self)
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	probe.hold_seconds = 1.0
	probe.activation_owned_tags.append(GameplayTags.State_Invulnerable)
	player.grant_ability(probe)
	assert_true(player.asc.try_activate_abilities_by_tag(&"Ability.Probe"))
	await next_frame()
	assert_true(player.asc.has_tag(GameplayTags.State_Invulnerable), "active and holding the tag")
	player.revoke_ability(probe)
	assert_false(player.asc.has_tag(GameplayTags.State_Invulnerable), "aborted before removal")
	await wait_seconds(0.1)
	assert_false(is_instance_valid(probe))


func test_revoking_an_unknown_ability_is_a_no_op() -> void:
	var player := ActorFixture.player(self)
	var stranger := adopt(PROBE_SCENE.instantiate()) as ProbeAbility
	player.revoke_ability(stranger)
	await next_frame()
	assert_true(is_instance_valid(stranger))
	assert_eq(stranger.get_parent(), host)


func test_abilities_granted_after_death_do_not_process() -> void:
	var player := ActorFixture.player(self)
	_hit_player(player, 9999.0)
	var probe := PROBE_SCENE.instantiate() as ProbeAbility
	player.grant_ability(probe)
	assert_false(probe.is_physics_processing())
	await next_physics_frame()
	await next_physics_frame()
	assert_eq(probe.physics_ticks, 0, "never ticked")
