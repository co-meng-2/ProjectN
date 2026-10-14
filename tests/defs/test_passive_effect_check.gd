extends TestCase


func _effect() -> GameplayEffect:
	return DefFactory.passive_effect("max_health", GameplayEffectModifier.Operation.ADD, PackedFloat32Array([10.0, 20.0]))


func test_a_valid_passive_has_no_errors() -> void:
	assert_no_errors(PassiveEffectCheck.validate(_effect(), "x"), "valid passive")


func test_a_non_infinite_effect_is_an_error() -> void:
	var effect := _effect()
	effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_non_free_stacking_is_an_error() -> void:
	var effect := _effect()
	effect.stacking_policy = GameplayEffect.StackingPolicy.REFRESH_DURATION
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_an_effect_without_modifiers_is_an_error() -> void:
	var effect := _effect()
	effect.modifiers.clear()
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_a_null_modifier_is_an_error() -> void:
	var effect := _effect()
	effect.modifiers.append(null)
	assert_eq(PassiveEffectCheck.validate(effect, "x").size(), 1)


func test_a_modifier_without_attribute_or_curve_or_with_zero_magnitude_is_an_error() -> void:
	var no_attribute := _effect()
	no_attribute.modifiers[0].attribute_name = ""
	assert_eq(PassiveEffectCheck.validate(no_attribute, "x").size(), 1, "attribute")
	var no_curve := _effect()
	no_curve.modifiers[0].scaling_curve = null
	assert_eq(PassiveEffectCheck.validate(no_curve, "x").size(), 1, "curve")
	var zero := _effect()
	zero.modifiers[0].magnitude = 0.0
	assert_eq(PassiveEffectCheck.validate(zero, "x").size(), 1, "magnitude")


func test_the_subject_prefixes_the_message() -> void:
	var effect := _effect()
	effect.policy = GameplayEffect.DurationPolicy.DURATION
	assert_true(PassiveEffectCheck.validate(effect, "Thing 'a'")[0].begins_with("Thing 'a'"))
