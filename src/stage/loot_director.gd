class_name LootDirector
extends Node
## Turns enemy drops into pickups and collected pickups into wallet credit.

const PICKUP_SCENE: PackedScene = preload("res://src/actors/pickup/pickup.tscn")

var _pickup_layer: Node2D
var _player: Player
var _wallet: Wallet
var _drop_multiplier: float = 1.0


func bind(factory: EnemyFactory, pickup_layer: Node2D, player: Player, wallet: Wallet, drop_multiplier: float = 1.0) -> void:
	_pickup_layer = pickup_layer
	_player = player
	_wallet = wallet
	_drop_multiplier = drop_multiplier
	factory.spawned.connect(_on_enemy_spawned)
	player.loot_collected.connect(_on_loot_collected)


func _on_enemy_spawned(enemy: Enemy) -> void:
	enemy.loot_dropped.connect(_on_loot_dropped)


## Deferred: pickups are collision objects and must not be added while physics signals are flushing.
func _on_loot_dropped(roll: DropRoll, position: Vector2) -> void:
	_spawn_pickup.call_deferred(roll, position)


func _spawn_pickup(roll: DropRoll, position: Vector2) -> void:
	# The stage's drop multiplier is applied to the drop itself, so the pickup shows what it pays.
	roll.amount = maxi(1, roundi(roll.amount * _drop_multiplier))
	var pickup := PICKUP_SCENE.instantiate() as Pickup
	_pickup_layer.add_child(pickup)
	pickup.global_position = position
	pickup.setup(roll)


func _on_loot_collected(kind: Wallet.Kind, amount: int) -> void:
	_wallet.add(kind, roundi(amount * _player.gain_multiplier(kind)))
