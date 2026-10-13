class_name EnemyFactory
extends Node
## Creates enemies from defs. The single place to add pooling later.

signal spawned(enemy: Enemy)

var _layer: Node2D
var _targets: TargetProvider
var _rng: RunRng
var _alive: Array[Enemy] = []


func bind(layer: Node2D, targets: TargetProvider, rng: RunRng) -> void:
	_layer = layer
	_targets = targets
	_rng = rng


func spawn(def: EnemyDef, position: Vector2) -> Enemy:
	var enemy := def.scene.instantiate() as Enemy
	_layer.add_child(enemy)
	enemy.global_position = position
	enemy.setup(def, _targets.target_for(def.target_policy), _rng)
	_alive.append(enemy)
	enemy.died.connect(_forget)
	enemy.tree_exited.connect(_forget.bind(enemy))
	spawned.emit(enemy)
	return enemy


## Enemies this factory created that are still alive (dying enemies no longer count toward spawn caps).
func alive_count() -> int:
	return _alive.size()


func _forget(enemy: Enemy) -> void:
	_alive.erase(enemy)
