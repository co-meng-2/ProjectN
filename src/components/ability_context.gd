class_name AbilityContext
extends RefCounted
## What an actor offers its abilities. The actor builds it once and hands it to every ActorAbility on its ASC.

var actor: Node2D
var mover: Mover
var targets: TargetProvider
var projectile_layer: Node
## The run's currency; on-kill/economy abilities credit it directly.
var wallet: Wallet
## Boss summons: `func(def: EnemyDef, position: Vector2) -> void`, injected by the Stage (it spawns through
## the EnemyFactory so summoned enemies count as kills and drop loot). Empty for the player.
var summon: Callable = Callable()


func _init(p_actor: Node2D, p_mover: Mover, p_targets: TargetProvider, p_projectile_layer: Node, p_wallet: Wallet) -> void:
	actor = p_actor
	mover = p_mover
	targets = p_targets
	projectile_layer = p_projectile_layer
	wallet = p_wallet
