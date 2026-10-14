class_name PassiveEffectCheck
extends RefCounted
## Shared validation for permanent "passive" GameplayEffects: ability passives and skill-tree effects are authored and
## applied the same way (INFINITE, modifiers scaled by a curve over the level, magnitude 1.0).


## `subject` names the effect in messages, for example "AbilityDef 'vitality': passive_effect".
static func validate(effect: GameplayEffect, subject: String) -> PackedStringArray:
	var errors := PackedStringArray()
	if effect.policy != GameplayEffect.DurationPolicy.INFINITE:
		errors.append("%s must be INFINITE" % subject)
	if effect.stacking_policy != GameplayEffect.StackingPolicy.FREE:
		# Non-FREE stacking can hand two holders the same handle; clearing one would remove both.
		errors.append("%s stacking_policy must be FREE" % subject)
	if effect.modifiers.is_empty():
		errors.append("%s has no modifiers" % subject)
	for modifier in effect.modifiers:
		if modifier == null:
			errors.append("%s: null modifier" % subject)
		elif modifier.attribute_name == "":
			errors.append("%s: modifier without attribute_name" % subject)
		elif modifier.scaling_curve == null:
			errors.append("%s: modifier '%s' needs a scaling_curve over the level" % [subject, modifier.attribute_name])
		elif is_zero_approx(modifier.magnitude):
			# GAS multiplies the curve sample by `magnitude`; 0 would silently zero every level.
			errors.append("%s: modifier '%s' has magnitude 0 (use 1.0 with a curve)" % [subject, modifier.attribute_name])
	return errors
