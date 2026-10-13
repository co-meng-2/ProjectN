class_name Pickup
extends Area2D
## A currency drop. Sits still until a collector attracts it, then flies to the target and is consumed.

signal reached(pickup: Pickup)

const ICON_SIZE: float = 16.0

@export var attract_speed: float = 260.0
@export var reach_distance: float = 8.0

var kind: Wallet.Kind = Wallet.Kind.GOLD
var amount: int = 0
var _target: Node2D

@onready var _sprite: Sprite2D = $Sprite2D


## Call after the pickup has entered the tree.
func setup(roll: DropRoll) -> void:
	kind = roll.kind
	amount = roll.amount
	_sprite.texture = roll.icon
	if roll.icon != null:
		var largest := maxf(roll.icon.get_width(), roll.icon.get_height())
		_sprite.scale = Vector2.ONE * (ICON_SIZE / largest)


func attract_to(target: Node2D) -> void:
	_target = target


func step(delta: float) -> void:
	if not is_instance_valid(_target):
		return
	var offset := _target.global_position - global_position
	var travel := attract_speed * delta
	if offset.length() <= reach_distance + travel:
		_target = null
		reached.emit(self)
		queue_free()
		return
	global_position += offset.normalized() * travel


func _physics_process(delta: float) -> void:
	step(delta)
