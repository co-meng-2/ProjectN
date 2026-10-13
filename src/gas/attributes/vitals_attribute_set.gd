class_name VitalsAttributeSet
extends AttributeSet
## Hit points. `health` is clamped to [0, max_health].

var health: AttributeData = AttributeData.new(100.0)
var max_health: AttributeData = AttributeData.new(100.0)


func pre_attribute_change(attribute_name: String, proposed_value: float) -> float:
	match attribute_name:
		"health":
			return clampf(proposed_value, 0.0, max_health.current_value)
		"max_health":
			return maxf(proposed_value, 1.0)
	return proposed_value


## A lowered max_health pulls health down with it; a raised one never heals.
func post_attribute_change(asc: Node, attribute_name: String, _old_value: float, new_value: float) -> void:
	if attribute_name == "max_health" and health.current_value > new_value:
		(asc as AbilitySystemComponent)._apply_attribute_change("health", new_value - health.current_value)
