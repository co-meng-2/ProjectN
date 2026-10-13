class_name LootCollector
extends Area2D
## Magnet area. Pickups entering it are attracted to `anchor`; `collected` fires when they arrive.
## Configure in the scene: collision_layer = 0, collision_mask = PICKUP, monitorable = false.

signal collected(kind: Wallet.Kind, amount: int)

var _anchor: Node2D


func bind(anchor: Node2D) -> void:
	_anchor = anchor
	area_entered.connect(_on_area_entered)


func _on_area_entered(area: Area2D) -> void:
	var pickup := area as Pickup
	if pickup == null:
		return
	# A pickup can leave and re-enter the magnet (dash), so connect only once.
	if not pickup.reached.is_connected(_on_pickup_reached):
		pickup.reached.connect(_on_pickup_reached, CONNECT_ONE_SHOT)
	pickup.attract_to(_anchor)


func _on_pickup_reached(pickup: Pickup) -> void:
	collected.emit(pickup.kind, pickup.amount)
