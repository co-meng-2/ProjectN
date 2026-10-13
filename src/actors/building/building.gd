class_name Building
extends StaticBody2D
## The structure the player defends. Emits `destroyed` when its health reaches zero.

signal destroyed

@onready var asc: AbilitySystemComponent = $AbilitySystemComponent
@onready var health: Health = $Health
@onready var _hurtbox: Hurtbox = $Hurtbox
@onready var _sprite: Sprite2D = $Sprite2D


func _ready() -> void:
	add_to_group(Groups.BUILDING)


## Call after the building has entered the tree.
func setup(texture: Texture2D, stats: StatBlock) -> void:
	_sprite.texture = texture
	asc.initialize_attribute_overrides(stats.to_overrides())
	health.bind(asc)
	_hurtbox.bind(asc, self)
	health.died.connect(destroyed.emit)
