extends TestCase

const BOUNTY_SCENE: PackedScene = preload("res://src/abilities/bounty/bounty_ability.tscn")


func _rig(level: float) -> Dictionary:
	var wallet := Wallet.new()
	var player := ActorFixture.player(self, wallet)
	var bounty := BOUNTY_SCENE.instantiate() as BountyAbility
	bounty.ability_level = level
	player.grant_ability(bounty)
	return {"player": player, "wallet": wallet, "bounty": bounty}


func test_pays_gold_for_every_kill_event_at_the_level_amount() -> void:
	var rig := _rig(4.0)  # 5 gold per kill
	var player: Player = rig["player"]
	var wallet: Wallet = rig["wallet"]
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq(wallet.gold, 5)
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, adopt(Node2D.new()))
	assert_eq(wallet.gold, 10, "payload type does not matter")
	assert_eq(wallet.stone, 0)


func test_level_one_and_six_pay_the_curve_ends() -> void:
	var low := _rig(1.0)
	(low["player"] as Player).asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((low["wallet"] as Wallet).gold, 1)
	var high := _rig(6.0)
	(high["player"] as Player).asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((high["wallet"] as Wallet).gold, 12)


func test_ignores_other_events() -> void:
	var rig := _rig(2.0)
	(rig["player"] as Player).asc.send_gameplay_event(GameplayTags.Ability_Dash, null)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_revoked_bounty_stops_paying() -> void:
	var rig := _rig(2.0)
	var player: Player = rig["player"]
	player.revoke_ability(rig["bounty"])
	await next_frame()
	player.asc.send_gameplay_event(GameplayTags.Event_Kill, null)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_without_a_wallet_it_fails_quietly() -> void:
	var rig := _rig(2.0)
	var bounty: BountyAbility = rig["bounty"]
	bounty.bind_context(AbilityContext.new(rig["player"], null, null, null, null))
	var paid: bool = await bounty.try_activate()
	assert_false(paid)
	assert_eq((rig["wallet"] as Wallet).gold, 0)


func test_validate_requires_the_curve() -> void:
	var bounty := BOUNTY_SCENE.instantiate() as BountyAbility
	assert_no_errors(bounty.validate(), "scene as shipped")
	bounty.gold_per_kill = null
	assert_eq(bounty.validate().size(), 1)
	bounty.free()
