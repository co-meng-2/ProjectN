class_name ActorFixture
extends RefCounted
## Builds a bare actor (Node2D + AbilitySystemComponent child) for GAS-level tests.

const ACTOR_ASC: PackedScene = preload("res://src/gas/actor_asc.tscn")
const PLAYER_SCENE: PackedScene = preload("res://src/actors/player/player.tscn")


## The ASC must be inside the tree before overrides are applied (attribute sets are copied in _ready).
static func bare_actor(test: TestCase, stats: StatBlock = null) -> Node2D:
	var actor := test.adopt(Node2D.new()) as Node2D
	var asc := ACTOR_ASC.instantiate() as AbilitySystemComponent
	asc.name = "AbilitySystemComponent"
	actor.add_child(asc)
	if stats != null:
		asc.initialize_attribute_overrides(stats.to_overrides())
	return actor


static func asc_of(actor: Node) -> AbilitySystemComponent:
	return actor.get_node("AbilitySystemComponent") as AbilitySystemComponent


## A set-up Player (real scene) inside the test tree, with a fresh TargetProvider and projectile layer
## reachable through `player.ability_context.targets` / `.projectile_layer`.
## `silence_arrow` (default true) turns the scene's ArrowAbility `auto_fire` off, so its per-physics-frame
## retry cannot fire projectiles or start the Cooldown.Arrow tag behind a test; pass false to test auto-fire.
static func player(test: TestCase, wallet: Wallet = Wallet.new(), silence_arrow: bool = true) -> Player:
	var layer := test.adopt(Node2D.new()) as Node2D
	var targets := test.adopt(TargetProvider.new()) as TargetProvider
	var player := test.adopt(PLAYER_SCENE.instantiate()) as Player
	player.setup(DefFactory.profile(), targets, layer, wallet)
	targets.player = player
	if silence_arrow:
		(asc_of(player).get_node("ArrowAbility") as ArrowAbility).auto_fire = false
	return player
