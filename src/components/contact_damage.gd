class_name ContactDamage
extends Node
## Periodically damages every hurtbox overlapping a hitbox (enemy melee, hazards).

signal attacked(targets: Array[Hurtbox])

var _hitbox: Hitbox
var _applier: EffectApplier
var _effect: GameplayEffect
var _damage: float = 0.0
var _interval: float = 1.0
var _cooldown: float = 0.0


func bind(hitbox: Hitbox, applier: EffectApplier, effect: GameplayEffect, damage: float, interval: float) -> void:
	_hitbox = hitbox
	_applier = applier
	_effect = effect
	_damage = damage
	_interval = interval


func step(delta: float) -> void:
	_cooldown = maxf(_cooldown - delta, 0.0)
	if _cooldown > 0.0 or _hitbox == null:
		return
	var targets := _hitbox.overlapping_hurtboxes()
	if targets.is_empty():
		return
	for target in targets:
		_applier.hit(target, _effect, _damage)
	_cooldown = _interval
	attacked.emit(targets)


func _physics_process(delta: float) -> void:
	step(delta)
