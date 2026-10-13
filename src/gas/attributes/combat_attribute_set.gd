class_name CombatAttributeSet
extends AttributeSet
## Offensive and mobility stats.

## Damage multiplier for hits this actor lands.
var attack: AttributeData = AttributeData.new(1.0)
## Pixels per second.
var move_speed: AttributeData = AttributeData.new(100.0)


func pre_attribute_change(_attribute_name: String, proposed_value: float) -> float:
	return maxf(proposed_value, 0.0)
