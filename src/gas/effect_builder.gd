class_name EffectBuilder
extends RefCounted
## The single place that assembles damage specs, so every hit carries the same context.


## `instigator` is the actor root owning an AbilitySystemComponent child; `causer` is whatever
## physically dealt the hit (projectile, the actor itself for melee).
static func damage(instigator: Node, causer: Node, effect: GameplayEffect, amount: float, level: float = 1.0) -> GameplayEffectSpec:
	var context := GameplayEffectContext.new(instigator, causer)
	var spec := GameplayEffectSpec.new(effect, context, level)
	spec.set_set_by_caller_magnitude(GameplayTags.Damage_Amount, amount)
	return spec
