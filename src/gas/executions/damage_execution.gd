class_name DamageExecution
extends GameplayExecutionCalculation
## Damage = the amount injected by the caller (Damage.Amount) x the source's `attack` multiplier.


func execute(spec: GameplayEffectSpec, _target_asc: AbilitySystemComponent) -> Dictionary:
	var amount := spec.get_set_by_caller_magnitude(GameplayTags.Damage_Amount)
	var damage := maxf(amount * _source_attack(spec), 0.0)
	return {"health": -damage}


func _source_attack(spec: GameplayEffectSpec) -> float:
	var instigator: Node = spec.context.instigator if spec.context != null else null
	if instigator == null:
		return 1.0
	var source_asc := instigator.get_node_or_null("AbilitySystemComponent") as AbilitySystemComponent
	if source_asc == null or not source_asc.has_attribute("attack"):
		return 1.0
	return source_asc.get_attribute("attack").current_value
