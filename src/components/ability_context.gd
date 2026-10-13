class_name AbilityContext
extends RefCounted
## What an actor offers its abilities. The actor builds it once and hands it to every ActorAbility on its ASC.

var actor: Node2D
var mover: Mover
var targets: TargetProvider
var projectile_layer: Node


func _init(p_actor: Node2D, p_mover: Mover, p_targets: TargetProvider, p_projectile_layer: Node) -> void:
	actor = p_actor
	mover = p_mover
	targets = p_targets
	projectile_layer = p_projectile_layer
