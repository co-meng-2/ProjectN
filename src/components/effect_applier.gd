class_name EffectApplier
extends Node
## Applies damage effects on behalf of one actor (the instigator).

var _source_asc: AbilitySystemComponent


func bind(source_asc: AbilitySystemComponent) -> void:
	_source_asc = source_asc


## Returns true only when the target actually received the effect.
func hit(hurtbox: Hurtbox, effect: GameplayEffect, amount: float, causer: Node = null) -> bool:
	if not is_instance_valid(_source_asc) or not is_instance_valid(hurtbox) or not is_instance_valid(hurtbox.asc):
		return false
	# Convention (spec 3.4): the ASC is a direct child of its actor root, so its parent is the instigator.
	var instigator := _source_asc.get_parent()
	var spec := EffectBuilder.damage(instigator, causer if causer != null else instigator, effect, amount)
	return _source_asc.apply_effect_spec_to_target(spec, hurtbox.asc) != null
