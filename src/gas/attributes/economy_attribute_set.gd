class_name EconomyAttributeSet
extends AttributeSet
## Multipliers applied to currency the actor collects.

var gold_gain: AttributeData = AttributeData.new(1.0)
var stone_gain: AttributeData = AttributeData.new(1.0)


func pre_attribute_change(_attribute_name: String, proposed_value: float) -> float:
	return maxf(proposed_value, 0.0)
