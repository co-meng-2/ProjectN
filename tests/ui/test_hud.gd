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
	hud.bind(_health_for(player_actor), _health_for(building_actor), wallet, clock, Timeline.new(100.0, PackedFloat32Array([30.0])), PackedFloat32Array([45.0, 80.0]))
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
	var label := hud.get_node("%WalletLabel") as Label
	assert_eq(label.text, "Gold 0   Stone 0   Ticket 0")
	wallet.add(Wallet.Kind.GOLD, 7)
	wallet.add(Wallet.Kind.STONE, 2)
	wallet.add(Wallet.Kind.TICKET, 1)
	assert_eq(label.text, "Gold 7   Stone 2   Ticket 1")


func test_show_result_displays_outcome_and_totals() -> void:
	var hud: Hud = _rig()["hud"]
	var result := RunResult.new()
	result.outcome = RunResult.Outcome.CLEARED
	result.kills = 3
	result.gem = 4
	hud.show_result(result)
	var label := hud.get_node("%ResultLabel") as Label
	assert_true(label.visible)
	assert_true(label.text.contains("STAGE CLEARED"))
	assert_true(label.text.contains("Kills 3"))
	assert_true(label.text.contains("Gem 4"))
	assert_true(label.text.contains("Enter: continue"))


func test_hud_hosts_an_unbound_ability_bar() -> void:
	var hud: Hud = _rig()["hud"]
	assert_not_null(hud.ability_bar)
	assert_eq(hud.ability_bar.get_child_count(), 0, "empty until Main binds it")


const PROBE: PackedScene = preload("res://tests/support/probe_event.tscn")


func test_bind_passes_the_event_times_to_the_timeline() -> void:
	var hud: Hud = _rig()["hud"]
	assert_eq((hud.get_node("%Timeline") as TimelineBar).event_marks(), PackedFloat32Array([45.0, 80.0]))


func test_event_signals_drive_the_banner_and_the_arrows() -> void:
	var hud: Hud = _rig()["hud"]
	var banner := hud.get_node("%EventBanner") as EventBanner
	var arrows := hud.get_node("%ObjectiveArrows") as ObjectiveArrows
	arrows.set_process(false)
	var def := DefFactory.event(PROBE, 20.0, &"meteors")
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	var far := adopt(Node2D.new()) as Node2D
	far.global_position = Vector2(5000, 0)
	event.objective_nodes.append(far)
	event.begin(ActorFixture.event_context(self, def))
	hud.show_event(def, event)
	assert_true(banner.visible)
	assert_eq(banner.title_text(), "Meteors")
	hud.show_event_progress(def, "Hits 1/3")
	assert_eq(banner.progress_text(), "Hits 1/3")
	arrows.refresh(Transform2D.IDENTITY, Vector2(800, 600))
	assert_eq(arrows.arrow_positions().size(), 1, "the HUD tracks the running event's objectives")
	hud.show_event_result(def, true)
	assert_true(banner.title_text().contains("Success"))
	arrows.refresh(Transform2D.IDENTITY, Vector2(800, 600))
	assert_eq(arrows.arrow_positions().size(), 0, "no arrows once the event ended")


func test_aborted_event_shows_interrupted_and_clears_the_arrows() -> void:
	var hud: Hud = _rig()["hud"]
	var banner := hud.get_node("%EventBanner") as EventBanner
	var def := DefFactory.event(PROBE, 25.0, &"meteors")
	var event := adopt(PROBE.instantiate()) as ProbeEvent
	event.set_physics_process(false)
	event.begin(ActorFixture.event_context(self, def))
	hud.show_event(def, event)
	hud.show_event_aborted(def)
	assert_eq(banner.title_text(), "Meteors: Interrupted")


func test_boss_appearance_shows_the_bar_and_the_timeline_label() -> void:
	var rig := _rig()
	var hud: Hud = rig["hud"]
	var boss_bar := hud.get_node("%BossBar") as BossBar
	assert_false(boss_bar.visible)
	var stats := StatBlock.new()
	stats.max_health = 2500.0
	hud.show_boss(_health_for(ActorFixture.bare_actor(self, stats)), "Orc Warlord")
	assert_true(boss_bar.visible)
	assert_eq(boss_bar.boss_name(), "Orc Warlord")
	assert_true((hud.get_node("%Timeline") as TimelineBar).is_showing_boss())
