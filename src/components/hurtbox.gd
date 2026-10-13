class_name Hurtbox
extends Area2D
## Hit-receiving area. Knows which ASC and actor it protects; holds no behaviour.
## Configure in the scene: collision_layer = the owner's hurt layer, collision_mask = 0, monitoring = false.

var asc: AbilitySystemComponent
var actor: Node2D


func bind(p_asc: AbilitySystemComponent, p_actor: Node2D) -> void:
	asc = p_asc
	actor = p_actor
