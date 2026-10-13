extends TestCase

const ENEMY_SCENE: PackedScene = preload("res://src/actors/enemy/enemy.tscn")
const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")
const DAMAGE: GameplayEffect = preload("res://data/effects/damage_basic.tres")


func _rig() -> Dictionary:
	var projectile_layer := adopt(Node2D.new()) as Node2D
	var targets := adopt(TargetProvider.new()) as TargetProvider
	var player := adopt(PLAYER_SCENE.instantiate()) as Player
	var profile := PlayerProfile.new()
	profile.stats = StatBlock.new()
	profile.visual = DefFactory.visual()
	player.setup(profile, targets, projectile_layer)
	targets.player = player
	targets.building = player
	var enemy_layer := adopt(Node2D.new()) as Node2D
	var pickup_layer := adopt(Node2D.new()) as Node2D
	var factory := adopt(EnemyFactory.new()) as EnemyFactory
	factory.bind(enemy_layer, targets, RunRng.new(6))
	var wallet := Wallet.new()
	var loot := adopt(LootDirector.new()) as LootDirector
	loot.bind(factory, pickup_layer, player, wallet)
	return {"player": player, "factory": factory, "pickups": pickup_layer, "wallet": wallet}


func test_collected_loot_is_credited_with_the_gain_multiplier() -> void:
	var rig := _rig()
	var player: Player = rig["player"]
	var wallet: Wallet = rig["wallet"]
	var overrides: Dictionary[String, float] = {"gold_gain": 2.0}
	(player.get_node("AbilitySystemComponent") as AbilitySystemComponent).initialize_attribute_overrides(overrides)
	player.loot_collected.emit(Wallet.Kind.GOLD, 5)
	player.loot_collected.emit(Wallet.Kind.STONE, 3)
	assert_eq(wallet.gold, 10)
	assert_eq(wallet.stone, 3)


func test_enemy_drops_become_pickups_on_the_pickup_layer() -> void:
	var rig := _rig()
	var factory: EnemyFactory = rig["factory"]
	var pickups: Node2D = rig["pickups"]
	var entry := DropEntry.new()
	entry.chance = 1.0
	entry.icon = DefFactory.texture(8, 8)
	var def := DefFactory.enemy(ENEMY_SCENE)
	def.contact_effect = DAMAGE
	def.drops = DropTable.new()
	def.drops.entries.append(entry)
	var enemy := factory.spawn(def, Vector2(50, 50))
	var killer := ActorFixture.bare_actor(self, StatBlock.new())
	var spec := EffectBuilder.damage(killer, killer, DAMAGE, 9999.0)
	ActorFixture.asc_of(killer).apply_effect_spec_to_target(spec, enemy.get_node("AbilitySystemComponent"))
	assert_eq(pickups.get_child_count(), 0, "spawned deferred, not inside the physics callback")
	await next_frame()
	await next_frame()
	assert_eq(pickups.get_child_count(), 1)
	assert_true(pickups.get_child(0) is Pickup)
	assert_true((pickups.get_child(0) as Pickup).global_position.distance_to(Vector2(50, 50)) <= 12.0)
