extends TestCase

const HUD_SCENE: PackedScene = preload("res://src/ui/hud/hud.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _health_for(actor: Node2D) -> Health:
	var health := Health.new()
	actor.add_child(health)
	health.bind(ActorFixture.asc_of(actor))
	return health


func _rig() -> Dictionary:
	var stats := StatBlock.new()
	stats.max_health = 50.0
	var player_actor := ActorFixture.bare_actor(self, stats)
	var building_actor := ActorFixture.bare_actor(self, stats)
	var wallet := Wallet.new()
	var clock := adopt(StageClock.new()) as StageClock
	var hud := adopt(HUD_SCENE.instantiate()) as Hud
	hud.bind(_health_for(player_actor), _health_for(building_actor), wallet, clock, Timeline.new(100.0, PackedFloat32Array([30.0])))
	return {"hud": hud, "player_actor": player_actor, "wallet": wallet}


func test_bars_show_initial_health_and_follow_damage() -> void:
	var rig := _rig()
	var hud: Hud = rig["hud"]
	var bar := hud.get_node("%PlayerBar") as ProgressBar
	assert_almost_eq(bar.value, 50.0)
	assert_almost_eq(bar.max_value, 50.0)
	var attacker := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(attacker, attacker, DAMAGE, 20.0)
	ActorFixture.asc_of(attacker).apply_effect_spec_to_target(spec, ActorFixture.asc_of(rig["player_actor"]))
	assert_almost_eq(bar.value, 30.0)


func test_wallet_label_follows_the_wallet() -> void:
	var rig := _rig()
	var hud: Hud = rig["hud"]
	var wallet: Wallet = rig["wallet"]
	wallet.add(Wallet.Kind.GOLD, 7)
	wallet.add(Wallet.Kind.STONE, 2)
	assert_eq((hud.get_node("%WalletLabel") as Label).text, "Gold 7   Stone 2")


func test_show_result_displays_outcome_and_totals() -> void:
	var hud: Hud = _rig()["hud"]
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	result.kills = 3
	hud.show_result(result)
	var label := hud.get_node("%ResultLabel") as Label
	assert_true(label.visible)
	assert_true(label.text.contains("STAGE CLEARED"))
	assert_true(label.text.contains("Kills 3"))


func test_hud_hosts_an_unbound_ability_bar() -> void:
	var hud: Hud = _rig()["hud"]
	assert_not_null(hud.ability_bar)
	assert_eq(hud.ability_bar.get_child_count(), 0, "empty until Main binds it")
