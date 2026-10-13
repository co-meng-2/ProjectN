class_name LootDropper
extends Node
## Rolls an actor's DropTable when it dies and asks the world to spawn the pickups.

signal drop_requested(roll: DropRoll, position: Vector2)

const SCATTER_RADIUS: float = 10.0

var _table: DropTable
var _rng: RunRng
var _anchor: Node2D


func bind(health: Health, table: DropTable, rng: RunRng, anchor: Node2D) -> void:
	_table = table
	_rng = rng
	_anchor = anchor
	health.died.connect(_on_died)


func _on_died() -> void:
	if _table == null:
		return
	for roll in _table.roll(_rng):
		drop_requested.emit(roll, _anchor.global_position + _rng.offset_in_circle(SCATTER_RADIUS))
