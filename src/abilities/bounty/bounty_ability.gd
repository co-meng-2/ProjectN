class_name BountyAbility
extends ActorAbility
## Pays gold into the run's wallet every time the owner is credited with a kill (Event.Kill).
## The amount is a curve over the grade level. The ASC activates it through `trigger_event_tag`.

@export var gold_per_kill: Curve


func validate() -> PackedStringArray:
	var errors := PackedStringArray()
	if gold_per_kill == null:
		errors.append("Bounty: gold_per_kill curve is null")
	return errors


func _activate_ability() -> bool:
	if context == null or context.wallet == null or gold_per_kill == null:
		return false
	context.wallet.add(Wallet.Kind.GOLD, roundi(gold_per_kill.sample(ability_level)))
	return true
